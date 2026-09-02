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

    public ICommand ApplyWallpaperCommand { get; }
    public ICommand ApplyPerformanceCommand { get; }
    public ICommand RollbackPerformanceCommand { get; }
    public ICommand ProvisionUsersCommand { get; }
    public ICommand RepairWindowsCommand { get; }
    public ICommand ValidateDomainCommand { get; }
    public ICommand CleanTempFilesCommand { get; }
    public ICommand OptimizeBrowsersCommand { get; }
    public ICommand ApplyDnsCommand { get; }

    public MaintenanceViewModel(
        IBrandingService brandingService,
        IPerformanceService performanceService,
        IUserService userService,
        IWindowsConfigurationService windowsService,
        IActiveDirectoryService adService,
        IConfigService configService,
        ILogService logger)
    {
        _brandingService = brandingService;
        _performanceService = performanceService;
        _userService = userService;
        _windowsService = windowsService;
        _adService = adService;
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
    }

    private void AppendLog(string message)
    {
        string entry = $"[{DateTime.Now:HH:mm:ss}] {message}";
        ActionLog += entry + Environment.NewLine;
        ExecutionStatus = message;
        _logger.LogInformation("Ferramentas", message);
    }

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
}

public class DnsOption
{
    public string DisplayName { get; set; } = string.Empty;
    public string PrimaryDns { get; set; } = string.Empty;
    public string? SecondaryDns { get; set; }
    public bool IsDhcp { get; set; }
    public override string ToString() => DisplayName;
}
