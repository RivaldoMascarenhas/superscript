using UniFAP.LabManager.Core.Enums;
using UniFAP.LabManager.Core.Interfaces;
using UniFAP.LabManager.Core.Models;
using UniFAP.LabManager.Infrastructure.Execution;

namespace UniFAP.LabManager.Services.Software.Installers;

public class WingetInstaller : ISoftwareInstaller
{
    private readonly IWingetService _wingetService;
    private readonly ProcessRunner _processRunner;
    private readonly ILogService _logger;

    public WingetInstaller(
        IWingetService wingetService,
        ProcessRunner processRunner,
        ILogService logger)
    {
        _wingetService = wingetService;
        _processRunner = processRunner;
        _logger = logger;
    }

    public bool CanHandle(SoftwareItem software)
    {
        return software.Type == SoftwareType.Winget || 
               (!string.IsNullOrWhiteSpace(software.WingetId) && software.Type == SoftwareType.Winget);
    }

    public async Task<SoftwareInstallResult> InstallAsync(
        SoftwareItem software,
        bool dryRun = false,
        Action<string>? progressCallback = null,
        CancellationToken cancellationToken = default)
    {
        if (dryRun)
        {
            _logger.LogInformation("WingetInstaller", $"[SIMULAÇÃO] Instalação via Winget simulada para '{software.Name}' ({software.WingetId}).");
            return new SoftwareInstallResult
            {
                Success = true,
                Status = SoftwareInstallStatus.Installed,
                Message = $"[SIMULAÇÃO] {software.Name} simulado via Winget.",
                ExitCode = 0
            };
        }

        if (string.IsNullOrWhiteSpace(software.WingetId))
        {
            return new SoftwareInstallResult
            {
                Success = false,
                Status = SoftwareInstallStatus.Failed,
                Message = $"ID do Winget não especificado para '{software.Name}'."
            };
        }

        // 1. Verificar se já está instalado
        progressCallback?.Invoke($"Verificando instalação prévia de {software.Name}...");
        if (await IsInstalledAsync(software))
        {
            _logger.LogInformation("WingetInstaller", $"'{software.Name}' ({software.WingetId}) já se encontra instalado no sistema.");
            return new SoftwareInstallResult
            {
                Success = true,
                Status = SoftwareInstallStatus.Installed,
                Message = $"{software.Name} já está instalado e operacional.",
                ExitCode = 0
            };
        }

        // 2. Executar instalação via Winget
        progressCallback?.Invoke($"Baixando e instalando {software.Name} via WinGet...");
        return await _wingetService.InstallPackageAsync(software.WingetId, software.Silent, progressCallback, cancellationToken);
    }

    public async Task<bool> IsInstalledAsync(SoftwareItem software)
    {
        if (string.IsNullOrWhiteSpace(software.WingetId)) return false;
        try
        {
            return await _wingetService.IsPackageInstalledAsync(software.WingetId);
        }
        catch (Exception ex)
        {
            _logger.LogDebug("WingetInstaller", $"Erro ao verificar status via Winget: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> UninstallAsync(SoftwareItem software, bool dryRun = false)
    {
        if (dryRun) return true;
        if (string.IsNullOrWhiteSpace(software.WingetId)) return false;

        _logger.LogInformation("WingetInstaller", $"Desinstalando '{software.Name}' via Winget ({software.WingetId})...");
        string args = $"uninstall --id {software.WingetId} --exact --silent --accept-source-agreements";
        var res = await _processRunner.RunAsync("winget.exe", args);
        return res.Success;
    }

    public async Task<bool> RepairAsync(SoftwareItem software, bool dryRun = false)
    {
        if (dryRun) return true;
        if (string.IsNullOrWhiteSpace(software.WingetId)) return false;

        _logger.LogInformation("WingetInstaller", $"Reparando/reinstalando '{software.Name}' via Winget com flag --force...");
        string args = $"install --id {software.WingetId} --exact --silent --force --accept-package-agreements --accept-source-agreements";
        var res = await _processRunner.RunAsync("winget.exe", args);
        return res.Success;
    }

    public Task<HashSet<string>> GetInstalledPackageIdsAsync()
    {
        return _wingetService.GetInstalledPackageIdsAsync();
    }
}
