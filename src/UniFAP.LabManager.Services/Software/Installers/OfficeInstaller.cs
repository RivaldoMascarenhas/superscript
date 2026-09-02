using System.IO;
using Microsoft.Win32;
using UniFAP.LabManager.Core.Enums;
using UniFAP.LabManager.Core.Interfaces;
using UniFAP.LabManager.Core.Models;
using UniFAP.LabManager.Infrastructure.Execution;

namespace UniFAP.LabManager.Services.Software.Installers;

public class OfficeInstaller : ISoftwareInstaller
{
    private readonly ProcessRunner _processRunner;
    private readonly ILogService _logger;

    public OfficeInstaller(
        ProcessRunner processRunner,
        ILogService logger)
    {
        _processRunner = processRunner;
        _logger = logger;
    }

    public bool CanHandle(SoftwareItem software)
    {
        return string.Equals(software.Id, "office365", StringComparison.OrdinalIgnoreCase) ||
               software.Name.Contains("Office 365", StringComparison.OrdinalIgnoreCase) ||
               software.Name.Contains("Microsoft 365", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<SoftwareInstallResult> InstallAsync(
        SoftwareItem software,
        bool dryRun = false,
        Action<string>? progressCallback = null,
        CancellationToken cancellationToken = default)
    {
        if (dryRun)
        {
            _logger.LogInformation("OfficeInstaller", "[SIMULAÇÃO] Instalação do Microsoft 365 / Office 2024 simulada.");
            return new SoftwareInstallResult
            {
                Success = true,
                Status = SoftwareInstallStatus.Installed,
                Message = "[SIMULAÇÃO] Microsoft 365 simulado com sucesso.",
                ExitCode = 0
            };
        }

        // 1. Verificar se o Office já se encontra instalado
        if (await IsInstalledAsync(software))
        {
            _logger.LogInformation("OfficeInstaller", "Microsoft 365 / Office já se encontra instalado e operacional.");
            return new SoftwareInstallResult
            {
                Success = true,
                Status = SoftwareInstallStatus.Installed,
                Message = "Microsoft 365 já está instalado no sistema.",
                ExitCode = 0
            };
        }

        // 2. Localizar diretório e arquivos de instalação institucional
        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
        string officeDir = Path.Combine(baseDir, "software", "Office365");

        if (!Directory.Exists(officeDir))
        {
            // Tentar pasta relativa direta no projeto ou raiz
            officeDir = Path.GetFullPath("software/Office365");
            if (!Directory.Exists(officeDir))
            {
                officeDir = Path.GetFullPath("365");
            }
        }

        string setupExe = Path.Combine(officeDir, "setup.exe");
        string configFile = Path.Combine(officeDir, "configuration.xml");
        string batScript = Path.Combine(officeDir, "365.bat");

        if (!File.Exists(setupExe) && !File.Exists(batScript))
        {
            _logger.LogError("OfficeInstaller", $"Binários do Office 365 não localizados em '{officeDir}'.");
            return new SoftwareInstallResult
            {
                Success = false,
                Status = SoftwareInstallStatus.Failed,
                Message = $"Arquivos de instalação do Office 365 não encontrados no diretório '{officeDir}'.",
                ExitCode = -1
            };
        }

        progressCallback?.Invoke("Iniciando Office Deployment Tool institucional...");
        _logger.LogInformation("OfficeInstaller", $"Executando instalação institucional do Office 365 em '{officeDir}'...");

        ProcessExecutionResult execResult;

        if (File.Exists(setupExe) && File.Exists(configFile))
        {
            string args = $"/configure \"{configFile}\"";
            execResult = await _processRunner.RunAsync(setupExe, args, officeDir, 1800, progressCallback, cancellationToken);
        }
        else if (File.Exists(batScript))
        {
            execResult = await _processRunner.RunAsync("cmd.exe", $"/c \"{batScript}\"", officeDir, 1800, progressCallback, cancellationToken);
        }
        else
        {
            return new SoftwareInstallResult
            {
                Success = false,
                Status = SoftwareInstallStatus.Failed,
                Message = "Estrutura de arquivos do Office 365 incompatível (necessário setup.exe + configuration.xml).",
                ExitCode = -1
            };
        }

        if (execResult.ExitCode == 0 || execResult.ExitCode == 3010)
        {
            _logger.LogInformation("OfficeInstaller", "Microsoft 365 instalado com sucesso pela ferramenta institucional.");
            return new SoftwareInstallResult
            {
                Success = true,
                Status = SoftwareInstallStatus.Installed,
                Message = "Microsoft 365 (Office institucional) instalado com sucesso.",
                ExitCode = execResult.ExitCode
            };
        }

        _logger.LogError("OfficeInstaller", $"Erro na instalação do Office 365 (Exit Code: {execResult.ExitCode}). Erro: {execResult.StandardError}");
        return new SoftwareInstallResult
        {
            Success = false,
            Status = SoftwareInstallStatus.Failed,
            Message = $"Falha na instalação do Microsoft 365 (Código: {execResult.ExitCode}).",
            Details = execResult.StandardError,
            ExitCode = execResult.ExitCode
        };
    }

    public Task<bool> IsInstalledAsync(SoftwareItem software)
    {
        try
        {
            // Checar chave ClickToRun do Office no Registro
            using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
            using var c2rKey = baseKey.OpenSubKey(@"SOFTWARE\Microsoft\Office\ClickToRun\Configuration");
            if (c2rKey != null)
            {
                var productCodes = c2rKey.GetValue("ProductReleaseIds") as string;
                if (!string.IsNullOrWhiteSpace(productCodes)) return Task.FromResult(true);
            }

            // Checar executáveis padrão (Word, Excel)
            string pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            string wordPath = Path.Combine(pf, "Microsoft Office", "root", "Office16", "WINWORD.EXE");
            if (File.Exists(wordPath)) return Task.FromResult(true);

            return Task.FromResult(false);
        }
        catch (Exception ex)
        {
            _logger.LogDebug("OfficeInstaller", $"Erro ao verificar presença do Microsoft 365: {ex.Message}");
            return Task.FromResult(false);
        }
    }

    public async Task<bool> UninstallAsync(SoftwareItem software, bool dryRun = false)
    {
        if (dryRun) return true;

        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
        string officeDir = Path.Combine(baseDir, "software", "Office365");
        string setupExe = Path.Combine(officeDir, "setup.exe");
        string uninstallConfig = Path.Combine(officeDir, "uninstall.xml");

        if (File.Exists(setupExe) && File.Exists(uninstallConfig))
        {
            _logger.LogInformation("OfficeInstaller", "Executando desinstalação silenciosa do Office 365...");
            var res = await _processRunner.RunAsync(setupExe, $"/configure \"{uninstallConfig}\"", officeDir, 1800);
            return res.Success;
        }

        return false;
    }

    public async Task<bool> RepairAsync(SoftwareItem software, bool dryRun = false)
    {
        return (await InstallAsync(software, dryRun)).Success;
    }
}
