using UniFAP.LabManager.Core.Enums;

namespace UniFAP.LabManager.Core.Models;

public class DiagnosticCheckResult
{
    public string Category { get; set; } = "Geral";
    public string Name { get; set; } = string.Empty;
    public HealthStatus Status { get; set; } = HealthStatus.Good;
    public string Value { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? Details { get; set; }
    public string? ResolutionHint { get; set; }
}

public class DiagnosticsReport
{
    public DateTime GeneratedAt { get; set; } = DateTime.Now;
    public HealthStatus OverallStatus { get; set; } = HealthStatus.Good;
    public SystemInfo SystemInfo { get; set; } = new();
    public List<DiagnosticCheckResult> Checks { get; set; } = new();
}
