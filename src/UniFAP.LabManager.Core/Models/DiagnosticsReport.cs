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

    /// <summary>
    /// Identificador único da ação de correção automática (ex.: "CleanDisk", "ConfigureUniFapDns", "StartService_Spooler").
    /// </summary>
    public string? RemediationAction { get; set; }

    /// <summary>
    /// Rótulo descritivo do botão de correção rápida na interface (ex.: "Limpar Disco", "Configurar DNS").
    /// </summary>
    public string? RemediationTitle { get; set; }

    /// <summary>
    /// Indica se este item possui rotina de auto-remediação disponível.
    /// </summary>
    public bool CanAutoRemediate => !string.IsNullOrWhiteSpace(RemediationAction);
}

public class DiagnosticRemediationResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public HealthStatus NewStatus { get; set; } = HealthStatus.Good;
    public string? NewValue { get; set; }
}

public class DiagnosticsReport
{
    public DateTime GeneratedAt { get; set; } = DateTime.Now;
    public HealthStatus OverallStatus { get; set; } = HealthStatus.Good;
    public SystemInfo SystemInfo { get; set; } = new();
    public List<DiagnosticCheckResult> Checks { get; set; } = new();
}

