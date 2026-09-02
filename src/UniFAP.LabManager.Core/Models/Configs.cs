using System.Text.Json.Serialization;

namespace UniFAP.LabManager.Core.Models;

public class InstitutionConfig
{
    public string Name { get; set; } = "Centro Universitário Paraíso - UNIFAP";
    public string ShortName { get; set; } = "UniFAP";
    public string Department { get; set; } = "Setor de Tecnologia da Informação (TI)";
    public string SupportEmail { get; set; } = "ti@unifap.edu.br";
    public string PortalUrl { get; set; } = "https://www.unifap.edu.br";
    public string AppVersion { get; set; } = "1.0.0";
    public string Environment { get; set; } = "Production";
}

public class ActiveDirectoryConfig
{
    public bool Enabled { get; set; } = true;
    public string Domain { get; set; } = "intranet.unifapce.edu.br";
    public string DomainController { get; set; } = string.Empty;
    public List<string> DnsServers { get; set; } = new();
    public string ComputerOu { get; set; } = string.Empty;
    public string AdministrativeOu { get; set; } = string.Empty;
    public string AcademicOu { get; set; } = string.Empty;
    public bool RequireCredentialPrompt { get; set; } = true;
    public AdPreValidationConfig PreValidation { get; set; } = new();
}

public class AdPreValidationConfig
{
    public int PingTimeoutMs { get; set; } = 3000;
    public int MaxTimeDriftMinutes { get; set; } = 5;
    public bool CheckDnsResolution { get; set; } = true;
    public bool CheckLdapConnectivity { get; set; } = true;
}

public class BrandingConfig
{
    public string InstitutionName { get; set; } = "Centro Universitário Paraíso - UNIFAP";
    public WallpaperConfig Wallpaper { get; set; } = new();
    public LockscreenConfig Lockscreen { get; set; } = new();
    public OemInfoConfig OemInfo { get; set; } = new();
}

public class WallpaperConfig
{
    public bool Enabled { get; set; } = true;
    public string Path { get; set; } = "assets/branding/wallpaper/papel_de_parede_unifap.jpg";
    public string Style { get; set; } = "Fill";
}

public class LockscreenConfig
{
    public bool Enabled { get; set; } = true;
    public string Path { get; set; } = "assets/branding/wallpaper/papel_de_parede_unifap.jpg";
}

public class OemInfoConfig
{
    public string Manufacturer { get; set; } = "Centro Universitário Paraíso - UNIFAP";
    public string SupportPhone { get; set; } = "TI Institucional";
    public string SupportUrl { get; set; } = "https://www.unifap.edu.br";
}

public class UsersConfig
{
    public Dictionary<string, UserAccountConfig> Users { get; set; } = new();
}

public class UserAccountConfig
{
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool Administrator { get; set; } = false;
    public bool PasswordPrompt { get; set; } = false;
    public bool PasswordNeverExpires { get; set; } = true;
    public bool UserCannotChangePassword { get; set; } = false;
    public bool Enabled { get; set; } = true;
}

public class PerformanceConfig
{
    public string Profile { get; set; } = "UniFAP_Balanced_Performance";
    public string Description { get; set; } = string.Empty;
    public VisualEffectsConfig VisualEffects { get; set; } = new();
    public SystemServicesConfig SystemServices { get; set; } = new();
    public StorageAndPowerConfig StorageAndPower { get; set; } = new();
    public NetworkPerformanceConfig Network { get; set; } = new();
}

public class VisualEffectsConfig
{
    public bool PreserveClearType { get; set; } = true;
    public bool PreserveFontSmoothing { get; set; } = true;
    public bool PreserveThumbnails { get; set; } = true;
    public bool PreserveWindowAnimations { get; set; } = true;
    public bool PreserveDropShadows { get; set; } = true;
    public bool OptimizeMenuShowDelay { get; set; } = true;
    public int MenuShowDelayMs { get; set; } = 150;
}

public class SystemServicesConfig
{
    public bool DisableDiagnosticsTracking { get; set; } = true;
    public bool DisableConnectedUserExperiences { get; set; } = true;
    public bool DisableFeedbackHub { get; set; } = true;
    public bool DisableCortana { get; set; } = true;
    public bool DisableTipsAndTricks { get; set; } = true;
    public bool DisableGameBarWhenUnused { get; set; } = true;
    public bool PreserveWindowsDefender { get; set; } = true;
    public bool PreserveWindowsFirewall { get; set; } = true;
    public bool PreserveWindowsUpdate { get; set; } = true;
}

public class StorageAndPowerConfig
{
    public bool EnableStorageSense { get; set; } = true;
    public bool DisableHibernateOnDesktops { get; set; } = true;
    public bool SetHighPerformancePowerPlan { get; set; } = false;
    public bool SetBalancedOptimizedPowerPlan { get; set; } = true;
}

public class NetworkPerformanceConfig
{
    public bool DisableDeliveryOptimizationBandwidthHog { get; set; } = true;
    public bool EnableNetworkThrottlingIndexOptimization { get; set; } = true;
}

public class ProfilesConfig
{
    public Dictionary<string, LaboratoryProfile> Laboratories { get; set; } = new();
    public LaboratoryProfile? Administrative { get; set; }
}

public class SettingsConfig
{
    public string Theme { get; set; } = "Dark";
    public string Language { get; set; } = "pt-BR";
    public bool AutoReboot { get; set; } = true;
    public bool AutoResume { get; set; } = true;
    public bool SilentInstall { get; set; } = true;
    public bool ShowAdvancedLogsByDefault { get; set; } = false;
    public StoragePathsConfig Paths { get; set; } = new();
    public WingetConfig Winget { get; set; } = new();
    public DiagnosticsConfig Diagnostics { get; set; } = new();
}

public class StoragePathsConfig
{
    public string ProgramDataDir { get; set; } = @"C:\ProgramData\UniFAP\LabManager";
    public string LogsDir { get; set; } = @"C:\ProgramData\UniFAP\LabManager\Logs";
    public string ReportsDir { get; set; } = @"C:\ProgramData\UniFAP\LabManager\Reports";
    public string JobsDir { get; set; } = @"C:\ProgramData\UniFAP\LabManager\Jobs";
}

public class WingetConfig
{
    public string Source { get; set; } = "winget";
    public bool AcceptAgreements { get; set; } = true;
    public int TimeoutSeconds { get; set; } = 900;
}

public class DiagnosticsConfig
{
    public List<string> ConnectivityHosts { get; set; } = new();
}

public class SoftwareCatalogConfig
{
    public List<string> Categories { get; set; } = new();
    public List<SoftwareItem> Items { get; set; } = new();
}

public class ThemeConfig
{
    public string Name { get; set; } = "Dark";
    public string DisplayName { get; set; } = "UniFAP Dark Mode";
    public Dictionary<string, string> Colors { get; set; } = new();
}
