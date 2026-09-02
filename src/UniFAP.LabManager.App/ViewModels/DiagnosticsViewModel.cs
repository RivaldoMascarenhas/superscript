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
    private string _selectedCategory = "Todos";

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

    public ObservableCollection<string> Categories { get; } = new();
    public ObservableCollection<DiagnosticCheckResult> FilteredChecks { get; } = new();

    public ICommand RunDiagnosticsCommand { get; }
    public ICommand SelectCategoryCommand { get; }

    public DiagnosticsViewModel(IDiagnosticsService diagnosticsService, ILogService logger)
    {
        _diagnosticsService = diagnosticsService;
        _logger = logger;

        RunDiagnosticsCommand = new AsyncRelayCommand(RunDiagnosticsAsync);
        SelectCategoryCommand = new RelayCommand(param =>
        {
            if (param is string cat) SelectedCategory = cat;
        });

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

    private void FilterChecks()
    {
        FilteredChecks.Clear();
        if (Report == null) return;

        var items = Report.Checks.AsEnumerable();
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
