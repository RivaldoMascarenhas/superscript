using Microsoft.Extensions.DependencyInjection;
using UniFAP.LabManager.Core.Enums;
using UniFAP.LabManager.Core.Interfaces;
using UniFAP.LabManager.Services;
using UniFAP.LabManager.Infrastructure.Execution;
using UniFAP.LabManager.Infrastructure.Persistence;

namespace UniFAP.LabManager.Agent;

public class Program
{
    public static async Task Main(string[] args)
    {
        Console.WriteLine("==========================================================");
        Console.WriteLine("   UNIFAP LAB MANAGER — AGENTE DE RETOMADA PÓS-REBOOT     ");
        Console.WriteLine("==========================================================");

        var services = new ServiceCollection();
        services.AddUniFapLabManagerServices();
        using var provider = services.BuildServiceProvider();

        var logger = provider.GetRequiredService<ILogService>();
        if (!provider.GetRequiredService<ISecurityService>().IsElevatedAdministrator())
        {
            logger.LogError("UniFAP.Agent", "Retomada exige privilegios administrativos.");
            Environment.ExitCode = 1;
            return;
        }
        string baseDirectory = AppContext.BaseDirectory;
        if (!Directory.Exists(Path.Combine(baseDirectory, "scripts")))
            baseDirectory = Directory.GetParent(baseDirectory.TrimEnd(Path.DirectorySeparatorChar))!.FullName;
        Directory.SetCurrentDirectory(baseDirectory);
        var configService = provider.GetRequiredService<IConfigService>();
        var jobOrchestrator = provider.GetRequiredService<IJobOrchestrator>();

        logger.LogInformation("UniFAP.Agent", "Iniciando verificação de Jobs pendentes após reinicialização...");

        await configService.LoadAllAsync();

        var pendingJob = await jobOrchestrator.CheckForPendingResumedJobAsync();

        if (pendingJob == null || !pendingJob.AutoResume)
        {
            if (pendingJob != null || await provider.GetRequiredService<JobPersistenceStore>().LoadActiveJobAsync() == null)
                await provider.GetRequiredService<PowerShellRunner>().ExecuteCommandAsync(
                    "Unregister-ScheduledTask -TaskName 'UniFAP_LabManager_Resume' -Confirm:$false -ErrorAction SilentlyContinue");
            logger.LogInformation("UniFAP.Agent", "Nenhum Job pendente encontrado. Encerrando agente com segurança.");
            Console.WriteLine("[INFO] Nenhum Job pendente. Sistema em operação normal.");
            return;
        }

        Console.WriteLine($"[INFO] Job detectado: {pendingJob.Id} (Perfil: {pendingJob.ProfileDisplayName})");
        Console.WriteLine($"[INFO] Retomando execução a partir da etapa {pendingJob.CurrentStepIndex + 1}/{pendingJob.Steps.Count}...");

        logger.LogInformation("UniFAP.Agent", $"Retomando execução do Job {pendingJob.Id} a partir do passo {pendingJob.CurrentStepIndex}...");

        jobOrchestrator.OnStepUpdated += step =>
        {
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Passo: {step.Name} -> Status: {step.Status}");
        };

        jobOrchestrator.OnLogMessage += msg =>
        {
            Console.WriteLine($"   > {msg}");
        };

        bool success = await jobOrchestrator.StartJobAsync(pendingJob);
        if (pendingJob.Status is JobStatus.Succeeded or JobStatus.Warning or JobStatus.Failed or JobStatus.Cancelled)
        {
            await provider.GetRequiredService<PowerShellRunner>().ExecuteCommandAsync(
                "Unregister-ScheduledTask -TaskName 'UniFAP_LabManager_Resume' -Confirm:$false -ErrorAction SilentlyContinue");
        }
        Environment.ExitCode = success ? 0 : 1;

        Console.WriteLine($"[INFO] Processamento finalizado. Resultado: {(success ? "SUCESSO" : "ALERTA/FALHA")}");
        logger.LogInformation("UniFAP.Agent", $"Execução do Job pós-reboot concluída com sucesso = {success}");
    }
}
