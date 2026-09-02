using Moq;
using UniFAP.LabManager.Core.Enums;
using UniFAP.LabManager.Core.Interfaces;
using UniFAP.LabManager.Core.Models;
using UniFAP.LabManager.Infrastructure.Persistence;
using UniFAP.LabManager.Services.Configuration;
using UniFAP.LabManager.Services.Orchestration;
using Xunit;

namespace UniFAP.LabManager.Tests;

public class JobOrchestratorTests
{
    private readonly Mock<IPreCheckService> _preCheckMock = new();
    private readonly Mock<IWindowsConfigurationService> _windowsMock = new();
    private readonly Mock<IUserService> _userMock = new();
    private readonly Mock<IBrandingService> _brandingMock = new();
    private readonly Mock<IPerformanceService> _perfMock = new();
    private readonly Mock<ISoftwareService> _softwareMock = new();
    private readonly Mock<IActiveDirectoryService> _adMock = new();
    private readonly Mock<IReportService> _reportMock = new();
    private readonly Mock<ILogService> _loggerMock = new();
    private readonly ConfigService _configService;
    private readonly JobPersistenceStore _persistenceStore;

    public JobOrchestratorTests()
    {
        string current = AppDomain.CurrentDomain.BaseDirectory;
        string configDir = current;
        string themesDir = current;

        for (int i = 0; i < 8; i++)
        {
            string candidateConfig = Path.Combine(current, "config");
            string candidateThemes = Path.Combine(current, "themes");
            if (Directory.Exists(candidateConfig) && Directory.Exists(candidateThemes))
            {
                configDir = candidateConfig;
                themesDir = candidateThemes;
                break;
            }
            var parent = Directory.GetParent(current);
            if (parent == null) break;
            current = parent.FullName;
        }

        _configService = new ConfigService(_loggerMock.Object, configDir, themesDir);
        _configService.LoadAllAsync().GetAwaiter().GetResult();

        _persistenceStore = new JobPersistenceStore(_loggerMock.Object);
    }

    [Fact]
    public async Task CreateJobAsync_ForAdministrative_ShouldIncludeActiveDirectoryStep()
    {
        var orchestrator = new JobOrchestrator(
            _preCheckMock.Object, _windowsMock.Object, _userMock.Object,
            _brandingMock.Object, _perfMock.Object, _softwareMock.Object,
            _adMock.Object, _reportMock.Object, _configService,
            _persistenceStore, _loggerMock.Object);

        var job = await orchestrator.CreateJobAsync(ComputerType.Administrative, "administrativo");

        Assert.NotNull(job);
        Assert.Equal(ComputerType.Administrative, job.ComputerType);
        Assert.True(job.JoinActiveDirectory);
        Assert.Contains(job.Steps, s => s.Type == StepType.ActiveDirectory);
        Assert.Contains(job.Steps, s => s.Type == StepType.Users);
        Assert.Contains(job.Steps, s => s.Type == StepType.Branding);
    }

    [Fact]
    public async Task CreateJobAsync_ForLaboratoryADS_ShouldQueueADSSoftwares()
    {
        var orchestrator = new JobOrchestrator(
            _preCheckMock.Object, _windowsMock.Object, _userMock.Object,
            _brandingMock.Object, _perfMock.Object, _softwareMock.Object,
            _adMock.Object, _reportMock.Object, _configService,
            _persistenceStore, _loggerMock.Object);

        var job = await orchestrator.CreateJobAsync(ComputerType.Laboratory, "ads");

        Assert.NotNull(job);
        Assert.Equal(ComputerType.Laboratory, job.ComputerType);
        Assert.False(job.JoinActiveDirectory);
        Assert.Contains(job.Steps, s => s.Type == StepType.Users);
        Assert.NotEmpty(job.SoftwareQueue);
        Assert.Contains(job.SoftwareQueue, s => s.Id == "vscode");
        Assert.Contains(job.SoftwareQueue, s => s.Id == "python311");
    }

    [Fact]
    public async Task StartJobAsync_InDryRunMode_ShouldSimulateAllStepsWithoutErrors()
    {
        // Setup Mocks
        _preCheckMock.Setup(p => p.RunPreCheckAsync(It.IsAny<ComputerType>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PreCheckReport { Summary = "STATUS: PRONTO" });

        _windowsMock.Setup(w => w.ApplyOptimizationsAsync(true)).ReturnsAsync(true);
        _userMock.Setup(u => u.ProvisionUsersAsync(It.IsAny<string>(), It.IsAny<string>(), true)).ReturnsAsync(true);
        _brandingMock.Setup(b => b.ApplyBrandingAsync(true)).ReturnsAsync(true);
        _perfMock.Setup(p => p.ApplyPerformanceTweaksAsync(true)).ReturnsAsync(true);
        _softwareMock.Setup(s => s.InstallAsync(It.IsAny<SoftwareItem>(), true, It.IsAny<Action<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SoftwareInstallResult { Success = true, Status = SoftwareInstallStatus.Installed });
        _adMock.Setup(a => a.JoinDomainAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), true))
            .ReturnsAsync(new AdJoinResult { Success = true, NeedsReboot = false });
        _reportMock.Setup(r => r.GenerateReportAsync(It.IsAny<Job>())).ReturnsAsync(new PreparationReport());

        var orchestrator = new JobOrchestrator(
            _preCheckMock.Object, _windowsMock.Object, _userMock.Object,
            _brandingMock.Object, _perfMock.Object, _softwareMock.Object,
            _adMock.Object, _reportMock.Object, _configService,
            _persistenceStore, _loggerMock.Object);

        var job = await orchestrator.CreateJobAsync(ComputerType.Laboratory, "geral", dryRun: true);

        bool success = await orchestrator.StartJobAsync(job);

        Assert.True(success);
        Assert.Equal(JobStatus.Succeeded, job.Status);
        Assert.Equal(100.0, job.CalculateProgress());
        Assert.True(job.Steps.All(s => s.Status == StepStatus.Succeeded));
    }

    [Fact]
    public void CalculateProgress_ShouldReturnCorrectPercentages()
    {
        var job = new Job();
        job.Steps.Add(new JobStep { Status = StepStatus.Succeeded });
        job.Steps.Add(new JobStep { Status = StepStatus.Succeeded });
        job.Steps.Add(new JobStep { Status = StepStatus.Pending });
        job.Steps.Add(new JobStep { Status = StepStatus.Pending });

        double progress = job.CalculateProgress();
        Assert.Equal(50.0, progress);
    }
}
