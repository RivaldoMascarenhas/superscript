using System.IO;
using UniFAP.LabManager.Core.Interfaces;
using UniFAP.LabManager.Infrastructure.Execution;

namespace UniFAP.LabManager.Services.Users;

public class UserService : IUserService
{
    private readonly PowerShellRunner _powerShellRunner;
    private readonly IConfigService _configService;
    private readonly ILogService _logger;

    public UserService(
        PowerShellRunner powerShellRunner,
        IConfigService configService,
        ILogService logger)
    {
        _powerShellRunner = powerShellRunner;
        _configService = configService;
        _logger = logger;
    }

    public async Task<bool> ProvisionUsersAsync(string? supportPassword = null, string? studentPassword = null, bool dryRun = false)
    {
        _logger.LogInformation("UserService", $"Iniciando provisionamento de contas de usuário [DryRun: {dryRun}]");

        if (dryRun)
        {
            _logger.LogInformation("UserService", "[DRY-RUN] Simulação: Usuários 'suporte' (Admin) e 'aluno' (Padrão) seriam provisionados.");
            return true;
        }

        try
        {
            string scriptPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "scripts", "User-Provision.ps1");
            if (!File.Exists(scriptPath))
            {
                scriptPath = Path.GetFullPath("scripts/User-Provision.ps1");
            }

            var usersConfig = _configService.Users.Users;
            string supportName = usersConfig.TryGetValue("support", out var sup) ? sup.Name : "suporte";
            string studentName = usersConfig.TryGetValue("student", out var stu) ? stu.Name : "aluno";

            string psCommand = $"& '{scriptPath.Replace("'", "''")}' -SupportUserName '{supportName.Replace("'", "''")}' -StudentUserName '{studentName.Replace("'", "''")}'";
            if (!string.IsNullOrWhiteSpace(supportPassword))
            {
                psCommand += $" -SupportPassword (ConvertTo-SecureString '{supportPassword.Replace("'", "''")}' -AsPlainText -Force)";
            }
            if (!string.IsNullOrWhiteSpace(studentPassword))
            {
                psCommand += $" -StudentPassword (ConvertTo-SecureString '{studentPassword.Replace("'", "''")}' -AsPlainText -Force)";
            }

            var result = await _powerShellRunner.ExecuteCommandAsync(psCommand, sensitive: true);
            bool isSuccess = result.Success && (
                result.StandardOutput.Contains("\"Success\":true", StringComparison.OrdinalIgnoreCase) ||
                result.StandardOutput.Contains("\"Success\": true", StringComparison.OrdinalIgnoreCase) ||
                result.StandardOutput.Contains("provisionados com sucesso", StringComparison.OrdinalIgnoreCase));

            if (isSuccess)
            {
                _logger.LogInformation("UserService", "Usuários locais 'suporte' e 'aluno' configurados com sucesso.");
                return true;
            }

            string errorDetail = !string.IsNullOrWhiteSpace(result.StandardError) ? result.StandardError : result.StandardOutput;
            _logger.LogError("UserService", $"Falha ao provisionar usuários locais: {errorDetail}");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError("UserService", "Erro ao provisionar usuários locais", ex);
            return false;
        }
    }

    public async Task<bool> IsUserConfiguredAsync(string username)
    {
        var result = await _powerShellRunner.ExecuteCommandAsync($"(Get-LocalUser -Name '{username.Replace("'", "''")}' -ErrorAction Stop).Enabled");
        return result.Success && result.StandardOutput.Trim().Equals("True", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<bool> IsInAdminGroupAsync(string username)
    {
        var result = await _powerShellRunner.ExecuteCommandAsync(
            $"$u = Get-LocalUser -Name '{username.Replace("'", "''")}' -ErrorAction Stop; @(Get-LocalGroupMember -SID 'S-1-5-32-544' -ErrorAction Stop).SID.Value -contains $u.SID.Value");
        return result.Success && result.StandardOutput.Trim().Equals("True", StringComparison.OrdinalIgnoreCase);
    }
}
