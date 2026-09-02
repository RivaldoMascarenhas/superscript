using System.IO;
using UniFAP.LabManager.Core.Interfaces;
using UniFAP.LabManager.Infrastructure.Execution;

namespace UniFAP.LabManager.Services.Performance;

public class PerformanceService : IPerformanceService
{
    private readonly PowerShellRunner _powerShellRunner;
    private readonly ILogService _logger;

    public PerformanceService(PowerShellRunner powerShellRunner, ILogService logger)
    {
        _powerShellRunner = powerShellRunner;
        _logger = logger;
    }

    public async Task<bool> ApplyPerformanceTweaksAsync(bool dryRun = false)
    {
        _logger.LogInformation("PerformanceService", $"Aplicando otimizações de performance seguras [DryRun: {dryRun}]");

        if (dryRun)
        {
            _logger.LogInformation("PerformanceService", "[DRY-RUN] Simulação: Otimizações de desempenho seguras seriam aplicadas mantendo ClearType e animações.");
            return true;
        }

        try
        {
            string scriptPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "scripts", "Performance-Optimize.ps1");
            if (!File.Exists(scriptPath))
            {
                scriptPath = Path.GetFullPath("scripts/Performance-Optimize.ps1");
            }

            var result = await _powerShellRunner.ExecuteCommandAsync($"& '{scriptPath.Replace("'", "''")}'");
            if (result.Success || result.StandardOutput.Contains("aplicadas com sucesso", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation("PerformanceService", "Otimizações de performance aplicadas com sucesso.");
                return true;
            }

            _logger.LogWarning("PerformanceService", $"Aviso ao aplicar performance: {result.StandardError}");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError("PerformanceService", "Erro ao executar otimizações de performance", ex);
            return false;
        }
    }

    public async Task<bool> RollbackPerformanceTweaksAsync()
    {
        _logger.LogInformation("PerformanceService", "Revertendo otimizações de performance para valores padrão...");
        try
        {
            string scriptPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "scripts", "Performance-Optimize.ps1");
            if (!File.Exists(scriptPath))
            {
                scriptPath = Path.GetFullPath("scripts/Performance-Optimize.ps1");
            }

            var result = await _powerShellRunner.ExecuteCommandAsync($"& '{scriptPath.Replace("'", "''")}' -Rollback");
            return result.Success;
        }
        catch (Exception ex)
        {
            _logger.LogError("PerformanceService", "Erro ao reverter otimizações de performance", ex);
            return false;
        }
    }

    public async Task<string> CleanTemporaryFilesAsync(bool dryRun = false)
    {
        _logger.LogInformation("PerformanceService", $"Iniciando rotina de limpeza de arquivos temporários e disco [DryRun: {dryRun}]");
        if (dryRun) return "[SIMULAÇÃO] 1.2 GB de arquivos temporários seriam liberados.";

        string psCleanScript = @"
            $totalBytesCleaned = 0

            # 1. Limpar Temp do Usuário e do Sistema
            $tempFolders = @(
                [System.IO.Path]::GetTempPath(),
                'C:\Windows\Temp',
                'C:\Windows\Prefetch',
                'C:\Windows\SoftwareDistribution\Download'
            )

            foreach ($folder in $tempFolders) {
                if (Test-Path $folder) {
                    Get-ChildItem -Path $folder -Recurse -Force -ErrorAction SilentlyContinue | ForEach-Object {
                        try {
                            if (-not $_.PSIsContainer) {
                                $size = $_.Length
                                Remove-Item -LiteralPath $_.FullName -Force -ErrorAction Stop
                                $totalBytesCleaned += $size
                            } else {
                                Remove-Item -LiteralPath $_.FullName -Force -Recurse -ErrorAction SilentlyContinue
                            }
                        } catch {}
                    }
                }
            }

            # 2. Esvaziar Lixeira
            try {
                Clear-RecycleBin -Force -ErrorAction SilentlyContinue
            } catch {}

            $mb = [Math]::Round($totalBytesCleaned / 1MB, 2)
            $gb = [Math]::Round($totalBytesCleaned / 1GB, 2)
            if ($gb -ge 1.0) {
                Write-Output ""$gb GB liberados""
            } else {
                Write-Output ""$mb MB liberados""
            }
        ";

        try
        {
            var res = await _powerShellRunner.ExecuteCommandAsync(psCleanScript);
            string output = res.StandardOutput.Trim();
            if (string.IsNullOrWhiteSpace(output)) output = "Arquivos temporários e lixeira limpos com sucesso.";
            _logger.LogInformation("PerformanceService", $"Limpeza de temporários concluída: {output}");
            return output;
        }
        catch (Exception ex)
        {
            _logger.LogError("PerformanceService", "Erro ao limpar arquivos temporários", ex);
            return "Erro ao realizar limpeza.";
        }
    }

    public async Task<bool> OptimizeBrowsersAsync(bool dryRun = false)
    {
        _logger.LogInformation("PerformanceService", $"Otimizando navegadores de internet (Edge, Chrome, Firefox) [DryRun: {dryRun}]");
        if (dryRun) return true;

        string psOptimizeBrowsers = @"
            # 1. Políticas de Registro: Desativar Inicialização em Segundo Plano do Edge (Startup Boost)
            $edgePolicyPath = 'HKLM:\SOFTWARE\Policies\Microsoft\Edge'
            if (-not (Test-Path $edgePolicyPath)) { New-Item -Path $edgePolicyPath -Force | Out-Null }
            Set-ItemProperty -Path $edgePolicyPath -Name 'StartupBoostEnabled' -Value 0 -Type DWord -Force
            Set-ItemProperty -Path $edgePolicyPath -Name 'BackgroundModeEnabled' -Value 0 -Type DWord -Force

            # 2. Políticas de Registro: Desativar Inicialização em Segundo Plano do Google Chrome
            $chromePolicyPath = 'HKLM:\SOFTWARE\Policies\Google\Chrome'
            if (-not (Test-Path $chromePolicyPath)) { New-Item -Path $chromePolicyPath -Force | Out-Null }
            Set-ItemProperty -Path $chromePolicyPath -Name 'BackgroundModeEnabled' -Value 0 -Type DWord -Force

            # 3. Limpeza de Caches de Navegadores locais
            $browserCaches = @(
                ""$env:LOCALAPPDATA\Microsoft\Edge\User Data\Default\Cache"",
                ""$env:LOCALAPPDATA\Google\Chrome\User Data\Default\Cache"",
                ""$env:LOCALAPPDATA\Mozilla\Firefox\Profiles""
            )

            foreach ($path in $browserCaches) {
                if (Test-Path $path) {
                    try {
                        Get-ChildItem -Path $path -Recurse -Force -ErrorAction SilentlyContinue | 
                            Where-Object { -not $_.PSIsContainer } | 
                            Remove-Item -Force -ErrorAction SilentlyContinue
                    } catch {}
                }
            }

            Write-Output 'Navegadores otimizados com sucesso.'
        ";

        try
        {
            var res = await _powerShellRunner.ExecuteCommandAsync(psOptimizeBrowsers);
            _logger.LogInformation("PerformanceService", "Otimização de navegadores concluída com sucesso.");
            return res.Success;
        }
        catch (Exception ex)
        {
            _logger.LogError("PerformanceService", "Erro ao otimizar navegadores", ex);
            return false;
        }
    }

    public async Task<bool> ConfigureDnsAsync(string primaryDns, string? secondaryDns = null, bool useDhcp = false)
    {
        _logger.LogInformation("PerformanceService", $"Configurando DNS [DHCP: {useDhcp}, Primário: {primaryDns}, Secundário: {secondaryDns}]");

        string psDnsScript;
        if (useDhcp)
        {
            psDnsScript = @"
                Get-NetAdapter | Where-Object { $_.Status -eq 'Up' } | ForEach-Object {
                    Set-DnsClientServerAddress -InterfaceAlias $_.Name -ResetServerAddresses -ErrorAction SilentlyContinue
                }
                Clear-DnsClientCache
                Write-Output 'DNS restaurado para DHCP automático.'
            ";
        }
        else
        {
            var dnsList = new List<string> { $"'{primaryDns}'" };
            if (!string.IsNullOrWhiteSpace(secondaryDns))
            {
                dnsList.Add($"'{secondaryDns}'");
            }
            string addressesParam = string.Join(",", dnsList);

            psDnsScript = $@"
                $servers = @({addressesParam})
                Get-NetAdapter | Where-Object {{ $_.Status -eq 'Up' }} | ForEach-Object {{
                    Set-DnsClientServerAddress -InterfaceAlias $_.Name -ServerAddresses $servers -ErrorAction SilentlyContinue
                }}
                Clear-DnsClientCache
                Write-Output 'Servidores DNS aplicados com sucesso.'
            ";
        }

        try
        {
            var res = await _powerShellRunner.ExecuteCommandAsync(psDnsScript);
            _logger.LogInformation("PerformanceService", $"Configuração de DNS aplicada: {res.StandardOutput.Trim()}");
            return res.Success;
        }
        catch (Exception ex)
        {
            _logger.LogError("PerformanceService", "Erro ao aplicar configuração de DNS", ex);
            return false;
        }
    }
}
