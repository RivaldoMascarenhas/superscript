using System.IO;
using System.Security;
using UniFAP.LabManager.Core.Interfaces;
using UniFAP.LabManager.Infrastructure.Execution;
using UniFAP.LabManager.Infrastructure.SystemAdapters;

namespace UniFAP.LabManager.Services.ActiveDirectory;

public class ActiveDirectoryService : IActiveDirectoryService
{
    private readonly PowerShellRunner _powerShellRunner;
    private readonly WmiAdapter _wmiAdapter;
    private readonly IConfigService _configService;
    private readonly ILogService _logger;

    public ActiveDirectoryService(
        PowerShellRunner powerShellRunner,
        WmiAdapter wmiAdapter,
        IConfigService configService,
        ILogService logger)
    {
        _powerShellRunner = powerShellRunner;
        _wmiAdapter = wmiAdapter;
        _configService = configService;
        _logger = logger;
    }

    public Task<bool> IsDomainJoinedAsync()
    {
        var info = _wmiAdapter.CollectSystemInfo();
        return Task.FromResult(info.IsDomainJoined);
    }

    public Task<string> GetCurrentDomainAsync()
    {
        var info = _wmiAdapter.CollectSystemInfo();
        return Task.FromResult(info.CurrentDomain);
    }

    public async Task<AdValidationResult> ValidateDomainPreRequisitesAsync(string domain, string? domainController)
    {
        _logger.LogInformation("ActiveDirectoryService", $"Validando pré-requisitos para ingresso no domínio: {domain}");

        var result = new AdValidationResult();

        try
        {
            var info = _wmiAdapter.CollectSystemInfo();
            if (info.IsDomainJoined && info.CurrentDomain.Equals(domain, StringComparison.OrdinalIgnoreCase))
            {
                result.Success = true;
                result.AlreadyJoined = true;
                result.Message = $"O computador já está ingressado no domínio institucional '{domain}'.";
                return result;
            }

            // 1. Resolução DNS
            try
            {
                var addresses = await System.Net.Dns.GetHostAddressesAsync(domain);
                result.DnsResolved = addresses.Length > 0;
            }
            catch
            {
                result.DnsResolved = false;
            }

            if (!result.DnsResolved)
            {
                result.Success = false;
                result.Message = $"Falha ao resolver o nome do domínio '{domain}'. Verifique a configuração dos servidores DNS institucionais.";
                return result;
            }

            // 2. Acessibilidade ao DC
            result.DcReachable = true;
            if (!string.IsNullOrWhiteSpace(domainController))
            {
                try
                {
                    using var ping = new System.Net.NetworkInformation.Ping();
                    var reply = await ping.SendPingAsync(domainController, 2500);
                    result.DcReachable = reply.Status == System.Net.NetworkInformation.IPStatus.Success;
                }
                catch
                {
                    result.DcReachable = false;
                }
            }

            result.Success = result.DnsResolved && result.DcReachable;
            result.Message = result.Success
                ? "Servidores de domínio e controlador acessíveis com sucesso."
                : $"Controlador de domínio '{domainController}' não responde aos testes de rede.";
        }
        catch (Exception ex)
        {
            _logger.LogError("ActiveDirectoryService", "Erro durante validação de pré-requisitos do AD", ex);
            result.Success = false;
            result.Message = $"Erro inesperado: {ex.Message}";
        }

        return result;
    }

    public async Task<AdJoinResult> JoinDomainAsync(
        string domain,
        string? domainController,
        string? ouPath,
        string username,
        string password,
        bool dryRun = false)
    {
        _logger.LogInformation("ActiveDirectoryService", $"Executando ingresso no domínio: {domain} (Usuário: {username}) [DryRun: {dryRun}]");

        if (dryRun)
        {
            _logger.LogInformation("ActiveDirectoryService", "[DRY-RUN] Simulação: Computador seria ingressado no domínio com sucesso.");
            return new AdJoinResult
            {
                Success = true,
                NeedsReboot = true,
                Message = $"[SIMULAÇÃO] Ingresso no domínio '{domain}' na OU '{ouPath}' simulado com êxito."
            };
        }

        try
        {
            string scriptPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "scripts", "AD-Join.ps1");
            if (!File.Exists(scriptPath))
            {
                // Fallback para pasta scripts relativa
                scriptPath = Path.GetFullPath("scripts/AD-Join.ps1");
            }

            // Converter senha para SecureString em memória volátil e descartar texto
            var securePassword = new SecureString();
            foreach (char c in password)
            {
                securePassword.AppendChar(c);
            }
            securePassword.MakeReadOnly();

            // Comando PowerShell com PSCredential
            string escapedDomain = domain.Replace("'", "''");
            string escapedUser = username.Replace("'", "''");
            string escapedOu = (ouPath ?? "").Replace("'", "''");

            string psCommand = $@"
                $secPass = ConvertTo-SecureString -String '{password.Replace("'", "''")}' -AsPlainText -Force
                & '{scriptPath.Replace("'", "''")}' -Domain '{escapedDomain}' -Username '{escapedUser}' -SecurePassword $secPass " +
                (!string.IsNullOrWhiteSpace(ouPath) ? $"-OUPath '{escapedOu}' " : "") +
                (!string.IsNullOrWhiteSpace(domainController) ? $"-DomainController '{domainController.Replace("'", "''")}' " : "");

            var execResult = await _powerShellRunner.ExecuteCommandAsync(psCommand);

            // Descartar SecureString da memória
            securePassword.Dispose();

            if (execResult.Success || execResult.StandardOutput.Contains("Joined", StringComparison.OrdinalIgnoreCase) || execResult.StandardOutput.Contains("ingressado com sucesso", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation("ActiveDirectoryService", $"Computador ingressado com sucesso no domínio '{domain}'.");
                return new AdJoinResult
                {
                    Success = true,
                    NeedsReboot = true,
                    Message = $"Computador ingressado com sucesso no domínio '{domain}'. Reinicialização necessária."
                };
            }

            _logger.LogError("ActiveDirectoryService", $"Falha ao ingressar no AD: {execResult.StandardError}");
            return new AdJoinResult
            {
                Success = false,
                NeedsReboot = false,
                Message = "Falha ao ingressar no domínio institucional.",
                ErrorDetails = !string.IsNullOrWhiteSpace(execResult.StandardError) ? execResult.StandardError : execResult.StandardOutput
            };
        }
        catch (Exception ex)
        {
            _logger.LogError("ActiveDirectoryService", "Exceção ao ingressar no Active Directory", ex);
            return new AdJoinResult
            {
                Success = false,
                NeedsReboot = false,
                Message = $"Exceção: {ex.Message}",
                ErrorDetails = ex.ToString()
            };
        }
    }
}
