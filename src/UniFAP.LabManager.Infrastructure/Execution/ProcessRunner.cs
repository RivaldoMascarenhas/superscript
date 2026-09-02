using System.Diagnostics;
using System.Text;
using UniFAP.LabManager.Core.Interfaces;

namespace UniFAP.LabManager.Infrastructure.Execution;

public class ProcessExecutionResult
{
    public int ExitCode { get; set; }
    public string StandardOutput { get; set; } = string.Empty;
    public string StandardError { get; set; } = string.Empty;
    public bool TimedOut { get; set; }
    public bool Success => ExitCode == 0;
}

public class ProcessRunner
{
    private readonly ILogService _logger;

    public ProcessRunner(ILogService logger)
    {
        _logger = logger;
    }

    public async Task<ProcessExecutionResult> RunAsync(
        string fileName,
        string arguments,
        string? workingDirectory = null,
        int timeoutSeconds = 600,
        Action<string>? onOutputLine = null,
        CancellationToken cancellationToken = default)
    {
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();

        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            WorkingDirectory = workingDirectory ?? Directory.GetCurrentDirectory(),
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        using var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data != null)
            {
                stdout.AppendLine(e.Data);
                onOutputLine?.Invoke(e.Data);
                _logger.LogDebug("ProcessRunner", $"[OUT] {e.Data}");
            }
        };

        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data != null)
            {
                stderr.AppendLine(e.Data);
                onOutputLine?.Invoke(e.Data);
                _logger.LogDebug("ProcessRunner", $"[ERR] {e.Data}");
            }
        };

        _logger.LogInformation("ProcessRunner", $"Iniciando processo: '{fileName}' com argumentos '{arguments}'");

        try
        {
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(timeoutSeconds), cts.Token);
            var processTask = process.WaitForExitAsync(cancellationToken);

            var completedTask = await Task.WhenAny(processTask, timeoutTask);

            if (completedTask == timeoutTask && !cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning("ProcessRunner", $"Processo '{fileName}' excedeu timeout de {timeoutSeconds}s e será encerrado.");
                try { process.Kill(true); } catch { }
                return new ProcessExecutionResult
                {
                    ExitCode = -1,
                    StandardOutput = stdout.ToString(),
                    StandardError = stderr.ToString() + "\nProcesso cancelado por timeout.",
                    TimedOut = true
                };
            }

            cts.Cancel(); // cancela o timeout se o processo terminou antes
            await processTask;

            _logger.LogInformation("ProcessRunner", $"Processo '{fileName}' finalizado com ExitCode: {process.ExitCode}");

            return new ProcessExecutionResult
            {
                ExitCode = process.ExitCode,
                StandardOutput = stdout.ToString(),
                StandardError = stderr.ToString()
            };
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("ProcessRunner", $"Processo '{fileName}' foi cancelado pelo usuário.");
            try { process.Kill(true); } catch { }
            return new ProcessExecutionResult
            {
                ExitCode = -2,
                StandardOutput = stdout.ToString(),
                StandardError = "Operação cancelada pelo usuário."
            };
        }
        catch (Exception ex)
        {
            _logger.LogError("ProcessRunner", $"Falha ao executar '{fileName}'", ex);
            return new ProcessExecutionResult
            {
                ExitCode = -99,
                StandardOutput = stdout.ToString(),
                StandardError = ex.Message
            };
        }
    }
}
