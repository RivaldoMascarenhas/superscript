using System.IO;
using System.Net.Http;
using System.Text.Json;
using UniFAP.LabManager.Core.Enums;
using UniFAP.LabManager.Core.Interfaces;
using UniFAP.LabManager.Core.Models;

namespace UniFAP.LabManager.Services.Catalog;

public class CatalogSyncService : ICatalogSyncService
{
    private readonly IConfigService _configService;
    private readonly ILogService _logger;
    private static readonly HttpClient HttpClient = new() { Timeout = TimeSpan.FromSeconds(15) };

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public CatalogSyncService(IConfigService configService, ILogService logger)
    {
        _configService = configService;
        _logger = logger;
    }

    public async Task<CatalogSourceConfig> GetCatalogSourceConfigAsync()
    {
        string cfgPath = ResolveConfigPath("config/catalog-source.json");
        if (File.Exists(cfgPath))
        {
            try
            {
                string json = await File.ReadAllTextAsync(cfgPath);
                var cfg = JsonSerializer.Deserialize<CatalogSourceConfig>(json, JsonOpts);
                if (cfg != null) return cfg;
            }
            catch (Exception ex)
            {
                _logger.LogWarning("CatalogSyncService", $"Erro ao ler catalog-source.json: {ex.Message}");
            }
        }

        return new CatalogSourceConfig();
    }

    public async Task<CatalogSyncResult> SyncWinUtilCatalogAsync(
        bool forceOnline = false,
        CancellationToken cancellationToken = default)
    {
        var result = new CatalogSyncResult();
        var sourceConfig = await GetCatalogSourceConfigAsync();

        _logger.LogInformation("CatalogSyncService", "Iniciando processo de sincronização e merge do catálogo WinUtil...");

        string? rawJson = null;
        bool usedFallback = false;

        // 1. Tentar obter online se permitido
        if (forceOnline || !string.IsNullOrWhiteSpace(sourceConfig.WinutilSourceUrl))
        {
            try
            {
                _logger.LogInformation("CatalogSyncService", $"Tentando download de {sourceConfig.WinutilSourceUrl}...");
                var response = await HttpClient.GetAsync(sourceConfig.WinutilSourceUrl, cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    rawJson = await response.Content.ReadAsStringAsync(cancellationToken);
                    _logger.LogInformation("CatalogSyncService", "Catálogo oficial WinUtil baixado com sucesso via HTTP.");
                }
                else
                {
                    _logger.LogWarning("CatalogSyncService", $"Download retornou HTTP {(int)response.StatusCode}. Recorrendo a snapshot offline.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning("CatalogSyncService", $"Falha ao baixar catálogo online ({ex.Message}). Utilizando snapshot local.");
            }
        }

        // 2. Se falhar ou offline, carregar snapshot local (Offline-First)
        if (string.IsNullOrWhiteSpace(rawJson))
        {
            string fallbackPath = ResolveConfigPath(sourceConfig.FallbackLocalFile);
            if (File.Exists(fallbackPath))
            {
                rawJson = await File.ReadAllTextAsync(fallbackPath, cancellationToken);
                usedFallback = true;
                _logger.LogInformation("CatalogSyncService", $"Carregado catálogo WinUtil a partir de snapshot local: {fallbackPath}");
            }
            else
            {
                result.Success = false;
                result.Message = "Não foi possível obter o catálogo WinUtil nem online nem do snapshot offline local.";
                _logger.LogError("CatalogSyncService", result.Message);
                return result;
            }
        }

        // 3. Deserializar dicionário de aplicativos WinUtil
        Dictionary<string, WinUtilAppEntry>? winUtilDict;
        try
        {
            winUtilDict = JsonSerializer.Deserialize<Dictionary<string, WinUtilAppEntry>>(rawJson, JsonOpts);
            if (winUtilDict == null || winUtilDict.Count == 0)
            {
                throw new InvalidOperationException("Catálogo WinUtil vazio ou formato incompatível.");
            }
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Message = $"Erro ao interpretar schema do catálogo WinUtil: {ex.Message}";
            _logger.LogError("CatalogSyncService", result.Message, ex);
            return result;
        }

        // 4. Catálogo Base da UniFAP (Soberano)
        var uniFapCatalog = _configService.SoftwareCatalog.Items.ToList();
        result.UniFapItemCount = uniFapCatalog.Count;
        result.WinUtilImportedCount = winUtilDict.Count;
        result.UsedLocalFallback = usedFallback;

        var mergedList = new List<SoftwareItem>();
        var uniFapLookupByWinget = new Dictionary<string, SoftwareItem>(StringComparer.OrdinalIgnoreCase);
        var uniFapLookupByName = new Dictionary<string, SoftwareItem>(StringComparer.OrdinalIgnoreCase);

        foreach (var uItem in uniFapCatalog)
        {
            if (!string.IsNullOrWhiteSpace(uItem.WingetId))
            {
                uniFapLookupByWinget[uItem.WingetId.Trim()] = uItem;
            }
            uniFapLookupByName[NormalizeKey(uItem.Name)] = uItem;
            mergedList.Add(uItem);
        }

        int mergedCount = 0;
        int addedCount = 0;

        // 5. Mesclar aplicativos WinUtil respeitando prioridade da UniFAP
        foreach (var kvp in winUtilDict)
        {
            string appId = kvp.Key;
            var entry = kvp.Value;
            string displayName = !string.IsNullOrWhiteSpace(entry.Content) ? entry.Content : appId;
            string? wingetId = entry.Winget?.Trim();

            // Checar se já existe no catálogo UniFAP por WingetId ou Nome Normalizado
            SoftwareItem? existing = null;
            if (!string.IsNullOrWhiteSpace(wingetId) && uniFapLookupByWinget.TryGetValue(wingetId, out var matchByWinget))
            {
                existing = matchByWinget;
            }
            else if (uniFapLookupByName.TryGetValue(NormalizeKey(displayName), out var matchByName))
            {
                existing = matchByName;
            }

            if (existing != null)
            {
                // CONFLITO / DUPLICATA DETECTADA: UniFAP tem prioridade absoluta
                // Enriquece apenas metadados faltantes e marca procedência combinada
                existing.Source = "UniFAP + WinUtil";
                if (string.IsNullOrWhiteSpace(existing.OfficialLink) && !string.IsNullOrWhiteSpace(entry.Link))
                {
                    existing.OfficialLink = entry.Link;
                }
                existing.IsOpenSource = existing.IsOpenSource || entry.Foss;
                if (!string.IsNullOrWhiteSpace(entry.Choco)) existing.ChocoId = entry.Choco;

                mergedCount++;
                result.MergedSoftwareNames.Add(existing.Name);
            }
            else
            {
                // NOVO SOFTWARE DO CATÁLOGO WINUTIL
                string normalizedCat = NormalizeCategory(entry.Category);
                var newItem = new SoftwareItem
                {
                    Id = $"winutil_{appId.ToLowerInvariant().Replace(" ", "_")}",
                    Name = displayName,
                    Category = normalizedCat,
                    Description = !string.IsNullOrWhiteSpace(entry.Description) 
                        ? entry.Description 
                        : $"Aplicativo do catálogo WinUtil ({displayName}).",
                    Type = SoftwareType.Winget,
                    WingetId = !string.IsNullOrWhiteSpace(wingetId) ? wingetId : null,
                    OfficialLink = entry.Link,
                    IsOpenSource = entry.Foss,
                    ChocoId = entry.Choco,
                    Source = "WinUtil",
                    Severity = SoftwareSeverity.Optional,
                    IconKey = GetCategoryIcon(normalizedCat),
                    EstimatedSeconds = 60,
                    Silent = true,
                    Enabled = true
                };

                mergedList.Add(newItem);
                addedCount++;
                result.AddedSoftwareNames.Add(displayName);
            }
        }

        result.MergedCount = mergedCount;
        result.TotalFinalCount = mergedList.Count;
        result.Success = true;
        result.Message = $"Sincronização concluída com sucesso: {result.UniFapItemCount} itens UniFAP preservados, " +
                         $"{mergedCount} duplicatas mescladas (UniFAP + WinUtil), {addedCount} novos softwares WinUtil integrados. " +
                         $"Total do catálogo: {result.TotalFinalCount} softwares.";

        _logger.LogInformation("CatalogSyncService", result.Message);

        // 6. Atualizar configuração em memória e gravar histórico de sincronização
        _configService.SoftwareCatalog.Items = mergedList;

        sourceConfig.LastSyncUtc = DateTime.UtcNow;
        sourceConfig.TotalUniFapItems = result.UniFapItemCount;
        sourceConfig.TotalWinUtilItems = result.WinUtilImportedCount;
        sourceConfig.MergedItems = mergedCount;
        await SaveCatalogSourceConfigAsync(sourceConfig);

        return result;
    }

    public async Task<List<SoftwareItem>> GetMergedCatalogAsync()
    {
        if (_configService.SoftwareCatalog.Items.Any(i => i.Source.Contains("WinUtil")))
        {
            return _configService.SoftwareCatalog.Items;
        }

        // Executar sincronização inicial offline
        await SyncWinUtilCatalogAsync(forceOnline: false);
        return _configService.SoftwareCatalog.Items;
    }

    public string NormalizeCategory(string? rawCategory)
    {
        if (string.IsNullOrWhiteSpace(rawCategory)) return "Other";

        string raw = rawCategory.Trim().ToLowerInvariant();

        if (raw.Contains("browser") || raw.Contains("navegador")) return "Browsers";
        if (raw.Contains("dev") || raw.Contains("code") || raw.Contains("program") || raw.Contains("git") || raw.Contains("ide")) return "Development";
        if (raw.Contains("doc") || raw.Contains("pdf") || raw.Contains("office") || raw.Contains("text") || raw.Contains("reader")) return "Document";
        if (raw.Contains("edu") || raw.Contains("ensino") || raw.Contains("academ")) return "Education";
        if (raw.Contains("game") || raw.Contains("jogo") || raw.Contains("steam")) return "Games";
        if (raw.Contains("media") || raw.Contains("audio") || raw.Contains("video") || raw.Contains("sound") || raw.Contains("music") || raw.Contains("multimedia")) return "Multimedia";
        if (raw.Contains("network") || raw.Contains("rede") || raw.Contains("vpn") || raw.Contains("ftp") || raw.Contains("wifi")) return "Networking";
        if (raw.Contains("microsoft") || raw.Contains("ms tool") || raw.Contains("powershell") || raw.Contains("terminal")) return "Microsoft Tools";
        if (raw.Contains("pro tool") || raw.Contains("sysinternal") || raw.Contains("advanced") || raw.Contains("scanner")) return "Pro Tools";
        if (raw.Contains("comm") || raw.Contains("chat") || raw.Contains("discord") || raw.Contains("zoom") || raw.Contains("teams") || raw.Contains("social")) return "Communication";
        if (raw.Contains("productiv") || raw.Contains("note") || raw.Contains("task") || raw.Contains("organizer")) return "Productivity";
        if (raw.Contains("sec") || raw.Contains("antivirus") || raw.Contains("pass") || raw.Contains("privacy") || raw.Contains("firewall")) return "Security";
        if (raw.Contains("util") || raw.Contains("tool") || raw.Contains("archive") || raw.Contains("zip") || raw.Contains("clean")) return "Utilities";

        return "Other";
    }

    private static string NormalizeKey(string text)
    {
        return new string(text.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
    }

    private static string GetCategoryIcon(string category)
    {
        return category switch
        {
            "Browsers" => "Globe",
            "Development" => "Code",
            "Document" => "FileDocument",
            "Education" => "School",
            "Games" => "Gamepad",
            "Multimedia" => "Media",
            "Networking" => "Network",
            "Microsoft Tools" => "Microsoft",
            "Pro Tools" => "Wrench",
            "Communication" => "Chat",
            "Productivity" => "CheckCircle",
            "Security" => "Shield",
            _ => "Package"
        };
    }

    private static string ResolveConfigPath(string relativePath)
    {
        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
        string direct = Path.Combine(baseDir, relativePath);
        if (File.Exists(direct)) return direct;

        string cwdPath = Path.Combine(Directory.GetCurrentDirectory(), relativePath);
        if (File.Exists(cwdPath)) return cwdPath;

        return direct;
    }

    private async Task SaveCatalogSourceConfigAsync(CatalogSourceConfig config)
    {
        try
        {
            string cfgPath = ResolveConfigPath("config/catalog-source.json");
            string dir = Path.GetDirectoryName(cfgPath)!;
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            string json = JsonSerializer.Serialize(config, JsonOpts);
            await File.WriteAllTextAsync(cfgPath, json);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("CatalogSyncService", $"Erro ao salvar catalog-source.json: {ex.Message}");
        }
    }
}
