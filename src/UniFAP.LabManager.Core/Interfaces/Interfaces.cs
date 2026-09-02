using System.Security;
using UniFAP.LabManager.Core.Enums;
using UniFAP.LabManager.Core.Models;

namespace UniFAP.LabManager.Core.Interfaces;

public interface ILogService
{
    void LogInformation(string source, string message);
    void LogWarning(string source, string message);
    void LogError(string source, string message, Exception? ex = null);
    void LogDebug(string source, string message);
    void AppendStepLog(string jobId, string stepName, string message);
    event Action<string, string, string>? OnLogEmitted; // source, level, message
    string GetLogsDirectory();
}

public interface IConfigService
{
    InstitutionConfig Institution { get; }
    ActiveDirectoryConfig ActiveDirectory { get; }
    BrandingConfig Branding { get; }
    PerformanceConfig Performance { get; }
    ProfilesConfig Profiles { get; }
    SettingsConfig Settings { get; }
    SoftwareCatalogConfig SoftwareCatalog { get; }
    UsersConfig Users { get; }
    ThemeConfig CurrentTheme { get; }

    Task LoadAllAsync();
    Task SaveSettingsAsync();
    LaboratoryProfile? GetProfile(string profileId);
    SoftwareItem? GetSoftware(string softwareId);
    List<SoftwareItem> GetSoftwareForProfile(string profileId);
    ThemeConfig LoadTheme(string themeName);
}

public interface IPreCheckService
{
    Task<PreCheckReport> RunPreCheckAsync(ComputerType computerType, bool joinActiveDirectory, CancellationToken cancellationToken = default);
}

public interface IJobOrchestrator
{
    Job? CurrentJob { get; }
    bool IsRunning { get; }
    event Action<Job>? OnJobUpdated;
    event Action<JobStep>? OnStepUpdated;
    event Action<string>? OnLogMessage;

    Task<Job> CreateJobAsync(
        ComputerType computerType,
        string profileId,
        List<string>? customSoftwareIds = null,
        bool dryRun = false,
        bool? joinDomain = null,
        string? supportPassword = null);
    Task<bool> StartJobAsync(Job job, CancellationToken cancellationToken = default);
    Task CancelJobAsync();
    Task<Job?> CheckForPendingResumedJobAsync();
    Task SaveJobStateAsync(Job job);
    Task ClearJobStateAsync(string jobId);
    Task<List<Job>> GetJobHistoryAsync();
}

public interface ISoftwareInstaller
{
    bool CanHandle(SoftwareItem software);
    Task<SoftwareInstallResult> InstallAsync(SoftwareItem software, bool dryRun = false, Action<string>? progressCallback = null, CancellationToken cancellationToken = default);
    Task<bool> IsInstalledAsync(SoftwareItem software);
    Task<bool> UninstallAsync(SoftwareItem software, bool dryRun = false);
    Task<bool> RepairAsync(SoftwareItem software, bool dryRun = false);
}

public interface ILocalInstallerService
{
    bool ValidateInstallerSecurity(SoftwareItem software, out string? errorMessage);
    Task<SoftwareInstallResult> RunInstallerAsync(SoftwareItem software, bool dryRun = false, Action<string>? progressCallback = null, CancellationToken cancellationToken = default);
}

public interface ICatalogSyncService
{
    Task<CatalogSyncResult> SyncWinUtilCatalogAsync(bool forceOnline = false, CancellationToken cancellationToken = default);
    Task<List<SoftwareItem>> GetMergedCatalogAsync();
    Task<CatalogSourceConfig> GetCatalogSourceConfigAsync();
    string NormalizeCategory(string? rawCategory);
}

public interface ISoftwareCatalogService : ISoftwareService
{
    Task<CatalogSyncResult> SyncCatalogAsync(bool forceOnline = false, CancellationToken cancellationToken = default);
    Task<List<string>> GetCategoriesAsync();
}

public interface ISoftwareService
{
    Task<List<SoftwareItem>> GetCatalogAsync();
    Task<SoftwareInstallResult> InstallAsync(SoftwareItem software, bool dryRun = false, Action<string>? progressCallback = null, CancellationToken cancellationToken = default);
    Task<bool> IsInstalledAsync(SoftwareItem software);
    Task<bool> UninstallAsync(SoftwareItem software, bool dryRun = false);
    Task<bool> RepairAsync(SoftwareItem software, bool dryRun = false);
    Task<HashSet<string>> GetInstalledPackageIdsAsync();
}

public class SoftwareInstallResult
{
    public bool Success { get; set; }
    public SoftwareInstallStatus Status { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? Details { get; set; }
    public int ExitCode { get; set; }
}

public interface IWingetService
{
    Task<bool> IsAvailableAsync();
    Task<SoftwareInstallResult> InstallPackageAsync(string packageId, bool silent = true, Action<string>? progressCallback = null, CancellationToken cancellationToken = default);
    Task<bool> IsPackageInstalledAsync(string packageId);
    Task<HashSet<string>> GetInstalledPackageIdsAsync();
    Task<List<string>> SearchPackagesAsync(string query);
}

public interface IWindowsConfigurationService
{
    Task<SystemInfo> GetSystemInfoAsync();
    Task<bool> ApplyOptimizationsAsync(bool dryRun = false);
    Task<bool> RepairSystemAsync(bool fullRepair = false, bool dryRun = false, Action<string>? progress = null);
    Task<bool> HasPendingRebootAsync();
    Task RequestRebootAsync(int delaySeconds = 10);
}

public interface IUserService
{
    Task<bool> ProvisionUsersAsync(string? supportPassword = null, string? studentPassword = null, bool dryRun = false);
    Task<bool> IsUserConfiguredAsync(string username);
    Task<bool> IsInAdminGroupAsync(string username);
}

public interface IPerformanceService
{
    Task<bool> ApplyPerformanceTweaksAsync(bool dryRun = false);
    Task<bool> RollbackPerformanceTweaksAsync();
    Task<string> CleanTemporaryFilesAsync(bool dryRun = false);
    Task<bool> OptimizeBrowsersAsync(bool dryRun = false);
    Task<bool> ConfigureDnsAsync(string primaryDns, string? secondaryDns = null, bool useDhcp = false);
}

public interface IBrandingService
{
    Task<bool> ApplyBrandingAsync(bool dryRun = false);
    string GetWallpaperPath();
}

public interface IActiveDirectoryService
{
    Task<AdValidationResult> ValidateDomainPreRequisitesAsync(string domain, string? domainController);
    Task<AdJoinResult> JoinDomainAsync(string domain, string? domainController, string? ouPath, string username, string password, bool dryRun = false);
    Task<bool> IsDomainJoinedAsync();
    Task<string> GetCurrentDomainAsync();
}

public class AdValidationResult
{
    public bool Success { get; set; }
    public bool DnsResolved { get; set; }
    public bool DcReachable { get; set; }
    public bool AlreadyJoined { get; set; }
    public string Message { get; set; } = string.Empty;
}

public class AdJoinResult
{
    public bool Success { get; set; }
    public bool NeedsReboot { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? ErrorDetails { get; set; }
}

public interface IDiagnosticsService
{
    Task<DiagnosticsReport> RunFullDiagnosticsAsync(CancellationToken cancellationToken = default);
    Task<SystemInfo> CollectSystemInfoAsync();
}

public interface IReportService
{
    Task<PreparationReport> GenerateReportAsync(Job job);
    Task<string> SaveReportJsonAsync(PreparationReport report);
    Task<string> SaveReportTxtAsync(PreparationReport report);
}

public interface ISecurityService
{
    bool IsElevatedAdministrator();
    bool ValidatePathSafety(string relativeOrAbsolutePath, string allowedBaseDirectory);
    string SanitizeLogString(string input);
}

public interface IThemeService
{
    ThemeConfig CurrentTheme { get; }
    void ApplyTheme(string themeName);
    List<string> GetAvailableThemes();
}
