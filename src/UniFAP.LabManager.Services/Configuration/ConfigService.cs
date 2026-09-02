using System.IO;
using System.Text.Json;
using UniFAP.LabManager.Core.Interfaces;
using UniFAP.LabManager.Core.Models;

namespace UniFAP.LabManager.Services.Configuration;

public class ConfigService : IConfigService
{
    private readonly ILogService _logger;
    private readonly string _configDirectory;
    private readonly string _themesDirectory;

    public InstitutionConfig Institution { get; private set; } = new();
    public ActiveDirectoryConfig ActiveDirectory { get; private set; } = new();
    public BrandingConfig Branding { get; private set; } = new();
    public PerformanceConfig Performance { get; private set; } = new();
    public ProfilesConfig Profiles { get; private set; } = new();
    public SettingsConfig Settings { get; private set; } = new();
    public SoftwareCatalogConfig SoftwareCatalog { get; private set; } = new();
    public UsersConfig Users { get; private set; } = new();
    public ThemeConfig CurrentTheme { get; private set; } = new();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter(System.Text.Json.JsonNamingPolicy.CamelCase, allowIntegerValues: true) }
    };

    public ConfigService(ILogService logger, string? configDir = null, string? themesDir = null)
    {
        _logger = logger;
        _configDirectory = configDir ?? FindDirectory("config");
        _themesDirectory = themesDir ?? FindDirectory("themes");
    }

    private static string FindDirectory(string folderName)
    {
        string current = AppDomain.CurrentDomain.BaseDirectory;
        for (int i = 0; i < 8; i++)
        {
            string candidate = Path.Combine(current, folderName);
            if (Directory.Exists(candidate))
                return candidate;

            var parent = Directory.GetParent(current);
            if (parent == null) break;
            current = parent.FullName;
        }
        return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, folderName);
    }

    public async Task LoadAllAsync()
    {
        _logger.LogInformation("ConfigService", $"Carregando configurações a partir de: {_configDirectory}");

        Institution = await LoadConfigFileAsync<InstitutionConfig>("institution.json") ?? new();
        ActiveDirectory = await LoadConfigFileAsync<ActiveDirectoryConfig>("active-directory.json") ?? new();
        Branding = await LoadConfigFileAsync<BrandingConfig>("branding.json") ?? new();
        Performance = await LoadConfigFileAsync<PerformanceConfig>("performance.json") ?? new();
        Profiles = await LoadConfigFileAsync<ProfilesConfig>("profiles.json") ?? new();
        Settings = await LoadConfigFileAsync<SettingsConfig>("settings.json") ?? new();
        SoftwareCatalog = await LoadConfigFileAsync<SoftwareCatalogConfig>("software.json") ?? new();
        Users = await LoadConfigFileAsync<UsersConfig>("users.json") ?? new();

        CurrentTheme = LoadTheme(Settings.Theme);
        _logger.LogInformation("ConfigService", "Todas as configurações foram carregadas com êxito.");
    }

    private async Task<T?> LoadConfigFileAsync<T>(string fileName) where T : class
    {
        try
        {
            string filePath = Path.Combine(_configDirectory, fileName);
            if (!File.Exists(filePath))
            {
                _logger.LogWarning("ConfigService", $"Arquivo de configuração '{fileName}' não encontrado em {_configDirectory}");
                return null;
            }

            string json = await File.ReadAllTextAsync(filePath);
            return JsonSerializer.Deserialize<T>(json, JsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError("ConfigService", $"Erro ao carregar '{fileName}'", ex);
            return null;
        }
    }

    public async Task SaveSettingsAsync()
    {
        try
        {
            string filePath = Path.Combine(_configDirectory, "settings.json");
            string json = JsonSerializer.Serialize(Settings, JsonOptions);
            await File.WriteAllTextAsync(filePath, json);
            _logger.LogInformation("ConfigService", "Preferências do usuário salvas com sucesso.");
        }
        catch (Exception ex)
        {
            _logger.LogError("ConfigService", "Erro ao salvar settings.json", ex);
        }
    }

    public LaboratoryProfile? GetProfile(string profileId)
    {
        if (profileId.Equals("administrativo", StringComparison.OrdinalIgnoreCase))
        {
            return Profiles.Administrative;
        }

        if (Profiles.Laboratories.TryGetValue(profileId.ToLowerInvariant(), out var profile))
        {
            return profile;
        }

        return null;
    }

    public SoftwareItem? GetSoftware(string softwareId)
    {
        return SoftwareCatalog.Items.FirstOrDefault(s => s.Id.Equals(softwareId, StringComparison.OrdinalIgnoreCase));
    }

    public List<SoftwareItem> GetSoftwareForProfile(string profileId)
    {
        var result = new List<SoftwareItem>();
        var profile = GetProfile(profileId);
        if (profile == null) return result;

        foreach (var id in profile.Software)
        {
            var item = GetSoftware(id);
            if (item != null)
            {
                result.Add(new SoftwareItem
                {
                    Id = item.Id,
                    Name = item.Name,
                    Category = item.Category,
                    Description = item.Description,
                    Type = item.Type,
                    WingetId = item.WingetId,
                    FallbackType = item.FallbackType,
                    Installer = item.Installer,
                    EntryPoint = item.EntryPoint,
                    SilentArgs = item.SilentArgs,
                    ScriptPath = item.ScriptPath,
                    InstallerDir = item.InstallerDir,
                    Arguments = item.Arguments,
                    Silent = item.Silent,
                    Severity = item.Severity,
                    Legacy = item.Legacy,
                    IconKey = item.IconKey,
                    EstimatedSeconds = item.EstimatedSeconds,
                    IsSelected = true
                });
            }
        }
        return result;
    }

    public ThemeConfig LoadTheme(string themeName)
    {
        try
        {
            string fileName = $"{themeName.ToLowerInvariant()}.json";
            string filePath = Path.Combine(_themesDirectory, fileName);
            if (File.Exists(filePath))
            {
                string json = File.ReadAllText(filePath);
                var theme = JsonSerializer.Deserialize<ThemeConfig>(json, JsonOptions);
                if (theme != null) return theme;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning("ConfigService", $"Erro ao carregar tema '{themeName}': {ex.Message}");
        }

        return new ThemeConfig { Name = "Dark", DisplayName = "Dark Default" };
    }
}
