using Moq;
using UniFAP.LabManager.App.ViewModels;
using UniFAP.LabManager.Core.Enums;
using UniFAP.LabManager.Core.Interfaces;
using UniFAP.LabManager.Core.Models;
using UniFAP.LabManager.Services.Diagnostics;
using Xunit;

namespace UniFAP.LabManager.Tests;

public class DiagnosticsRemediationTests
{
    private readonly Mock<IDiagnosticsService> _diagServiceMock;
    private readonly Mock<ILogService> _logMock;

    public DiagnosticsRemediationTests()
    {
        _diagServiceMock = new Mock<IDiagnosticsService>();
        _logMock = new Mock<ILogService>();
    }

    [Fact]
    public void DiagnosticCheckResult_WithRemediationAction_ShouldHaveCanAutoRemediateTrue()
    {
        var check = new DiagnosticCheckResult
        {
            Name = "Espaço em Disco (C:)",
            Status = HealthStatus.Critical,
            RemediationAction = "CleanDisk",
            RemediationTitle = "Limpar Disco"
        };

        Assert.True(check.CanAutoRemediate);
        Assert.Equal("CleanDisk", check.RemediationAction);
        Assert.Equal("Limpar Disco", check.RemediationTitle);
    }

    [Fact]
    public void DiagnosticItemViewModel_WhenStatusIsNotGood_ShowsRemediationButton()
    {
        var check = new DiagnosticCheckResult
        {
            Name = "Espaço em Disco (C:)",
            Status = HealthStatus.Critical,
            RemediationAction = "CleanDisk",
            RemediationTitle = "Limpar Disco"
        };

        var vm = new DiagnosticItemViewModel(check, _diagServiceMock.Object, _logMock.Object);

        Assert.True(vm.ShowRemediationButton);
        Assert.False(vm.IsRemediating);
    }

    [Fact]
    public void DiagnosticItemViewModel_WhenStatusIsGood_HidesRemediationButton()
    {
        var check = new DiagnosticCheckResult
        {
            Name = "Espaço em Disco (C:)",
            Status = HealthStatus.Good,
            RemediationAction = "CleanDisk",
            RemediationTitle = "Limpar Disco"
        };

        var vm = new DiagnosticItemViewModel(check, _diagServiceMock.Object, _logMock.Object);

        Assert.False(vm.ShowRemediationButton);
    }

    [Fact]
    public async Task DiagnosticItemViewModel_ExecuteFixAsync_UpdatesStatusAndValue()
    {
        var check = new DiagnosticCheckResult
        {
            Name = "Espaço em Disco (C:)",
            Status = HealthStatus.Critical,
            Value = "7.7 GB livres",
            RemediationAction = "CleanDisk",
            RemediationTitle = "Limpar Disco"
        };

        _diagServiceMock
            .Setup(s => s.RemediateCheckAsync("CleanDisk", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DiagnosticRemediationResult
            {
                Success = true,
                Message = "1.5 GB liberados com sucesso.",
                NewStatus = HealthStatus.Good,
                NewValue = "9.2 GB livres"
            });

        var vm = new DiagnosticItemViewModel(check, _diagServiceMock.Object, _logMock.Object);

        var result = await vm.ExecuteFixAsync();

        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal(HealthStatus.Good, vm.Status);
        Assert.Equal("9.2 GB livres", vm.Value);
        Assert.False(vm.ShowRemediationButton);
        Assert.Contains("1.5 GB liberados", vm.RemediationFeedback);
    }

    [Fact]
    public async Task DiagnosticsViewModel_FixAllProblemsAsync_RemediatesAllIssues()
    {
        var report = new DiagnosticsReport
        {
            OverallStatus = HealthStatus.Critical,
            Checks = new List<DiagnosticCheckResult>
            {
                new DiagnosticCheckResult
                {
                    Name = "Espaço em Disco (C:)",
                    Status = HealthStatus.Critical,
                    RemediationAction = "CleanDisk",
                    RemediationTitle = "Limpar Disco"
                },
                new DiagnosticCheckResult
                {
                    Name = "Resolução de Domínio UniFAP",
                    Status = HealthStatus.Warning,
                    RemediationAction = "ConfigureUniFapDns",
                    RemediationTitle = "Configurar DNS"
                },
                new DiagnosticCheckResult
                {
                    Name = "Acesso à Internet",
                    Status = HealthStatus.Good
                }
            }
        };

        _diagServiceMock
            .Setup(s => s.RunFullDiagnosticsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(report);

        _diagServiceMock
            .Setup(s => s.RemediateCheckAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DiagnosticRemediationResult
            {
                Success = true,
                Message = "Resolvido",
                NewStatus = HealthStatus.Good
            });

        var vm = new DiagnosticsViewModel(_diagServiceMock.Object, _logMock.Object);
        await vm.RunDiagnosticsAsync();

        Assert.True(vm.HasFixableProblems);
        Assert.Equal(2, vm.FixableProblemsCount);

        await vm.FixAllProblemsAsync();

        Assert.False(vm.HasFixableProblems);
        Assert.Equal(0, vm.FixableProblemsCount);
        Assert.Equal(HealthStatus.Good, vm.Report!.OverallStatus);
    }

    [Fact]
    public async Task DiagnosticsService_RemediateCheckAsync_CleanDisk_CallsPerformanceService()
    {
        var perfMock = new Mock<IPerformanceService>();
        perfMock.Setup(p => p.CleanTemporaryFilesAsync(false))
            .ReturnsAsync("1.8 GB liberados");

        var secMock = new Mock<ISecurityService>();
        var cfgMock = new Mock<IConfigService>();
        var wingetMock = new Mock<IWingetService>();
        var supportMock = new Mock<ISupportToolsService>();
        var logMock = new Mock<ILogService>();
        var wmi = new UniFAP.LabManager.Infrastructure.SystemAdapters.WmiAdapter(logMock.Object);
        var psRunner = new UniFAP.LabManager.Infrastructure.Execution.PowerShellRunner(
            new UniFAP.LabManager.Infrastructure.Execution.ProcessRunner(logMock.Object),
            logMock.Object);

        var service = new DiagnosticsService(
            wmi,
            secMock.Object,
            cfgMock.Object,
            wingetMock.Object,
            perfMock.Object,
            supportMock.Object,
            psRunner,
            logMock.Object);

        var result = await service.RemediateCheckAsync("CleanDisk");

        Assert.True(result.Success);
        Assert.Contains("1.8 GB liberados", result.Message);
        perfMock.Verify(p => p.CleanTemporaryFilesAsync(false), Times.Once);
    }

    [Fact]
    public async Task DiagnosticsService_RemediateCheckAsync_StartServiceSpooler_CallsSupportTools()
    {
        var perfMock = new Mock<IPerformanceService>();
        var secMock = new Mock<ISecurityService>();
        var cfgMock = new Mock<IConfigService>();
        var wingetMock = new Mock<IWingetService>();
        var supportMock = new Mock<ISupportToolsService>();
        supportMock.Setup(s => s.RepairPrintSpoolerAsync(false))
            .ReturnsAsync("Spooler reparado.");

        var logMock = new Mock<ILogService>();
        var wmi = new UniFAP.LabManager.Infrastructure.SystemAdapters.WmiAdapter(logMock.Object);
        var psRunner = new UniFAP.LabManager.Infrastructure.Execution.PowerShellRunner(
            new UniFAP.LabManager.Infrastructure.Execution.ProcessRunner(logMock.Object),
            logMock.Object);

        var service = new DiagnosticsService(
            wmi,
            secMock.Object,
            cfgMock.Object,
            wingetMock.Object,
            perfMock.Object,
            supportMock.Object,
            psRunner,
            logMock.Object);

        var result = await service.RemediateCheckAsync("StartService_Spooler");

        Assert.True(result.Success);
        Assert.Equal(HealthStatus.Good, result.NewStatus);
        Assert.Equal("Em execução", result.NewValue);
        supportMock.Verify(s => s.RepairPrintSpoolerAsync(false), Times.Once);
    }

    [Fact]
    public async Task DiagnosticsService_RemediateCheckAsync_UnknownAction_ReturnsFailure()
    {
        var perfMock = new Mock<IPerformanceService>();
        var secMock = new Mock<ISecurityService>();
        var cfgMock = new Mock<IConfigService>();
        var wingetMock = new Mock<IWingetService>();
        var supportMock = new Mock<ISupportToolsService>();
        var logMock = new Mock<ILogService>();
        var wmi = new UniFAP.LabManager.Infrastructure.SystemAdapters.WmiAdapter(logMock.Object);
        var psRunner = new UniFAP.LabManager.Infrastructure.Execution.PowerShellRunner(
            new UniFAP.LabManager.Infrastructure.Execution.ProcessRunner(logMock.Object),
            logMock.Object);

        var service = new DiagnosticsService(
            wmi,
            secMock.Object,
            cfgMock.Object,
            wingetMock.Object,
            perfMock.Object,
            supportMock.Object,
            psRunner,
            logMock.Object);

        var result = await service.RemediateCheckAsync("NonExistentAction");

        Assert.False(result.Success);
        Assert.Contains("Nenhuma ação configurada", result.Message);
    }
}
