using UniFAP.LabManager.Core.Enums;

namespace UniFAP.LabManager.Core.Models;

public class Job
{
    public string Id { get; set; } = $"UNIFAP-{DateTime.Now:yyyyMMdd}-{Random.Shared.Next(10000, 99999)}";
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public ComputerType ComputerType { get; set; } = ComputerType.Laboratory;
    public string ProfileId { get; set; } = "geral";
    public string ProfileDisplayName { get; set; } = "Laboratório Geral";
    public string TargetComputerName { get; set; } = Environment.MachineName;
    public JobStatus Status { get; set; } = JobStatus.Pending;
    public int CurrentStepIndex { get; set; } = 0;
    public List<JobStep> Steps { get; set; } = new();
    public List<SoftwareItem> SoftwareQueue { get; set; } = new();
    public List<string> SelectedSoftwareIds { get; set; } = new();
    public bool JoinActiveDirectory { get; set; } = false;
    public bool AutoReboot { get; set; } = true;
    public bool AutoResume { get; set; } = true;
    public bool DryRun { get; set; } = false;
    public bool NeedsReboot { get; set; } = false;
    public DateTime? RebootRequestedAtUtc { get; set; }
    public bool IsResumed { get; set; } = false;
    public string? ErrorMessage { get; set; }
    public string? ExecutionSummary { get; set; }
    [System.Text.Json.Serialization.JsonIgnore]
    public string? SupportPasswordText { get; set; }
    public string? DomainUsername { get; set; }
    [System.Text.Json.Serialization.JsonIgnore]
    public string? DomainPasswordText { get; set; }
    public string? NewComputerName { get; set; }

    public double CalculateProgress()
    {
        if (Steps.Count == 0) return 0;
        int completed = Steps.Count(s => s.Status == StepStatus.Succeeded || s.Status == StepStatus.Warning || s.Status == StepStatus.Skipped);
        return (double)completed / Steps.Count * 100.0;
    }
}
