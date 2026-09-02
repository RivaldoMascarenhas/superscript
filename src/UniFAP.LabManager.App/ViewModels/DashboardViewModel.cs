using System.Windows.Input;
using UniFAP.LabManager.Core.Enums;
using UniFAP.LabManager.Core.Interfaces;
using UniFAP.LabManager.Core.Models;

namespace UniFAP.LabManager.App.ViewModels;

public class DashboardViewModel : ViewModelBase
{
    private readonly IDiagnosticsService _diagnosticsService;
    private readonly IPerformanceService _performanceService;
    private readonly ILogService _logger;

    private SystemInfo _systemInfo = new();
    private HealthStatus _overallHealth = HealthStatus.Good;
    private string _overallStatusText = "PRONTO";
    private bool _isRefreshing;

    public SystemInfo SystemInfo
    {
        get => _systemInfo;
        set => SetProperty(ref _systemInfo, value);
    }

    public HealthStatus OverallHealth
    {
        get => _overallHealth;
        set => SetProperty(ref _overallHealth, value);
    }

    public string OverallStatusText
    {
        get => _overallStatusText;
        set => SetProperty(ref _overallStatusText, value);
    }

    public bool HasMultipleIps => SystemInfo?.ConnectedAdapters?.Count > 1;
    public string PendingRebootDisplay => SystemInfo.HasPendingReboot ? "Sim (Pendente)" : "Não";
    public bool IsAdminRequiredWarning => !SystemInfo.IsAdministrator;

    public bool IsRefreshing
    {
        get => _isRefreshing;
        set => SetProperty(ref _isRefreshing, value);
    }

    public ICommand RefreshCommand { get; }
    public ICommand QuickOptimizeCommand { get; }

    public event Action? OnNavigateToPreparationRequested;
    public event Action? OnNavigateToDiagnosticsRequested;

    public ICommand NavigateToPreparationCommand => new RelayCommand(() => OnNavigateToPreparationRequested?.Invoke());
    public ICommand NavigateToDiagnosticsCommand => new RelayCommand(() => OnNavigateToDiagnosticsRequested?.Invoke());

    public DashboardViewModel(
        IDiagnosticsService diagnosticsService,
        IPerformanceService performanceService,
        ILogService logger)
    {
        _diagnosticsService = diagnosticsService;
        _performanceService = performanceService;
        _logger = logger;

        RefreshCommand = new AsyncRelayCommand(RefreshAsync);
        QuickOptimizeCommand = new AsyncRelayCommand(QuickOptimizeAsync);
    }

    public async Task InitializeAsync(SystemInfo? initialInfo = null)
    {
        if (initialInfo != null)
        {
            SystemInfo = initialInfo;
        }
        else
        {
            await RefreshAsync();
        }
    }

    public async Task RefreshAsync()
    {
        IsRefreshing = true;
        try
        {
            SystemInfo = await _diagnosticsService.CollectSystemInfoAsync();
            var report = await _diagnosticsService.RunFullDiagnosticsAsync();
            OverallHealth = report.OverallStatus;
            OverallStatusText = OverallHealth switch
            {
                HealthStatus.Good => "PRONTO",
                HealthStatus.Warning => "ATENÇÃO",
                HealthStatus.Critical => "CRÍTICO",
                _ => "DESCONHECIDO"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError("DashboardViewModel", "Erro ao atualizar Dashboard", ex);
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    private async Task QuickOptimizeAsync()
    {
        IsRefreshing = true;
        try
        {
            await _performanceService.ApplyPerformanceTweaksAsync();
            await RefreshAsync();
        }
        finally
        {
            IsRefreshing = false;
        }
    }
}
