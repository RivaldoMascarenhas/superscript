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

    public ICommand ApplyWallpaperCommand { get; }
    public ICommand ApplyPerformanceCommand { get; }
    public ICommand RollbackPerformanceCommand { get; }
    public ICommand ProvisionUsersCommand { get; }
    public ICommand RepairWindowsCommand { get; }
    public ICommand ValidateDomainCommand { get; }

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

        ApplyWallpaperCommand = new AsyncRelayCommand(ApplyWallpaperAsync);
        ApplyPerformanceCommand = new AsyncRelayCommand(ApplyPerformanceAsync);
        RollbackPerformanceCommand = new AsyncRelayCommand(RollbackPerformanceAsync);
        ProvisionUsersCommand = new AsyncRelayCommand(ProvisionUsersAsync);
        RepairWindowsCommand = new AsyncRelayCommand(RepairWindowsAsync);
        ValidateDomainCommand = new AsyncRelayCommand(ValidateDomainAsync);
    }

    private void AppendLog(string message)
    {
        string entry = $"[{DateTime.Now:HH:mm:ss}] {message}";
        ActionLog += entry + Environment.NewLine;
        ExecutionStatus = message;
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
        IsExecuting = true;
        AppendLog("Provisionando usuários 'suporte' (Admin) e 'aluno' (Padrão)...");
        try
        {
            bool ok = await _userService.ProvisionUsersAsync(null, null, dryRun: false);
            if (ok)
            {
                AppendLog("✓ Usuários criados e privilégios isolados com sucesso!");
                AppendLog("  -> suporte (Administrador): UniFAP@Suporte2026!");
                AppendLog("  -> aluno (Usuário Padrão): UniFAP@Aluno2026!");
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
}
