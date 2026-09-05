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

    public async Task<bool> RenameComputerAsync(string name, string? domainUsername, string? domainPassword, CancellationToken cancellationToken = default)
    {
        if (!System.Text.RegularExpressions.Regex.IsMatch(name, @"^[A-Za-z0-9](?:[A-Za-z0-9-]{0,13}[A-Za-z0-9])?$") || name.All(char.IsDigit))
            throw new ArgumentException("Nome de computador invalido.", nameof(name));
        string command = $"Rename-Computer -NewName '{name}' -Force -ErrorAction Stop";
        if (!string.IsNullOrWhiteSpace(domainUsername) && !string.IsNullOrWhiteSpace(domainPassword))
        {
            command += $" -DomainCredential ([PSCredential]::new('{domainUsername.Replace("'", "''")}', (ConvertTo-SecureString '{domainPassword.Replace("'", "''")}' -AsPlainText -Force)))";
        }
        var result = await _powerShellRunner.ExecuteCommandAsync(command, cancellationToken: cancellationToken, sensitive: true);
        return result.Success;
    }

    public async Task RequestRebootAsync(int delaySeconds = 10, bool autoResume = true)
    {
        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
        string agentPath = Path.Combine(baseDir, "Agent", "UniFAP.LabManager.Agent.exe");
        if (!File.Exists(agentPath)) agentPath = Path.Combine(baseDir, "UniFAP.LabManager.Agent.exe");
        if (autoResume && !File.Exists(agentPath))
            throw new FileNotFoundException("Agente de retomada ausente. Publique o pacote completo antes de reiniciar.", agentPath);

        // Interactive logon preserves the operator's WinGet context; Highest supplies elevation without storing credentials.
        string registration = autoResume ? $@"
$ErrorActionPreference = 'Stop'
$identity = [Security.Principal.WindowsIdentity]::GetCurrent().Name
$action = New-ScheduledTaskAction -Execute '{agentPath.Replace("'", "''")}' -WorkingDirectory '{baseDir.Replace("'", "''")}'
$trigger = New-ScheduledTaskTrigger -AtLogOn -User $identity
$principal = New-ScheduledTaskPrincipal -UserId $identity -LogonType Interactive -RunLevel Highest
$settings = New-ScheduledTaskSettingsSet -ExecutionTimeLimit (New-TimeSpan -Hours 4) -StartWhenAvailable
Register-ScheduledTask -TaskName 'UniFAP_LabManager_Resume' -Action $action -Trigger $trigger -Principal $principal -Settings $settings -Force | Out-Null
" : "Unregister-ScheduledTask -TaskName 'UniFAP_LabManager_Resume' -Confirm:$false -ErrorAction SilentlyContinue";
        var prepared = await _powerShellRunner.ExecuteCommandAsync(registration);
        if (autoResume && !prepared.Success)
            throw new InvalidOperationException("Nao foi possivel preparar retomada elevada. Reinicializacao abortada: " + prepared.StandardError);

        using (var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\RunOnce", true))
            key?.DeleteValue("UniFAP_LabManager_Resume", false);
        var reboot = await _processRunner.RunAsync("shutdown.exe", $"/r /t {Math.Max(10, delaySeconds)}", null, 15);
        if (!reboot.Success) throw new InvalidOperationException("Falha ao agendar reinicializacao: " + reboot.StandardError);
    }
}
