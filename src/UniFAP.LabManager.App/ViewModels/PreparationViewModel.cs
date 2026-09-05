using System.Collections.ObjectModel;
using System.Windows.Input;
using UniFAP.LabManager.Core.Enums;
using UniFAP.LabManager.Core.Interfaces;
using UniFAP.LabManager.Core.Models;

namespace UniFAP.LabManager.App.ViewModels;

public class PreparationViewModel : ViewModelBase
{
    private readonly IConfigService _configService;
    private readonly IPreCheckService _preCheckService;
    private readonly IActiveDirectoryService _adService;
    private readonly IJobOrchestrator _jobOrchestrator;
    private readonly ILogService _logger;

    public event Action<Job>? OnExecutionStarted;
    public event Func<ActiveDirectoryConfig, Task<(bool success, string username, string password)>>? OnPromptActiveDirectoryCredentials;
    public event Func<Task<(bool success, string password)>>? OnPromptSupportPassword;

    private ComputerType _selectedComputerType = ComputerType.Administrative;
    private string _selectedProfileId = "geral";
    private LaboratoryProfile? _selectedProfile;
    private bool _isCustomSoftwareMode;
    private string _softwareSearchText = string.Empty;
    private string _selectedCategory = "Todos";
    private bool _isRunningPreCheck;
    private PreCheckReport? _preCheckReport;
    private string _supportAdminPassword = string.Empty;
    private string _supportAdminPasswordConfirm = string.Empty;
    private bool _isAdministrativeJoinAdChecked = true;
    private string _validationErrorMessage = string.Empty;
    private string _newComputerName = string.Empty;

    public string NewComputerName
    {
        get => _newComputerName;
        set => SetProperty(ref _newComputerName, value);
    }

    public string CurrentMachineName => Environment.MachineName;

    public bool IsAdministrativeJoinAdChecked
    {
        get => _isAdministrativeJoinAdChecked;
        set
        {
            if (SetProperty(ref _isAdministrativeJoinAdChecked, value))
            {
                _ = RunPreCheckAsync();
            }
        }
    }

    public string SupportAdminPasswordConfirm
    {
        get => _supportAdminPasswordConfirm;
        set
        {
            if (SetProperty(ref _supportAdminPasswordConfirm, value))
            {
                ValidatePasswords();
            }
        }
    }

    public string ValidationErrorMessage
    {
        get => _validationErrorMessage;
        set => SetProperty(ref _validationErrorMessage, value);
    }

    public ComputerType SelectedComputerType
    {
        get => _selectedComputerType;
        set
        {
            if (SetProperty(ref _selectedComputerType, value))
            {
                OnPropertyChanged(nameof(IsAdministrativeMode));
                OnPropertyChanged(nameof(IsLaboratoryMode));
                UpdateSelectedProfile();
                _ = RunPreCheckAsync();
            }
        }
    }

    public bool IsAdministrativeMode => SelectedComputerType == ComputerType.Administrative;
    public bool IsLaboratoryMode => SelectedComputerType == ComputerType.Laboratory;

    public string SelectedProfileId
    {
        get => _selectedProfileId;
        set
        {
            if (SetProperty(ref _selectedProfileId, value))
            {
                IsCustomSoftwareMode = value == "personalizado";
                UpdateSelectedProfile();
            }
        }
    }

    public LaboratoryProfile? SelectedProfile
    {
        get => _selectedProfile;
        set => SetProperty(ref _selectedProfile, value);
    }

    public bool IsCustomSoftwareMode
    {
        get => _isCustomSoftwareMode;
        set => SetProperty(ref _isCustomSoftwareMode, value);
    }

    public string SoftwareSearchText
    {
        get => _softwareSearchText;
        set
        {
            if (SetProperty(ref _softwareSearchText, value))
            {
                FilterSoftwareItems();
            }
        }
    }

    public string SelectedCategory
    {
        get => _selectedCategory;
        set
        {
            if (SetProperty(ref _selectedCategory, value))
            {
                FilterSoftwareItems();
            }
        }
    }

    public bool IsRunningPreCheck
    {
        get => _isRunningPreCheck;
        set => SetProperty(ref _isRunningPreCheck, value);
    }

    public PreCheckReport? PreCheckReport
    {
        get => _preCheckReport;
        set => SetProperty(ref _preCheckReport, value);
    }

    public string SupportAdminPassword
    {
        get => _supportAdminPassword;
        set
        {
            if (SetProperty(ref _supportAdminPassword, value))
            {
                ValidatePasswords();
            }
        }
    }

    public ObservableCollection<LaboratoryProfile> LaboratoryProfiles { get; } = new();
    public ObservableCollection<string> Categories { get; } = new();
    public ObservableCollection<SoftwareItem> AvailableSoftwareItems { get; } = new();
    public ObservableCollection<SoftwareItem> FilteredSoftwareItems { get; } = new();
    public ObservableCollection<SoftwareItem> SelectedSoftwareSummaryList { get; } = new();

    public int SelectedSoftwareCount => AvailableSoftwareItems.Count(s => s.IsSelected);

    public ICommand SelectAdministrativeCommand { get; }
    public ICommand SelectLaboratoryCommand { get; }
    public ICommand SelectProfileCommand { get; }
    public ICommand SelectAllSoftwareCommand { get; }
    public ICommand ClearSoftwareSelectionCommand { get; }
    public ICommand ToggleSoftwareSelectionCommand { get; }
    public ICommand RunPreCheckCommand { get; }
    public ICommand StartPreparationCommand { get; }
    public ICommand StartDryRunCommand { get; }

    public PreparationViewModel(
        IConfigService configService,
        IPreCheckService preCheckService,
        IActiveDirectoryService adService,
        IJobOrchestrator jobOrchestrator,
        ILogService logger)
    {
        _configService = configService;
        _preCheckService = preCheckService;
        _adService = adService;
        _jobOrchestrator = jobOrchestrator;
        _logger = logger;

        SelectAdministrativeCommand = new RelayCommand(() => SelectedComputerType = ComputerType.Administrative);
        SelectLaboratoryCommand = new RelayCommand(() => SelectedComputerType = ComputerType.Laboratory);
        SelectProfileCommand = new RelayCommand(param =>
        {
            if (param is string profileId)
            {
                SelectedProfileId = profileId;
            }
        });

        SelectAllSoftwareCommand = new RelayCommand(SelectAllSoftware);
        ClearSoftwareSelectionCommand = new RelayCommand(ClearSoftwareSelection);
        ToggleSoftwareSelectionCommand = new RelayCommand(param =>
        {
            if (param is SoftwareItem item)
            {
                item.IsSelected = !item.IsSelected;
                UpdateSelectedSoftwareSummary();
            }
        });

        RunPreCheckCommand = new AsyncRelayCommand(RunPreCheckAsync);
        StartPreparationCommand = new AsyncRelayCommand(() => LaunchExecutionAsync(dryRun: false));
        StartDryRunCommand = new AsyncRelayCommand(() => LaunchExecutionAsync(dryRun: true));
    }

    public async Task InitializeAsync()
    {
        LaboratoryProfiles.Clear();
        foreach (var prof in _configService.Profiles.Laboratories.Values)
        {
            LaboratoryProfiles.Add(prof);
        }

        // Adiciona perfil personalizado
        LaboratoryProfiles.Add(new LaboratoryProfile
        {
            Id = "personalizado",
            DisplayName = "Personalizado",
            Description = "Escolha livre de softwares por categorias.",
            JoinDomain = false
        });

        Categories.Clear();
        Categories.Add("Todos");
        foreach (var cat in _configService.SoftwareCatalog.Categories)
        {
            Categories.Add(cat);
        }

        AvailableSoftwareItems.Clear();
        foreach (var item in _configService.SoftwareCatalog.Items)
        {
            AvailableSoftwareItems.Add(item);
        }

        UpdateSelectedProfile();
        await RunPreCheckAsync();
    }

    private void UpdateSelectedProfile()
    {
        if (IsAdministrativeMode)
        {
            SelectedProfile = _configService.Profiles.Administrative;
            // Marcar apenas softwares administrativos
            var adminSw = _configService.Profiles.Administrative?.Software ?? new List<string>();
            foreach (var sw in AvailableSoftwareItems)
            {
                sw.IsSelected = adminSw.Contains(sw.Id);
            }
        }
        else
        {
            if (SelectedProfileId == "personalizado")
            {
                SelectedProfile = LaboratoryProfiles.FirstOrDefault(p => p.Id == "personalizado");
            }
            else
            {
                SelectedProfile = _configService.GetProfile(SelectedProfileId);
                var profSw = SelectedProfile?.Software ?? new List<string>();
                foreach (var sw in AvailableSoftwareItems)
                {
                    sw.IsSelected = profSw.Contains(sw.Id);
                }
            }
        }

        foreach (var p in LaboratoryProfiles)
        {
            p.IsSelected = IsLaboratoryMode && (p.Id == SelectedProfileId);
        }

        UpdateSelectedSoftwareSummary();
        FilterSoftwareItems();
    }

    private void UpdateSelectedSoftwareSummary()
    {
        SelectedSoftwareSummaryList.Clear();
        foreach (var item in AvailableSoftwareItems.Where(s => s.IsSelected))
        {
            SelectedSoftwareSummaryList.Add(item);
        }
        OnPropertyChanged(nameof(SelectedSoftwareCount));
    }

    private void FilterSoftwareItems()
    {
        FilteredSoftwareItems.Clear();
        var items = AvailableSoftwareItems.AsEnumerable();

        if (SelectedCategory != "Todos")
        {
            items = items.Where(s => s.Category.Equals(SelectedCategory, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(SoftwareSearchText))
        {
            items = items.Where(s => s.Name.Contains(SoftwareSearchText, StringComparison.OrdinalIgnoreCase) ||
                                     s.Category.Contains(SoftwareSearchText, StringComparison.OrdinalIgnoreCase) ||
                                     s.Description.Contains(SoftwareSearchText, StringComparison.OrdinalIgnoreCase));
        }

        foreach (var item in items)
        {
            FilteredSoftwareItems.Add(item);
        }
    }

    private void SelectAllSoftware()
    {
        foreach (var sw in FilteredSoftwareItems)
        {
            sw.IsSelected = true;
        }
        UpdateSelectedSoftwareSummary();
    }

    private void ClearSoftwareSelection()
    {
        foreach (var sw in AvailableSoftwareItems)
        {
            sw.IsSelected = false;
        }
        UpdateSelectedSoftwareSummary();
    }

    public bool ValidatePasswords()
    {
        if (string.IsNullOrWhiteSpace(SupportAdminPassword))
        {
            ValidationErrorMessage = "⚠️ A definição de senha para o usuário administrador 'suporte' é OBRIGATÓRIA.";
            return false;
        }

        if (SupportAdminPassword != SupportAdminPasswordConfirm)
        {
            ValidationErrorMessage = "⚠️ A senha e a confirmação de senha não coincidem.";
            return false;
        }

        ValidationErrorMessage = string.Empty;
        return true;
    }

    public async Task RunPreCheckAsync()
    {
        IsRunningPreCheck = true;
        try
        {
            bool joinAd = IsAdministrativeMode && IsAdministrativeJoinAdChecked;
            PreCheckReport = await _preCheckService.RunPreCheckAsync(SelectedComputerType, joinAd);
        }
        catch (Exception ex)
        {
            _logger.LogError("PreparationViewModel", "Erro ao executar pré-checagem", ex);
        }
        finally
        {
            IsRunningPreCheck = false;
        }
    }

    private async Task LaunchExecutionAsync(bool dryRun)
    {
        try
        {
        // Validar obrigatoriedade de senha do usuário suporte
        if (!dryRun)
        {
            if (string.IsNullOrWhiteSpace(SupportAdminPassword))
            {
                if (OnPromptSupportPassword != null)
                {
                    var promptResult = await OnPromptSupportPassword.Invoke();
                    if (!promptResult.success || string.IsNullOrWhiteSpace(promptResult.password))
                    {
                        _logger.LogInformation("PreparationViewModel", "Técnico cancelou a definição de senha do suporte.");
                        return;
                    }
                    SupportAdminPassword = promptResult.password;
                    SupportAdminPasswordConfirm = promptResult.password;
                }
                else
                {
                    if (!ValidatePasswords()) return;
                }
            }
            else
            {
                if (!ValidatePasswords()) return;
            }
        }

        // 1. Se for administrativo e o toggle de AD estiver ativado, verificar credencial AD
        string? domainUser = null;
        string? domainPass = null;
        bool shouldJoinAd = IsAdministrativeMode && IsAdministrativeJoinAdChecked;

        if (shouldJoinAd && !dryRun)
        {
            // Validar pré-requisitos do AD primeiro
            var adValidation = await _adService.ValidateDomainPreRequisitesAsync(_configService.ActiveDirectory.Domain, _configService.ActiveDirectory.DomainController);
            if (!adValidation.AlreadyJoined || (!string.IsNullOrWhiteSpace(NewComputerName) && !NewComputerName.Equals(Environment.MachineName, StringComparison.OrdinalIgnoreCase)))
            {
                if (OnPromptActiveDirectoryCredentials != null)
                {
                    var credResult = await OnPromptActiveDirectoryCredentials.Invoke(_configService.ActiveDirectory);
                    if (!credResult.success)
                    {
                        _logger.LogInformation("PreparationViewModel", "Operador cancelou diálogo de credenciais do AD.");
                        return;
                    }
                    domainUser = credResult.username;
                    domainPass = credResult.password;
                }
            }
        }

        // 2. Criar Job
        var selectedSoftwareIds = AvailableSoftwareItems.Where(s => s.IsSelected).Select(s => s.Id).ToList();
        string profileId = IsAdministrativeMode ? "administrativo" : SelectedProfileId;

        var job = await _jobOrchestrator.CreateJobAsync(
            SelectedComputerType,
            profileId,
            selectedSoftwareIds,
            dryRun: dryRun,
            joinDomain: shouldJoinAd,
            supportPassword: SupportAdminPassword,
            newComputerName: NewComputerName?.Trim());

        job.DomainUsername = domainUser;
        job.DomainPasswordText = domainPass;
        SupportAdminPassword = string.Empty;
        SupportAdminPasswordConfirm = string.Empty;

        _logger.LogInformation("PreparationViewModel", $"Iniciando Job {job.Id} (Perfil: {job.ProfileDisplayName})");
        OnExecutionStarted?.Invoke(job);
        await _jobOrchestrator.StartJobAsync(job);
        }
        catch (Exception ex)
        {
            ValidationErrorMessage = ex.Message;
            _logger.LogError("PreparationViewModel", "Nao foi possivel iniciar a preparacao.", ex);
        }
    }
}
