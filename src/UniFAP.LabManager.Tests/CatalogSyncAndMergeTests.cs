using Moq;
using UniFAP.LabManager.Core.Enums;
using UniFAP.LabManager.Core.Interfaces;
using UniFAP.LabManager.Core.Models;
using UniFAP.LabManager.Services.Catalog;
using Xunit;

namespace UniFAP.LabManager.Tests;

public class CatalogSyncAndMergeTests
{
    private readonly Mock<IConfigService> _configMock;
    private readonly Mock<ILogService> _logMock;
    private readonly CatalogSyncService _syncService;

    public CatalogSyncAndMergeTests()
    {
        _configMock = new Mock<IConfigService>();
        _logMock = new Mock<ILogService>();

        var catalogConfig = new SoftwareCatalogConfig
        {
            Categories = new List<string> { "Básicos", "Desenvolvimento", "Engenharia" },
            Items = new List<SoftwareItem>
            {
                new SoftwareItem
                {
                    Id = "chrome",
                    Name = "Google Chrome",
                    Category = "Básicos",
                    Type = SoftwareType.Winget,
                    WingetId = "Google.Chrome",
                    Source = "UniFAP",
                    Severity = SoftwareSeverity.Critical
                },
                new SoftwareItem
                {
                    Id = "autocad2025",
                    Name = "AutoCAD 2025",
                    Category = "Engenharia",
                    Type = SoftwareType.Local,
                    Installer = "software/Autodesk/AutoCAD",
                    Source = "UniFAP",
                    Severity = SoftwareSeverity.Warning
                }
            }
        };

        _configMock.Setup(c => c.SoftwareCatalog).Returns(catalogConfig);
        _syncService = new CatalogSyncService(_configMock.Object, _logMock.Object);
    }

    [Theory]
    [InlineData("Browsers", "Browsers")]
    [InlineData("Web Browsers", "Browsers")]
    [InlineData("Development Tools", "Development")]
    [InlineData("Documents", "Document")]
    [InlineData("Multimedia Tools", "Multimedia")]
    [InlineData("Pro Tools", "Pro Tools")]
    [InlineData("Sysinternals", "Pro Tools")]
    [InlineData("Security & Antivirus", "Security")]
    [InlineData("Utilities", "Utilities")]
    [InlineData("Random Unknown", "Other")]
    public void NormalizeCategory_MapsTaxonomyCorrectly(string raw, string expected)
    {
        string result = _syncService.NormalizeCategory(raw);
        Assert.Equal(expected, result);
    }

    [Fact]
    public async Task SyncWinUtilCatalog_PreservesUniFapPriority_WhenDuplicateExists()
    {
        // Executar sincronização usando fallback local
        var result = await _syncService.SyncWinUtilCatalogAsync(forceOnline: false);

        Assert.True(result.Success);
        Assert.True(result.UniFapItemCount >= 2);
        Assert.True(result.TotalFinalCount > result.UniFapItemCount);

        // Verificar que o Chrome da UniFAP não foi sobrescrito, mas sim enriquecido e marcado
        var chrome = _configMock.Object.SoftwareCatalog.Items.FirstOrDefault(i => i.Id == "chrome");
        Assert.NotNull(chrome);
        Assert.Equal("UniFAP + WinUtil", chrome.Source);
        Assert.Equal(SoftwareSeverity.Critical, chrome.Severity); // Prioridade soberana UniFAP
        Assert.Equal("Google.Chrome", chrome.WingetId);
    }

    [Fact]
    public async Task SyncWinUtilCatalog_AddsNewWinUtilSoftware_WithWinUtilSourceTag()
    {
        var result = await _syncService.SyncWinUtilCatalogAsync(forceOnline: false);

        Assert.True(result.Success);

        // Verificar se novos softwares do catálogo WinUtil foram adicionados (ex: 7zip ou bitwarden)
        var winUtilApp = _configMock.Object.SoftwareCatalog.Items.FirstOrDefault(i => i.Source == "WinUtil");
        Assert.NotNull(winUtilApp);
        Assert.StartsWith("winutil_", winUtilApp.Id);
        Assert.False(string.IsNullOrWhiteSpace(winUtilApp.Category));
    }

    [Fact]
    public async Task SyncWinUtilCatalog_GracefullyFallsBackToOfflineSnapshot_WhenOnlineFails()
    {
        // Forçar tentativa online que apontará para URL inválida ou contingência sem quebrar
        var result = await _syncService.SyncWinUtilCatalogAsync(forceOnline: true);

        // A aplicação não pode quebrar caso GitHub esteja offline ou indisponível
        Assert.True(result.Success);
        Assert.True(result.TotalFinalCount > 0);
    }
}
