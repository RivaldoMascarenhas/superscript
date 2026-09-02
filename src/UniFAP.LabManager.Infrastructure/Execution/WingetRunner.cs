using System.IO;
using UniFAP.LabManager.Core.Enums;
using UniFAP.LabManager.Core.Interfaces;

namespace UniFAP.LabManager.Infrastructure.Execution;

public class WingetRunner : IWingetService
{
    private readonly ProcessRunner _processRunner;
    private readonly ILogService _logger;

    public WingetRunner(ProcessRunner processRunner, ILogService logger)
    {
        _processRunner = processRunner;
        _logger = logger;
    }

    public async Task<bool> IsAvailableAsync()
    {
        try
        {
            var result = await _processRunner.RunAsync("winget.exe", "--version", null, 10);
            return result.Success && !string.IsNullOrWhiteSpace(result.StandardOutput);
        }
        catch
        {
            return false;
        }
    }

    public async Task<SoftwareInstallResult> InstallPackageAsync(
        string packageId,
        bool silent = true,
        Action<string>? progressCallback = null,
        CancellationToken cancellationToken = default)
    {
        string arguments = $"install --id {packageId} --exact --accept-package-agreements --accept-source-agreements --disable-interactivity --ignore-security-hash --force";
        if (silent)
        {
            arguments += " --silent";
        }

        _logger.LogInformation("WingetRunner", $"Iniciando instalação do pacote Winget: '{packageId}'");
        progressCallback?.Invoke($"Instalando via Winget: {packageId}...");

        var result = await _processRunner.RunAsync("winget.exe", arguments, null, 1200, progressCallback, cancellationToken);

        // Exit codes comuns do Winget:
        // 0: Sucesso
        // 3010 / -1978335189 (0x8A15002B): Sucesso, mas reinicialização necessária
        // -1978335212 (0x8A150014): Pacote já instalado
        // -1978335216: Nenhuma versão aplicável encontrada

        bool isExplicitSuccess = result.ExitCode == 0 || result.ExitCode == 3010 || result.ExitCode == unchecked((int)0x8A15002B) ||
                                 result.StandardOutput.Contains("Successfully installed", StringComparison.OrdinalIgnoreCase) ||
                                 result.StandardOutput.Contains("Instalado com êxito", StringComparison.OrdinalIgnoreCase);

        if (isExplicitSuccess)
        {
            _logger.LogInformation("WingetRunner", $"Pacote '{packageId}' instalado com sucesso (ExitCode: {result.ExitCode})");
            return new SoftwareInstallResult
            {
                Success = true,
                Status = SoftwareInstallStatus.Installed,
                ExitCode = result.ExitCode,
                Message = result.ExitCode == 3010 ? "Instalado com sucesso (Requer reinicialização)." : "Instalado com sucesso.",
                Details = result.StandardOutput
            };
        }

        if (result.ExitCode == unchecked((int)0x8A150014) || result.StandardOutput.Contains("já está instalado", StringComparison.OrdinalIgnoreCase) || result.StandardOutput.Contains("already installed", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation("WingetRunner", $"Pacote '{packageId}' já se encontra instalado no sistema.");
            return new SoftwareInstallResult
            {
                Success = true,
                Status = SoftwareInstallStatus.Installed,
                ExitCode = result.ExitCode,
                Message = "O software já estava instalado no sistema.",
                Details = result.StandardOutput
            };
        }

        // CONTINGÊNCIA CORPORATIVA AUTOMÁTICA PARA GOOGLE CHROME
        if (packageId.Equals("Google.Chrome", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation("WingetRunner", "Acionando instalador corporativo direto (MSI) para o Google Chrome...");
            progressCallback?.Invoke("Baixando Google Chrome Enterprise oficial direto da Google...");
            string tempMsi = Path.Combine(Path.GetTempPath(), "googlechrome_enterprise.msi");
            try
            {
                using var httpClient = new System.Net.Http.HttpClient();
                httpClient.Timeout = TimeSpan.FromMinutes(5);
                var bytes = await httpClient.GetByteArrayAsync("https://dl.google.com/chrome/install/googlechromestandaloneenterprise64.msi", cancellationToken);
                await File.WriteAllBytesAsync(tempMsi, bytes, cancellationToken);

                progressCallback?.Invoke("Instalando Google Chrome Enterprise silenciosamente...");
                var msiResult = await _processRunner.RunAsync("msiexec.exe", $"/i \"{tempMsi}\" /qn /norestart", null, 600, progressCallback, cancellationToken);
                try { File.Delete(tempMsi); } catch { }

                if (msiResult.ExitCode == 0 || msiResult.ExitCode == 3010)
                {
                    _logger.LogInformation("WingetRunner", "Google Chrome instalado com sucesso via contingência Enterprise.");
                    return new SoftwareInstallResult
                    {
                        Success = true,
                        Status = SoftwareInstallStatus.Installed,
                        ExitCode = msiResult.ExitCode,
                        Message = "Google Chrome instalado com sucesso via instalador corporativo oficial.",
                        Details = msiResult.StandardOutput
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning("WingetRunner", $"Contingência direta do Chrome reportou: {ex.Message}");
            }
        }

        _logger.LogWarning("WingetRunner", $"Falha na instalação de '{packageId}' (ExitCode: {result.ExitCode})");
        return new SoftwareInstallResult
        {
            Success = false,
            Status = SoftwareInstallStatus.Failed,
            ExitCode = result.ExitCode,
            Message = $"Falha ao instalar o pacote '{packageId}'. Código: {result.ExitCode}",
            Details = !string.IsNullOrWhiteSpace(result.StandardError) ? result.StandardError : result.StandardOutput
        };
    }

    public async Task<bool> IsPackageInstalledAsync(string packageId)
    {
        try
        {
            var result = await _processRunner.RunAsync("winget.exe", $"list --id {packageId} --exact", null, 30);
            return result.Success && result.StandardOutput.Contains(packageId, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    public async Task<HashSet<string>> GetInstalledPackageIdsAsync()
    {
        var installed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var result = await _processRunner.RunAsync("winget.exe", "list --accept-source-agreements", null, 60);
            if (result.Success && !string.IsNullOrWhiteSpace(result.StandardOutput))
            {
                var lines = result.StandardOutput.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                bool headerPassed = false;
                foreach (var line in lines)
                {
                    if (line.StartsWith("---") || line.Contains("------"))
                    {
                        headerPassed = true;
                        continue;
                    }
                    if (!headerPassed) continue;

                    var parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var part in parts)
                    {
                        if (part.Contains('.') || part.StartsWith("ARP\\") || part.StartsWith("MSIX\\"))
                        {
                            installed.Add(part.Trim());
                        }
                    }
                    installed.Add(line);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning("WingetRunner", $"Erro ao listar pacotes instalados: {ex.Message}");
        }
        return installed;
    }

    public async Task<List<string>> SearchPackagesAsync(string query)
    {
        var packages = new List<string>();
        try
        {
            var result = await _processRunner.RunAsync("winget.exe", $"search \"{query}\" --source winget", null, 30);
            if (result.Success && !string.IsNullOrWhiteSpace(result.StandardOutput))
            {
                var lines = result.StandardOutput.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var line in lines.Skip(2))
                {
                    var parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 2)
                    {
                        packages.Add(parts[1]);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning("WingetRunner", $"Erro ao buscar pacote Winget '{query}': {ex.Message}");
        }
        return packages;
    }
}
