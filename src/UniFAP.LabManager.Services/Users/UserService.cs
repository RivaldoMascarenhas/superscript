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

            string psCommand = $"& '{scriptPath.Replace("'", "''")}' -SupportUserName '{supportName}' -StudentUserName '{studentName}'";
            if (!string.IsNullOrWhiteSpace(supportPassword))
            {
                psCommand += $" -SupportPassword (ConvertTo-SecureString '{supportPassword.Replace("'", "''")}' -AsPlainText -Force)";
            }
            if (!string.IsNullOrWhiteSpace(studentPassword))
            {
                psCommand += $" -StudentPassword (ConvertTo-SecureString '{studentPassword.Replace("'", "''")}' -AsPlainText -Force)";
            }

            var result = await _powerShellRunner.ExecuteCommandAsync(psCommand);
            if (result.Success || result.StandardOutput.Contains("provisionados com sucesso", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation("UserService", "Usuários locais 'suporte' e 'aluno' configurados com sucesso.");
                return true;
            }

            _logger.LogWarning("UserService", $"Retorno do script de usuários: {result.StandardError}");
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
        var result = await _powerShellRunner.ExecuteCommandAsync($"Get-LocalUser -Name '{username}' -ErrorAction SilentlyContinue");
        return result.Success && !string.IsNullOrWhiteSpace(result.StandardOutput);
    }

    public async Task<bool> IsInAdminGroupAsync(string username)
    {
        var result = await _powerShellRunner.ExecuteCommandAsync(
            $"(Get-LocalGroupMember -Group 'Administradores' -ErrorAction SilentlyContinue).Name -contains '{username}' -or (Get-LocalGroupMember -Group 'Administrators' -ErrorAction SilentlyContinue).Name -contains '{username}'");
        return result.Success && result.StandardOutput.Trim().Equals("True", StringComparison.OrdinalIgnoreCase);
    }
}
