using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.ServiceProcess;
using UniFAP.LabManager.Core.Enums;
using UniFAP.LabManager.Core.Interfaces;
using UniFAP.LabManager.Core.Models;
using UniFAP.LabManager.Infrastructure.Security;
using UniFAP.LabManager.Infrastructure.SystemAdapters;

namespace UniFAP.LabManager.Services.Diagnostics;

public class DiagnosticsService : IDiagnosticsService
{
    private readonly WmiAdapter _wmiAdapter;
    private readonly ISecurityService _securityService;
    private readonly IConfigService _configService;
    private readonly IWingetService _wingetService;
    private readonly ILogService _logger;

    public DiagnosticsService(
        WmiAdapter wmiAdapter,
        ISecurityService securityService,
        IConfigService configService,
        IWingetService wingetService,
        ILogService logger)
    {
        _wmiAdapter = wmiAdapter;
        _securityService = securityService;
        _configService = configService;
        _wingetService = wingetService;
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
        report.Checks.Add(new DiagnosticCheckResult
        {
            Category = "Sistema",
            Name = "Versão do Windows",
            Value = $"{sysInfo.OperatingSystem} (Build {sysInfo.BuildNumber})",
            Status = sysInfo.OperatingSystem.Contains("Windows 11") ? HealthStatus.Good : HealthStatus.Warning,
            Message = "Sistema operacional Windows 11 detectado.",
            ResolutionHint = "Atualize para a versão mais recente do Windows 11."
        });

        report.Checks.Add(new DiagnosticCheckResult
        {
            Category = "Hardware",
            Name = "Memória RAM Total",
            Value = $"{sysInfo.TotalMemoryGb} GB (Livre: {sysInfo.FreeMemoryGb} GB)",
            Status = sysInfo.TotalMemoryGb >= 7.5 ? HealthStatus.Good : HealthStatus.Warning,
            Message = sysInfo.TotalMemoryGb >= 7.5 ? "Capacidade de memória adequada." : "Memória abaixo do recomendado para softwares pesados (8 GB+).",
            ResolutionHint = "Adicione mais memória RAM física se planeja usar AutoCAD / Docker."
        });

        report.Checks.Add(new DiagnosticCheckResult
        {
            Category = "Armazenamento",
            Name = "Espaço em Disco (C:)",
            Value = $"{sysInfo.SystemDiskFreeGb} GB livres ({sysInfo.SystemDiskTotalGb} GB total)",
            Status = sysInfo.SystemDiskFreeGb >= 25.0 ? HealthStatus.Good : (sysInfo.SystemDiskFreeGb >= 10.0 ? HealthStatus.Warning : HealthStatus.Critical),
            Message = sysInfo.SystemDiskFreeGb >= 25.0 ? "Espaço em disco saudável." : "Espaço livre reduzido na unidade principal C:.",
            ResolutionHint = "Execute a limpeza de disco ou ative o Sensor de Armazenamento."
        });

        // 2. Rede & Conectividade
        report.Checks.Add(new DiagnosticCheckResult
        {
            Category = "Rede",
            Name = "Endereço IPv4 e Gateway",
            Value = $"{sysInfo.IpAddress} (Gateway: {sysInfo.DefaultGateway})",
            Status = (!string.IsNullOrWhiteSpace(sysInfo.IpAddress) && sysInfo.IpAddress != "127.0.0.1") ? HealthStatus.Good : HealthStatus.Critical,
            Message = "Adaptador de rede local configurado.",
            ResolutionHint = "Verifique o cabo de rede ou conexão Wi-Fi da instituição."
        });

        bool internetOk = await TestPingAsync("8.8.8.8");
        report.Checks.Add(new DiagnosticCheckResult
        {
            Category = "Rede",
            Name = "Acesso à Internet",
            Value = internetOk ? "Conectado" : "Sem Internet",
            Status = internetOk ? HealthStatus.Good : HealthStatus.Warning,
            Message = internetOk ? "Comunicação com a internet operacional." : "Sem acesso à internet externa.",
            ResolutionHint = "Verifique as configurações de proxy ou firewall de borda da UniFAP."
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
            Message = domainResolved ? $"Domínio '{domain}' resolvido com sucesso." : $"Não foi possível resolver o domínio '{domain}'.",
            ResolutionHint = "Configure os servidores DNS institucionais nas propriedades da placa de rede."
        });

        report.Checks.Add(new DiagnosticCheckResult
        {
            Category = "Active Directory",
            Name = "Status de Ingresso no Domínio",
            Value = sysInfo.IsDomainJoined ? $"Ingressado em {sysInfo.CurrentDomain}" : "Grupo de Trabalho (Workgroup)",
            Status = sysInfo.IsDomainJoined ? HealthStatus.Good : HealthStatus.Warning,
            Message = sysInfo.IsDomainJoined ? $"Máquina ingressada no domínio {sysInfo.CurrentDomain}." : "Máquina em Workgroup. Para setor administrativo, utilize a opção 'Preparar -> Administrativo'.",
            ResolutionHint = "Utilize o assistente de preparação para ingressar no domínio corporativo."
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
            ResolutionHint = isAdmin ? null : "Abra o aplicativo clicando com o botão direito e selecionando 'Executar como administrador'."
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
            ResolutionHint = "Instale o 'Instalador de Aplicativo' da Microsoft Store ou via pacote offline."
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
            report.Checks.Add(new DiagnosticCheckResult
            {
                Category = "Serviços",
                Name = displayName,
                Value = sc.Status.ToString(),
                Status = isRunning ? HealthStatus.Good : HealthStatus.Warning,
                Message = isRunning ? $"Serviço '{displayName}' em execução." : $"Serviço '{displayName}' está {sc.Status}.",
                ResolutionHint = isRunning ? null : $"Inicie o serviço '{serviceName}' via services.msc."
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
                ResolutionHint = null
            });
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
