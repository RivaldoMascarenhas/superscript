using System.IO;
using UniFAP.LabManager.Core.Interfaces;
using UniFAP.LabManager.Core.Models;
using UniFAP.LabManager.Infrastructure.Execution;
using UniFAP.LabManager.Infrastructure.SystemAdapters;

namespace UniFAP.LabManager.Services.Windows;

public class WindowsConfigurationService : IWindowsConfigurationService
{
    private readonly WmiAdapter _wmiAdapter;
    private readonly PowerShellRunner _powerShellRunner;
    private readonly ProcessRunner _processRunner;
    private readonly ILogService _logger;

    public WindowsConfigurationService(
        WmiAdapter wmiAdapter,
        PowerShellRunner powerShellRunner,
        ProcessRunner processRunner,
        ILogService logger)
    {
        _wmiAdapter = wmiAdapter;
        _powerShellRunner = powerShellRunner;
        _processRunner = processRunner;
        _logger = logger;
    }

    public async Task<SystemInfo> GetSystemInfoAsync()
    {
        return await Task.Run(() => _wmiAdapter.CollectSystemInfo());
    }

    public async Task<bool> ApplyOptimizationsAsync(bool dryRun = false)
    {
        if (dryRun) return true;
        var result = await _powerShellRunner.ExecuteCommandAsync("Set-ItemProperty -Path 'HKCU:\\Control Panel\\Desktop' -Name 'MenuShowDelay' -Value '150' -Force");
        return result.Success;
    }

    public async Task<bool> RepairSystemAsync(bool fullRepair = false, bool dryRun = false, Action<string>? progress = null)
    {
        _logger.LogInformation("WindowsConfigurationService", $"Iniciando reparo de integridade do Windows [FullRepair: {fullRepair}] [DryRun: {dryRun}]");

        if (dryRun)
        {
            _logger.LogInformation("WindowsConfigurationService", "[DRY-RUN] Simulação: Verificação de integridade DISM e SFC seria executada.");
            progress?.Invoke("[SIMULAÇÃO] Verificação de integridade DISM e SFC concluída.");
            return true;
        }

        try
        {
            string scriptPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "scripts", "Windows-Repair.ps1");
            if (!File.Exists(scriptPath))
            {
                scriptPath = Path.GetFullPath("scripts/Windows-Repair.ps1");
            }

            string mode = fullRepair ? "FullRepair" : "ScanOnly";
            var result = await _powerShellRunner.ExecuteScriptFileAsync(scriptPath, $"-Mode {mode}", progress);

            _logger.LogInformation("WindowsConfigurationService", $"Reparo do Windows finalizado com código {result.ExitCode}");
            return result.Success;
        }
        catch (Exception ex)
        {
            _logger.LogError("WindowsConfigurationService", "Erro ao executar reparo do Windows", ex);
            return false;
        }
    }

    public async Task<bool> HasPendingRebootAsync()
    {
        var info = await GetSystemInfoAsync();
        return info.HasPendingReboot;
    }

    public async Task RequestRebootAsync(int delaySeconds = 10)
    {
        _logger.LogInformation("WindowsConfigurationService", $"Agendando reinicialização do sistema em {delaySeconds} segundos...");

        // Configurar inicialização automática do Agent no RunOnce
        try
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string agentPath = Path.Combine(baseDir, "Agent", "UniFAP.LabManager.Agent.exe");
            if (!File.Exists(agentPath))
            {
                agentPath = Path.Combine(baseDir, "UniFAP.LabManager.Agent.exe");
            }

            if (File.Exists(agentPath))
            {
                using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\RunOnce", true);
                key?.SetValue("UniFAP_LabManager_Resume", $"\"{agentPath}\"");
                _logger.LogInformation("WindowsConfigurationService", "Chave RunOnce configurada com sucesso para retomada do Agente.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning("WindowsConfigurationService", $"Não foi possível gravar chave RunOnce: {ex.Message}");
        }

        await _processRunner.RunAsync("shutdown.exe", $"/r /t {delaySeconds} /c \"UniFAP Lab Manager: Reiniciando para concluir a preparação do computador...\"", null, 15);
    }
}
