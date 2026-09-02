using System.IO;
using System.Text.Json;
using Microsoft.Win32;
using UniFAP.LabManager.Core.Interfaces;

namespace UniFAP.LabManager.Infrastructure.SystemAdapters;

public class RegistrySnapshotEntry
{
    public string Root { get; set; } = "HKCU";
    public string SubKey { get; set; } = string.Empty;
    public string ValueName { get; set; } = string.Empty;
    public object? OriginalValue { get; set; }
    public RegistryValueKind ValueKind { get; set; }
}

public class RegistryAdapter
{
    private readonly ILogService _logger;

    public RegistryAdapter(ILogService logger)
    {
        _logger = logger;
    }

    public object? GetValue(RegistryHive hive, string subKey, string valueName)
    {
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Default);
            using var key = baseKey.OpenSubKey(subKey);
            return key?.GetValue(valueName);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("RegistryAdapter", $"Erro ao ler registro [{hive}\\{subKey}\\{valueName}]: {ex.Message}");
            return null;
        }
    }

    public bool SetValue(RegistryHive hive, string subKey, string valueName, object value, RegistryValueKind kind = RegistryValueKind.DWord)
    {
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Default);
            using var key = baseKey.CreateSubKey(subKey, true);
            key.SetValue(valueName, value, kind);
            _logger.LogDebug("RegistryAdapter", $"Definido registro [{hive}\\{subKey}\\{valueName}] = {value}");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError("RegistryAdapter", $"Erro ao gravar registro [{hive}\\{subKey}\\{valueName}]", ex);
            return false;
        }
    }

    public void SaveRollbackSnapshot(string filePath, List<RegistrySnapshotEntry> entries)
    {
        try
        {
            string dir = Path.GetDirectoryName(filePath)!;
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            string json = JsonSerializer.Serialize(entries, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(filePath, json);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("RegistryAdapter", $"Falha ao salvar rollback de registro em '{filePath}': {ex.Message}");
        }
    }
}
