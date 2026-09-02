using System.IO;
using System.Text.Json;
using UniFAP.LabManager.Core.Interfaces;
using UniFAP.LabManager.Core.Models;

namespace UniFAP.LabManager.Infrastructure.Persistence;

public class JobPersistenceStore
{
    private readonly ILogService _logger;
    private readonly string _baseDir;
    private readonly string _jobsDir;
    private readonly string _activeJobStateFile;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter(System.Text.Json.JsonNamingPolicy.CamelCase, allowIntegerValues: true) }
    };

    public JobPersistenceStore(ILogService logger, string? baseDir = null)
    {
        _logger = logger;
        _baseDir = baseDir ?? @"C:\ProgramData\UniFAP\LabManager";
        _jobsDir = Path.Combine(_baseDir, "Jobs");
        _activeJobStateFile = Path.Combine(_baseDir, "active_job_state.json");
        EnsureDirectories();
    }

    private void EnsureDirectories()
    {
        try
        {
            if (!Directory.Exists(_jobsDir))
            {
                Directory.CreateDirectory(_jobsDir);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning("JobPersistenceStore", $"Erro ao criar diretório de jobs: {ex.Message}");
        }
    }

    public async Task SaveActiveJobAsync(Job job)
    {
        try
        {
            EnsureDirectories();
            string json = JsonSerializer.Serialize(job, JsonOptions);

            // Grava o estado ativo para retomada pós-reboot
            await File.WriteAllTextAsync(_activeJobStateFile, json);

            // Grava o histórico individual do job
            string historyFile = Path.Combine(_jobsDir, $"{job.Id}.json");
            await File.WriteAllTextAsync(historyFile, json);

            _logger.LogDebug("JobPersistenceStore", $"Job '{job.Id}' salvo com sucesso.");
        }
        catch (Exception ex)
        {
            _logger.LogError("JobPersistenceStore", $"Falha ao salvar Job '{job.Id}'", ex);
        }
    }

    public async Task<Job?> LoadActiveJobAsync()
    {
        try
        {
            if (!File.Exists(_activeJobStateFile))
                return null;

            string json = await File.ReadAllTextAsync(_activeJobStateFile);
            return JsonSerializer.Deserialize<Job>(json, JsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError("JobPersistenceStore", "Falha ao carregar active_job_state.json", ex);
            return null;
        }
    }

    public void ClearActiveJob()
    {
        try
        {
            if (File.Exists(_activeJobStateFile))
            {
                File.Delete(_activeJobStateFile);
                _logger.LogInformation("JobPersistenceStore", "Estado de Job ativo limpo com sucesso.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning("JobPersistenceStore", $"Falha ao remover arquivo de estado de job ativo: {ex.Message}");
        }
    }

    public async Task<List<Job>> GetAllJobsHistoryAsync()
    {
        var list = new List<Job>();
        try
        {
            EnsureDirectories();
            var files = Directory.GetFiles(_jobsDir, "*.json");
            foreach (var file in files.OrderByDescending(f => File.GetCreationTime(f)))
            {
                try
                {
                    string json = await File.ReadAllTextAsync(file);
                    var job = JsonSerializer.Deserialize<Job>(json, JsonOptions);
                    if (job != null)
                    {
                        list.Add(job);
                    }
                }
                catch { }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError("JobPersistenceStore", "Erro ao listar histórico de jobs", ex);
        }
        return list;
    }
}
