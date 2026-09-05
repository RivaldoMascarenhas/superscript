using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.ServiceProcess;
using UniFAP.LabManager.Core.Enums;
using UniFAP.LabManager.Core.Interfaces;
using UniFAP.LabManager.Core.Models;
using UniFAP.LabManager.Infrastructure.Execution;
using UniFAP.LabManager.Infrastructure.Security;
using UniFAP.LabManager.Infrastructure.SystemAdapters;

namespace UniFAP.LabManager.Services.Diagnostics;

public class DiagnosticsService : IDiagnosticsService
{
    private readonly WmiAdapter _wmiAdapter;
    private readonly ISecurityService _securityService;
    private readonly IConfigService _configService;
    private readonly IWingetService _wingetService;
    private readonly IPerformanceService _performanceService;
    private readonly ISupportToolsService _supportToolsService;
    private readonly PowerShellRunner _powerShellRunner;
    private readonly ILogService _logger;

    public DiagnosticsService(
        WmiAdapter wmiAdapter,
        ISecurityService securityService,
        IConfigService configService,
        IWingetService wingetService,
        IPerformanceService performanceService,
        ISupportToolsService supportToolsService,
        PowerShellRunner powerShellRunner,
        ILogService logger)
    {
        _wmiAdapter = wmiAdapter;
        _securityService = securityService;
        _configService = configService;
        _wingetService = wingetService;
        _performanceService = performanceService;
        _supportToolsService = supportToolsService;
        _powerShellRunner = powerShellRunner;
        _logger = logger;
    }

    public async Task<SystemInfo> CollectSystemInfoAsync()
    {
        return await Task.Run(() => _wmiAdapter.CollectSystemInfo());
    }

    public async Task<DiagnosticsReport> RunFullDiagnosticsAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("DiagnosticsService", "Iniciando bateria completa de diagnósticos do sistema...");
        var report = new DiagnosticsReport();
        var sysInfo = await CollectSystemInfoAsync();
        report.SystemInfo = sysInfo;

        // 1. Sistema & Hardware
        bool isWin11 = sysInfo.OperatingSystem.Contains("Windows 11");
        report.Checks.Add(new DiagnosticCheckResult
        {
            Category = "Sistema",
            Name = "Versão do Windows",
            Value = $"{sysInfo.OperatingSystem} (Build {sysInfo.BuildNumber})",
            Status = isWin11 ? HealthStatus.Good : HealthStatus.Warning,
            Message = isWin11 ? "Sistema operacional Windows 11 detectado." : "Recomendado atualizar para Windows 11 para total compatibilidade.",
            ResolutionHint = "Atualize para a versão mais recente do Windows 11.",
            RemediationAction = isWin11 ? null : "OpenWindowsUpdate",
            RemediationTitle = "Atualizar Windows"
        });

        report.Checks.Add(new DiagnosticCheckResult
        {
            Category = "Hardware",
            Name = "Memória RAM Total",
            Value = $"{sysInfo.TotalMemoryGb} GB (Livre: {sysInfo.FreeMemoryGb} GB)",
            Status = sysInfo.TotalMemoryGb >= 7.5 ? HealthStatus.Good : HealthStatus.Warning,
            Message = sysInfo.TotalMemoryGb >= 7.5 ? "Capacidade de memória adequada." : "Memória abaixo do recomendado para softwares pesados (8 GB+).",
            ResolutionHint = "Adicione mais memória RAM física se planeja usar AutoCAD / Docker.",
            RemediationAction = null,
            RemediationTitle = null
        });

        var diskStatus = sysInfo.SystemDiskFreeGb >= 25.0 ? HealthStatus.Good : (sysInfo.SystemDiskFreeGb >= 10.0 ? HealthStatus.Warning : HealthStatus.Critical);
        report.Checks.Add(new DiagnosticCheckResult
        {
            Category = "Armazenamento",
            Name = "Espaço em Disco (C:)",
            Value = $"{sysInfo.SystemDiskFreeGb} GB livres ({sysInfo.SystemDiskTotalGb} GB total)",
            Status = diskStatus,
            Message = sysInfo.SystemDiskFreeGb >= 25.0 ? "Espaço em disco saudável." : "Espaço livre reduzido na unidade principal C:.",
            ResolutionHint = "Execute a limpeza de disco ou ative o Sensor de Armazenamento.",
            RemediationAction = "CleanDisk",
            RemediationTitle = "Limpar Disco"
        });

        // 2. Rede & Conectividade
        bool ipConfigured = !string.IsNullOrWhiteSpace(sysInfo.IpAddress) && sysInfo.IpAddress != "127.0.0.1";
        report.Checks.Add(new DiagnosticCheckResult
        {
            Category = "Rede",
            Name = "Endereço IPv4 e Gateway",
            Value = $"{sysInfo.IpAddress} (Gateway: {sysInfo.DefaultGateway})",
            Status = ipConfigured ? HealthStatus.Good : HealthStatus.Critical,
            Message = ipConfigured ? "Adaptador de rede local configurado." : "Adaptador de rede sem endereço IPv4 válido.",
            ResolutionHint = "Verifique o cabo de rede ou conexão Wi-Fi da instituição.",
            RemediationAction = ipConfigured ? null : "ResetNetwork",
            RemediationTitle = "Reparar Rede"
        });

        bool internetOk = await TestPingAsync("8.8.8.8");
        report.Checks.Add(new DiagnosticCheckResult
        {
            Category = "Rede",
            Name = "Acesso à Internet",
            Value = internetOk ? "Conectado" : "Sem Internet",
            Status = internetOk ? HealthStatus.Good : HealthStatus.Warning,
            Message = internetOk ? "Comunicação com a internet operacional." : "Sem acesso à internet externa.",
            ResolutionHint = "Verifique as configurações de proxy ou firewall de borda da UniFAP.",
            RemediationAction = internetOk ? null : "ResetNetwork",
            RemediationTitle = "Reparar Rede"
        });

        // 3. Active Directory
        string domain = _configService.ActiveDirectory.Domain;
        bool domainResolved = false;
        try
        {
            var addrs = await System.Net.Dns.GetHostAddressesAsync(domain);
            domainResolved = addrs.Length > 0;
        }
        catch { }

        report.Checks.Add(new DiagnosticCheckResult
        {
            Category = "Active Directory",
            Name = "Resolução de Domínio UniFAP",
            Value = domain,
            Status = domainResolved ? HealthStatus.Good : (sysInfo.IsDomainJoined ? HealthStatus.Warning : HealthStatus.Unknown),
            Message = domainResolved ? $"Domínio '{domain}' resolvido com sucesso." : (sysInfo.IsDomainJoined ? $"Não foi possível resolver o domínio '{domain}'." : $"Domínio '{domain}' não resolvido (fora da intranet institucional)."),
            ResolutionHint = domainResolved ? null : "Configure os servidores DNS institucionais ou conecte-se à rede da UniFAP.",
            RemediationAction = domainResolved ? null : "ConfigureUniFapDns",
            RemediationTitle = "Configurar DNS UniFAP"
        });

        report.Checks.Add(new DiagnosticCheckResult
        {
            Category = "Active Directory",
            Name = "Status de Ingresso no Domínio",
            Value = sysInfo.IsDomainJoined ? $"Ingressado em {sysInfo.CurrentDomain}" : "Grupo de Trabalho (Workgroup)",
            Status = HealthStatus.Good,
            Message = sysInfo.IsDomainJoined ? $"Máquina ingressada no domínio {sysInfo.CurrentDomain}." : "Estação em Grupo de Trabalho (Workgroup). Pronta para perfil de Laboratório ou Administrativo.",
            ResolutionHint = sysInfo.IsDomainJoined ? null : "Caso deseje ingressar no domínio corporativo, utilize a opção 'Preparar -> Administrativo'.",
            RemediationAction = null,
            RemediationTitle = null
        });

        // 4. Segurança
        bool isAdmin = _securityService.IsElevatedAdministrator();
        report.Checks.Add(new DiagnosticCheckResult
        {
            Category = "Segurança",
            Name = "Elevação de Privilégios (Admin)",
            Value = isAdmin ? "Administrador (Elevado)" : "Usuário Padrão (Não Elevado)",
            Status = isAdmin ? HealthStatus.Good : HealthStatus.Warning,
            Message = isAdmin ? "O aplicativo possui privilégios administrativos completos." : "O aplicativo está rodando sem elevação. Ações que alteram configurações exigirão executar como Administrador.",
            ResolutionHint = isAdmin ? null : "Abra o aplicativo clicando com o botão direito e selecionando 'Executar como administrador'.",
            RemediationAction = isAdmin ? null : "RestartAsAdmin",
            RemediationTitle = "Executar como Admin"
        });

        // 5. Software & Gerenciador Winget
        bool wingetOk = await _wingetService.IsAvailableAsync() || sysInfo.IsWingetAvailable;
        report.Checks.Add(new DiagnosticCheckResult
        {
            Category = "Software",
            Name = "Windows Package Manager (Winget)",
            Value = wingetOk ? "Disponível" : "Indisponível",
            Status = wingetOk ? HealthStatus.Good : HealthStatus.Warning,
            Message = wingetOk ? "Instalador automático Winget operacional." : "Winget não encontrado. Apenas instaladores locais funcionarão.",
            ResolutionHint = "Instale o 'Instalador de Aplicativo' da Microsoft Store ou via pacote offline.",
            RemediationAction = wingetOk ? null : "InstallWinget",
            RemediationTitle = "Instalar Winget"
        });

        // 6. Serviços Críticos do Windows
        CheckWindowsService(report, "Spooler", "Spooler de Impressão");
        CheckWindowsService(report, "wuauserv", "Windows Update");
        CheckWindowsService(report, "W32Time", "Horário do Windows (NTP)");

        // Status Geral
        if (report.Checks.Any(c => c.Status == HealthStatus.Critical))
            report.OverallStatus = HealthStatus.Critical;
        else if (report.Checks.Any(c => c.Status == HealthStatus.Warning))
            report.OverallStatus = HealthStatus.Warning;
        else
            report.OverallStatus = HealthStatus.Good;

        _logger.LogInformation("DiagnosticsService", $"Diagnóstico concluído. Status Geral: {report.OverallStatus}");
        return report;
    }

    private void CheckWindowsService(DiagnosticsReport report, string serviceName, string displayName)
    {
        try
        {
            using var sc = new ServiceController(serviceName);
            bool isRunning = sc.Status == ServiceControllerStatus.Running;
            bool isDisabled = false;
            try
            {
                isDisabled = sc.StartType == ServiceStartMode.Disabled;
            }
            catch { }

            HealthStatus status;
            string message;
            if (isDisabled)
            {
                status = HealthStatus.Warning;
                message = $"Serviço '{displayName}' está desativado.";
            }
            else if (isRunning)
            {
                status = HealthStatus.Good;
                message = $"Serviço '{displayName}' em execução.";
            }
            else if (serviceName.Equals("Spooler", StringComparison.OrdinalIgnoreCase))
            {
                status = HealthStatus.Warning;
                message = $"Serviço '{displayName}' parado.";
            }
            else
            {
                // wuauserv e W32Time em modo de inicialização manual/gatilho são normais no Windows quando ociosos
                status = HealthStatus.Good;
                message = $"Serviço '{displayName}' configurado em demanda ({sc.Status}).";
            }

            report.Checks.Add(new DiagnosticCheckResult
            {
                Category = "Serviços",
                Name = displayName,
                Value = isRunning ? "Em execução" : (isDisabled ? "Desativado" : "Em demanda"),
                Status = status,
                Message = message,
                ResolutionHint = isRunning ? null : (isDisabled ? $"Ative o serviço '{serviceName}' via services.msc." : null),
                RemediationAction = isRunning ? null : $"StartService_{serviceName}",
                RemediationTitle = isDisabled ? "Ativar e Iniciar" : "Iniciar Serviço"
            });
        }
        catch
        {
            report.Checks.Add(new DiagnosticCheckResult
            {
                Category = "Serviços",
                Name = displayName,
                Value = "Não Detectado",
                Status = HealthStatus.Unknown,
                Message = $"Não foi possível consultar o serviço '{displayName}'.",
                ResolutionHint = null,
                RemediationAction = null,
                RemediationTitle = null
            });
        }
    }

    public async Task<DiagnosticRemediationResult> RemediateCheckAsync(string remediationAction, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("DiagnosticsService", $"Executando remediação automática para a ação: '{remediationAction}'");

        try
        {
            switch (remediationAction)
            {
                case "CleanDisk":
                {
                    string cleanOutput = await _performanceService.CleanTemporaryFilesAsync();
                    
                    // Reavaliar espaço em disco
                    double freeGb = 0;
                    double totalGb = 0;
                    try
                    {
                        var drive = new DriveInfo("C");
                        freeGb = Math.Round(drive.AvailableFreeSpace / (1024.0 * 1024.0 * 1024.0), 1);
                        totalGb = Math.Round(drive.TotalSize / (1024.0 * 1024.0 * 1024.0), 1);
                    }
                    catch
                    {
                        var sys = await CollectSystemInfoAsync();
                        freeGb = sys.SystemDiskFreeGb;
                        totalGb = sys.SystemDiskTotalGb;
                    }

                    var newStatus = freeGb >= 25.0 ? HealthStatus.Good : (freeGb >= 10.0 ? HealthStatus.Warning : HealthStatus.Critical);
                    return new DiagnosticRemediationResult
                    {
                        Success = true,
                        Message = $"Limpeza concluída com sucesso ({cleanOutput}). Espaço em disco atualizado.",
                        NewStatus = newStatus,
                        NewValue = $"{freeGb} GB livres ({totalGb} GB total)"
                    };
                }

                case "ConfigureUniFapDns":
                {
                    // Aplica DNS primário 10.0.0.1 e secundário 1.1.1.1 e limpa o cache
                    await _performanceService.ConfigureDnsAsync("10.0.0.1", "1.1.1.1");
                    await _powerShellRunner.ExecuteCommandAsync("Clear-DnsClientCache -ErrorAction SilentlyContinue");

                    string domain = _configService.ActiveDirectory.Domain;
                    bool resolved = false;
                    try
                    {
                        var addrs = await System.Net.Dns.GetHostAddressesAsync(domain);
                        resolved = addrs.Length > 0;
                    }
                    catch { }

                    return new DiagnosticRemediationResult
                    {
                        Success = true,
                        Message = resolved
                            ? $"Servidores DNS da UniFAP aplicados e domínio '{domain}' resolvido com sucesso!"
                            : $"DNS da UniFAP configurado. Se o computador estiver fora do campus, conecte-se à VPN institucional.",
                        NewStatus = resolved ? HealthStatus.Good : HealthStatus.Warning,
                        NewValue = resolved ? domain : "DNS Configurado"
                    };
                }

                case "ResetNetwork":
                {
                    string resetLog = await _supportToolsService.ResetNetworkStackAsync();
                    bool internetOk = await TestPingAsync("8.8.8.8");
                    return new DiagnosticRemediationResult
                    {
                        Success = internetOk,
                        Message = internetOk ? "Pilha de rede redefinida e internet restabelecida com sucesso." : "Pilha de rede redefinida. Verifique o cabo ou Wi-Fi.",
                        NewStatus = internetOk ? HealthStatus.Good : HealthStatus.Warning,
                        NewValue = internetOk ? "Conectado" : "Sem Conexão"
                    };
                }

                case "StartService_Spooler":
                {
                    await _supportToolsService.RepairPrintSpoolerAsync();
                    return new DiagnosticRemediationResult
                    {
                        Success = true,
                        Message = "Spooler de Impressão reparado e reiniciado com sucesso.",
                        NewStatus = HealthStatus.Good,
                        NewValue = "Em execução"
                    };
                }

                case "StartService_wuauserv":
                {
                    await _supportToolsService.ResetWindowsUpdateAsync();
                    return new DiagnosticRemediationResult
                    {
                        Success = true,
                        Message = "Serviço do Windows Update reparado e redefinido com sucesso.",
                        NewStatus = HealthStatus.Good,
                        NewValue = "Em execução"
                    };
                }

                case string s when s.StartsWith("StartService_"):
                {
                    string serviceName = s.Substring("StartService_".Length);
                    await _powerShellRunner.ExecuteCommandAsync($"Set-Service -Name '{serviceName}' -StartupType Automatic -ErrorAction SilentlyContinue; Start-Service -Name '{serviceName}' -ErrorAction SilentlyContinue");
                    
                    bool running = false;
                    try
                    {
                        using var sc = new ServiceController(serviceName);
                        running = sc.Status == ServiceControllerStatus.Running;
                    }
                    catch { }

                    return new DiagnosticRemediationResult
                    {
                        Success = true,
                        Message = running ? $"Serviço '{serviceName}' iniciado com sucesso." : $"Comando de inicialização enviado para o serviço '{serviceName}'.",
                        NewStatus = running ? HealthStatus.Good : HealthStatus.Warning,
                        NewValue = running ? "Em execução" : "Iniciado"
                    };
                }

                case "RestartAsAdmin":
                {
                    string? exePath = Environment.ProcessPath;
                    if (!string.IsNullOrEmpty(exePath) && File.Exists(exePath))
                    {
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = exePath,
                            UseShellExecute = true,
                            Verb = "runas"
                        });
                    }

                    return new DiagnosticRemediationResult
                    {
                        Success = true,
                        Message = "Janela com privilégios de Administrador solicitada via UAC.",
                        NewStatus = HealthStatus.Good,
                        NewValue = "Elevando..."
                    };
                }

                case "OpenWindowsUpdate":
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "ms-settings:windowsupdate",
                        UseShellExecute = true
                    });

                    return new DiagnosticRemediationResult
                    {
                        Success = true,
                        Message = "Painel do Windows Update aberto.",
                        NewStatus = HealthStatus.Good,
                        NewValue = "Aberto"
                    };
                }

                case "InstallWinget":
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "ms-windows-store://pdp/?productid=9NBLGGH4NNS1",
                        UseShellExecute = true
                    });

                    return new DiagnosticRemediationResult
                    {
                        Success = true,
                        Message = "Página do Instalador de Aplicativos (Winget) aberta na Microsoft Store.",
                        NewStatus = HealthStatus.Good,
                        NewValue = "Loja Aberta"
                    };
                }

                default:
                    return new DiagnosticRemediationResult
                    {
                        Success = false,
                        Message = $"Nenhuma ação configurada para '{remediationAction}'.",
                        NewStatus = HealthStatus.Warning
                    };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError("DiagnosticsService", $"Erro ao executar remediação '{remediationAction}'", ex);
            return new DiagnosticRemediationResult
            {
                Success = false,
                Message = $"Erro ao aplicar correção: {ex.Message}",
                NewStatus = HealthStatus.Warning
            };
        }
    }

    private async Task<bool> TestPingAsync(string host)
    {
        // 1. DNS check
        try
        {
            var addrs = await System.Net.Dns.GetHostAddressesAsync("www.google.com");
            if (addrs != null && addrs.Length > 0) return true;
        }
        catch { }

        // 2. Ping ICMP
        try
        {
            using var ping = new Ping();
            var reply = await ping.SendPingAsync(host, 1500);
            if (reply.Status == IPStatus.Success) return true;
        }
        catch { }

        // 3. Fallback TCP
        try
        {
            using var tcp = new TcpClient();
            var connectTask = tcp.ConnectAsync("1.1.1.1", 53);
            var completed = await Task.WhenAny(connectTask, Task.Delay(1500));
            return completed == connectTask && tcp.Connected;
        }
        catch
        {
            return false;
        }
    }
}
