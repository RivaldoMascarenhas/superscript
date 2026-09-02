using UniFAP.LabManager.Core.Enums;

namespace UniFAP.LabManager.Core.Models;

public class PreparationReport
{
    public string JobId { get; set; } = string.Empty;
    public string Institution { get; set; } = "Centro Universitário Paraíso - UNIFAP";
    public string ComputerName { get; set; } = string.Empty;
    public string ProfileDisplayName { get; set; } = string.Empty;
    public ComputerType ComputerType { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public TimeSpan TotalDuration => EndTime - StartTime;
    public JobStatus Status { get; set; }
    public int TotalSoftwareCount { get; set; }
    public int InstalledCount { get; set; }
    public int WarningCount { get; set; }
    public int ErrorCount { get; set; }
    public string OverallApproval => ErrorCount > 0 ? "REPROVADO" : (WarningCount > 0 ? "APROVADO COM ADVERTÊNCIAS" : "APROVADO");
    public List<JobStep> StepResults { get; set; } = new();
    public List<SoftwareItem> SoftwareResults { get; set; } = new();
}
