using System.Text;
using Moq;
using UniFAP.LabManager.Core.Enums;
using UniFAP.LabManager.Core.Interfaces;
using UniFAP.LabManager.Core.Models;
using UniFAP.LabManager.Infrastructure.Execution;
using UniFAP.LabManager.Infrastructure.Persistence;
using UniFAP.LabManager.Infrastructure.Security;
using UniFAP.LabManager.Services.ActiveDirectory;
using UniFAP.LabManager.Services.Orchestration;

namespace UniFAP.LabManager.Tests;

public class ExecutionRegressionTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "UniFAP_Regression_" + Guid.NewGuid().ToString("N"));
    private readonly Mock<ILogService> _log = new();
    private readonly Mock<IWindowsConfigurationService> _windows = new();
    private readonly Mock<IUserService> _users = new();
    private readonly Mock<IBrandingService> _branding = new();
    private readonly Mock<IActiveDirectoryService> _ad = new();
    private readonly Mock<IReportService> _reports = new();
    private readonly Mock<ISoftwareService> _software = new();
    private readonly Mock<IConfigService> _config = new();
    private readonly JobPersistenceStore _store;
    private readonly JobOrchestrator _orchestrator;

    public ExecutionRegressionTests()
    {
        _store = new JobPersistenceStore(_log.Object, _directory);
        _config.SetupGet(c => c.Settings).Returns(new SettingsConfig());
        _config.SetupGet(c => c.Profiles).Returns(new ProfilesConfig());
        _config.SetupGet(c => c.Users).Returns(new UsersConfig());
        _config.SetupGet(c => c.ActiveDirectory).Returns(new ActiveDirectoryConfig { Domain = "lab.test" });
        _users.Setup(u => u.ProvisionUsersAsync(It.IsAny<string>(), It.IsAny<string>(), false)).ReturnsAsync(true);
        _users.Setup(u => u.IsUserConfiguredAsync(It.IsAny<string>())).ReturnsAsync(true);
        _users.Setup(u => u.IsInAdminGroupAsync("suporte")).ReturnsAsync(true);
        _ad.Setup(a => a.IsDomainJoinedAsync()).ReturnsAsync(true);
        _ad.Setup(a => a.GetCurrentDomainAsync()).ReturnsAsync("lab.test");
        _ad.Setup(a => a.JoinDomainAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), false))
            .ReturnsAsync(new AdJoinResult { Success = true, NeedsReboot = true });
        _reports.Setup(r => r.GenerateReportAsync(It.IsAny<Job>())).ReturnsAsync(new PreparationReport());
        _orchestrator = new JobOrchestrator(Mock.Of<IPreCheckService>(), _windows.Object, _users.Object,
            _branding.Object, Mock.Of<IPerformanceService>(), _software.Object, _ad.Object, _reports.Object,
            _config.Object, _store, _log.Object);
    }

    private static Job JobWith(params StepType[] types) => new()
    {
        Steps = types.Select(t => new JobStep { Type = t, Name = t.ToString() }).ToList()
    };

    [Fact]
    public async Task ConcurrentExecution_IsRejectedAcrossOrchestratorInstances()
    {
        using var lease = _store.TryAcquireExecutionLease();
        Assert.NotNull(lease);
        Assert.False(await _orchestrator.StartJobAsync(JobWith(StepType.Validation)));
        _users.Verify(u => u.IsUserConfiguredAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task CompletedJob_IsSavedInHistoryWithoutActiveState()
    {
        var job = JobWith(StepType.Report);
        job.Status = JobStatus.Succeeded;
        await _store.SaveActiveJobAsync(job);
        Assert.Null(await _store.LoadActiveJobAsync());
        Assert.Single(await _store.GetAllJobsHistoryAsync());
        Assert.Empty(Directory.GetFiles(_directory, "*.tmp", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task Simulation_DoesNotCreateShortcutsOrValidateRealMachine()
    {
        var job = JobWith(StepType.Software, StepType.Validation, StepType.Report);
        job.DryRun = true;
        Assert.True(await _orchestrator.StartJobAsync(job));
        _branding.Verify(b => b.CreateDesktopShortcutsAsync(), Times.Never);
        _users.Verify(u => u.IsUserConfiguredAsync(It.IsAny<string>()), Times.Never);
        _windows.Verify(w => w.RequestRebootAsync(It.IsAny<int>(), It.IsAny<bool>()), Times.Never);
        Assert.Null(await _store.LoadActiveJobAsync());
    }

    [Fact]
    public async Task Report_ReceivesFinalStateAndCompletedReportStep()
    {
        JobStatus? capturedStatus = null;
        _reports.Setup(r => r.GenerateReportAsync(It.IsAny<Job>())).Callback<Job>(job =>
        {
            capturedStatus = job.Status;
            Assert.NotNull(job.CompletedAt);
            Assert.All(job.Steps, step => Assert.Equal(StepStatus.Succeeded, step.Status));
        }).ReturnsAsync(new PreparationReport());
        var job = JobWith(StepType.Validation, StepType.Report);
        Assert.True(await _orchestrator.StartJobAsync(job));
        Assert.Equal(JobStatus.Succeeded, capturedStatus);
        _reports.Verify(r => r.GenerateReportAsync(job), Times.Once);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Reboot_IsDeferredUntilCredentialsAreConsumed_AndNotRepeated(bool autoResume)
    {
        var job = JobWith(StepType.Users, StepType.ActiveDirectory, StepType.Validation, StepType.Report);
        job.SupportPasswordText = "test-support";
        job.DomainPasswordText = "test-domain";
        job.JoinActiveDirectory = true;
        job.NeedsReboot = true; // e.g. computer renamed in an earlier step
        job.AutoResume = autoResume;
        Assert.True(await _orchestrator.StartJobAsync(job));
        Assert.Equal(2, job.CurrentStepIndex);
        Assert.Null(job.SupportPasswordText);
        Assert.Null(job.DomainPasswordText);
        _users.Verify(u => u.ProvisionUsersAsync("test-support", null, false), Times.Once);
        _ad.Verify(a => a.JoinDomainAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), "test-domain", false), Times.Once);
        _windows.Verify(w => w.RequestRebootAsync(10, autoResume), Times.Once);
        Assert.Null(await _orchestrator.CheckForPendingResumedJobAsync()); // same Windows boot
        DateTime started = job.StartedAt!.Value;
        job.RebootRequestedAtUtc = DateTime.UtcNow - TimeSpan.FromMilliseconds(Environment.TickCount64) - TimeSpan.FromMinutes(1);
        await _store.SaveActiveJobAsync(job);
        var resumed = await _orchestrator.CheckForPendingResumedJobAsync();
        Assert.NotNull(resumed);
        Assert.False(resumed.NeedsReboot);
        Assert.True(await _orchestrator.StartJobAsync(resumed));
        Assert.Equal(started, resumed.StartedAt);
        _windows.Verify(w => w.RequestRebootAsync(It.IsAny<int>(), It.IsAny<bool>()), Times.Once);
    }

    [Fact]
    public async Task ManualReboot_PreservesPendingValidation()
    {
        var job = JobWith(StepType.Validation, StepType.Report);
        job.NeedsReboot = true;
        job.AutoReboot = false;
        Assert.True(await _orchestrator.StartJobAsync(job));
        Assert.Equal(JobStatus.Running, job.Status);
        Assert.NotNull(await _store.LoadActiveJobAsync());
        _windows.Verify(w => w.RequestRebootAsync(It.IsAny<int>(), It.IsAny<bool>()), Times.Never);
    }

    [Fact]
    public async Task Validation_FailsWhenStudentHasAdministratorPrivileges()
    {
        _users.Setup(u => u.IsInAdminGroupAsync("aluno")).ReturnsAsync(true);
        var job = JobWith(StepType.Validation, StepType.Report);
        Assert.False(await _orchestrator.StartJobAsync(job));
        Assert.Equal(JobStatus.Failed, job.Status);
        Assert.Contains("aluno", job.Steps[0].ErrorMessage);
    }

    [Fact]
    public async Task ReportFailure_MarksJobFailed()
    {
        _reports.Setup(r => r.GenerateReportAsync(It.IsAny<Job>())).ThrowsAsync(new IOException("disk full"));
        var job = JobWith(StepType.Report);
        Assert.False(await _orchestrator.StartJobAsync(job));
        Assert.Equal(StepStatus.Failed, job.Steps[0].Status);
    }

    [Fact]
    public async Task Cancellation_IsNotConvertedToSoftwareWarningOrSuccess()
    {
        using var cts = new CancellationTokenSource();
        _software.Setup(s => s.InstallAsync(It.IsAny<SoftwareItem>(), false, It.IsAny<Action<string>>(), It.IsAny<CancellationToken>()))
            .Callback(() => cts.Cancel()).ReturnsAsync(new SoftwareInstallResult { Status = SoftwareInstallStatus.Failed });
        var job = JobWith(StepType.Software, StepType.Validation);
        job.SoftwareQueue.Add(new SoftwareItem { Name = "test" });
        Assert.False(await _orchestrator.StartJobAsync(job, cts.Token));
        Assert.Equal(JobStatus.Cancelled, job.Status);
        _branding.Verify(b => b.CreateDesktopShortcutsAsync(), Times.Never);
    }

    [Theory]
    [InlineData("PC'; Write-Output 'injected")]
    [InlineData("1234")]
    [InlineData("-PC")]
    [InlineData("PC-")]
    [InlineData("ABCDEFGHIJKLMNOP")]
    public async Task InvalidComputerName_IsRejectedBeforeSaving(string name)
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _orchestrator.CreateJobAsync(ComputerType.Laboratory, "geral", newComputerName: name));
        Assert.Null(await _store.LoadActiveJobAsync());
    }

    [Fact]
    public async Task EmptySelection_DoesNotRepopulateProfileSoftware()
    {
        var job = await _orchestrator.CreateJobAsync(ComputerType.Laboratory, "geral", new List<string>());
        Assert.Empty(job.SoftwareQueue);
        _config.Verify(c => c.GetSoftwareForProfile(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void PathValidation_RejectsSiblingWithSamePrefix()
    {
        Assert.False(new SecurityService().ValidatePathSafety(@"C:\app-externo\evil.exe", @"C:\app"));
        Assert.True(new SecurityService().ValidatePathSafety(@"C:\app\software\setup.exe", @"C:\app"));
    }

    [Fact]
    public async Task SensitivePowerShell_UsesStdinAndDoesNotLogPayload()
    {
        var messages = new List<string>();
        _log.Setup(l => l.LogDebug(It.IsAny<string>(), It.IsAny<string>())).Callback<string, string>((_, m) => { lock (messages) messages.Add(m); });
        _log.Setup(l => l.LogInformation(It.IsAny<string>(), It.IsAny<string>())).Callback<string, string>((_, m) => { lock (messages) messages.Add(m); });
        const string secret = "only-a-regression-test-secret-áç漢";
        string script = "$secret = '" + secret + "'; Write-Output ('stdin-ok:' + [int][char]$secret[$secret.Length-1])";
        var runner = new PowerShellRunner(new ProcessRunner(_log.Object), _log.Object);
        var result = await runner.ExecuteCommandAsync(script, sensitive: true);
        Assert.True(result.Success, result.StandardError);
        Assert.Contains("stdin-ok:28450", result.StandardOutput);
        Assert.DoesNotContain(messages, m => m.Contains(secret) || m.Contains(Convert.ToBase64String(Encoding.Unicode.GetBytes(script))));
        string encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));
        Assert.DoesNotContain(encoded, new SecurityService().SanitizeLogString("-EncodedCommand " + encoded));
    }

    [Theory]
    [InlineData("-WhatIf", true)]
    [InlineData("", false)]
    public async Task UserProvision_SimulationOrMissingPassword_DoesNotAccessAccounts(string arguments, bool expected)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "UniFAP.LabManager.sln"))) directory = directory.Parent;
        Assert.NotNull(directory);
        string path = Path.Combine(directory.FullName, "scripts", "User-Provision.ps1").Replace("'", "''");
        string command = "function Get-LocalUser { throw 'ACCOUNT_ACCESS_FORBIDDEN' }; & '" + path + "' " + arguments;
        var result = await new PowerShellRunner(new ProcessRunner(_log.Object), _log.Object).ExecuteCommandAsync(command);
        Assert.DoesNotContain("ACCOUNT_ACCESS_FORBIDDEN", result.StandardOutput);
        using var json = System.Text.Json.JsonDocument.Parse(result.StandardOutput.Trim());
        Assert.Equal(expected, json.RootElement.GetProperty("Success").GetBoolean());
        if (!expected) Assert.Contains("Informe a senha", result.StandardOutput);
    }

    [Theory]
    [InlineData(0, "{\"Success\":false,\"Message\":\"DNS failed\"}", false)]
    [InlineData(0, "{\"Success\":true,\"NeedsReboot\":false}", true)]
    [InlineData(1, "{\"Success\":true}", false)]
    [InlineData(0, "unexpected output", false)]
    public void DomainJoin_RespectsStructuredResult(int exitCode, string output, bool expected)
    {
        var result = ActiveDirectoryService.ParseJoinResult(new ProcessExecutionResult { ExitCode = exitCode, StandardOutput = output });
        Assert.Equal(expected, result.Success);
        Assert.False(result.NeedsReboot);
    }

    public void Dispose() => Directory.Delete(_directory, true);
}
