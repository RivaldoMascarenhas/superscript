using UniFAP.LabManager.Core.Enums;

namespace UniFAP.LabManager.Core.Models;

public class JobStep
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public StepType Type { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public StepStatus Status { get; set; } = StepStatus.Pending;
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public TimeSpan? Duration => (StartTime.HasValue && EndTime.HasValue) ? EndTime.Value - StartTime.Value : null;
    public string? ErrorMessage { get; set; }
    public string? Details { get; set; }
    public bool IsCritical { get; set; } = true;
}
