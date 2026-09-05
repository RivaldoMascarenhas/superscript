using System.Text.RegularExpressions;
using UniFAP.LabManager.Core.Enums;
using UniFAP.LabManager.Core.Interfaces;
using UniFAP.LabManager.Core.Models;
using UniFAP.LabManager.Infrastructure.Persistence;

namespace UniFAP.LabManager.Services.Orchestration;

public class JobOrchestrator : IJobOrchestrator
{
    private readonly IPreCheckService _preCheckService;
    private readonly IWindowsConfigurationService _windowsService;
    private readonly IUserService _userService;
    private readonly IBrandingService _brandingService;
    private readonly IPerformanceService _performanceService;
    private readonly ISoftwareService _softwareService;
    private readonly IActiveDirectoryService _adService;
    private readonly IReportService _reportService;
    private readonly IConfigService _configService;
    private readonly JobPersistenceStore _persistenceStore;
    private readonly ILogService _logger;

    private static readonly HashSet<string> BlockedAdministrativeSoftwareIds = new(StringComparer.OrdinalIgnoreCase)
    {
        "autocad", "autocad2025", "revit", "revit2025", "eberick", "lingo",
        "androidstudio", "arduino", "docker", "pycharm", "intellij",
        "python", "python3", "python311", "python312", "nodejs", "postgresql", "pgadmin", "qgis",
        "r", "r_project", "wireshark", "devcpp", "jdk21", "temurin-jdk21",
        "sniffy", "googleearth", "figma", "anylogic", "torbrowser"
    };

    private CancellationTokenSource? _currentCts;

    public Job? CurrentJob { get; private set; }
    public bool IsRunning { get; private set; }

    public event Action<Job>? OnJobUpdated;
    public event Action<JobStep>? OnStepUpdated;
    public event Action<string>? OnLogMessage;

    public JobOrchestrator(
        IPreCheckService preCheckService,
        IWindowsConfigurationService windowsService,
        IUserService userService,
        IBrandingService brandingService,
        IPerformanceService performanceService,
        ISoftwareService softwareService,
        IActiveDirectoryService adService,
        IReportService reportService,
        IConfigService configService,
        JobPersistenceStore persistenceStore,
        ILogService logger)
    {
        _preCheckService = preCheckService;
        _windowsService = windowsService;
        _userService = userService;
        _brandingService = brandingService;
        _performanceService = performanceService;
        _softwareService = softwareService;
        _adService = adService;
        _reportService = reportService;
        _configService = configService;
        _persistenceStore = persistenceStore;
        _logger = logger;
    }

    public async Task<Job> CreateJobAsync(
        ComputerType computerType,
        string profileId,
        List<string>? customSoftwareIds = null,
        bool dryRun = false,
        bool? joinDomain = null,
        string? supportPassword = null,
        string? newComputerName = null)
    {
        if (!string.IsNullOrWhiteSpace(newComputerName) &&
            (!Regex.IsMatch(newComputerName.Trim(), @"^[A-Za-z0-9](?:[A-Za-z0-9-]{0,13}[A-Za-z0-9])?$") ||
             newComputerName.Trim().All(char.IsDigit)))
            throw new ArgumentException("Nome do computador: use 1 a 15 letras, numeros ou hifens, sem hifen nas extremidades e nao apenas numeros.", nameof(newComputerName));
        var profile = _configService.GetProfile(profileId);
        string profileName = profile?.DisplayName ?? (computerType == ComputerType.Administrative ? "Administrativo Institucional" : "Laboratório Personalizado");
        bool joinAd = joinDomain ?? ((computerType == ComputerType.Administrative) || (profile?.JoinDomain ?? false));

        var job = new Job
        {
            ComputerType = computerType,
            ProfileId = profileId,
            ProfileDisplayName = profileName,
            JoinActiveDirectory = joinAd,
            DryRun = dryRun,
            TargetComputerName = Environment.MachineName,
            AutoReboot = _configService.Settings.AutoReboot,
            AutoResume = _configService.Settings.AutoResume,
            SupportPasswordText = supportPassword,
            NewComputerName = !string.IsNullOrWhiteSpace(newComputerName) ? newComputerName.Trim().ToUpperInvariant() : null
        };

        // Construir Lista de Softwares
        if (customSoftwareIds != null)
        {
            foreach (var id in customSoftwareIds)
            {
                var item = _configService.GetSoftware(id);
                if (item != null) job.SoftwareQueue.Add(CloneSoftware(item));
            }
        }
        else
        {
            job.SoftwareQueue = (_configService.GetSoftwareForProfile(profileId) ?? new List<SoftwareItem>()).Select(CloneSoftware).ToList();
        }

        if (computerType == ComputerType.Administrative)
        {
            // REGRA 25: O perfil Administrativo NUNCA deve instalar softwares específicos de laboratório, garantido pelo código.
            var adminAllowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "chrome", "firefox", "office365", "winrar", "adobe_reader"
            };
            if (_configService.Profiles.Administrative?.Software != null)
            {
                foreach (var s in _configService.Profiles.Administrative.Software)
                {
                    if (!BlockedAdministrativeSoftwareIds.Contains(s))
                    {
                        adminAllowed.Add(s);
                    }
                }
            }
            job.SoftwareQueue = job.SoftwareQueue
                .Where(s => adminAllowed.Contains(s.Id) && !BlockedAdministrativeSoftwareIds.Contains(s.Id))
                .ToList();
        }

        job.SelectedSoftwareIds = job.SoftwareQueue.Select(s => s.Id).ToList();

        // Construir Etapas do Job
        job.Steps.Add(new JobStep { Type = StepType.PreCheck, Name = "Validação Prévia do Sistema", Description = "Verificação de requisitos, espaço, conectividade e permissões", IsCritical = true });

        if (!string.IsNullOrWhiteSpace(job.NewComputerName))
        {
            job.Steps.Add(new JobStep { Type = StepType.ComputerName, Name = $"Renomear Computador → {job.NewComputerName}", Description = $"Renomeação da máquina para o tombamento de patrimônio '{job.NewComputerName}'", IsCritical = false });
        }

        job.Steps.Add(new JobStep { Type = StepType.Windows, Name = "Padronização do Windows 11", Description = "Aplicação das diretivas e configurações base da UniFAP", IsCritical = false });
        job.Steps.Add(new JobStep { Type = StepType.Users, Name = "Provisionamento de Usuários", Description = "Criação das contas 'suporte' (Admin) e 'aluno' (Padrão)", IsCritical = false });
        job.Steps.Add(new JobStep { Type = StepType.Branding, Name = "Identidade Visual UniFAP", Description = "Aplicação de papel de parede institucional e dados OEM", IsCritical = false });
        job.Steps.Add(new JobStep { Type = StepType.Performance, Name = "Otimização de Desempenho", Description = "Ajustes seguros sem degradar fontes ou estética visual", IsCritical = false });

        if (job.SoftwareQueue.Count > 0)
        {
            job.Steps.Add(new JobStep { Type = StepType.Software, Name = $"Instalação de Softwares ({job.SoftwareQueue.Count})", Description = $"Instalação do pacote de programas selecionado para {profileName}", IsCritical = false });
        }

        if (job.JoinActiveDirectory)
        {
            job.Steps.Add(new JobStep { Type = StepType.ActiveDirectory, Name = "Ingresso no Active Directory", Description = $"Ingresso no domínio institucional {_configService.ActiveDirectory.Domain}", IsCritical = false });
        }

        job.Steps.Add(new JobStep { Type = StepType.Validation, Name = "Validação e Diagnóstico Final", Description = "Verificação do estado final dos serviços e configurações", IsCritical = false });
        job.Steps.Add(new JobStep { Type = StepType.Report, Name = "Geração de Relatório", Description = "Emissão do relatório final em JSON e TXT", IsCritical = false });

        CurrentJob = job;
        await SaveJobStateAsync(job);
        return job;
    }

    private static SoftwareItem CloneSoftware(SoftwareItem original)
    {
        return new SoftwareItem
        {
            Id = original.Id,
            Name = original.Name,
            Category = original.Category,
            Description = original.Description,
            Type = original.Type,
            WingetId = original.WingetId,
            FallbackType = original.FallbackType,
            Installer = original.Installer,
            EntryPoint = original.EntryPoint,
            SilentArgs = original.SilentArgs,
            ScriptPath = original.ScriptPath,
            InstallerDir = original.InstallerDir,
            Arguments = original.Arguments,
            Silent = original.Silent,
            Severity = original.Severity,
            Legacy = original.Legacy,
            IconKey = original.IconKey,
            EstimatedSeconds = original.EstimatedSeconds,
            Source = original.Source,
            Hash = original.Hash,
            Enabled = original.Enabled,
            OfficialLink = original.OfficialLink,
            IsOpenSource = original.IsOpenSource,
            Version = original.Version,
            ChocoId = original.ChocoId,
            IsSelected = true
        };
    }

    public async Task<bool> StartJobAsync(Job job, CancellationToken cancellationToken = default)
    {
        if (IsRunning)
        {
            _logger.LogWarning("JobOrchestrator", "Um Job já está em execução.");
            return false;
        }

        using var lease = _persistenceStore.TryAcquireExecutionLease();
        if (lease == null)
        {
            _logger.LogWarning("JobOrchestrator", "Outra instancia esta executando a preparacao.");
            return false;
        }
        CurrentJob = job;
        IsRunning = true;
        _currentCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var token = _currentCts.Token;

        try
        {
            job.StartedAt ??= DateTime.Now;
            job.Status = JobStatus.Running;
            NotifyJobUpdated(job);
            await SaveJobStateAsync(job);

            _logger.LogInformation("JobOrchestrator", $"Iniciando Job {job.Id} para perfil '{job.ProfileDisplayName}' [DryRun: {job.DryRun}]");
            EmitLog($"Iniciando preparação do computador: {job.TargetComputerName}...");

            for (int i = job.CurrentStepIndex; i < job.Steps.Count; i++)
            {
                if (token.IsCancellationRequested)
                {
                    job.Status = JobStatus.Cancelled;
                    break;
                }

                job.CurrentStepIndex = i;
                var step = job.Steps[i];
                if (step.Type == StepType.Report) continue;

                if (step.Type == StepType.Validation && job.NeedsReboot && !job.DryRun)
                {
                    job.RebootRequestedAtUtc = DateTime.UtcNow;
                    await SaveJobStateAsync(job);
                    if (job.AutoReboot)
                        await _windowsService.RequestRebootAsync(10, job.AutoResume);
                    else
                        EmitLog("Preparacao pausada. Reinicie o Windows e abra o aplicativo para concluir a validacao.");
                    return true;
                }

                // RETOMADA PÓS-REBOOT: Não reexecutar etapas já concluídas com sucesso
                if (step.Status == StepStatus.Succeeded || step.Status == StepStatus.Skipped)
                {
                    EmitLog($"✓ Etapa [{i + 1}/{job.Steps.Count}] '{step.Name}' já concluída anteriormente. Pulando...");
                    continue;
                }

                step.Status = StepStatus.Running;
                step.StartTime = DateTime.Now;
                NotifyStepUpdated(step);
                NotifyJobUpdated(job);
                await SaveJobStateAsync(job);

                EmitLog($"--> Executando Etapa [{i + 1}/{job.Steps.Count}]: {step.Name}");

                bool stepSuccess = await ExecuteStepAsync(job, step, token);

                step.EndTime = DateTime.Now;
                if (stepSuccess)
                {
                    step.Status = step.Status == StepStatus.Warning ? StepStatus.Warning : StepStatus.Succeeded;
                    EmitLog($"✓ Etapa '{step.Name}' concluída com sucesso.");
                }
                else
                {
                    if (step.IsCritical)
                    {
                        step.Status = StepStatus.Failed;
                        job.Status = JobStatus.Failed;
                        job.ErrorMessage = $"Falha na etapa crítica: {step.Name} — {step.ErrorMessage}";
                        EmitLog($"✗ Falha na etapa crítica '{step.Name}': {step.ErrorMessage}");
                        NotifyStepUpdated(step);
                        NotifyJobUpdated(job);
                        await SaveJobStateAsync(job);
                        break;
                    }
                    else
                    {
                        step.Status = StepStatus.Warning;
                        EmitLog($"⚠ Etapa '{step.Name}' concluída com advertências.");
                    }
                }

                NotifyStepUpdated(step);
                NotifyJobUpdated(job);
                await SaveJobStateAsync(job);


            }

            if (job.Status != JobStatus.Cancelled && job.Status != JobStatus.Failed)
            {
                int warningCount = job.Steps.Count(s => s.Status == StepStatus.Warning) + job.SoftwareQueue.Count(sw => sw.Status == SoftwareInstallStatus.Warning);
                job.Status = job.Steps.Any(s => s.Status == StepStatus.Failed) ||
                    job.SoftwareQueue.Any(s => s.Status == SoftwareInstallStatus.Failed && s.Severity == SoftwareSeverity.Critical)
                    ? JobStatus.Failed : warningCount > 0 ? JobStatus.Warning : JobStatus.Succeeded;
            }

            job.CompletedAt = DateTime.Now;
            var reportStep = job.Steps.FirstOrDefault(s => s.Type == StepType.Report);
            if (reportStep != null)
            {
                reportStep.StartTime = DateTime.Now;
                reportStep.EndTime = DateTime.Now;
                reportStep.Status = StepStatus.Succeeded;
                try { await _reportService.GenerateReportAsync(job); }
                catch (Exception ex)
                {
                    reportStep.Status = StepStatus.Failed;
                    reportStep.ErrorMessage = ex.Message;
                    job.Status = JobStatus.Failed;
                    job.ErrorMessage = "Falha ao gravar o relatorio final: " + ex.Message;
                }
                NotifyStepUpdated(reportStep);
            }
            _logger.LogInformation("JobOrchestrator", $"Job {job.Id} finalizado com status: {job.Status}");
            EmitLog($"=== PROCESSO FINALIZADO: STATUS {job.Status.ToString().ToUpper()} ===");

            NotifyJobUpdated(job);
            await SaveJobStateAsync(job);
            return job.Status == JobStatus.Succeeded || job.Status == JobStatus.Warning;
        }
        catch (OperationCanceledException)
        {
            job.Status = JobStatus.Cancelled;
            EmitLog("Preparação cancelada pelo usuário.");
            NotifyJobUpdated(job);
            await SaveJobStateAsync(job);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError("JobOrchestrator", $"Exceção fatal durante Job {job.Id}", ex);
            job.Status = JobStatus.Failed;
            job.ErrorMessage = ex.Message;
            EmitLog($"ERRO FATAL: {ex.Message}");
            NotifyJobUpdated(job);
            await SaveJobStateAsync(job);
            return false;
        }
        finally
        {
            job.SupportPasswordText = null;
            job.DomainPasswordText = null;
            _currentCts?.Dispose();
            _currentCts = null;
            IsRunning = false;
        }
    }

    private async Task<bool> ExecuteStepAsync(Job job, JobStep step, CancellationToken token)
    {
        try
        {
            switch (step.Type)
            {
                case StepType.PreCheck:
                    var preCheck = await _preCheckService.RunPreCheckAsync(job.ComputerType, job.JoinActiveDirectory, token);
                    step.Details = preCheck.Summary;
                    if (!preCheck.IsReady && !job.DryRun)
                    {
                        step.ErrorMessage = "Requisitos prévios não atendidos para preparação.";
                        return false;
                    }
                    return true;

                case StepType.ComputerName:
                    if (string.IsNullOrWhiteSpace(job.NewComputerName))
                    {
                        step.Details = "Nenhum nome de computador informado. Etapa ignorada.";
                        return true;
                    }
                    if (Environment.MachineName.Equals(job.NewComputerName, StringComparison.OrdinalIgnoreCase))
                    {
                        step.Details = $"O computador já possui o nome '{job.NewComputerName}'. Nenhuma alteração necessária.";
                        return true;
                    }
                    if (job.DryRun)
                    {
                        step.Details = $"[DRY-RUN] Renomearia o computador de '{Environment.MachineName}' para '{job.NewComputerName}'.";
                        return true;
                    }
                    EmitLog($"Renomeando computador de '{Environment.MachineName}' para '{job.NewComputerName}'...");
                    bool renamed = await _windowsService.RenameComputerAsync(job.NewComputerName, job.DomainUsername, job.DomainPasswordText, token);
                    if (!renamed)
                    {
                        step.ErrorMessage = "Falha ao renomear o computador. Consulte o log.";
                        return false;
                    }
                    job.TargetComputerName = job.NewComputerName;
                    job.NeedsReboot = true;
                    step.Details = "Nome alterado; sera efetivado na reinicializacao.";
                    return true;

                case StepType.Windows:
                    return await _windowsService.ApplyOptimizationsAsync(job.DryRun);

                case StepType.Users:
                    if (!job.DryRun && string.IsNullOrWhiteSpace(job.SupportPasswordText))
                    {
                        step.IsCritical = true;
                        step.ErrorMessage = "Senha do suporte indisponivel. Inicie uma nova preparacao com as credenciais.";
                        return false;
                    }
                    try
                    {
                        return await _userService.ProvisionUsersAsync(job.SupportPasswordText, null, job.DryRun);
                    }
                    finally
                    {
                        job.SupportPasswordText = null;
                    }

                case StepType.Branding:
                    return await _brandingService.ApplyBrandingAsync(job.DryRun);

                case StepType.Performance:
                    return await _performanceService.ApplyPerformanceTweaksAsync(job.DryRun);

                case StepType.Software:
                    return await ExecuteSoftwareStepAsync(job, step, token);

                case StepType.ActiveDirectory:
                    return await ExecuteActiveDirectoryStepAsync(job, step, token);

                case StepType.Validation:
                    return await ValidatePreparationAsync(job, step, token);

                case StepType.Report:
                    // O relatorio e gravado apos calcular o resultado final.
                    return true;

                default:
                    return true;
            }
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _logger.LogError("JobOrchestrator", $"Erro ao executar etapa '{step.Name}'", ex);
            step.ErrorMessage = ex.Message;
            return false;
        }
    }

    private async Task<bool> ValidatePreparationAsync(Job job, JobStep step, CancellationToken token)
    {
        if (job.DryRun)
        {
            step.Details = "[SIMULACAO] Verificaria usuarios, privilegios, nome e dominio sem modificar o Windows.";
            return true;
        }
        var failures = new List<string>();
        token.ThrowIfCancellationRequested();
        string support = _configService.Users.Users.TryGetValue("support", out var sup) ? sup.Name : "suporte";
        string student = _configService.Users.Users.TryGetValue("student", out var stu) ? stu.Name : "aluno";
        if (!await _userService.IsUserConfiguredAsync(support) || !await _userService.IsInAdminGroupAsync(support))
            failures.Add("Conta de suporte ausente, desativada ou sem privilegios administrativos.");
        if (!await _userService.IsUserConfiguredAsync(student) || await _userService.IsInAdminGroupAsync(student))
            failures.Add("Conta de aluno ausente, desativada ou com privilegios administrativos indevidos.");
        if (!string.IsNullOrWhiteSpace(job.NewComputerName) && !Environment.MachineName.Equals(job.NewComputerName, StringComparison.OrdinalIgnoreCase))
            failures.Add("Nome do computador diferente do solicitado.");
        if (job.JoinActiveDirectory && (!await _adService.IsDomainJoinedAsync() ||
            !string.Equals(await _adService.GetCurrentDomainAsync(), _configService.ActiveDirectory.Domain, StringComparison.OrdinalIgnoreCase)))
            failures.Add("Computador nao ingressou no dominio solicitado.");
        token.ThrowIfCancellationRequested();
        if (failures.Count > 0)
        {
            step.ErrorMessage = string.Join(" ", failures);
            step.IsCritical = true;
            return false;
        }
        step.Details = "Usuarios, privilegios, nome e dominio conferidos. Softwares conferidos ao terminar cada instalacao.";
        return true;
    }

    private async Task<bool> ExecuteSoftwareStepAsync(Job job, JobStep step, CancellationToken token)
    {
        bool allOk = true;
        for (int i = 0; i < job.SoftwareQueue.Count; i++)
        {
            token.ThrowIfCancellationRequested();

            var sw = job.SoftwareQueue[i];

            // RETOMADA PÓS-REBOOT: Se o software já foi instalado com sucesso, pular!
            if (sw.Status == SoftwareInstallStatus.Installed)
            {
                EmitLog($"[{i + 1}/{job.SoftwareQueue.Count}] '{sw.Name}' já instalado anteriormente. Pulando...");
                continue;
            }

            sw.Status = SoftwareInstallStatus.Installing;
            NotifyJobUpdated(job);

            EmitLog($"[{i + 1}/{job.SoftwareQueue.Count}] Instalando: {sw.Name}...");

            var result = await _softwareService.InstallAsync(sw, job.DryRun, msg => EmitLog($"   > {msg}"), token);

            token.ThrowIfCancellationRequested();
            if (result.Success && (result.ExitCode == 3010 || result.ExitCode == 1641)) job.NeedsReboot = true;
            if (!job.DryRun && result.Status == SoftwareInstallStatus.Installed && !await _softwareService.IsInstalledAsync(sw))
            {
                result.Status = sw.Type == SoftwareType.Script || sw.Legacy ? SoftwareInstallStatus.Warning : SoftwareInstallStatus.Failed;
                result.Message = "O instalador terminou, mas a presenca do software nao foi confirmada.";
            }
            sw.Status = result.Status;
            sw.ErrorMessage = result.Message;

            if (result.Status == SoftwareInstallStatus.Installed)
            {
                EmitLog($"   ✓ {sw.Name} instalado com sucesso.");
            }
            else if (result.Status == SoftwareInstallStatus.Warning)
            {
                EmitLog($"   ⚠ {sw.Name}: {result.Message}");
                step.Status = StepStatus.Warning;
            }
            else
            {
                EmitLog($"   ✗ Falha ao instalar {sw.Name}: {result.Message}");
                if (sw.Severity == SoftwareSeverity.Critical)
                {
                    allOk = false;
                }
                else
                {
                    step.Status = StepStatus.Warning;
                }
            }

            NotifyJobUpdated(job);
            await SaveJobStateAsync(job);
        }

        if (job.DryRun) return allOk;
        token.ThrowIfCancellationRequested();

        // Criar atalhos na Área de Trabalho Pública para todos os usuários do computador
        try
        {
            EmitLog("Criando atalhos na Área de Trabalho para todos os usuários...");
            int shortcuts = await _brandingService.CreateDesktopShortcutsAsync();
            EmitLog($"✓ Atalhos da Área de Trabalho configurados com sucesso ({shortcuts} atalhos criados/atualizados).");
        }
        catch (Exception ex)
        {
            _logger.LogWarning("JobOrchestrator", $"Falha ao gerar atalhos de desktop: {ex.Message}");
        }

        return allOk;
    }

    private async Task<bool> ExecuteActiveDirectoryStepAsync(Job job, JobStep step, CancellationToken token)
    {
        string domain = _configService.ActiveDirectory.Domain;
        string dc = _configService.ActiveDirectory.DomainController;
        string ou = job.ComputerType == ComputerType.Administrative
            ? _configService.ActiveDirectory.AdministrativeOu
            : _configService.ActiveDirectory.AcademicOu;

        string username = job.DomainUsername ?? "admin";
        string password = job.DomainPasswordText ?? "";

        try
        {
            var result = await _adService.JoinDomainAsync(domain, dc, ou, username, password, job.DryRun);

            if (result.Success)
            {
                if (result.NeedsReboot)
                {
                    job.NeedsReboot = true;
                }
                return true;
            }

            step.ErrorMessage = result.ErrorDetails ?? result.Message;
            return false;
        }
        finally
        {
            // REGRA 5: A senha só deve existir na memória durante a operação e é descartada imediatamente
            job.DomainPasswordText = null;
        }
    }

    public Task CancelJobAsync()
    {
        if (IsRunning && _currentCts != null)
        {
            _logger.LogWarning("JobOrchestrator", "Cancelamento de Job solicitado pelo operador.");
            _currentCts.Cancel();
        }
        return Task.CompletedTask;
    }

    public async Task<Job?> CheckForPendingResumedJobAsync()
    {
        var job = await _persistenceStore.LoadActiveJobAsync();
        if (job != null && (job.Status == JobStatus.Running || job.Status == JobStatus.Pending))
        {
            if (job.RebootRequestedAtUtc.HasValue)
            {
                var bootTime = DateTime.UtcNow - TimeSpan.FromMilliseconds(Environment.TickCount64);
                if (bootTime <= job.RebootRequestedAtUtc.Value) return null;
                job.NeedsReboot = false;
                job.RebootRequestedAtUtc = null;
            }
            job.IsResumed = true;
            _logger.LogInformation("JobOrchestrator", $"Detectado Job pendente para retomada pós-reboot: {job.Id}");
            CurrentJob = job;
            return job;
        }
        return null;
    }

    public async Task SaveJobStateAsync(Job job)
    {
        await _persistenceStore.SaveActiveJobAsync(job);
    }

    public Task ClearJobStateAsync(string jobId)
    {
        _persistenceStore.ClearActiveJob();
        return Task.CompletedTask;
    }

    public async Task<List<Job>> GetJobHistoryAsync()
    {
        return await _persistenceStore.GetAllJobsHistoryAsync();
    }

    private void NotifyJobUpdated(Job job)
    {
        OnJobUpdated?.Invoke(job);
    }

    private void NotifyStepUpdated(JobStep step)
    {
        OnStepUpdated?.Invoke(step);
    }

    private void EmitLog(string message)
    {
        _logger.LogInformation("JobEngine", message);
        OnLogMessage?.Invoke(message);
    }
}
