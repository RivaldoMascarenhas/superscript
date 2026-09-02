using UniFAP.LabManager.Core.Enums;
using UniFAP.LabManager.Core.Interfaces;
using UniFAP.LabManager.Core.Models;

namespace UniFAP.LabManager.Services.Software.Installers;

public class ScriptInstaller : ISoftwareInstaller
{
    private readonly ILocalInstallerService _localInstaller;
    private readonly ILogService _logger;

    public ScriptInstaller(
        ILocalInstallerService localInstaller,
        ILogService logger)
    {
        _localInstaller = localInstaller;
        _logger = logger;
    }

    public bool CanHandle(SoftwareItem software)
    {
        return software.Type == SoftwareType.Script && 
               !string.Equals(software.Id, "office365", StringComparison.OrdinalIgnoreCase);
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
                Message = $"[SIMULAÇÃO] Execução de script para {software.Name} simulada com sucesso.",
                ExitCode = 0
            };
        }

        _logger.LogInformation("ScriptInstaller", $"Executando script de instalação para '{software.Name}'...");
        return await _localInstaller.RunInstallerAsync(software, dryRun, progressCallback, cancellationToken);
    }

    public Task<bool> IsInstalledAsync(SoftwareItem software)
    {
        // Scripts geralmente executam provimento procedural
        return Task.FromResult(false);
    }

    public Task<bool> UninstallAsync(SoftwareItem software, bool dryRun = false)
    {
        _logger.LogInformation("ScriptInstaller", $"Desinstalação de script para '{software.Name}' não configurada.");
        return Task.FromResult(true);
    }

    public async Task<bool> RepairAsync(SoftwareItem software, bool dryRun = false)
    {
        if (dryRun) return true;
        var res = await _localInstaller.RunInstallerAsync(software, dryRun);
        return res.Success;
    }
}
