using Moq;
using UniFAP.LabManager.Core.Enums;
using UniFAP.LabManager.Core.Interfaces;
using UniFAP.LabManager.Core.Models;
using UniFAP.LabManager.Infrastructure.Execution;
using UniFAP.LabManager.Services.Software;
using UniFAP.LabManager.Services.Software.Installers;
using Xunit;

namespace UniFAP.LabManager.Tests;

public class SoftwareInstallerTests
{
    private readonly Mock<IWingetService> _wingetMock;
    private readonly Mock<ILocalInstallerService> _localInstallerMock;
    private readonly Mock<ILogService> _logMock;
    private readonly ProcessRunner _processRunner;

    public SoftwareInstallerTests()
    {
        _wingetMock = new Mock<IWingetService>();
        _localInstallerMock = new Mock<ILocalInstallerService>();
        _logMock = new Mock<ILogService>();
        _processRunner = new ProcessRunner(_logMock.Object);
    }

    [Fact]
    public void WingetInstaller_CanHandle_WingetPackages()
    {
        var installer = new WingetInstaller(_wingetMock.Object, _processRunner, _logMock.Object);
        var wingetItem = new SoftwareItem { Type = SoftwareType.Winget, WingetId = "Google.Chrome" };
        var localItem = new SoftwareItem { Type = SoftwareType.Local, Installer = "setup.exe" };

        Assert.True(installer.CanHandle(wingetItem));
        Assert.False(installer.CanHandle(localItem));
    }

    [Fact]
    public void OfficeInstaller_CanHandle_Office365Item()
    {
        var installer = new OfficeInstaller(_processRunner, _logMock.Object);
        var officeItem = new SoftwareItem { Id = "office365", Name = "Microsoft 365 (Office 2024)" };
        var otherItem = new SoftwareItem { Id = "chrome", Name = "Google Chrome" };

        Assert.True(installer.CanHandle(officeItem));
        Assert.False(installer.CanHandle(otherItem));
    }

    [Fact]
    public void MsiInstaller_CanHandle_MsiSoftware()
    {
        var installer = new MsiInstaller(_localInstallerMock.Object, _processRunner, _logMock.Object);
        var msiItem = new SoftwareItem { Type = SoftwareType.Msi, Installer = "installer.msi" };
        var exeItem = new SoftwareItem { Type = SoftwareType.Exe, Installer = "setup.exe" };

        Assert.True(installer.CanHandle(msiItem));
        Assert.False(installer.CanHandle(exeItem));
    }

    [Fact]
    public async Task SoftwareEngine_DryRun_ReturnsSuccessInstantly()
    {
        var catalogSyncMock = new Mock<ICatalogSyncService>();
        var configMock = new Mock<IConfigService>();
        var installerMock = new Mock<ISoftwareInstaller>();

        var engine = new SoftwareEngine(
            new[] { installerMock.Object },
            catalogSyncMock.Object,
            configMock.Object,
            _logMock.Object);

        var item = new SoftwareItem { Id = "vscode", Name = "VS Code", Type = SoftwareType.Winget };
        var result = await engine.InstallAsync(item, dryRun: true);

        Assert.True(result.Success);
        Assert.Equal(SoftwareInstallStatus.Installed, result.Status);
        Assert.Contains("SIMULAÇÃO", result.Message);
    }

    [Fact]
    public async Task LocalInstaller_ReturnsWarning_WhenLegacySoftwareIsMissing()
    {
        var secMock = new Mock<ISecurityService>();
        secMock.Setup(s => s.ValidatePathSafety(It.IsAny<string>(), It.IsAny<string>())).Returns(true);

        var localService = new LocalInstallerService(_processRunner, secMock.Object, _logMock.Object);

        var legacyItem = new SoftwareItem
        {
            Id = "sniffy",
            Name = "Sniffy Pro",
            Type = SoftwareType.Local,
            Legacy = true,
            Installer = "non_existent_folder/setup.exe"
        };

        var result = await localService.RunInstallerAsync(legacyItem, dryRun: false);

        // Software legado não deve quebrar a execução global, retornando status Warning
        Assert.True(result.Success);
        Assert.Equal(SoftwareInstallStatus.Warning, result.Status);
    }
}
