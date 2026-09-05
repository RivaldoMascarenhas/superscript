using System.Collections.ObjectModel;
using System.Windows.Input;
using UniFAP.LabManager.Core.Enums;
using UniFAP.LabManager.Core.Interfaces;
using UniFAP.LabManager.Core.Models;

namespace UniFAP.LabManager.App.ViewModels;

public class DiagnosticsViewModel : ViewModelBase
{
    private readonly IDiagnosticsService _diagnosticsService;
    private readonly ILogService _logger;

    private DiagnosticsReport? _report;
    private bool _isRunning;
    private bool _isBatchFixing;
    private string _selectedCategory = "Todos";

    private readonly List<DiagnosticItemViewModel> _allItems = new();

    public DiagnosticsReport? Report
    {
        get => _report;
        set => SetProperty(ref _report, value);
    }

    public bool IsRunning
    {
        get => _isRunning;
        set => SetProperty(ref _isRunning, value);
    }

    public bool IsBatchFixing
    {
        get => _isBatchFixing;
        set
        {
            if (SetProperty(ref _isBatchFixing, value))
            {
                (FixAllCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
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
                FilterChecks();
            }
        }
    }

    public bool HasFixableProblems => FixableProblemsCount > 0;

    public int FixableProblemsCount => _allItems.Count(i => i.CanAutoRemediate && i.Status != HealthStatus.Good);

    public ObservableCollection<string> Categories { get; } = new();
    public ObservableCollection<DiagnosticItemViewModel> FilteredChecks { get; } = new();

    public ICommand RunDiagnosticsCommand { get; }
    public ICommand SelectCategoryCommand { get; }
    public ICommand FixAllCommand { get; }

    public DiagnosticsViewModel(IDiagnosticsService diagnosticsService, ILogService logger)
    {
        _diagnosticsService = diagnosticsService;
        _logger = logger;

        RunDiagnosticsCommand = new AsyncRelayCommand(RunDiagnosticsAsync);
        SelectCategoryCommand = new RelayCommand(param =>
        {
            if (param is string cat) SelectedCategory = cat;
        });
        FixAllCommand = new AsyncRelayCommand(FixAllProblemsAsync, () => HasFixableProblems && !IsBatchFixing);

        InitializeCategories();
    }

    private void InitializeCategories()
    {
        Categories.Clear();
        Categories.Add("Todos");
        Categories.Add("Sistema");
        Categories.Add("Hardware");
        Categories.Add("Armazenamento");
        Categories.Add("Rede");
        Categories.Add("Active Directory");
        Categories.Add("Segurança");
        Categories.Add("Software");
        Categories.Add("Serviços");
    }

    public async Task RunDiagnosticsAsync()
    {
        IsRunning = true;
        try
        {
            Report = await _diagnosticsService.RunFullDiagnosticsAsync();
            BuildItemViewModels();
            FilterChecks();
        }
        catch (Exception ex)
        {
            _logger.LogError("DiagnosticsViewModel", "Erro ao rodar diagnóstico", ex);
        }
        finally
        {
            IsRunning = false;
        }
    }

    private void BuildItemViewModels()
    {
        _allItems.Clear();
        if (Report == null)
        {
            UpdateFixableSummary();
            return;
        }

        foreach (var check in Report.Checks)
        {
            var vm = new DiagnosticItemViewModel(check, _diagnosticsService, _logger);
            vm.OnStatusChanged += OnItemStatusChanged;
            _allItems.Add(vm);
        }

        UpdateFixableSummary();
    }

    private void OnItemStatusChanged()
    {
        UpdateFixableSummary();

        // Recalcula o status geral do relatório
        if (Report != null && _allItems.Count > 0)
        {
            if (_allItems.Any(i => i.Status == HealthStatus.Critical))
                Report.OverallStatus = HealthStatus.Critical;
            else if (_allItems.Any(i => i.Status == HealthStatus.Warning))
                Report.OverallStatus = HealthStatus.Warning;
            else
                Report.OverallStatus = HealthStatus.Good;

            OnPropertyChanged(nameof(Report));
        }
    }

    private void UpdateFixableSummary()
    {
        OnPropertyChanged(nameof(HasFixableProblems));
        OnPropertyChanged(nameof(FixableProblemsCount));
        (FixAllCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
    }

    public async Task FixAllProblemsAsync()
    {
        if (IsBatchFixing) return;

        IsBatchFixing = true;
        _logger.LogInformation("DiagnosticsViewModel", "Iniciando correção em lote de todos os problemas detectados...");

        try
        {
            var fixableItems = _allItems.Where(i => i.CanAutoRemediate && i.Status != HealthStatus.Good).ToList();
            foreach (var item in fixableItems)
            {
                await item.ExecuteFixAsync();
            }

            _logger.LogInformation("DiagnosticsViewModel", "Correção em lote finalizada.");
        }
        catch (Exception ex)
        {
            _logger.LogError("DiagnosticsViewModel", "Erro ao executar correções em lote", ex);
        }
        finally
        {
            IsBatchFixing = false;
            UpdateFixableSummary();
        }
    }

    private void FilterChecks()
    {
        FilteredChecks.Clear();
        if (Report == null) return;

        var items = _allItems.AsEnumerable();
        if (SelectedCategory != "Todos")
        {
            items = items.Where(c => c.Category.Equals(SelectedCategory, StringComparison.OrdinalIgnoreCase));
        }

        foreach (var item in items)
        {
            FilteredChecks.Add(item);
        }
    }
}

