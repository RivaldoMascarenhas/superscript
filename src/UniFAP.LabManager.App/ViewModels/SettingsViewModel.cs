using System.Collections.ObjectModel;
using System.Windows.Input;
using UniFAP.LabManager.Core.Interfaces;
using UniFAP.LabManager.Core.Models;

namespace UniFAP.LabManager.App.ViewModels;

public class SettingsViewModel : ViewModelBase
{
    private readonly IConfigService _configService;
    private readonly IThemeService _themeService;
    private readonly ILogService _logger;

    private string _selectedTheme = "Dark";
    private string _domain = string.Empty;
    private string _domainController = string.Empty;
    private string _computerOu = string.Empty;
    private bool _autoReboot = true;
    private bool _autoResume = true;
    private bool _silentInstall = true;
    private string _saveMessage = string.Empty;

    public string SelectedTheme
    {
        get => _selectedTheme;
        set
        {
            if (SetProperty(ref _selectedTheme, value))
            {
                _themeService.ApplyTheme(value);
            }
        }
    }

    public string Domain
    {
        get => _domain;
        set => SetProperty(ref _domain, value);
    }

    public string DomainController
    {
        get => _domainController;
        set => SetProperty(ref _domainController, value);
    }

    public string ComputerOu
    {
        get => _computerOu;
        set => SetProperty(ref _computerOu, value);
    }

    public bool AutoReboot
    {
        get => _autoReboot;
        set => SetProperty(ref _autoReboot, value);
    }

    public bool AutoResume
    {
        get => _autoResume;
        set => SetProperty(ref _autoResume, value);
    }

    public bool SilentInstall
    {
        get => _silentInstall;
        set => SetProperty(ref _silentInstall, value);
    }

    public string SaveMessage
    {
        get => _saveMessage;
        set => SetProperty(ref _saveMessage, value);
    }

    public ObservableCollection<string> AvailableThemes { get; } = new();

    public ICommand SaveSettingsCommand { get; }

    public SettingsViewModel(
        IConfigService configService,
        IThemeService themeService,
        ILogService logger)
    {
        _configService = configService;
        _themeService = themeService;
        _logger = logger;

        AvailableThemes.Add("Dark");
        AvailableThemes.Add("Light");

        SaveSettingsCommand = new AsyncRelayCommand(SaveSettingsAsync);
        LoadValues();
    }

    private void LoadValues()
    {
        var settings = _configService.Settings;
        var ad = _configService.ActiveDirectory;

        SelectedTheme = settings.Theme;
        AutoReboot = settings.AutoReboot;
        AutoResume = settings.AutoResume;
        SilentInstall = settings.SilentInstall;

        Domain = ad.Domain;
        DomainController = ad.DomainController;
        ComputerOu = ad.ComputerOu;
    }

    private async Task SaveSettingsAsync()
    {
        try
        {
            _configService.Settings.Theme = SelectedTheme;
            _configService.Settings.AutoReboot = AutoReboot;
            _configService.Settings.AutoResume = AutoResume;
            _configService.Settings.SilentInstall = SilentInstall;

            _configService.ActiveDirectory.Domain = Domain;
            _configService.ActiveDirectory.DomainController = DomainController;
            _configService.ActiveDirectory.ComputerOu = ComputerOu;

            await _configService.SaveSettingsAsync();
            SaveMessage = "✓ Configurações salvas com sucesso!";
        }
        catch (Exception ex)
        {
            _logger.LogError("SettingsViewModel", "Erro ao salvar configurações", ex);
            SaveMessage = $"Erro: {ex.Message}";
        }
    }
}

public class AboutViewModel : ViewModelBase
{
    private readonly IConfigService _configService;

    public string AppTitle => "UNIFAP LAB MANAGER";
    public string InstitutionName => _configService.Institution.Name;
    public string Department => _configService.Institution.Department;
    public string Version => $"Versão {_configService.Institution.AppVersion} LTS";
    public string SupportEmail => _configService.Institution.SupportEmail;
    public string PortalUrl => _configService.Institution.PortalUrl;

    public AboutViewModel(IConfigService configService)
    {
        _configService = configService;
    }
}
