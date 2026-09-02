using System.IO;
using System.Security.Cryptography;
using UniFAP.LabManager.Core.Enums;
using UniFAP.LabManager.Core.Interfaces;
using UniFAP.LabManager.Core.Models;

namespace UniFAP.LabManager.Infrastructure.Execution;

public class LocalInstallerService : ILocalInstallerService
{
    private readonly ProcessRunner _processRunner;
    private readonly ISecurityService _securityService;
    private readonly ILogService _logger;

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".exe", ".msi", ".bat", ".cmd", ".ps1"
    };

    public LocalInstallerService(
        ProcessRunner processRunner,
        ISecurityService securityService,
        ILogService logger)
    {
        _processRunner = processRunner;
        _securityService = securityService;
        _logger = logger;
    }

    public bool ValidateInstallerSecurity(SoftwareItem software, out string? errorMessage)
    {
        errorMessage = null;
        string? targetPath = ResolveInstallerPath(software);

        if (string.IsNullOrWhiteSpace(targetPath))
        {
            errorMessage = $"Nenhum caminho de instalador ou diretório configurado para '{software.Name}'.";
            return false;
        }

        // 1. Bloqueio de Path Traversal e diretórios não autorizados
        string appBase = Path.GetFullPath(AppDomain.CurrentDomain.BaseDirectory);
        string currentBase = Path.GetFullPath(Environment.CurrentDirectory);
        string programDataBase = Path.GetFullPath(@"C:\ProgramData\UniFAP\LabManager");

        bool isInsideAllowedBase = _securityService.ValidatePathSafety(targetPath, appBase) ||
                                   _securityService.ValidatePathSafety(targetPath, currentBase) ||
                                   _securityService.ValidatePathSafety(targetPath, programDataBase);

        if (!isInsideAllowedBase)
        {
            errorMessage = $"Bloqueio de segurança: Caminho não autorizado ou tentativa de Path Traversal para '{targetPath}'.";
            return false;
        }

        // 2. Extensão permitida
        string ext = Path.GetExtension(targetPath);
        if (!AllowedExtensions.Contains(ext))
        {
            errorMessage = $"Extensão de instalador '{ext}' não autorizada pela política de segurança UniFAP.";
            return false;
        }

        // 3. Existência do arquivo
        if (!File.Exists(targetPath))
        {
            errorMessage = $"Instalador não encontrado no caminho: '{targetPath}'. " +
                           $"Certifique-se de posicionar os binários oficiais no diretório correspondente da UniFAP.";
            return false;
        }

        // 4. Validação de Hash SHA256 opcional
        if (!string.IsNullOrWhiteSpace(software.Hash))
        {
            try
            {
                using var stream = File.OpenRead(targetPath);
                using var sha = SHA256.Create();
                byte[] hashBytes = sha.ComputeHash(stream);
                string computedHash = Convert.ToHexString(hashBytes);

                if (!string.Equals(computedHash, software.Hash, StringComparison.OrdinalIgnoreCase))
                {
                    errorMessage = $"Falha de integridade: Hash SHA-256 do instalador '{software.Name}' diverge do valor homologado.";
                    return false;
                }
            }
            catch (Exception ex)
            {
                errorMessage = $"Erro ao calcular hash do instalador: {ex.Message}";
                return false;
            }
        }

        return true;
    }

    public async Task<SoftwareInstallResult> RunInstallerAsync(
        SoftwareItem software,
        bool dryRun = false,
        Action<string>? progressCallback = null,
        CancellationToken cancellationToken = default)
    {
        if (dryRun)
        {
            _logger.LogInformation("LocalInstallerService", $"[SIMULAÇÃO] Instalador local para '{software.Name}' simulado com sucesso.");
            return new SoftwareInstallResult
            {
                Success = true,
                Status = SoftwareInstallStatus.Installed,
                Message = $"[SIMULAÇÃO] Instalação de {software.Name} simulada com sucesso.",
                ExitCode = 0
            };
        }

        if (!ValidateInstallerSecurity(software, out string? validationError))
        {
            _logger.LogWarning("LocalInstallerService", $"Validação de instalador rejeitada para '{software.Name}': {validationError}");

            // Software legado ou opcional não quebra todo o processo
            if (software.Legacy || software.Severity == SoftwareSeverity.Optional)
            {
                return new SoftwareInstallResult
                {
                    Success = true,
                    Status = SoftwareInstallStatus.Warning,
                    Message = $"Software legado/opcional ausente ou não validado: {validationError}",
                    Details = validationError,
                    ExitCode = -1
                };
            }

            return new SoftwareInstallResult
            {
                Success = false,
                Status = SoftwareInstallStatus.Failed,
                Message = validationError ?? "Instalador local não validado.",
                ExitCode = -1
            };
        }

        string fullPath = ResolveInstallerPath(software)!;
        string workingDir = Path.GetDirectoryName(fullPath) ?? AppDomain.CurrentDomain.BaseDirectory;
        string args = software.SilentArgs ?? software.Arguments ?? "";
        string ext = Path.GetExtension(fullPath).ToLowerInvariant();

        progressCallback?.Invoke($"Executando instalador local: {Path.GetFileName(fullPath)}...");
        _logger.LogInformation("LocalInstallerService", $"Iniciando '{fullPath}' com argumentos: '{args}' (Dir: '{workingDir}')");

        ProcessExecutionResult execResult;

        if (ext == ".msi")
        {
            // Executar msiexec
            string msiArgs = $"/i \"{fullPath}\" {args} /qn /norestart";
            execResult = await _processRunner.RunAsync("msiexec.exe", msiArgs, workingDir, 1800, progressCallback, cancellationToken);
        }
        else if (ext == ".ps1")
        {
            string psArgs = $"-NoProfile -ExecutionPolicy Bypass -File \"{fullPath}\" {args}";
            execResult = await _processRunner.RunAsync("powershell.exe", psArgs, workingDir, 1800, progressCallback, cancellationToken);
        }
        else
        {
            // .exe, .bat, .cmd
            execResult = await _processRunner.RunAsync(fullPath, args, workingDir, 1800, progressCallback, cancellationToken);
        }

        // Códigos 0 e 3010 (Reboot required) são considerados êxito no Windows Installer
        bool isSuccess = execResult.ExitCode == 0 || execResult.ExitCode == 3010;

        if (isSuccess)
        {
            _logger.LogInformation("LocalInstallerService", $"Instalação de '{software.Name}' finalizada com código {execResult.ExitCode}.");
            return new SoftwareInstallResult
            {
                Success = true,
                Status = SoftwareInstallStatus.Installed,
                Message = execResult.ExitCode == 3010 
                    ? $"{software.Name} instalado com sucesso (reinicialização do sistema necessária)."
                    : $"{software.Name} instalado com sucesso.",
                ExitCode = execResult.ExitCode
            };
        }

        _logger.LogWarning("LocalInstallerService", $"Instalador de '{software.Name}' retornou código de erro: {execResult.ExitCode}. Erro: {execResult.StandardError}");

        if (software.Legacy || software.Severity == SoftwareSeverity.Optional)
        {
            return new SoftwareInstallResult
            {
                Success = true,
                Status = SoftwareInstallStatus.Warning,
                Message = $"Software legado/opcional finalizado com aviso (código {execResult.ExitCode}).",
                Details = execResult.StandardError,
                ExitCode = execResult.ExitCode
            };
        }

        return new SoftwareInstallResult
        {
            Success = false,
            Status = SoftwareInstallStatus.Failed,
            Message = $"Falha ao executar instalador de {software.Name} (Exit Code: {execResult.ExitCode}).",
            Details = execResult.StandardError,
            ExitCode = execResult.ExitCode
        };
    }

    private static string? ResolveInstallerPath(SoftwareItem software)
    {
        string? candidate = null;

        if (!string.IsNullOrWhiteSpace(software.Installer))
        {
            candidate = software.Installer;
            // Se for diretório com entryPoint
            if (Directory.Exists(candidate) && !string.IsNullOrWhiteSpace(software.EntryPoint))
            {
                candidate = Path.Combine(candidate, software.EntryPoint);
            }
        }
        else if (!string.IsNullOrWhiteSpace(software.ScriptPath))
        {
            candidate = software.ScriptPath;
        }
        else if (!string.IsNullOrWhiteSpace(software.InstallerDir) && !string.IsNullOrWhiteSpace(software.EntryPoint))
        {
            candidate = Path.Combine(software.InstallerDir, software.EntryPoint);
        }

        if (string.IsNullOrWhiteSpace(candidate)) return null;

        if (Path.IsPathRooted(candidate))
        {
            return Path.GetFullPath(candidate);
        }

        return Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, candidate));
    }
}
