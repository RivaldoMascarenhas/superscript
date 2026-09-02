using System.IO;
using System.Text.Json;
using Moq;
using UniFAP.LabManager.Core.Interfaces;
using UniFAP.LabManager.Core.Models;
using UniFAP.LabManager.Services.Configuration;
using Xunit;

namespace UniFAP.LabManager.Tests;

public class ConfigurationTests
{
    private readonly string _configDir;
    private readonly string _themesDir;
    private readonly Mock<ILogService> _loggerMock;

    public ConfigurationTests()
    {
        _loggerMock = new Mock<ILogService>();

        // Localizar a pasta config na raiz do repositório
        string current = AppDomain.CurrentDomain.BaseDirectory;
        _configDir = current;
        _themesDir = current;

        for (int i = 0; i < 8; i++)
        {
            string candidateConfig = Path.Combine(current, "config");
            string candidateThemes = Path.Combine(current, "themes");
            if (Directory.Exists(candidateConfig) && Directory.Exists(candidateThemes))
            {
                _configDir = candidateConfig;
                _themesDir = candidateThemes;
                break;
            }
            var parent = Directory.GetParent(current);
            if (parent == null) break;
            current = parent.FullName;
        }
    }

    [Fact]
    public async Task LoadAllAsync_ShouldLoadInstitutionAndProfilesSuccessfully()
    {
        var configService = new ConfigService(_loggerMock.Object, _configDir, _themesDir);
        await configService.LoadAllAsync();

        Assert.NotNull(configService.Institution);
        Assert.Contains("UNIFAP", configService.Institution.Name, StringComparison.OrdinalIgnoreCase);
        Assert.NotNull(configService.Profiles);
        Assert.NotNull(configService.Profiles.Administrative);
        Assert.True(configService.Profiles.Laboratories.Count > 0);
        Assert.Equal("intranet.unifapce.edu.br", configService.ActiveDirectory.Domain);
    }

    [Fact]
    public async Task GetProfile_ShouldResolveLaboratoriesAndAdministrativeCorrectly()
    {
        var configService = new ConfigService(_loggerMock.Object, _configDir, _themesDir);
        await configService.LoadAllAsync();

        var adminProfile = configService.GetProfile("administrativo");
        Assert.NotNull(adminProfile);
        Assert.True(adminProfile.JoinDomain);

        var adsProfile = configService.GetProfile("ads");
        Assert.NotNull(adsProfile);
        Assert.Equal("Análise e Desenvolvimento de Sistemas (ADS)", adsProfile.DisplayName);
        Assert.Contains("vscode", adsProfile.Software);
        Assert.Contains("postgresql", adsProfile.Software);

        var engProfile = configService.GetProfile("engenharia");
        Assert.NotNull(engProfile);
        Assert.Contains("autocad2025", engProfile.Software);
    }

    [Fact]
    public async Task GetSoftwareForProfile_ShouldReturnClonedInstancesWithSelection()
    {
        var configService = new ConfigService(_loggerMock.Object, _configDir, _themesDir);
        await configService.LoadAllAsync();

        var swList = configService.GetSoftwareForProfile("geral");
        Assert.NotEmpty(swList);
        Assert.True(swList.All(s => s.IsSelected));
        Assert.Contains(swList, s => s.Id == "chrome");
    }

    [Fact]
    public async Task SoftwareCatalog_ShouldContainRequiredCategoriesAndItems()
    {
        var configService = new ConfigService(_loggerMock.Object, _configDir, _themesDir);
        await configService.LoadAllAsync();

        Assert.Contains("Básicos", configService.SoftwareCatalog.Categories);
        Assert.Contains("Desenvolvimento", configService.SoftwareCatalog.Categories);
        Assert.Contains("Engenharia", configService.SoftwareCatalog.Categories);

        var sniffy = configService.GetSoftware("sniffy");
        Assert.NotNull(sniffy);
        Assert.True(sniffy.Legacy);
    }

    [Fact]
    public async Task BrandingService_GetWallpaperPath_ShouldFindPhysicalWallpaperFile()
    {
        var configService = new ConfigService(_loggerMock.Object, _configDir, _themesDir);
        await configService.LoadAllAsync();

        var brandingService = new UniFAP.LabManager.Services.Branding.BrandingService(configService, _loggerMock.Object);
        string path = brandingService.GetWallpaperPath();

        Assert.True(System.IO.File.Exists(path), $"O arquivo de wallpaper deve existir fisicamente em: {path}");
    }
}
