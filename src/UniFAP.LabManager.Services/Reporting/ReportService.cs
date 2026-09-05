using System.IO;
using System.Text;
using System.Text.Json;
using UniFAP.LabManager.Core.Enums;
using UniFAP.LabManager.Core.Interfaces;
using UniFAP.LabManager.Core.Models;

namespace UniFAP.LabManager.Services.Reporting;

public class ReportService : IReportService
{
    private readonly ILogService _logger;
    private readonly string _reportsDir;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter(System.Text.Json.JsonNamingPolicy.CamelCase, allowIntegerValues: true) }
    };

    public ReportService(ILogService logger, string? reportsDirectory = null)
    {
        _logger = logger;
        _reportsDir = reportsDirectory ?? @"C:\ProgramData\UniFAP\LabManager\Reports";
        EnsureDirectory();
    }

    private void EnsureDirectory()
    {
        try
        {
            if (!Directory.Exists(_reportsDir))
            {
                Directory.CreateDirectory(_reportsDir);
            }
        }
        catch { }
    }

    public async Task<PreparationReport> GenerateReportAsync(Job job)
    {
        var report = new PreparationReport
        {
            JobId = job.Id,
            Institution = "Centro Universitário Paraíso - UNIFAP",
            ComputerName = job.TargetComputerName,
            ProfileDisplayName = job.ProfileDisplayName,
            ComputerType = job.ComputerType,
            StartTime = job.StartedAt ?? job.CreatedAt,
            EndTime = job.CompletedAt ?? DateTime.Now,
            Status = job.Status,
            DryRun = job.DryRun,
            TotalSoftwareCount = job.SoftwareQueue.Count,
            InstalledCount = job.SoftwareQueue.Count(s => s.Status == SoftwareInstallStatus.Installed),
            WarningCount = job.SoftwareQueue.Count(s => s.Status == SoftwareInstallStatus.Warning) + job.Steps.Count(st => st.Status == StepStatus.Warning),
            ErrorCount = job.SoftwareQueue.Count(s => s.Status == SoftwareInstallStatus.Failed) + job.Steps.Count(st => st.Status == StepStatus.Failed),
            StepResults = new List<JobStep>(job.Steps),
            SoftwareResults = new List<SoftwareItem>(job.SoftwareQueue)
        };

        await SaveReportJsonAsync(report);
        await SaveReportTxtAsync(report);

        return report;
    }

    public async Task<string> SaveReportJsonAsync(PreparationReport report)
    {
        EnsureDirectory();
        string fileName = $"Report_{report.JobId}.json";
        string filePath = Path.Combine(_reportsDir, fileName);

        string json = JsonSerializer.Serialize(report, JsonOptions);
        await File.WriteAllTextAsync(filePath, json);
        _logger.LogInformation("ReportService", $"Relatório JSON gerado em: {filePath}");
        return filePath;
    }

    public async Task<string> SaveReportTxtAsync(PreparationReport report)
    {
        EnsureDirectory();
        string fileName = $"UniFAP-LabManager-Report-{report.JobId}.txt";
        string filePath = Path.Combine(_reportsDir, fileName);

        var sb = new StringBuilder();
        sb.AppendLine("================================================================================");
        sb.AppendLine("                 CENTRO UNIVERSITÁRIO PARAÍSO - UNIFAP                          ");
        sb.AppendLine("                 UNIFAP LAB MANAGER — RELATÓRIO DE PREPARAÇÃO                   ");
        sb.AppendLine("================================================================================");
        sb.AppendLine();
        sb.AppendLine($"Job ID:           {report.JobId}");
        sb.AppendLine($"Computador:       {report.ComputerName}");
        sb.AppendLine($"Perfil Aplicado:  {report.ProfileDisplayName} ({report.ComputerType})");
        sb.AppendLine($"Data / Hora:      {report.StartTime:dd/MM/yyyy HH:mm:ss} até {report.EndTime:HH:mm:ss} (Duração: {report.TotalDuration:mm\\:ss})");
        sb.AppendLine();
        sb.AppendLine("--------------------------------------------------------------------------------");
        sb.AppendLine("ETAPAS EXECUTADAS:");
        sb.AppendLine("--------------------------------------------------------------------------------");

        foreach (var step in report.StepResults)
        {
            string icon = step.Status switch
            {
                StepStatus.Succeeded => "✓",
                StepStatus.Warning => "⚠",
                StepStatus.Failed => "✗",
                StepStatus.Skipped => "○",
                _ => "-"
            };
            sb.AppendLine($"{icon} [{step.Status,-9}] {step.Name,-30} {(step.Duration.HasValue ? $"({step.Duration.Value:mm\\:ss})" : "")}");
            if (!string.IsNullOrWhiteSpace(step.ErrorMessage))
            {
                sb.AppendLine($"    -> {step.ErrorMessage}");
            }
        }

        sb.AppendLine();
        sb.AppendLine("--------------------------------------------------------------------------------");
        sb.AppendLine("SOFTWARES INSTALADOS:");
        sb.AppendLine("--------------------------------------------------------------------------------");

        if (report.SoftwareResults.Count == 0)
        {
            sb.AppendLine("Nenhum software adicional solicitado.");
        }
        else
        {
            foreach (var sw in report.SoftwareResults)
            {
                string icon = sw.Status switch
                {
                    SoftwareInstallStatus.Installed => "✓",
                    SoftwareInstallStatus.Warning => "⚠",
                    SoftwareInstallStatus.Failed => "✗",
                    SoftwareInstallStatus.Skipped => "○",
                    _ => "-"
                };
                sb.AppendLine($"{icon} [{sw.Status,-10}] {sw.Name,-35} ({sw.Category})");
                if (!string.IsNullOrWhiteSpace(sw.ErrorMessage))
                {
                    sb.AppendLine($"    -> Aviso: {sw.ErrorMessage}");
                }
            }
        }

        sb.AppendLine();
        sb.AppendLine("--------------------------------------------------------------------------------");
        sb.AppendLine("SUMÁRIO E RESULTADO FINAL:");
        sb.AppendLine("--------------------------------------------------------------------------------");
        sb.AppendLine($"Total de Softwares:   {report.TotalSoftwareCount}");
        sb.AppendLine($"Instalados com Êxito: {report.InstalledCount}");
        sb.AppendLine($"Advertências:         {report.WarningCount}");
        sb.AppendLine($"Erros / Falhas:       {report.ErrorCount}");
        sb.AppendLine();
        sb.AppendLine($"STATUS FINAL: {report.OverallApproval}");
        sb.AppendLine("================================================================================");

        await File.WriteAllTextAsync(filePath, sb.ToString());
        _logger.LogInformation("ReportService", $"Relatório TXT gerado em: {filePath}");
        return filePath;
    }
}
