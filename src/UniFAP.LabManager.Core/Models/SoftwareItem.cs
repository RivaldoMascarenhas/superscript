using System.ComponentModel;
using System.Runtime.CompilerServices;
using UniFAP.LabManager.Core.Enums;

namespace UniFAP.LabManager.Core.Models;

public class SoftwareItem : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

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

    private bool _isSelected = false;
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected != value)
            {
                _isSelected = value;
                OnPropertyChanged();
            }
        }
    }

    private SoftwareInstallStatus _status = SoftwareInstallStatus.Pending;
    public SoftwareInstallStatus Status
    {
        get => _status;
        set
        {
            if (_status != value)
            {
                _status = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsInstalling));
                OnPropertyChanged(nameof(IsInstalled));
                OnPropertyChanged(nameof(IsFailed));
                OnPropertyChanged(nameof(ButtonText));
                OnPropertyChanged(nameof(StatusDisplay));
            }
        }
    }

    private string? _errorMessage;
    public string? ErrorMessage
    {
        get => _errorMessage;
        set
        {
            if (_errorMessage != value)
            {
                _errorMessage = value;
                OnPropertyChanged();
            }
        }
    }

    public int EstimatedSeconds { get; set; } = 45;

    // Catálogo e rastreabilidade (UniFAP + WinUtil)
    public string Source { get; set; } = "UniFAP";
    public string? OfficialLink { get; set; }
    public bool IsOpenSource { get; set; } = false;
    public string? Version { get; set; }
    public bool Enabled { get; set; } = true;
    public string? Hash { get; set; }
    public string? ChocoId { get; set; }

    // Helpers reativos para binding WPF
    public bool IsInstalling => Status == SoftwareInstallStatus.Installing;
    public bool IsInstalled => Status == SoftwareInstallStatus.Installed;
    public bool IsFailed => Status == SoftwareInstallStatus.Failed;

    public string ButtonText => Status switch
    {
        SoftwareInstallStatus.Installing => "Instalando...",
        SoftwareInstallStatus.Installed => "✓ Instalado",
        SoftwareInstallStatus.Failed => "Reinstalar",
        _ => "Instalar"
    };

    public string StatusDisplay => Status switch
    {
        SoftwareInstallStatus.Installing => "Instalando...",
        SoftwareInstallStatus.Installed => "Instalado",
        SoftwareInstallStatus.Failed => "Falha na instalação",
        SoftwareInstallStatus.Warning => "Aviso",
        _ => "Pendente"
    };
}
