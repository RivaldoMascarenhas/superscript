using Microsoft.Extensions.DependencyInjection;
using UniFAP.LabManager.Core.Enums;
using UniFAP.LabManager.Core.Interfaces;
using UniFAP.LabManager.Services;

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
        var configService = provider.GetRequiredService<IConfigService>();
        var jobOrchestrator = provider.GetRequiredService<IJobOrchestrator>();

        logger.LogInformation("UniFAP.Agent", "Iniciando verificação de Jobs pendentes após reinicialização...");

        await configService.LoadAllAsync();

        var pendingJob = await jobOrchestrator.CheckForPendingResumedJobAsync();

        if (pendingJob == null)
        {
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

        Console.WriteLine($"[INFO] Processamento finalizado. Resultado: {(success ? "SUCESSO" : "ALERTA/FALHA")}");
        logger.LogInformation("UniFAP.Agent", $"Execução do Job pós-reboot concluída com sucesso = {success}");
    }
}
