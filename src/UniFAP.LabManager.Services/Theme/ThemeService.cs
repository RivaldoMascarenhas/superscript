using System.IO;
using UniFAP.LabManager.Core.Interfaces;
using UniFAP.LabManager.Core.Models;

namespace UniFAP.LabManager.Services.Theme;

public class ThemeService : IThemeService
{
    private readonly IConfigService _configService;
    private readonly ILogService _logger;

    public ThemeConfig CurrentTheme { get; private set; }

    public ThemeService(IConfigService configService, ILogService logger)
    {
        _configService = configService;
        _logger = logger;
        CurrentTheme = _configService.CurrentTheme ?? new ThemeConfig();
    }

    public void ApplyTheme(string themeName)
    {
        CurrentTheme = _configService.LoadTheme(themeName);
        _logger.LogInformation("ThemeService", $"Tema alterado para: {CurrentTheme.DisplayName}");
    }

    public List<string> GetAvailableThemes()
    {
        return new List<string> { "Dark", "Light" };
    }
}
