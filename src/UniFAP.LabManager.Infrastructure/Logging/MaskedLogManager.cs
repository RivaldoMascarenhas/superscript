using System.IO;
using UniFAP.LabManager.Core.Interfaces;

namespace UniFAP.LabManager.Infrastructure.Logging;

public class MaskedLogManager : ILogService
{
    private readonly ISecurityService _securityService;
    private readonly string _logsDirectory;
    private readonly object _lock = new();

    public event Action<string, string, string>? OnLogEmitted;

    public MaskedLogManager(ISecurityService securityService)
    {
        _securityService = securityService;
        _logsDirectory = @"C:\ProgramData\UniFAP\LabManager\Logs";
        EnsureDirectories();
    }

    private void EnsureDirectories()
    {
        try
        {
            if (!Directory.Exists(_logsDirectory))
            {
                Directory.CreateDirectory(_logsDirectory);
            }
        }
        catch
        {
            // Fallback para AppData local se ProgramData não for acessível
        }
    }

    public string GetLogsDirectory() => _logsDirectory;

    public void LogInformation(string source, string message) => WriteLog(source, "INFO", message);
    public void LogWarning(string source, string message) => WriteLog(source, "WARN", message);
    public void LogError(string source, string message, Exception? ex = null)
    {
        string fullMessage = ex != null ? $"{message} | Exceção: {ex.Message} | StackTrace: {ex.StackTrace}" : message;
        WriteLog(source, "ERROR", fullMessage);
    }
    public void LogDebug(string source, string message) => WriteLog(source, "DEBUG", message);

    public void AppendStepLog(string jobId, string stepName, string message)
    {
        WriteLog($"Job:{jobId}", "STEP", $"[{stepName}] {message}");
    }

    private void WriteLog(string source, string level, string message)
    {
        try
        {
            string sanitized = _securityService.SanitizeLogString(message);
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            string line = $"[{timestamp}] [{level}] [{source}] {sanitized}";

            // Notifica assinantes da UI em tempo real
            OnLogEmitted?.Invoke(source, level, sanitized);

            // Grava no arquivo
            lock (_lock)
            {
                EnsureDirectories();
                string fileName = GetTargetLogFileName(source);
                string filePath = Path.Combine(_logsDirectory, fileName);
                File.AppendAllText(filePath, line + Environment.NewLine);
            }
        }
        catch
        {
            // Não deve quebrar a execução se o log em disco falhar temporariamente
        }
    }

    private string GetTargetLogFileName(string source)
    {
        if (source.Contains("AD", StringComparison.OrdinalIgnoreCase) || source.Contains("ActiveDirectory", StringComparison.OrdinalIgnoreCase))
            return $"ActiveDirectory_{DateTime.Now:yyyyMMdd}.log";

        if (source.Contains("Software", StringComparison.OrdinalIgnoreCase) || source.Contains("Winget", StringComparison.OrdinalIgnoreCase))
            return $"Installation_{DateTime.Now:yyyyMMdd}.log";

        if (source.Contains("Diag", StringComparison.OrdinalIgnoreCase))
            return $"Diagnostics_{DateTime.Now:yyyyMMdd}.log";

        return $"Application_{DateTime.Now:yyyyMMdd}.log";
    }
}
