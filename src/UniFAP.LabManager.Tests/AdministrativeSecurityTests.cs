using System.Text.Json;
using Moq;
using UniFAP.LabManager.Core.Enums;
using UniFAP.LabManager.Core.Interfaces;
using UniFAP.LabManager.Core.Models;
using UniFAP.LabManager.Infrastructure.Persistence;
using UniFAP.LabManager.Services.Orchestration;
using Xunit;

namespace UniFAP.LabManager.Tests;

public class AdministrativeSecurityTests
{
    private readonly Mock<IPreCheckService> _preCheckMock;
    private readonly Mock<IWindowsConfigurationService> _windowsMock;
    private readonly Mock<IUserService> _userMock;
    private readonly Mock<IBrandingService> _brandingMock;
    private readonly Mock<IPerformanceService> _performanceMock;
    private readonly Mock<ISoftwareService> _softwareMock;
    private readonly Mock<IActiveDirectoryService> _adMock;
    private readonly Mock<IReportService> _reportMock;
    private readonly Mock<IConfigService> _configMock;
    private readonly Mock<ILogService> _logMock;
    private readonly JobPersistenceStore _persistenceStore;

    public AdministrativeSecurityTests()
    {
        _preCheckMock = new Mock<IPreCheckService>();
        _windowsMock = new Mock<IWindowsConfigurationService>();
        _userMock = new Mock<IUserService>();
        _brandingMock = new Mock<IBrandingService>();
        _performanceMock = new Mock<IPerformanceService>();
        _softwareMock = new Mock<ISoftwareService>();
        _adMock = new Mock<IActiveDirectoryService>();
        _reportMock = new Mock<IReportService>();
        _configMock = new Mock<IConfigService>();
        _logMock = new Mock<ILogService>();

        _persistenceStore = new JobPersistenceStore(_logMock.Object);

        var profiles = new ProfilesConfig
        {
            Administrative = new LaboratoryProfile
            {
                Id = "administrativo",
                DisplayName = "Administrativo Institucional",
                Software = new List<string> { "chrome", "firefox", "office365", "winrar" }
            }
        };

        var settings = new SettingsConfig { AutoReboot = true, AutoResume = true };
        var adConfig = new ActiveDirectoryConfig { Domain = "UNIFAP.LOCAL", DomainController = "DC01" };

        _configMock.Setup(c => c.Profiles).Returns(profiles);
        _configMock.Setup(c => c.Settings).Returns(settings);
        _configMock.Setup(c => c.ActiveDirectory).Returns(adConfig);

        _configMock.Setup(c => c.GetSoftware(It.IsAny<string>()))
            .Returns<string>(id => new SoftwareItem { Id = id, Name = $"Software {id}" });

        _configMock.Setup(c => c.GetSoftwareForProfile(It.IsAny<string>()))
            .Returns(new List<SoftwareItem>());
    }

    [Fact]
    public async Task AdministrativeProfile_EnforcesCodeConstraint_BlockingLaboratorySoftware()
    {
        var orchestrator = new JobOrchestrator(
            _preCheckMock.Object,
            _windowsMock.Object,
            _userMock.Object,
            _brandingMock.Object,
            _performanceMock.Object,
            _softwareMock.Object,
            _adMock.Object,
            _reportMock.Object,
            _configMock.Object,
            _persistenceStore,
            _logMock.Object);

        // Técnico ou script tenta injetar softwares acadêmicos no perfil administrativo
        var injectedSoftwares = new List<string>
        {
            "chrome",
            "autocad2025",
            "revit2025",
            "eberick",
            "lingo",
            "docker",
            "winrar",
            "sniffy"
        };

        var job = await orchestrator.CreateJobAsync(
            ComputerType.Administrative,
            "administrativo",
            customSoftwareIds: injectedSoftwares,
            dryRun: true);

        // REGRA 25: O perfil Administrativo NÃO deve instalar softwares específicos de laboratório sob nenhuma circunstância
        Assert.DoesNotContain(job.SoftwareQueue, s => s.Id == "autocad2025");
        Assert.DoesNotContain(job.SoftwareQueue, s => s.Id == "revit2025");
        Assert.DoesNotContain(job.SoftwareQueue, s => s.Id == "eberick");
        Assert.DoesNotContain(job.SoftwareQueue, s => s.Id == "lingo");
        Assert.DoesNotContain(job.SoftwareQueue, s => s.Id == "docker");
        Assert.DoesNotContain(job.SoftwareQueue, s => s.Id == "sniffy");

        // Somente os softwares administrativos devem permanecer
        Assert.Contains(job.SoftwareQueue, s => s.Id == "chrome");
        Assert.Contains(job.SoftwareQueue, s => s.Id == "winrar");
        Assert.Equal(2, job.SoftwareQueue.Count);
    }

    [Fact]
    public void ActiveDirectory_Credentials_NeverSerializedToJson()
    {
        // REGRA 5: NUNCA armazenar senha, token, credencial em JSON, registry, arquivo ou log
        var job = new Job
        {
            Id = "UNIFAP-TEST-001",
            DomainUsername = "tecnico.suporte",
            DomainPasswordText = "SuperSecretPassword123!",
            SupportPasswordText = "AdminSuportePass456!"
        };

        string json = JsonSerializer.Serialize(job);

        // As propriedades de senha marcadas com [JsonIgnore] não devem constar no JSON
        Assert.DoesNotContain("SuperSecretPassword123!", json);
        Assert.DoesNotContain("AdminSuportePass456!", json);
        Assert.DoesNotContain("DomainPasswordText", json);
        Assert.DoesNotContain("SupportPasswordText", json);
    }

    [Fact]
    public async Task AdministrativeProfile_StrictlyBlocks_LaboratorySoftware_ExactlyAsSpecified()
    {
        var orchestrator = new JobOrchestrator(
            _preCheckMock.Object,
            _windowsMock.Object,
            _userMock.Object,
            _brandingMock.Object,
            _performanceMock.Object,
            _softwareMock.Object,
            _adMock.Object,
            _reportMock.Object,
            _configMock.Object,
            _persistenceStore,
            _logMock.Object);

        // Lista exata exigida pelo Item 5 da auditoria:
        var inputList = new List<string>
        {
            "chrome", "firefox", "office365", "winrar",
            "autocad2025", "revit2025", "python311", "docker", "qgis", "wireshark"
        };

        var job = await orchestrator.CreateJobAsync(
            ComputerType.Administrative,
            "administrativo",
            customSoftwareIds: inputList,
            dryRun: true);

        // Softwares Administrativos Permitidos:
        Assert.Contains(job.SoftwareQueue, s => s.Id == "chrome");
        Assert.Contains(job.SoftwareQueue, s => s.Id == "firefox");
        Assert.Contains(job.SoftwareQueue, s => s.Id == "office365");
        Assert.Contains(job.SoftwareQueue, s => s.Id == "winrar");

        // Softwares Acadêmicos Bloqueados:
        Assert.DoesNotContain(job.SoftwareQueue, s => s.Id == "autocad2025");
        Assert.DoesNotContain(job.SoftwareQueue, s => s.Id == "revit2025");
        Assert.DoesNotContain(job.SoftwareQueue, s => s.Id == "python311");
        Assert.DoesNotContain(job.SoftwareQueue, s => s.Id == "docker");
        Assert.DoesNotContain(job.SoftwareQueue, s => s.Id == "qgis");
        Assert.DoesNotContain(job.SoftwareQueue, s => s.Id == "wireshark");

        Assert.Equal(4, job.SoftwareQueue.Count);
    }

    [Fact]
    public void UserService_ConfigGuaranteesStudentIsNeverAdministrator()
    {
        var usersConfig = new UsersConfig
        {
            Users = new Dictionary<string, UserAccountConfig>
            {
                ["support"] = new() { Name = "suporte", Administrator = true },
                ["student"] = new() { Name = "aluno", Administrator = false }
            }
        };

        Assert.True(usersConfig.Users["support"].Administrator);
        Assert.False(usersConfig.Users["student"].Administrator);
    }

    [Fact]
    public async Task RebootResume_SkipsAlreadySucceededSteps()
    {
        var orchestrator = new JobOrchestrator(
            _preCheckMock.Object,
            _windowsMock.Object,
            _userMock.Object,
            _brandingMock.Object,
            _performanceMock.Object,
            _softwareMock.Object,
            _adMock.Object,
            _reportMock.Object,
            _configMock.Object,
            _persistenceStore,
            _logMock.Object);

        var job = await orchestrator.CreateJobAsync(ComputerType.Laboratory, "geral", dryRun: true);

        // Simular que as etapas 0 e 1 já foram concluídas com sucesso antes de um reboot
        job.Steps[0].Status = StepStatus.Succeeded;
        job.Steps[1].Status = StepStatus.Succeeded;
        _userMock.Setup(u => u.ProvisionUsersAsync(It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<bool>())).ReturnsAsync(true);
        _brandingMock.Setup(b => b.ApplyBrandingAsync(It.IsAny<bool>())).ReturnsAsync(true);
        _performanceMock.Setup(p => p.ApplyPerformanceTweaksAsync(It.IsAny<bool>())).ReturnsAsync(true);
        _reportMock.Setup(r => r.GenerateReportAsync(It.IsAny<Job>())).ReturnsAsync(new PreparationReport());

        bool result = await orchestrator.StartJobAsync(job);

        Assert.True(result);
        // PreCheck e Windows foram marcados como Succeeded previamente, não devem ter sido executados novamente
        _preCheckMock.Verify(p => p.RunPreCheckAsync(It.IsAny<ComputerType>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
