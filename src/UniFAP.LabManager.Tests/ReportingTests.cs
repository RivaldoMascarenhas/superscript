using Moq;
using UniFAP.LabManager.Core.Enums;
using UniFAP.LabManager.Core.Interfaces;
using UniFAP.LabManager.Core.Models;
using UniFAP.LabManager.Services.Reporting;
using Xunit;

namespace UniFAP.LabManager.Tests;

public class ReportingTests
{
    private readonly Mock<ILogService> _loggerMock = new();

    [Fact]
    public async Task GenerateReportAsync_ShouldComputeStatisticsAndApproval()
    {
        var reportService = new ReportService(_loggerMock.Object);

        var job = new Job
        {
            Id = "UNIFAP-TEST-001",
            ProfileDisplayName = "ADS",
            ComputerType = ComputerType.Laboratory,
            Status = JobStatus.Succeeded
        };

        job.SoftwareQueue.Add(new SoftwareItem { Name = "Chrome", Status = SoftwareInstallStatus.Installed });
        job.SoftwareQueue.Add(new SoftwareItem { Name = "VS Code", Status = SoftwareInstallStatus.Installed });
        job.SoftwareQueue.Add(new SoftwareItem { Name = "Sniffy", Status = SoftwareInstallStatus.Warning, Legacy = true });

        job.Steps.Add(new JobStep { Name = "Windows", Status = StepStatus.Succeeded });
        job.Steps.Add(new JobStep { Name = "Software", Status = StepStatus.Warning });

        var report = await reportService.GenerateReportAsync(job);

        Assert.NotNull(report);
        Assert.Equal("UNIFAP-TEST-001", report.JobId);
        Assert.Equal(3, report.TotalSoftwareCount);
        Assert.Equal(2, report.InstalledCount);
        Assert.Equal(2, report.WarningCount); // 1 sw warning + 1 step warning
        Assert.Equal(0, report.ErrorCount);
        Assert.Equal("APROVADO COM ADVERTÊNCIAS", report.OverallApproval);
    }
}
