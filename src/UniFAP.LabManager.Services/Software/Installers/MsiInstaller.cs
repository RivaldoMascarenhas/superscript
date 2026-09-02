using Microsoft.Win32;
using UniFAP.LabManager.Core.Enums;
using UniFAP.LabManager.Core.Interfaces;
using UniFAP.LabManager.Core.Models;
using UniFAP.LabManager.Infrastructure.Execution;

namespace UniFAP.LabManager.Services.Software.Installers;

public class MsiInstaller : ISoftwareInstaller
{
    private readonly ILocalInstallerService _localInstaller;
    private readonly ProcessRunner _processRunner;
    private readonly ILogService _logger;

    public MsiInstaller(
        ILocalInstallerService localInstaller,
        ProcessRunner processRunner,
        ILogService logger)
    {
        _localInstaller = localInstaller;
        _processRunner = processRunner;
        _logger = logger;
    }

    public bool CanHandle(SoftwareItem software)
    {
        return software.Type == SoftwareType.Msi ||
               (!string.IsNullOrWhiteSpace(software.Installer) && software.Installer.EndsWith(".msi", StringComparison.OrdinalIgnoreCase));
    }

    public async Task<SoftwareInstallResult> InstallAsync(
        SoftwareItem software,
        bool dryRun = false,
        Action<string>? progressCallback = null,
        CancellationToken cancellationToken = default)
    {
        if (dryRun)
        {
            return new SoftwareInstallResult
            {
                Success = true,
                Status = SoftwareInstallStatus.Installed,
                Message = $"[SIMULAÇÃO] Instalação MSI de {software.Name} concluída.",
                ExitCode = 0
            };
        }

        if (await IsInstalledAsync(software))
        {
            _logger.LogInformation("MsiInstaller", $"'{software.Name}' já detectado como instalado no registro.");
            return new SoftwareInstallResult
            {
                Success = true,
                Status = SoftwareInstallStatus.Installed,
                Message = $"{software.Name} já se encontra instalado no sistema.",
                ExitCode = 0
            };
        }

        return await _localInstaller.RunInstallerAsync(software, dryRun, progressCallback, cancellationToken);
    }

    public Task<bool> IsInstalledAsync(SoftwareItem software)
    {
        try
        {
            return Task.FromResult(CheckUninstallRegistry(software.Name));
        }
        catch (Exception ex)
        {
            _logger.LogDebug("MsiInstaller", $"Erro ao verificar registro de desinstalação para '{software.Name}': {ex.Message}");
            return Task.FromResult(false);
        }
    }

    public async Task<bool> UninstallAsync(SoftwareItem software, bool dryRun = false)
    {
        if (dryRun) return true;
        if (string.IsNullOrWhiteSpace(software.Installer)) return false;

        _logger.LogInformation("MsiInstaller", $"Desinstalando '{software.Name}' via msiexec...");
        string args = $"/x \"{software.Installer}\" /qn /norestart";
        var res = await _processRunner.RunAsync("msiexec.exe", args);
        return res.ExitCode == 0 || res.ExitCode == 3010;
    }

    public async Task<bool> RepairAsync(SoftwareItem software, bool dryRun = false)
    {
        if (dryRun) return true;
        if (string.IsNullOrWhiteSpace(software.Installer)) return false;

        _logger.LogInformation("MsiInstaller", $"Reparando '{software.Name}' via msiexec...");
        string args = $"/f \"{software.Installer}\" /qn /norestart";
        var res = await _processRunner.RunAsync("msiexec.exe", args);
        return res.ExitCode == 0 || res.ExitCode == 3010;
    }

    private static bool CheckUninstallRegistry(string appName)
    {
        string[] rootKeys =
        {
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
            @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
        };

        foreach (var rootKey in rootKeys)
        {
            using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
            using var subKey = baseKey.OpenSubKey(rootKey);
            if (subKey == null) continue;

            foreach (var subName in subKey.GetSubKeyNames())
            {
                using var item = subKey.OpenSubKey(subName);
                var displayName = item?.GetValue("DisplayName") as string;
                if (!string.IsNullOrWhiteSpace(displayName) && 
                    displayName.Contains(appName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
