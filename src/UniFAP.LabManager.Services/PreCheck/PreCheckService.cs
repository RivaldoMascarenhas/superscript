using System.IO;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using UniFAP.LabManager.Core.Enums;
using UniFAP.LabManager.Core.Interfaces;
using UniFAP.LabManager.Core.Models;
using UniFAP.LabManager.Infrastructure.Security;
using UniFAP.LabManager.Infrastructure.SystemAdapters;

namespace UniFAP.LabManager.Services.PreCheck;

public class PreCheckService : IPreCheckService
{
    private readonly WmiAdapter _wmiAdapter;
    private readonly ISecurityService _securityService;
    private readonly IWingetService _wingetService;
    private readonly IConfigService _configService;
    private readonly ILogService _logger;

    public PreCheckService(
        WmiAdapter wmiAdapter,
        ISecurityService securityService,
        IWingetService wingetService,
        IConfigService configService,
        ILogService logger)
    {
        _wmiAdapter = wmiAdapter;
        _securityService = securityService;
        _wingetService = wingetService;
        _configService = configService;
        _logger = logger;
    }

    public async Task<PreCheckReport> RunPreCheckAsync(ComputerType computerType, bool joinActiveDirectory, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("PreCheckService", $"Iniciando pré-validação do sistema para modo: {computerType} (JoinAD: {joinActiveDirectory})");
        var report = new PreCheckReport();
        var sysInfo = _wmiAdapter.CollectSystemInfo();

        // 1. Verificação de Privilégios de Administrador
        bool isAdmin = _securityService.IsElevatedAdministrator();
        report.Items.Add(new PreCheckItem
        {
            Name = "Privilégios de Administrador",
            Category = "Segurança",
            Status = isAdmin ? PreCheckStatus.Passed : PreCheckStatus.Failed,
            Message = isAdmin ? "Executando como Administrador com privilégios de elevação." : "O aplicativo não está em modo de Administrador. Execute com 'Executar como Administrador'.",
            IsBlocking = true
        });

        // 2. Sistema Operacional (Windows 11)
        bool isWin11 = sysInfo.OperatingSystem.Contains("Windows 11") || (Environment.OSVersion.Version.Major >= 10 && Environment.OSVersion.Version.Build >= 22000);
        report.Items.Add(new PreCheckItem
        {
            Name = "Sistema Operacional",
            Category = "Sistema",
            Status = isWin11 ? PreCheckStatus.Passed : PreCheckStatus.Warning,
            Message = $"{sysInfo.OperatingSystem} (Build {sysInfo.BuildNumber})",
            Details = $"Arquitetura: {sysInfo.Architecture}",
            IsBlocking = false
        });

        // 3. Espaço em Disco
        bool diskOk = sysInfo.SystemDiskFreeGb >= 20.0;
        report.Items.Add(new PreCheckItem
        {
            Name = "Espaço Livre em Disco (C:)",
            Category = "Armazenamento",
            Status = diskOk ? PreCheckStatus.Passed : (sysInfo.SystemDiskFreeGb >= 10.0 ? PreCheckStatus.Warning : PreCheckStatus.Failed),
            Message = $"{sysInfo.SystemDiskFreeGb} GB livres de {sysInfo.SystemDiskTotalGb} GB",
            IsBlocking = sysInfo.SystemDiskFreeGb < 10.0
        });

        // 4. Memória RAM
        bool ramOk = sysInfo.TotalMemoryGb >= 7.5;
        report.Items.Add(new PreCheckItem
        {
            Name = "Memória RAM",
            Category = "Hardware",
            Status = ramOk ? PreCheckStatus.Passed : PreCheckStatus.Warning,
            Message = $"{sysInfo.TotalMemoryGb} GB instalados ({sysInfo.FreeMemoryGb} GB livres)",
            IsBlocking = false
        });

        // 5. Conectividade de Rede e Gateway
        bool netOk = sysInfo.IsNetworkConnected && !string.IsNullOrWhiteSpace(sysInfo.IpAddress) && sysInfo.IpAddress != "127.0.0.1";
        report.Items.Add(new PreCheckItem
        {
            Name = "Conexão de Rede Local",
            Category = "Rede",
            Status = netOk ? PreCheckStatus.Passed : PreCheckStatus.Failed,
            Message = netOk ? $"Conectado no IP {sysInfo.IpAddress} (Gateway: {sysInfo.DefaultGateway})" : "Nenhuma interface de rede ativa detectada.",
            IsBlocking = true
        });

        // 6. Resolução DNS e Conexão com a Internet
        bool dnsOk = sysInfo.DnsServers.Count > 0;
        bool internetOk = await TestInternetPingAsync();
        report.Items.Add(new PreCheckItem
        {
            Name = "DNS e Acesso à Internet",
            Category = "Rede",
            Status = (dnsOk && internetOk) ? PreCheckStatus.Passed : (dnsOk ? PreCheckStatus.Warning : PreCheckStatus.Failed),
            Message = internetOk ? $"Internet operacional via DNS: {string.Join(", ", sysInfo.DnsServers)}" : "Acesso à internet não detectado. Apenas softwares locais estarão disponíveis.",
            IsBlocking = false
        });

        // 7. Gerenciador de Pacotes Winget
        bool wingetOk = await _wingetService.IsAvailableAsync() || sysInfo.IsWingetAvailable;
        report.Items.Add(new PreCheckItem
        {
            Name = "Windows Package Manager (Winget)",
            Category = "Software",
            Status = wingetOk ? PreCheckStatus.Passed : PreCheckStatus.Warning,
            Message = wingetOk ? "Winget disponível e operacional." : "Winget não encontrado. Instalações serão feitas via instaladores locais ou executáveis.",
            IsBlocking = false
        });

        // 8. Pendência de Reinicialização
        report.Items.Add(new PreCheckItem
        {
            Name = "Pendência de Reinicialização",
            Category = "Sistema",
            Status = sysInfo.HasPendingReboot ? PreCheckStatus.Warning : PreCheckStatus.Passed,
            Message = sysInfo.HasPendingReboot ? "Existe uma reinicialização pendente do Windows." : "Nenhuma reinicialização pendente.",
            IsBlocking = false
        });

        // 9. Active Directory (se for Administrativo ou JoinAD marcado)
        if (computerType == ComputerType.Administrative || joinActiveDirectory)
        {
            string domain = _configService.ActiveDirectory.Domain;
            string dc = _configService.ActiveDirectory.DomainController;

            bool dnsResolved = await CheckDomainDnsResolutionAsync(domain);
            bool dcReachable = await CheckHostReachabilityAsync(dc);

            if (sysInfo.IsDomainJoined && sysInfo.CurrentDomain.Equals(domain, StringComparison.OrdinalIgnoreCase))
            {
                report.Items.Add(new PreCheckItem
                {
                    Name = "Active Directory Institucional",
                    Category = "Domínio",
                    Status = PreCheckStatus.Passed,
                    Message = $"O computador já está ingressado no domínio institucional '{domain}'.",
                    IsBlocking = false
                });
            }
            else
            {
                bool adReady = dnsResolved && dcReachable;
                report.Items.Add(new PreCheckItem
                {
                    Name = "Controlador de Domínio (AD)",
                    Category = "Domínio",
                    Status = adReady ? PreCheckStatus.Passed : PreCheckStatus.Failed,
                    Message = adReady
                        ? $"Domínio '{domain}' resolvido e Controlador '{dc}' acessível."
                        : $"Não foi possível localizar o controlador de domínio '{dc}' ou resolver o domínio '{domain}'. A preparação administrativa requer conectividade com o AD.",
                    IsBlocking = true
                });
            }
        }

        report.Summary = report.IsReady ? "STATUS: PRONTO" : "STATUS: ATENÇÃO — Ações bloqueantes detectadas";
        _logger.LogInformation("PreCheckService", $"Pré-validação finalizada: {report.Summary}");
        return report;
    }

    private async Task<bool> TestInternetPingAsync()
    {
        // 1. Tentar resolução DNS de múltiplos hosts
        string[] hosts = new[] { "intranet.unifapce.edu.br", "unifapce.edu.br", "www.google.com", "one.one.one.one" };
        foreach (var host in hosts)
        {
            try
            {
                var addrs = await System.Net.Dns.GetHostAddressesAsync(host);
                if (addrs != null && addrs.Length > 0)
                {
                    return true;
                }
            }
            catch { }
        }

        // 2. Fallback: Ping ICMP
        try
        {
            using var ping = new Ping();
            var reply = await ping.SendPingAsync("8.8.8.8", 1500);
            if (reply.Status == IPStatus.Success) return true;
        }
        catch { }

        // 3. Fallback: Conexão TCP na porta 80/443
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

    private async Task<bool> CheckDomainDnsResolutionAsync(string domain)
    {
        try
        {
            var addresses = await System.Net.Dns.GetHostAddressesAsync(domain);
            return addresses.Length > 0;
        }
        catch
        {
            return false;
        }
    }

    private async Task<bool> CheckHostReachabilityAsync(string host)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(host)) return true;
            using var ping = new Ping();
            var reply = await ping.SendPingAsync(host, 2500);
            return reply.Status == IPStatus.Success;
        }
        catch
        {
            // Em muitas redes corporativas ICMP pode estar desativado no firewall do DC, testamos porta LDAP 389 via TCP
            try
            {
                using var tcp = new TcpClient();
                var connectTask = tcp.ConnectAsync(host, 389);
                var timeoutTask = Task.Delay(2000);
                var completed = await Task.WhenAny(connectTask, timeoutTask);
                return completed == connectTask && tcp.Connected;
            }
            catch
            {
                return false;
            }
        }
    }
}
