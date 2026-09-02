using UniFAP.LabManager.Core.Enums;

namespace UniFAP.LabManager.Core.Models;

public class SoftwareItem
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = "Básicos";
    public string Description { get; set; } = string.Empty;
    public SoftwareType Type { get; set; } = SoftwareType.Winget;
    public string? WingetId { get; set; }
    public SoftwareType? FallbackType { get; set; }
    public string? Installer { get; set; }
    public string? EntryPoint { get; set; }
    public string? SilentArgs { get; set; }
    public string? ScriptPath { get; set; }
    public string? InstallerDir { get; set; }
    public string? Arguments { get; set; }
    public bool Silent { get; set; } = true;
    public SoftwareSeverity Severity { get; set; } = SoftwareSeverity.Warning;
    public bool Legacy { get; set; } = false;
    public string IconKey { get; set; } = "Package";
    public bool IsSelected { get; set; } = false;
    public SoftwareInstallStatus Status { get; set; } = SoftwareInstallStatus.Pending;
    public string? ErrorMessage { get; set; }
    public int EstimatedSeconds { get; set; } = 45;

    // Catálogo e rastreabilidade (UniFAP + WinUtil)
    public string Source { get; set; } = "UniFAP";
    public string? OfficialLink { get; set; }
    public bool IsOpenSource { get; set; } = false;
    public string? Version { get; set; }
    public bool Enabled { get; set; } = true;
    public string? Hash { get; set; }
    public string? ChocoId { get; set; }
}
