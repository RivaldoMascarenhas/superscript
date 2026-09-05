using System.Collections.ObjectModel;
using System.Windows.Input;
using UniFAP.LabManager.Core.Interfaces;

namespace UniFAP.LabManager.App.ViewModels;

public class MaintenanceViewModel : ViewModelBase
{
    private readonly IBrandingService _brandingService;
    private readonly IPerformanceService _performanceService;
    private readonly IUserService _userService;
    private readonly IWindowsConfigurationService _windowsService;
    private readonly IActiveDirectoryService _adService;
    private readonly ISupportToolsService _supportToolsService;
    private readonly IConfigService _configService;
    private readonly ILogService _logger;

    private bool _isExecuting;
    private string _executionStatus = "Pronto para manutenção";
    private string _actionLog = string.Empty;

    public bool IsExecuting
    {
        get => _isExecuting;
        set => SetProperty(ref _isExecuting, value);
    }

    public string ExecutionStatus
    {
        get => _executionStatus;
        set => SetProperty(ref _executionStatus, value);
    }

    public string ActionLog
    {
        get => _actionLog;
        set => SetProperty(ref _actionLog, value);
    }

    public event Func<Task<(bool success, string password)>>? OnPromptSupportPassword;
    public event Action? OnLogAppended;

    public ObservableCollection<DnsOption> DnsOptions { get; } = new()
    {
        new DnsOption { DisplayName = "⚡ Cloudflare (1.1.1.1 / 1.0.0.1)", PrimaryDns = "1.1.1.1", SecondaryDns = "1.0.0.1" },
        new DnsOption { DisplayName = "🌐 Google Public DNS (8.8.8.8 / 8.8.4.4)", PrimaryDns = "8.8.8.8", SecondaryDns = "8.8.4.4" },
        new DnsOption { DisplayName = "🛡️ Quad9 Protegido (9.9.9.9 / 149.112.112.112)", PrimaryDns = "9.9.9.9", SecondaryDns = "149.112.112.112" },
        new DnsOption { DisplayName = "🏛️ Intranet UniFAP (10.0.0.1 / 1.1.1.1)", PrimaryDns = "10.0.0.1", SecondaryDns = "1.1.1.1" },
        new DnsOption { DisplayName = "🔄 DHCP Automático (Padrão do Roteador)", PrimaryDns = "", IsDhcp = true }
    };

    private DnsOption? _selectedDnsOption;
    public DnsOption? SelectedDnsOption
    {
        get => _selectedDnsOption;
        set => SetProperty(ref _selectedDnsOption, value);
    }

    // Comandos de Ferramentas Existentes
    public ICommand ApplyWallpaperCommand { get; }
    public ICommand ApplyPerformanceCommand { get; }
    public ICommand RollbackPerformanceCommand { get; }
    public ICommand ProvisionUsersCommand { get; }
    public ICommand RepairWindowsCommand { get; }
    public ICommand ValidateDomainCommand { get; }
    public ICommand CleanTempFilesCommand { get; }
    public ICommand OptimizeBrowsersCommand { get; }
    public ICommand ApplyDnsCommand { get; }
    public ICommand ClearLogCommand { get; }

    // Novos Comandos Especializados para Suporte de TI
    public ICommand ResetNetworkStackCommand { get; }
    public ICommand ClearWindowsProxyCommand { get; }
    public ICommand TestNetworkConnectivityCommand { get; }
    public ICommand RepairPrintSpoolerCommand { get; }
    public ICommand ResetWindowsUpdateCommand { get; }
    public ICommand RestartShellAndAudioCommand { get; }
    public ICommand SyncGroupPolicyCommand { get; }
    public ICommand ClearCredentialVaultCommand { get; }
    public ICommand DisableHibernationCommand { get; }
    public ICommand OptimizeStorageDriveCommand { get; }
    public ICommand GenerateBatteryReportCommand { get; }
    public ICommand CheckWindowsActivationCommand { get; }
    public ICommand UpdateDefenderAndScanCommand { get; }

    public MaintenanceViewModel(
        IBrandingService brandingService,
        IPerformanceService performanceService,
        IUserService userService,
        IWindowsConfigurationService windowsService,
        IActiveDirectoryService adService,
        ISupportToolsService supportToolsService,
        IConfigService configService,
        ILogService logger)
    {
        _brandingService = brandingService;
        _performanceService = performanceService;
        _userService = userService;
        _windowsService = windowsService;
        _adService = adService;
        _supportToolsService = supportToolsService;
        _configService = configService;
        _logger = logger;

        _selectedDnsOption = DnsOptions[0];

        ApplyWallpaperCommand = new AsyncRelayCommand(ApplyWallpaperAsync);
        ApplyPerformanceCommand = new AsyncRelayCommand(ApplyPerformanceAsync);
        RollbackPerformanceCommand = new AsyncRelayCommand(RollbackPerformanceAsync);
        ProvisionUsersCommand = new AsyncRelayCommand(ProvisionUsersAsync);
        RepairWindowsCommand = new AsyncRelayCommand(RepairWindowsAsync);
        ValidateDomainCommand = new AsyncRelayCommand(ValidateDomainAsync);
        CleanTempFilesCommand = new AsyncRelayCommand(CleanTempFilesAsync);
        OptimizeBrowsersCommand = new AsyncRelayCommand(OptimizeBrowsersAsync);
        ApplyDnsCommand = new AsyncRelayCommand(ApplyDnsAsync);
        ClearLogCommand = new RelayCommand(() => ActionLog = string.Empty);

        // Novos Comandos de Suporte
        ResetNetworkStackCommand = new AsyncRelayCommand(ResetNetworkStackAsync);
        ClearWindowsProxyCommand = new AsyncRelayCommand(ClearWindowsProxyAsync);
        TestNetworkConnectivityCommand = new AsyncRelayCommand(TestNetworkConnectivityAsync);
        RepairPrintSpoolerCommand = new AsyncRelayCommand(RepairPrintSpoolerAsync);
        ResetWindowsUpdateCommand = new AsyncRelayCommand(ResetWindowsUpdateAsync);
        RestartShellAndAudioCommand = new AsyncRelayCommand(RestartShellAndAudioAsync);
        SyncGroupPolicyCommand = new AsyncRelayCommand(SyncGroupPolicyAsync);
        ClearCredentialVaultCommand = new AsyncRelayCommand(ClearCredentialVaultAsync);
        DisableHibernationCommand = new AsyncRelayCommand(DisableHibernationAsync);
        OptimizeStorageDriveCommand = new AsyncRelayCommand(OptimizeStorageDriveAsync);
        GenerateBatteryReportCommand = new AsyncRelayCommand(GenerateBatteryReportAsync);
        CheckWindowsActivationCommand = new AsyncRelayCommand(CheckWindowsActivationAsync);
        UpdateDefenderAndScanCommand = new AsyncRelayCommand(UpdateDefenderAndScanAsync);
    }

    private void AppendLog(string message)
    {
        string entry = $"[{DateTime.Now:HH:mm:ss}] {message}";
        ActionLog += entry + Environment.NewLine;
        ExecutionStatus = message;
        _logger.LogInformation("Ferramentas", message);
        OnLogAppended?.Invoke();
    }

    // ================== FERRAMENTAS EXISTENTES ==================

    private async Task ApplyWallpaperAsync()
    {
        IsExecuting = true;
        AppendLog("Aplicando papel de parede institucional UniFAP...");
        try
        {
            bool ok = await _brandingService.ApplyBrandingAsync(dryRun: false);
            AppendLog(ok ? "✓ Papel de parede e informações OEM aplicados com sucesso!" : "⚠ Falha ao aplicar papel de parede.");
        }
        catch (Exception ex)
        {
            AppendLog($"✗ Erro: {ex.Message}");
        }
        finally
        {
            IsExecuting = false;
        }
    }

    private async Task ApplyPerformanceAsync()
    {
        IsExecuting = true;
        AppendLog("Aplicando otimizações de performance seguras...");
        try
        {
            bool ok = await _performanceService.ApplyPerformanceTweaksAsync(dryRun: false);
            AppendLog(ok ? "✓ Otimizações aplicadas mantendo fontes e ClearType!" : "⚠ Erro ao aplicar performance.");
        }
        catch (Exception ex)
        {
            AppendLog($"✗ Erro: {ex.Message}");
        }
        finally
        {
            IsExecuting = false;
        }
    }

    private async Task RollbackPerformanceAsync()
    {
        IsExecuting = true;
        AppendLog("Revertendo otimizações de performance...");
        try
        {
            bool ok = await _performanceService.RollbackPerformanceTweaksAsync();
            AppendLog(ok ? "✓ Configurações padrão do Windows restauradas." : "⚠ Falha no rollback.");
        }
        catch (Exception ex)
        {
            AppendLog($"✗ Erro: {ex.Message}");
        }
        finally
        {
            IsExecuting = false;
        }
    }

    private async Task ProvisionUsersAsync()
    {
        string? supportPassword = null;
        if (OnPromptSupportPassword != null)
        {
            var prompt = await OnPromptSupportPassword.Invoke();
            if (!prompt.success)
            {
                AppendLog("Operação de provisionamento de usuários cancelada pelo técnico.");
                return;
            }
            supportPassword = prompt.password;
        }

        IsExecuting = true;
        AppendLog("Provisionando usuário administrador 'suporte' e usuário padrão 'aluno' (sem senha)...");
        try
        {
            bool ok = await _userService.ProvisionUsersAsync(supportPassword, null, dryRun: false);
            if (ok)
            {
                AppendLog("✓ Usuários criados e privilégios isolados com sucesso!");
                AppendLog("  -> suporte (Administrador): Senha definida pelo técnico configurada.");
                AppendLog("  -> aluno (Usuário Padrão): Configurado SEM SENHA (acesso livre nos laboratórios).");
            }
            else
            {
                AppendLog("✗ Falha ao provisionar usuários locais.");
                AppendLog("  Dica: Certifique-se de executar o aplicativo com privilégios de Administrador.");
            }
        }
        catch (Exception ex)
        {
            AppendLog($"✗ Erro: {ex.Message}");
        }
        finally
        {
            IsExecuting = false;
        }
    }

    private async Task RepairWindowsAsync()
    {
        IsExecuting = true;
        AppendLog("Iniciando verificação e reparo de integridade do Windows (DISM / SFC)...");
        try
        {
            bool ok = await _windowsService.RepairSystemAsync(fullRepair: true, dryRun: false, line => AppendLog(line));
            AppendLog(ok ? "✓ Imagem do Windows e arquivos de sistema íntegros." : "⚠ Verificação concluída com alertas.");
        }
        catch (Exception ex)
        {
            AppendLog($"✗ Erro no reparo: {ex.Message}");
        }
        finally
        {
            IsExecuting = false;
        }
    }

    private async Task ValidateDomainAsync()
    {
        IsExecuting = true;
        AppendLog($"Validando conexão com Active Directory ({_configService.ActiveDirectory.Domain})...");
        try
        {
            var res = await _adService.ValidateDomainPreRequisitesAsync(_configService.ActiveDirectory.Domain, _configService.ActiveDirectory.DomainController);
            AppendLog(res.Success ? $"✓ Domínio e Controlador acessíveis: {res.Message}" : $"✗ Falha no AD: {res.Message}");
        }
        catch (Exception ex)
        {
            AppendLog($"✗ Erro: {ex.Message}");
        }
        finally
        {
            IsExecuting = false;
        }
    }

    private async Task CleanTempFilesAsync()
    {
        IsExecuting = true;
        AppendLog("Iniciando limpeza de arquivos temporários (%TEMP%, Windows Temp, Prefetch e Lixeira)...");
        try
        {
            string result = await _performanceService.CleanTemporaryFilesAsync(dryRun: false);
            AppendLog($"✓ Limpeza concluída: {result}");
        }
        catch (Exception ex)
        {
            AppendLog($"✗ Erro na limpeza: {ex.Message}");
        }
        finally
        {
            IsExecuting = false;
        }
    }

    private async Task OptimizeBrowsersAsync()
    {
        IsExecuting = true;
        AppendLog("Otimizando navegadores (Edge, Chrome, Firefox) e desativando inicialização em segundo plano...");
        try
        {
            bool ok = await _performanceService.OptimizeBrowsersAsync(dryRun: false);
            AppendLog(ok ? "✓ Navegadores otimizados! Startup Boost e caches redundantes desativados." : "⚠ Otimização concluída com avisos.");
        }
        catch (Exception ex)
        {
            AppendLog($"✗ Erro ao otimizar navegadores: {ex.Message}");
        }
        finally
        {
            IsExecuting = false;
        }
    }

    private async Task ApplyDnsAsync()
    {
        if (SelectedDnsOption == null) return;

        IsExecuting = true;
        AppendLog($"Configurando servidores DNS para: {SelectedDnsOption.DisplayName}...");
        try
        {
            bool ok = await _performanceService.ConfigureDnsAsync(
                SelectedDnsOption.PrimaryDns,
                SelectedDnsOption.SecondaryDns,
                SelectedDnsOption.IsDhcp);

            AppendLog(ok ? $"✓ DNS configurado com sucesso para todos os adaptadores de rede ativos!" : "⚠ Falha ao aplicar servidores DNS.");
        }
        catch (Exception ex)
        {
            AppendLog($"✗ Erro ao aplicar DNS: {ex.Message}");
        }
        finally
        {
            IsExecuting = false;
        }
    }

    // ================== NOVAS FERRAMENTAS PARA SUPORTE DE TI ==================

    private async Task ResetNetworkStackAsync()
    {
        IsExecuting = true;
        AppendLog("Redefinindo pilha de rede (Winsock, TCP/IP, DNS, ARP e DHCP)...");
        try
        {
            string res = await _supportToolsService.ResetNetworkStackAsync(dryRun: false);
            AppendLog($"✓ {res}");
        }
        catch (Exception ex)
        {
            AppendLog($"✗ Erro no reset de rede: {ex.Message}");
        }
        finally
        {
            IsExecuting = false;
        }
    }

    private async Task ClearWindowsProxyAsync()
    {
        IsExecuting = true;
        AppendLog("Redefinindo configurações de proxy do Windows e WinHTTP...");
        try
        {
            string res = await _supportToolsService.ClearWindowsProxyAsync(dryRun: false);
            AppendLog($"✓ {res}");
        }
        catch (Exception ex)
        {
            AppendLog($"✗ Erro ao redefinir proxy: {ex.Message}");
        }
        finally
        {
            IsExecuting = false;
        }
    }

    private async Task TestNetworkConnectivityAsync()
    {
        IsExecuting = true;
        AppendLog("Executando teste completo de conectividade institucional e internet...");
        try
        {
            string res = await _supportToolsService.TestNetworkConnectivityAsync(dryRun: false);
            AppendLog($"📊 Resultado da Conectividade:");
            foreach (var part in res.Split('|', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            {
                AppendLog($"   • {part}");
            }
        }
        catch (Exception ex)
        {
            AppendLog($"✗ Erro no teste de rede: {ex.Message}");
        }
        finally
        {
            IsExecuting = false;
        }
    }

    private async Task RepairPrintSpoolerAsync()
    {
        IsExecuting = true;
        AppendLog("Iniciando desbloqueio do Spooler de Impressão e esvaziamento de fila...");
        try
        {
            string res = await _supportToolsService.RepairPrintSpoolerAsync(dryRun: false);
            AppendLog($"✓ {res}");
        }
        catch (Exception ex)
        {
            AppendLog($"✗ Erro no spooler: {ex.Message}");
        }
        finally
        {
            IsExecuting = false;
        }
    }

    private async Task ResetWindowsUpdateAsync()
    {
        IsExecuting = true;
        AppendLog("Resetando serviços e caches do Windows Update (SoftwareDistribution / Catroot2)...");
        try
        {
            string res = await _supportToolsService.ResetWindowsUpdateAsync(dryRun: false);
            AppendLog($"✓ {res}");
        }
        catch (Exception ex)
        {
            AppendLog($"✗ Erro no Windows Update: {ex.Message}");
        }
        finally
        {
            IsExecuting = false;
        }
    }

    private async Task RestartShellAndAudioAsync()
    {
        IsExecuting = true;
        AppendLog("Reiniciando Windows Explorer e serviço de áudio...");
        try
        {
            string res = await _supportToolsService.RestartShellAndAudioAsync(dryRun: false);
            AppendLog($"✓ {res}");
        }
        catch (Exception ex)
        {
            AppendLog($"✗ Erro ao reiniciar serviços: {ex.Message}");
        }
        finally
        {
            IsExecuting = false;
        }
    }

    private async Task SyncGroupPolicyAsync()
    {
        IsExecuting = true;
        AppendLog("Forçando sincronização de Diretivas de Grupo (gpupdate /force)...");
        try
        {
            string res = await _supportToolsService.SyncGroupPolicyAsync(dryRun: false);
            AppendLog($"✓ {res}");
        }
        catch (Exception ex)
        {
            AppendLog($"✗ Erro no gpupdate: {ex.Message}");
        }
        finally
        {
            IsExecuting = false;
        }
    }

    private async Task ClearCredentialVaultAsync()
    {
        IsExecuting = true;
        AppendLog("Limpando credenciais em cache no Gerenciador de Credenciais do Windows...");
        try
        {
            string res = await _supportToolsService.ClearCredentialVaultAsync(dryRun: false);
            AppendLog($"✓ {res}");
        }
        catch (Exception ex)
        {
            AppendLog($"✗ Erro ao limpar credenciais: {ex.Message}");
        }
        finally
        {
            IsExecuting = false;
        }
    }

    private async Task DisableHibernationAsync()
    {
        IsExecuting = true;
        AppendLog("Desativando hibernação e Fast Startup (removendo hiberfil.sys)...");
        try
        {
            string res = await _supportToolsService.DisableHibernationAsync(dryRun: false);
            AppendLog($"✓ {res}");
        }
        catch (Exception ex)
        {
            AppendLog($"✗ Erro na hibernação: {ex.Message}");
        }
        finally
        {
            IsExecuting = false;
        }
    }

    private async Task OptimizeStorageDriveAsync()
    {
        IsExecuting = true;
        AppendLog("Executando otimização e comando TRIM na unidade C:...");
        try
        {
            string res = await _supportToolsService.OptimizeStorageDriveAsync("C", dryRun: false);
            AppendLog($"✓ {res}");
        }
        catch (Exception ex)
        {
            AppendLog($"✗ Erro ao otimizar unidade: {ex.Message}");
        }
        finally
        {
            IsExecuting = false;
        }
    }

    private async Task GenerateBatteryReportAsync()
    {
        IsExecuting = true;
        AppendLog("Gerando relatório oficial de saúde da bateria do dispositivo...");
        try
        {
            string res = await _supportToolsService.GenerateBatteryReportAsync(dryRun: false);
            AppendLog($"✓ {res}");
        }
        catch (Exception ex)
        {
            AppendLog($"✗ Erro ao gerar relatório de bateria: {ex.Message}");
        }
        finally
        {
            IsExecuting = false;
        }
    }

    private async Task CheckWindowsActivationAsync()
    {
        IsExecuting = true;
        AppendLog("Consultando status de licenciamento e ativação do Windows...");
        try
        {
            string res = await _supportToolsService.CheckWindowsActivationAsync(dryRun: false);
            AppendLog($"ℹ {res}");
        }
        catch (Exception ex)
        {
            AppendLog($"✗ Erro ao consultar ativação: {ex.Message}");
        }
        finally
        {
            IsExecuting = false;
        }
    }

    private async Task UpdateDefenderAndScanAsync()
    {
        IsExecuting = true;
        AppendLog("Atualizando assinaturas do Windows Defender e disparando verificação rápida...");
        try
        {
            string res = await _supportToolsService.UpdateDefenderAndScanAsync(dryRun: false);
            AppendLog($"✓ {res}");
        }
        catch (Exception ex)
        {
            AppendLog($"✗ Erro no Windows Defender: {ex.Message}");
        }
        finally
        {
            IsExecuting = false;
        }
    }
}

public class DnsOption
{
    public string DisplayName { get; set; } = string.Empty;
    public string PrimaryDns { get; set; } = string.Empty;
    public string? SecondaryDns { get; set; }
    public bool IsDhcp { get; set; }
    public override string ToString() => DisplayName;
}
