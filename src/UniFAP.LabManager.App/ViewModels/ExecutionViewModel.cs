using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using UniFAP.LabManager.Core.Enums;
using UniFAP.LabManager.Core.Interfaces;
using UniFAP.LabManager.Core.Models;

namespace UniFAP.LabManager.App.ViewModels;

public class ExecutionViewModel : ViewModelBase
{
    private readonly IJobOrchestrator _jobOrchestrator;
    private readonly ILogService _logger;
    private readonly DispatcherTimer _timer;

    private Job? _currentJob;
    private double _progressPercentage;
    private string _currentStepName = "Aguardando início...";
    private string _elapsedTimeString = "00:00";
    private bool _showAdvancedLogs = false;
    private string _fullLogsText = string.Empty;
    private DateTime? _startTime;

    public Job? CurrentJob
    {
        get => _currentJob;
        set => SetProperty(ref _currentJob, value);
    }

    public double ProgressPercentage
    {
        get => _progressPercentage;
        set => SetProperty(ref _progressPercentage, value);
    }

    public string CurrentStepName
    {
        get => _currentStepName;
        set => SetProperty(ref _currentStepName, value);
    }

    public string ElapsedTimeString
    {
        get => _elapsedTimeString;
        set => SetProperty(ref _elapsedTimeString, value);
    }

    public bool ShowAdvancedLogs
    {
        get => _showAdvancedLogs;
        set => SetProperty(ref _showAdvancedLogs, value);
    }

    public string FullLogsText
    {
        get => _fullLogsText;
        set => SetProperty(ref _fullLogsText, value);
    }

    public ObservableCollection<JobStep> Steps { get; } = new();
    public ObservableCollection<SoftwareItem> SoftwareQueue { get; } = new();
    public ObservableCollection<string> LiveLogsList { get; } = new();

    public bool IsCompleted => CurrentJob != null && (CurrentJob.Status == JobStatus.Succeeded || CurrentJob.Status == JobStatus.Warning || CurrentJob.Status == JobStatus.Failed || CurrentJob.Status == JobStatus.Cancelled);
    public bool IsSuccessful => CurrentJob != null && (CurrentJob.Status == JobStatus.Succeeded || CurrentJob.Status == JobStatus.Warning);
    public bool HasFailed => CurrentJob != null && CurrentJob.Status == JobStatus.Failed;

    public ICommand ToggleAdvancedLogsCommand { get; }
    public ICommand CancelExecutionCommand { get; }
    public ICommand OpenReportTxtCommand { get; }
    public ICommand OpenReportJsonCommand { get; }
    public ICommand OpenLogsFolderCommand { get; }

    public event Action? OnReturnToDashboardRequested;
    public ICommand ReturnToDashboardCommand => new RelayCommand(() => OnReturnToDashboardRequested?.Invoke());

    public ExecutionViewModel(IJobOrchestrator jobOrchestrator, ILogService logger)
    {
        _jobOrchestrator = jobOrchestrator;
        _logger = logger;

        ToggleAdvancedLogsCommand = new RelayCommand(() => ShowAdvancedLogs = !ShowAdvancedLogs);
        CancelExecutionCommand = new AsyncRelayCommand(_jobOrchestrator.CancelJobAsync);
        OpenReportTxtCommand = new RelayCommand(OpenReportTxt);
        OpenReportJsonCommand = new RelayCommand(OpenReportJson);
        OpenLogsFolderCommand = new RelayCommand(OpenLogsFolder);

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (s, e) => UpdateElapsedTime();

        _jobOrchestrator.OnJobUpdated += HandleJobUpdated;
        _jobOrchestrator.OnStepUpdated += HandleStepUpdated;
        _jobOrchestrator.OnLogMessage += HandleLogMessage;
    }

    public void LoadJob(Job job)
    {
        CurrentJob = job;
        _startTime = DateTime.Now;
        _timer.Start();

        Application.Current?.Dispatcher.Invoke(() =>
        {
            Steps.Clear();
            foreach (var step in job.Steps) Steps.Add(step);

            SoftwareQueue.Clear();
            foreach (var sw in job.SoftwareQueue) SoftwareQueue.Add(sw);

            LiveLogsList.Clear();
            FullLogsText = string.Empty;
        });

        UpdateProgress();
    }

    private void HandleJobUpdated(Job job)
    {
        Application.Current?.Dispatcher.Invoke(() =>
        {
            CurrentJob = job;
            UpdateProgress();

            // Atualiza status das etapas
            foreach (var step in job.Steps)
            {
                var existing = Steps.FirstOrDefault(s => s.Id == step.Id);
                if (existing != null)
                {
                    existing.Status = step.Status;
                    existing.ErrorMessage = step.ErrorMessage;
                    existing.Details = step.Details;
                }
            }

            // Atualiza fila de software
            SoftwareQueue.Clear();
            foreach (var sw in job.SoftwareQueue) SoftwareQueue.Add(sw);

            if (job.Status == JobStatus.Running)
            {
                var activeStep = job.Steps.FirstOrDefault(s => s.Status == StepStatus.Running);
                CurrentStepName = activeStep?.Name ?? "Processando...";
            }
            else if (IsCompleted)
            {
                _timer.Stop();
                CurrentStepName = job.Status switch
                {
                    JobStatus.Succeeded => "Computador preparado com sucesso!",
                    JobStatus.Warning => "Computador preparado com advertências.",
                    JobStatus.Failed => $"Falha na preparação: {job.ErrorMessage}",
                    JobStatus.Cancelled => "Preparação cancelada pelo usuário.",
                    _ => "Concluído"
                };
            }

            OnPropertyChanged(nameof(IsCompleted));
            OnPropertyChanged(nameof(IsSuccessful));
            OnPropertyChanged(nameof(HasFailed));
        });
    }

    private void HandleStepUpdated(JobStep step)
    {
        Application.Current?.Dispatcher.Invoke(() =>
        {
            var existing = Steps.FirstOrDefault(s => s.Id == step.Id);
            if (existing != null)
            {
                existing.Status = step.Status;
                existing.ErrorMessage = step.ErrorMessage;
                existing.Details = step.Details;
                int index = Steps.IndexOf(existing);
                Steps[index] = step;
            }
            UpdateProgress();
        });
    }

    private void HandleLogMessage(string message)
    {
        Application.Current?.Dispatcher.Invoke(() =>
        {
            string line = $"[{DateTime.Now:HH:mm:ss}] {message}";
            LiveLogsList.Add(line);
            FullLogsText += line + Environment.NewLine;
        });
    }

    private void UpdateProgress()
    {
        if (CurrentJob != null)
        {
            ProgressPercentage = Math.Round(CurrentJob.CalculateProgress(), 1);
        }
    }

    private void UpdateElapsedTime()
    {
        if (_startTime.HasValue)
        {
            var elapsed = DateTime.Now - _startTime.Value;
            ElapsedTimeString = $"{elapsed.Minutes:D2}:{elapsed.Seconds:D2}";
        }
    }

    private void OpenReportTxt()
    {
        if (CurrentJob == null) return;
        string reportPath = Path.Combine(@"C:\ProgramData\UniFAP\LabManager\Reports", $"UniFAP-LabManager-Report-{CurrentJob.Id}.txt");
        if (File.Exists(reportPath))
        {
            Process.Start(new ProcessStartInfo(reportPath) { UseShellExecute = true });
        }
    }

    private void OpenReportJson()
    {
        if (CurrentJob == null) return;
        string reportPath = Path.Combine(@"C:\ProgramData\UniFAP\LabManager\Reports", $"Report_{CurrentJob.Id}.json");
        if (File.Exists(reportPath))
        {
            Process.Start(new ProcessStartInfo(reportPath) { UseShellExecute = true });
        }
    }

    private void OpenLogsFolder()
    {
        string logsDir = @"C:\ProgramData\UniFAP\LabManager\Logs";
        if (Directory.Exists(logsDir))
        {
            Process.Start(new ProcessStartInfo("explorer.exe", logsDir) { UseShellExecute = true });
        }
    }
}
