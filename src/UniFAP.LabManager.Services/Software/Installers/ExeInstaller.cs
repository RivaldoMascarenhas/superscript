using System.IO;
using Microsoft.Win32;
using UniFAP.LabManager.Core.Enums;
using UniFAP.LabManager.Core.Interfaces;
using UniFAP.LabManager.Core.Models;
using UniFAP.LabManager.Infrastructure.Execution;

namespace UniFAP.LabManager.Services.Software.Installers;

public class ExeInstaller : ISoftwareInstaller
{
    private readonly ILocalInstallerService _localInstaller;
    private readonly ProcessRunner _processRunner;
    private readonly ILogService _logger;

    public ExeInstaller(
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
        return software.Type == SoftwareType.Local ||
               software.Type == SoftwareType.Exe ||
               software.Type == SoftwareType.Legacy;
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
                Message = $"[SIMULAÇÃO] Instalação executável de {software.Name} concluída com sucesso.",
                ExitCode = 0
            };
        }

        if (await IsInstalledAsync(software))
        {
            _logger.LogInformation("ExeInstaller", $"'{software.Name}' já detectado como instalado no sistema operacional.");
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
            // 1. Checar Registro do Windows
            if (CheckUninstallRegistry(software.Name)) return Task.FromResult(true);

            // 2. Checar diretórios padrão em Program Files
            string pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            string pfx86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);

            string[] searchFolders = { software.Name, software.Id };
            foreach (var name in searchFolders)
            {
                if (Directory.Exists(Path.Combine(pf, name)) || Directory.Exists(Path.Combine(pfx86, name)))
                {
                    return Task.FromResult(true);
                }
            }

            return Task.FromResult(false);
        }
        catch (Exception ex)
        {
            _logger.LogDebug("ExeInstaller", $"Falha ao verificar instalação para '{software.Name}': {ex.Message}");
            return Task.FromResult(false);
        }
    }

    public Task<bool> UninstallAsync(SoftwareItem software, bool dryRun = false)
    {
        if (dryRun) return Task.FromResult(true);
        _logger.LogWarning("ExeInstaller", $"Desinstalação automática genérica para EXE de '{software.Name}' requer ação manual ou painel de controle.");
        return Task.FromResult(true);
    }

    public async Task<bool> RepairAsync(SoftwareItem software, bool dryRun = false)
    {
        if (dryRun) return true;
        _logger.LogInformation("ExeInstaller", $"Executando reparo/reinstalação de '{software.Name}'...");
        var res = await _localInstaller.RunInstallerAsync(software, dryRun);
        return res.Success;
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
