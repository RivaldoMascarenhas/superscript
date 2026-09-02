using System.IO;
using UniFAP.LabManager.Core.Interfaces;
using UniFAP.LabManager.Infrastructure.Execution;

namespace UniFAP.LabManager.Services.Performance;

public class PerformanceService : IPerformanceService
{
    private readonly PowerShellRunner _powerShellRunner;
    private readonly ILogService _logger;

    public PerformanceService(PowerShellRunner powerShellRunner, ILogService logger)
    {
        _powerShellRunner = powerShellRunner;
        _logger = logger;
    }

    public async Task<bool> ApplyPerformanceTweaksAsync(bool dryRun = false)
    {
        _logger.LogInformation("PerformanceService", $"Aplicando otimizações de performance seguras [DryRun: {dryRun}]");

        if (dryRun)
        {
            _logger.LogInformation("PerformanceService", "[DRY-RUN] Simulação: Otimizações de desempenho seguras seriam aplicadas mantendo ClearType e animações.");
            return true;
        }

        try
        {
            string scriptPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "scripts", "Performance-Optimize.ps1");
            if (!File.Exists(scriptPath))
            {
                scriptPath = Path.GetFullPath("scripts/Performance-Optimize.ps1");
            }

            var result = await _powerShellRunner.ExecuteCommandAsync($"& '{scriptPath.Replace("'", "''")}'");
            if (result.Success || result.StandardOutput.Contains("aplicadas com sucesso", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation("PerformanceService", "Otimizações de performance aplicadas com sucesso.");
                return true;
            }

            _logger.LogWarning("PerformanceService", $"Aviso ao aplicar performance: {result.StandardError}");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError("PerformanceService", "Erro ao executar otimizações de performance", ex);
            return false;
        }
    }

    public async Task<bool> RollbackPerformanceTweaksAsync()
    {
        _logger.LogInformation("PerformanceService", "Revertendo otimizações de performance para valores padrão...");
        try
        {
            string scriptPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "scripts", "Performance-Optimize.ps1");
            if (!File.Exists(scriptPath))
            {
                scriptPath = Path.GetFullPath("scripts/Performance-Optimize.ps1");
            }

            var result = await _powerShellRunner.ExecuteCommandAsync($"& '{scriptPath.Replace("'", "''")}' -Rollback");
            return result.Success;
        }
        catch (Exception ex)
        {
            _logger.LogError("PerformanceService", "Erro ao reverter otimizações de performance", ex);
            return false;
        }
    }
}
