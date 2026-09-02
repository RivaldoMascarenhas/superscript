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

    private string _overallStatusReason = "Estação 100% pronta para preparação e uso institucional.";
    public string OverallStatusReason
    {
        get => _overallStatusReason;
        set => SetProperty(ref _overallStatusReason, value);
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
        await RefreshAsync();
    }

    public async Task RefreshAsync()
    {
        IsRefreshing = true;
        try
        {
            SystemInfo = await _diagnosticsService.CollectSystemInfoAsync();
            var report = await _diagnosticsService.RunFullDiagnosticsAsync();
            OverallHealth = report.OverallStatus;

            var warnings = report.Checks.Where(c => c.Status == HealthStatus.Warning).ToList();
            var criticals = report.Checks.Where(c => c.Status == HealthStatus.Critical).ToList();

            if (criticals.Count > 0)
            {
                OverallHealth = HealthStatus.Critical;
                OverallStatusText = "CRÍTICO";
                OverallStatusReason = $"{criticals.Count} alerta(s) crítico(s): {string.Join(", ", criticals.Select(c => c.Name))}";
            }
            else if (warnings.Count > 0)
            {
                OverallHealth = HealthStatus.Warning;
                OverallStatusText = "ATENÇÃO";
                OverallStatusReason = $"{warnings.Count} recomendação(ões): {string.Join(", ", warnings.Select(c => c.Name))}";
            }
            else
            {
                OverallHealth = HealthStatus.Good;
                OverallStatusText = "PRONTO";
                OverallStatusReason = "Estação 100% pronta para preparação e uso institucional.";
            }
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
