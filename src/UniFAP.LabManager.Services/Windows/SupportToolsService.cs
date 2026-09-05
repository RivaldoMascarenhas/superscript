using System.IO;
using UniFAP.LabManager.Core.Interfaces;
using UniFAP.LabManager.Infrastructure.Execution;

namespace UniFAP.LabManager.Services.Windows;

public class SupportToolsService : ISupportToolsService
{
    private readonly PowerShellRunner _powerShellRunner;
    private readonly ILogService _logger;

    public SupportToolsService(PowerShellRunner powerShellRunner, ILogService logger)
    {
        _powerShellRunner = powerShellRunner;
        _logger = logger;
    }

    public async Task<string> ResetNetworkStackAsync(bool dryRun = false)
        => await RunToolActionAsync("ResetNetworkStack", "", dryRun);

    public async Task<string> ClearWindowsProxyAsync(bool dryRun = false)
        => await RunToolActionAsync("ClearWindowsProxy", "", dryRun);

    public async Task<string> TestNetworkConnectivityAsync(string testHost = "unifap.edu.br", bool dryRun = false)
        => await RunToolActionAsync("TestNetworkConnectivity", "", dryRun);

    public async Task<string> RepairPrintSpoolerAsync(bool dryRun = false)
        => await RunToolActionAsync("RepairPrintSpooler", "", dryRun);

    public async Task<string> ResetWindowsUpdateAsync(bool dryRun = false)
        => await RunToolActionAsync("ResetWindowsUpdate", "", dryRun);

    public async Task<string> RestartShellAndAudioAsync(bool dryRun = false)
        => await RunToolActionAsync("RestartShellAndAudio", "", dryRun);

    public async Task<string> SyncGroupPolicyAsync(bool dryRun = false)
        => await RunToolActionAsync("SyncGroupPolicy", "", dryRun);

    public async Task<string> ClearCredentialVaultAsync(bool dryRun = false)
        => await RunToolActionAsync("ClearCredentialVault", "", dryRun);

    public async Task<string> DisableHibernationAsync(bool dryRun = false)
        => await RunToolActionAsync("DisableHibernation", "", dryRun);

    public async Task<string> OptimizeStorageDriveAsync(string driveLetter = "C", bool dryRun = false)
        => await RunToolActionAsync("OptimizeStorageDrive", $"-Target {driveLetter}", dryRun);

    public async Task<string> GenerateBatteryReportAsync(bool dryRun = false)
        => await RunToolActionAsync("GenerateBatteryReport", "", dryRun);

    public async Task<string> CheckWindowsActivationAsync(bool dryRun = false)
        => await RunToolActionAsync("CheckWindowsActivation", "", dryRun);

    public async Task<string> UpdateDefenderAndScanAsync(bool dryRun = false)
        => await RunToolActionAsync("UpdateDefenderAndScan", "", dryRun);

    private async Task<string> RunToolActionAsync(string action, string additionalArgs = "", bool dryRun = false)
    {
        _logger.LogInformation("SupportToolsService", $"Iniciando ferramenta de suporte: '{action}' [DryRun: {dryRun}]");

        string scriptPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "scripts", "Support-Tools.ps1");
        if (!File.Exists(scriptPath))
        {
            scriptPath = Path.GetFullPath("scripts/Support-Tools.ps1");
        }

        string args = $"-Action {action} {additionalArgs}".Trim();
        if (dryRun)
        {
            args += " -WhatIf";
        }

        try
        {
            var result = await _powerShellRunner.ExecuteScriptWithJsonResultAsync<ToolResultJson>(scriptPath, args);
            if (result.Success && result.Data != null && !string.IsNullOrWhiteSpace(result.Data.Message))
            {
                _logger.LogInformation("SupportToolsService", $"Ação '{action}' finalizada: {result.Data.Message}");
                return result.Data.Message;
            }

            if (!string.IsNullOrWhiteSpace(result.RawOutput))
            {
                // Limpa quebras de linha e retorna saída limpa
                var lines = result.RawOutput.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                string cleanOutput = lines.Length > 0 ? lines[^1] : result.RawOutput.Trim();
                _logger.LogInformation("SupportToolsService", $"Ação '{action}' finalizada com saída textual.");
                return cleanOutput;
            }

            string err = string.IsNullOrWhiteSpace(result.ErrorMessage) ? "Operação concluída sem detalhes adicionais." : $"Erro: {result.ErrorMessage}";
            _logger.LogWarning("SupportToolsService", $"Ação '{action}' reportou aviso: {err}");
            return err;
        }
        catch (Exception ex)
        {
            _logger.LogError("SupportToolsService", $"Falha inesperada ao executar ação '{action}'", ex);
            return $"Erro inesperado: {ex.Message}";
        }
    }

    private class ToolResultJson
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}
