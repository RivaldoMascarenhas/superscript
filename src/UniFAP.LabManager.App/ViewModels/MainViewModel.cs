using System.Windows.Input;
using UniFAP.LabManager.Core.Interfaces;
using UniFAP.LabManager.Core.Models;

namespace UniFAP.LabManager.App.ViewModels;

public class MainViewModel : ViewModelBase
{
    private readonly IConfigService _configService;
    private readonly IDiagnosticsService _diagnosticsService;
    private readonly IJobOrchestrator _jobOrchestrator;
    private readonly ILogService _logger;

    private ViewModelBase? _currentViewModel;
    private SystemInfo _systemInfo = new();
    private string _statusMessage = "Pronto";
    private bool _isBusy;

    public ViewModelBase? CurrentViewModel
    {
        get => _currentViewModel;
        set => SetProperty(ref _currentViewModel, value);
    }

    public SystemInfo SystemInfo
    {
        get => _systemInfo;
        set => SetProperty(ref _systemInfo, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        set => SetProperty(ref _isBusy, value);
    }

    // ViewModels filhas
    public DashboardViewModel DashboardVM { get; }
    public PreparationViewModel PreparationVM { get; }
    public ExecutionViewModel ExecutionVM { get; }
    public SoftwareCatalogViewModel SoftwareCatalogVM { get; }
    public MaintenanceViewModel MaintenanceVM { get; }
    public DiagnosticsViewModel DiagnosticsVM { get; }
    public HistoryViewModel HistoryVM { get; }
    public SettingsViewModel SettingsVM { get; }
    public AboutViewModel AboutVM { get; }

    // Comandos de navegação
    public ICommand NavigateDashboardCommand { get; }
    public ICommand NavigatePreparationCommand { get; }
    public ICommand NavigateExecutionCommand { get; }
    public ICommand NavigateSoftwareCommand { get; }
    public ICommand NavigateMaintenanceCommand { get; }
    public ICommand NavigateDiagnosticsCommand { get; }
    public ICommand NavigateHistoryCommand { get; }
    public ICommand NavigateSettingsCommand { get; }
    public ICommand NavigateAboutCommand { get; }

    public MainViewModel(
        IConfigService configService,
        IDiagnosticsService diagnosticsService,
        IJobOrchestrator jobOrchestrator,
        DashboardViewModel dashboardVM,
        PreparationViewModel preparationVM,
        ExecutionViewModel executionVM,
        SoftwareCatalogViewModel softwareCatalogVM,
        MaintenanceViewModel maintenanceVM,
        DiagnosticsViewModel diagnosticsVM,
        HistoryViewModel historyVM,
        SettingsViewModel settingsVM,
        AboutViewModel aboutVM,
        ILogService logger)
    {
        _configService = configService;
        _diagnosticsService = diagnosticsService;
        _jobOrchestrator = jobOrchestrator;
        _logger = logger;

        DashboardVM = dashboardVM;
        PreparationVM = preparationVM;
        ExecutionVM = executionVM;
        SoftwareCatalogVM = softwareCatalogVM;
        MaintenanceVM = maintenanceVM;
        DiagnosticsVM = diagnosticsVM;
        HistoryVM = historyVM;
        SettingsVM = settingsVM;
        AboutVM = aboutVM;

        NavigateDashboardCommand = new RelayCommand(() => CurrentViewModel = DashboardVM);
        NavigatePreparationCommand = new RelayCommand(() => CurrentViewModel = PreparationVM);
        NavigateExecutionCommand = new RelayCommand(() => CurrentViewModel = ExecutionVM);
        NavigateSoftwareCommand = new RelayCommand(() => CurrentViewModel = SoftwareCatalogVM);
        NavigateMaintenanceCommand = new RelayCommand(() => CurrentViewModel = MaintenanceVM);
        NavigateDiagnosticsCommand = new RelayCommand(() => CurrentViewModel = DiagnosticsVM);
        NavigateHistoryCommand = new RelayCommand(() => CurrentViewModel = HistoryVM);
        NavigateSettingsCommand = new RelayCommand(() => CurrentViewModel = SettingsVM);
        NavigateAboutCommand = new RelayCommand(() => CurrentViewModel = AboutVM);

        // Inicia na Dashboard
        CurrentViewModel = DashboardVM;

        // Assinar eventos de navegação da PreparationVM e JobOrchestrator
        PreparationVM.OnExecutionStarted += (job) =>
        {
            ExecutionVM.LoadJob(job);
            CurrentViewModel = ExecutionVM;
        };
    }

    public async Task InitializeAsync()
    {
        IsBusy = true;
        StatusMessage = "Carregando configurações institucionais...";
        try
        {
            await _configService.LoadAllAsync();
            SystemInfo = await _diagnosticsService.CollectSystemInfoAsync();
            await DashboardVM.InitializeAsync(SystemInfo);
            await SoftwareCatalogVM.InitializeAsync();
            await PreparationVM.InitializeAsync();

            // Verificar se existe Job pendente para retomada automática pós-reboot
            var pendingJob = await _jobOrchestrator.CheckForPendingResumedJobAsync();
            if (pendingJob != null)
            {
                _logger.LogInformation("MainViewModel", $"Job pendente detectado pós-reboot: {pendingJob.Id}. Navegando para Execução...");
                ExecutionVM.LoadJob(pendingJob);
                CurrentViewModel = ExecutionVM;
                _ = _jobOrchestrator.StartJobAsync(pendingJob);
            }

            StatusMessage = "Sistema pronto.";
        }
        catch (Exception ex)
        {
            _logger.LogError("MainViewModel", "Erro ao inicializar MainViewModel", ex);
            StatusMessage = "Erro na inicialização.";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
