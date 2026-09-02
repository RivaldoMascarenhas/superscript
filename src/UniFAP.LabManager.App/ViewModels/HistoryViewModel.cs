using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows.Input;
using UniFAP.LabManager.Core.Interfaces;
using UniFAP.LabManager.Core.Models;

namespace UniFAP.LabManager.App.ViewModels;

public class HistoryViewModel : ViewModelBase
{
    private readonly IJobOrchestrator _jobOrchestrator;
    private readonly ILogService _logger;

    private Job? _selectedJob;
    private bool _isLoading;

    public Job? SelectedJob
    {
        get => _selectedJob;
        set => SetProperty(ref _selectedJob, value);
    }

    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }

    public ObservableCollection<Job> JobsHistory { get; } = new();

    public ICommand RefreshCommand { get; }
    public ICommand OpenReportTxtCommand { get; }
    public ICommand OpenReportJsonCommand { get; }

    public HistoryViewModel(IJobOrchestrator jobOrchestrator, ILogService logger)
    {
        _jobOrchestrator = jobOrchestrator;
        _logger = logger;

        RefreshCommand = new AsyncRelayCommand(LoadHistoryAsync);
        OpenReportTxtCommand = new RelayCommand(OpenReportTxt);
        OpenReportJsonCommand = new RelayCommand(OpenReportJson);
    }

    public async Task LoadHistoryAsync()
    {
        IsLoading = true;
        try
        {
            var list = await _jobOrchestrator.GetJobHistoryAsync();
            JobsHistory.Clear();
            foreach (var job in list)
            {
                JobsHistory.Add(job);
            }
            if (SelectedJob == null && JobsHistory.Count > 0)
            {
                SelectedJob = JobsHistory[0];
            }
        }
        catch (Exception ex)
        {
            _logger.LogError("HistoryViewModel", "Erro ao carregar histórico de jobs", ex);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void OpenReportTxt()
    {
        if (SelectedJob == null) return;
        string reportPath = Path.Combine(@"C:\ProgramData\UniFAP\LabManager\Reports", $"UniFAP-LabManager-Report-{SelectedJob.Id}.txt");
        if (File.Exists(reportPath))
        {
            Process.Start(new ProcessStartInfo(reportPath) { UseShellExecute = true });
        }
    }

    private void OpenReportJson()
    {
        if (SelectedJob == null) return;
        string reportPath = Path.Combine(@"C:\ProgramData\UniFAP\LabManager\Reports", $"Report_{SelectedJob.Id}.json");
        if (File.Exists(reportPath))
        {
            Process.Start(new ProcessStartInfo(reportPath) { UseShellExecute = true });
        }
    }
}
