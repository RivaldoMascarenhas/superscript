using System.IO;
using System.Text.Json;
using UniFAP.LabManager.Core.Interfaces;

namespace UniFAP.LabManager.Infrastructure.Execution;

public class PowerShellResult<T>
{
    public bool Success { get; set; }
    public T? Data { get; set; }
    public string RawOutput { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
    public int ExitCode { get; set; }
}

public class PowerShellRunner
{
    private readonly ProcessRunner _processRunner;
    private readonly ILogService _logger;

    public PowerShellRunner(ProcessRunner processRunner, ILogService logger)
    {
        _processRunner = processRunner;
        _logger = logger;
    }

    public async Task<ProcessExecutionResult> ExecuteCommandAsync(
        string command,
        Action<string>? onOutputLine = null,
        CancellationToken cancellationToken = default)
    {
        string arguments = $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command \"{command.Replace("\"", "`\"")}\"";
        return await _processRunner.RunAsync("powershell.exe", arguments, null, 600, onOutputLine, cancellationToken);
    }

    public async Task<ProcessExecutionResult> ExecuteScriptFileAsync(
        string scriptPath,
        string scriptArguments = "",
        Action<string>? onOutputLine = null,
        CancellationToken cancellationToken = default)
    {
        string fullPath = Path.GetFullPath(scriptPath);
        if (!File.Exists(fullPath))
        {
            _logger.LogError("PowerShellRunner", $"Script não encontrado no caminho: {fullPath}");
            return new ProcessExecutionResult
            {
                ExitCode = -404,
                StandardError = $"Arquivo de script não encontrado: {fullPath}"
            };
        }

        string arguments = $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -File \"{fullPath}\" {scriptArguments}";
        return await _processRunner.RunAsync("powershell.exe", arguments, Path.GetDirectoryName(fullPath), 900, onOutputLine, cancellationToken);
    }

    public async Task<PowerShellResult<T>> ExecuteScriptWithJsonResultAsync<T>(
        string scriptPath,
        string scriptArguments = "",
        CancellationToken cancellationToken = default)
    {
        var execResult = await ExecuteScriptFileAsync(scriptPath, scriptArguments, null, cancellationToken);
        var result = new PowerShellResult<T>
        {
            ExitCode = execResult.ExitCode,
            RawOutput = execResult.StandardOutput,
            ErrorMessage = execResult.StandardError
        };

        if (execResult.Success && !string.IsNullOrWhiteSpace(execResult.StandardOutput))
        {
            try
            {
                // Tenta localizar a linha JSON na saída
                string[] lines = execResult.StandardOutput.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                string? jsonLine = lines.LastOrDefault(l => l.Trim().StartsWith("{") && l.Trim().EndsWith("}"));

                if (jsonLine != null)
                {
                    result.Data = JsonSerializer.Deserialize<T>(jsonLine, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                    result.Success = true;
                }
                else
                {
                    result.Success = execResult.Success;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning("PowerShellRunner", $"Falha ao converter saída JSON de '{scriptPath}': {ex.Message}");
                result.ErrorMessage = $"Falha ao interpretar JSON retornado: {ex.Message}";
            }
        }
        else
        {
            result.Success = false;
        }

        return result;
    }
}
