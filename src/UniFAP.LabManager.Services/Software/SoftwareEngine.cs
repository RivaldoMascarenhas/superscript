using UniFAP.LabManager.Core.Enums;
using UniFAP.LabManager.Core.Interfaces;
using UniFAP.LabManager.Core.Models;

namespace UniFAP.LabManager.Services.Software;

public class SoftwareEngine : ISoftwareCatalogService
{
    private readonly IEnumerable<ISoftwareInstaller> _installers;
    private readonly ICatalogSyncService _catalogSyncService;
    private readonly IConfigService _configService;
    private readonly ILogService _logger;

    public SoftwareEngine(
        IEnumerable<ISoftwareInstaller> installers,
        ICatalogSyncService catalogSyncService,
        IConfigService configService,
        ILogService logger)
    {
        _installers = installers;
        _catalogSyncService = catalogSyncService;
        _configService = configService;
        _logger = logger;
    }

    public async Task<List<SoftwareItem>> GetCatalogAsync()
    {
        return await _catalogSyncService.GetMergedCatalogAsync();
    }

    public async Task<CatalogSyncResult> SyncCatalogAsync(bool forceOnline = false, CancellationToken cancellationToken = default)
    {
        return await _catalogSyncService.SyncWinUtilCatalogAsync(forceOnline, cancellationToken);
    }

    public Task<List<string>> GetCategoriesAsync()
    {
        var cats = new HashSet<string>(_configService.SoftwareCatalog.Categories, StringComparer.OrdinalIgnoreCase);
        foreach (var cat in new[]
        {
            "Browsers", "Development", "Document", "Education", "Games", 
            "Multimedia", "Networking", "Utilities", "Microsoft Tools", 
            "Pro Tools", "Communication", "Productivity", "Security", "Other"
        })
        {
            cats.Add(cat);
        }
        return Task.FromResult(cats.ToList());
    }

    public async Task<SoftwareInstallResult> InstallAsync(
        SoftwareItem software,
        bool dryRun = false,
        Action<string>? progressCallback = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("SoftwareEngine", $"Iniciando pipeline de instalação de '{software.Name}' [Tipo: {software.Type}] [Fonte: {software.Source}] [DryRun: {dryRun}]");
        progressCallback?.Invoke($"Instalando: {software.Name}...");

        if (dryRun)
        {
            await Task.Delay(250, cancellationToken);
            _logger.LogInformation("SoftwareEngine", $"[SIMULAÇÃO] Instalação de '{software.Name}' simulada com sucesso.");
            return new SoftwareInstallResult
            {
                Success = true,
                Status = SoftwareInstallStatus.Installed,
                Message = $"[SIMULAÇÃO] Instalação de {software.Name} simulada com êxito."
            };
        }

        try
        {
            var installer = FindInstaller(software);
            if (installer == null)
            {
                _logger.LogWarning("SoftwareEngine", $"Nenhum instalador compatível registrado para o software '{software.Name}' (Tipo: {software.Type}).");
                return HandleFailureOrWarning(software, $"Instalador não compatível para o tipo {software.Type}");
            }

            var result = await installer.InstallAsync(software, dryRun, progressCallback, cancellationToken);

            // Fallback configurado caso o primário falhe (ex: Winget offline falha -> recorre ao instalador local)
            if (!result.Success && software.FallbackType.HasValue)
            {
                _logger.LogWarning("SoftwareEngine", $"Instalador primário falhou para '{software.Name}'. Tentando instalador de fallback: {software.FallbackType.Value}");
                var fallbackSw = CloneWithFallback(software);
                var fallbackInstaller = FindInstaller(fallbackSw);
                if (fallbackInstaller != null)
                {
                    progressCallback?.Invoke($"Tentando método de contingência local para {software.Name}...");
                    result = await fallbackInstaller.InstallAsync(fallbackSw, dryRun, progressCallback, cancellationToken);
                }
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError("SoftwareEngine", $"Exceção não tratada durante instalação de '{software.Name}'", ex);
            return HandleFailureOrWarning(software, ex.Message, ex.ToString());
        }
    }

    public async Task<bool> IsInstalledAsync(SoftwareItem software)
    {
        var installer = FindInstaller(software);
        if (installer != null)
        {
            return await installer.IsInstalledAsync(software);
        }
        return false;
    }

    public async Task<bool> UninstallAsync(SoftwareItem software, bool dryRun = false)
    {
        var installer = FindInstaller(software);
        if (installer != null)
        {
            return await installer.UninstallAsync(software, dryRun);
        }
        return false;
    }

    public async Task<bool> RepairAsync(SoftwareItem software, bool dryRun = false)
    {
        var installer = FindInstaller(software);
        if (installer != null)
        {
            return await installer.RepairAsync(software, dryRun);
        }
        return false;
    }

    public async Task<HashSet<string>> GetInstalledPackageIdsAsync()
    {
        var wingetInstaller = _installers.OfType<UniFAP.LabManager.Services.Software.Installers.WingetInstaller>().FirstOrDefault();
        if (wingetInstaller != null)
        {
            return await wingetInstaller.GetInstalledPackageIdsAsync();
        }
        return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    }

    private ISoftwareInstaller? FindInstaller(SoftwareItem software)
    {
        return _installers.FirstOrDefault(i => i.CanHandle(software));
    }

    private static SoftwareInstallResult HandleFailureOrWarning(SoftwareItem software, string message, string? details = null)
    {
        if (software.Legacy || software.Severity == SoftwareSeverity.Optional)
        {
            return new SoftwareInstallResult
            {
                Success = true,
                Status = SoftwareInstallStatus.Warning,
                Message = $"Software opcional ou legado reportou aviso: {message}",
                Details = details,
                ExitCode = -1
            };
        }

        return new SoftwareInstallResult
        {
            Success = false,
            Status = SoftwareInstallStatus.Failed,
            Message = $"Falha na instalação de {software.Name}: {message}",
            Details = details,
            ExitCode = -1
        };
    }

    private static SoftwareItem CloneWithFallback(SoftwareItem original)
    {
        return new SoftwareItem
        {
            Id = original.Id,
            Name = original.Name,
            Category = original.Category,
            Description = original.Description,
            Type = original.FallbackType ?? SoftwareType.Local,
            WingetId = original.WingetId,
            Installer = original.Installer,
            EntryPoint = original.EntryPoint,
            SilentArgs = original.SilentArgs,
            ScriptPath = original.ScriptPath,
            InstallerDir = original.InstallerDir,
            Arguments = original.Arguments,
            Silent = original.Silent,
            Severity = original.Severity,
            Legacy = original.Legacy,
            IconKey = original.IconKey,
            Source = original.Source,
            OfficialLink = original.OfficialLink,
            IsOpenSource = original.IsOpenSource
        };
    }
}
