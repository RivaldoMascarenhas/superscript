<#
.SYNOPSIS
    Support-Tools.ps1 - Utilitarios e Scripts de Manutencao para o Suporte de TI UniFAP.
.DESCRIPTION
    Conjunto modular de rotinas operacionais para solucao rapida de problemas de rede,
    spooler de impressao, Windows Update, GPO, credenciais, hibernacao, disco e ativacao.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet(
        "ResetNetworkStack",
        "ClearWindowsProxy",
        "TestNetworkConnectivity",
        "RepairPrintSpooler",
        "ResetWindowsUpdate",
        "RestartShellAndAudio",
        "SyncGroupPolicy",
        "ClearCredentialVault",
        "DisableHibernation",
        "OptimizeStorageDrive",
        "GenerateBatteryReport",
        "CheckWindowsActivation",
        "UpdateDefenderAndScan"
    )]
    [string]$Action,

    [Parameter(Mandatory = $false)]
    [string]$Target = "C",

    [Parameter(Mandatory = $false)]
    [switch]$WhatIf
)

$ErrorActionPreference = "Continue"

function Write-JsonResult {
    param(
        [bool]$Success,
        [string]$Message,
        [hashtable]$Details = @{}
    )
    $output = [PSCustomObject]@{
        Success   = $Success
        Message   = $Message
        Details   = $Details
        Timestamp = (Get-Date).ToString("o")
    }
    $output | ConvertTo-Json -Depth 10 -Compress
}

try {
    switch ($Action) {
        "ResetNetworkStack" {
            if ($WhatIf) {
                Write-JsonResult -Success $true -Message "[SIMULACAO] A pilha de rede (Winsock, TCP/IP, DNS, ARP e DHCP) seria redefinida."
                return
            }

            Write-Host "[1/6] Liberando e renovando concessao DHCP..."
            & ipconfig.exe /release | Out-Null
            & ipconfig.exe /renew | Out-Null

            Write-Host "[2/6] Limpando cache do resolvedor DNS..."
            & ipconfig.exe /flushdns | Out-Null
            Clear-DnsClientCache -ErrorAction SilentlyContinue

            Write-Host "[3/6] Limpando tabela ARP de enderecamento fisico..."
            & arp.exe -d * 2>&1 | Out-Null

            Write-Host "[4/6] Redefinindo catalogo Winsock..."
            & netsh.exe winsock reset 2>&1 | Out-Null

            Write-Host "[5/6] Redefinindo pilha TCP/IP do Windows..."
            & netsh.exe int ip reset 2>&1 | Out-Null

            Write-Host "[6/6] Reiniciando adaptadores de rede fisicos ativos..."
            Get-NetAdapter -Physical -ErrorAction SilentlyContinue | Where-Object { $_.Status -eq 'Up' } | ForEach-Object {
                try {
                    Restart-NetAdapter -Name $_.Name -Confirm:$false -ErrorAction SilentlyContinue
                } catch {}
            }

            Write-JsonResult -Success $true -Message "Pilha de rede, catalogo Winsock, cache DNS e tabela ARP redefinidos com sucesso!"
        }

        "ClearWindowsProxy" {
            if ($WhatIf) {
                Write-JsonResult -Success $true -Message "[SIMULACAO] As configuracoes de proxy WinHTTP e do navegador seriam desativadas."
                return
            }

            Write-Host "Redefinindo proxy WinHTTP do sistema..."
            & netsh.exe winhttp reset proxy 2>&1 | Out-Null

            Write-Host "Desativando proxy manual e autodeteccao no Registro..."
            $regInternet = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Internet Settings"
            if (Test-Path $regInternet) {
                Set-ItemProperty -Path $regInternet -Name "ProxyEnable" -Value 0 -Type DWord -Force
                Remove-ItemProperty -Path $regInternet -Name "ProxyServer" -Force -ErrorAction SilentlyContinue
                Remove-ItemProperty -Path $regInternet -Name "AutoConfigURL" -Force -ErrorAction SilentlyContinue
            }

            Write-JsonResult -Success $true -Message "Proxy do sistema e do usuario desativado e redefinido para conexao direta."
        }

        "TestNetworkConnectivity" {
            if ($WhatIf) {
                Write-JsonResult -Success $true -Message "[SIMULACAO] Teste de conectividade sequencial seria realizado."
                return
            }

            $tests = [System.Collections.Generic.List[string]]::new()

            # 1. Gateway Padrao
            $gw = (Get-NetRoute -DestinationPrefix '0.0.0.0/0' -ErrorAction SilentlyContinue | Select-Object -ExpandProperty NextHop -First 1)
            if ($gw) {
                $pGw = Test-Connection -ComputerName $gw -Count 1 -Quiet -ErrorAction SilentlyContinue
                if ($pGw) {
                    $tests.Add("Gateway Local ($gw): [OK] Online")
                } else {
                    $tests.Add("Gateway Local ($gw): [FALHA] Inacessivel")
                }
            } else {
                $tests.Add("Gateway Local: [AVISO] Nao configurado")
            }

            # 2. DNS Publico Cloudflare (1.1.1.1)
            $pCloudflare = Test-Connection -ComputerName "1.1.1.1" -Count 1 -Quiet -ErrorAction SilentlyContinue
            if ($pCloudflare) {
                $tests.Add("Internet Publica (1.1.1.1): [OK] Conectada")
            } else {
                $tests.Add("Internet Publica (1.1.1.1): [FALHA] Sem Acesso")
            }

            # 3. DNS Publico Google (8.8.8.8)
            $pGoogle = Test-Connection -ComputerName "8.8.8.8" -Count 1 -Quiet -ErrorAction SilentlyContinue
            if ($pGoogle) {
                $tests.Add("DNS Google (8.8.8.8): [OK] Conectado")
            } else {
                $tests.Add("DNS Google (8.8.8.8): [FALHA] Sem Acesso")
            }

            # 4. Resolucao de Nomes (unifap.edu.br)
            try {
                $dnsRes = [System.Net.Dns]::GetHostAddresses("unifap.edu.br")
                $tests.Add("Resolucao DNS (unifap.edu.br): [OK] " + $dnsRes[0].ToString())
            } catch {
                $tests.Add("Resolucao DNS (unifap.edu.br): [FALHA] Nao resolvido")
            }

            # 5. Conectividade Web HTTP/HTTPS
            try {
                $req = Invoke-WebRequest -Uri "https://www.unifap.edu.br" -UseBasicParsing -TimeoutSec 5 -ErrorAction Stop
                $tests.Add("Portal Web UniFAP: [OK] Codigo " + $req.StatusCode)
            } catch {
                $tests.Add("Portal Web UniFAP: [AVISO] " + $_.Exception.Message)
            }

            $summary = $tests -join " | "
            Write-JsonResult -Success $true -Message $summary -Details @{ Tests = $tests }
        }

        "RepairPrintSpooler" {
            if ($WhatIf) {
                Write-JsonResult -Success $true -Message "[SIMULACAO] O spooler de impressao seria parado, fila limpa e servico reiniciado."
                return
            }

            Write-Host "Parando servico do Spooler de Impressao..."
            Stop-Service -Name "Spooler" -Force -ErrorAction SilentlyContinue

            $spoolDir = "$env:SystemRoot\System32\spool\PRINTERS"
            $filesRemoved = 0
            if (Test-Path $spoolDir) {
                $files = Get-ChildItem -Path $spoolDir -Force -ErrorAction SilentlyContinue
                foreach ($f in $files) {
                    try {
                        Remove-Item -LiteralPath $f.FullName -Force -ErrorAction Stop
                        $filesRemoved++
                    } catch {}
                }
            }

            Write-Host "Reiniciando servico do Spooler..."
            Start-Service -Name "Spooler" -ErrorAction SilentlyContinue
            $status = (Get-Service -Name "Spooler").Status

            Write-JsonResult -Success ($status -eq "Running") -Message "Spooler de impressao reiniciado ($status). $filesRemoved arquivo(s) de fila pendente removidos."
        }

        "ResetWindowsUpdate" {
            if ($WhatIf) {
                Write-JsonResult -Success $true -Message "[SIMULACAO] Os servicos de update seriam parados e pastas de cache limpas."
                return
            }

            $services = @("wuauserv", "cryptSvc", "bits", "msiserver")
            Write-Host "Interrompendo servicos do Windows Update..."
            foreach ($s in $services) {
                Stop-Service -Name $s -Force -ErrorAction SilentlyContinue
            }

            Write-Host "Limpando caches SoftwareDistribution e Catroot2..."
            $softDist = "$env:SystemRoot\SoftwareDistribution"
            if (Test-Path $softDist) {
                try {
                    Get-ChildItem -Path $softDist -Recurse -Force -ErrorAction SilentlyContinue | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
                } catch {}
            }

            $catroot2 = "$env:SystemRoot\System32\catroot2"
            if (Test-Path $catroot2) {
                try {
                    Get-ChildItem -Path $catroot2 -Recurse -Force -ErrorAction SilentlyContinue | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
                } catch {}
            }

            Write-Host "Reiniciando servicos do Windows Update..."
            foreach ($s in $services) {
                Start-Service -Name $s -ErrorAction SilentlyContinue
            }

            Write-JsonResult -Success $true -Message "Windows Update resetado com sucesso! Caches SoftwareDistribution e Catroot2 esvaziados."
        }

        "RestartShellAndAudio" {
            if ($WhatIf) {
                Write-JsonResult -Success $true -Message "[SIMULACAO] Windows Explorer e o servico de audio seriam reiniciados."
                return
            }

            Write-Host "Reiniciando servico de audio do Windows (Audiosrv)..."
            Restart-Service -Name "Audiosrv" -Force -ErrorAction SilentlyContinue

            Write-Host "Reiniciando Windows Explorer..."
            Stop-Process -Name "explorer" -Force -ErrorAction SilentlyContinue
            Start-Sleep -Milliseconds 500
            if (-not (Get-Process -Name "explorer" -ErrorAction SilentlyContinue)) {
                Start-Process explorer.exe
            }

            Write-JsonResult -Success $true -Message "Windows Explorer e servico de audio reiniciados com sucesso."
        }

        "SyncGroupPolicy" {
            if ($WhatIf) {
                Write-JsonResult -Success $true -Message "[SIMULACAO] 'gpupdate /force' seria executado."
                return
            }

            Write-Host "Forcando sincronizacao de Diretivas de Grupo (GPO)..."
            $gpOut = & gpupdate.exe /force 2>&1 | Out-String

            $ok = ($gpOut -match "concluída com êxito" -or $gpOut -match "completed successfully" -or $gpOut -match "êxito")
            $msg = if ($ok) {
                "Diretivas de grupo (computador e usuario) atualizadas com sucesso!"
            } else {
                "Sincronizacao de GPO finalizada: " + ($gpOut.Trim().Split("`n")[0])
            }

            Write-JsonResult -Success $true -Message $msg -Details @{ Output = $gpOut.Trim() }
        }

        "ClearCredentialVault" {
            if ($WhatIf) {
                Write-JsonResult -Success $true -Message "[SIMULACAO] As credenciais salvas em cache no Windows seriam limpas."
                return
            }

            Write-Host "Consultando credenciais em cache no Gerenciador de Credenciais..."
            $rawCreds = & cmdkey.exe /list 2>&1 | Out-String
            $removedCount = 0

            $rawCreds -split "`n" | ForEach-Object {
                if ($_ -match "Destino:\s*(.+)" -or $_ -match "Target:\s*(.+)") {
                    $targetName = $matches[1].Trim()
                    if ($targetName -notmatch "Virtualapp" -and $targetName -notmatch "WindowsLive") {
                        & cmdkey.exe /delete:$targetName 2>&1 | Out-Null
                        $removedCount++
                    }
                }
            }

            Write-JsonResult -Success $true -Message "Cofre de credenciais limpo com sucesso! $removedCount credencial(is) de rede removida(s)."
        }

        "DisableHibernation" {
            if ($WhatIf) {
                Write-JsonResult -Success $true -Message "[SIMULACAO] A hibernacao e o Fast Startup seriam desativados, liberando espaco do hiberfil.sys."
                return
            }

            Write-Host "Desativando modo de hibernacao e Fast Startup..."
            & powercfg.exe -h off 2>&1 | Out-Null

            Write-JsonResult -Success $true -Message "Hibernacao desativada com sucesso! Arquivo hiberfil.sys removido (libera de 8 a 32 GB de SSD)."
        }

        "OptimizeStorageDrive" {
            $drive = $Target.Substring(0, 1).ToUpper()
            if ($WhatIf) {
                Write-JsonResult -Success $true -Message "[SIMULACAO] Otimizacao (TRIM/Defrag) da unidade $drive`: seria executada."
                return
            }

            Write-Host "Executando otimizacao e TRIM na unidade $drive`:..."
            try {
                $vol = Get-Volume -DriveLetter $drive -ErrorAction Stop
                Optimize-Volume -DriveLetter $drive -ReTrim -Verbose -ErrorAction SilentlyContinue
                Write-JsonResult -Success $true -Message "Otimizacao de armazenamento e TRIM concluidos na unidade $drive`: ($($vol.FileSystemType))."
            } catch {
                Write-JsonResult -Success $false -Message "Falha ao otimizar unidade $drive`: $_"
            }
        }

        "GenerateBatteryReport" {
            if ($WhatIf) {
                Write-JsonResult -Success $true -Message "[SIMULACAO] Relatorio detalhado de bateria seria gerado em HTML."
                return
            }

            $hasBattery = (Get-CimInstance -ClassName Win32_Battery -ErrorAction SilentlyContinue)
            if (-not $hasBattery) {
                Write-JsonResult -Success $false -Message "Nenhuma bateria fisica detectada nesta estacao (dispositivo Desktop / Fixo)."
                return
            }

            $outPath = "$env:TEMP\UniFAP_Battery_Report.html"
            Write-Host "Gerando relatorio de saude da bateria em: $outPath..."
            & powercfg.exe /batteryreport /output "$outPath" 2>&1 | Out-Null

            if (Test-Path $outPath) {
                Start-Process "$outPath"
                Write-JsonResult -Success $true -Message "Relatorio de saude da bateria gerado com sucesso e aberto no navegador!"
            } else {
                Write-JsonResult -Success $false -Message "Nao foi possivel gerar o relatorio de bateria."
            }
        }

        "CheckWindowsActivation" {
            if ($WhatIf) {
                Write-JsonResult -Success $true -Message "[SIMULACAO] Consulta de status de ativacao do Windows seria realizada."
                return
            }

            Write-Host "Consultando status de licenciamento do Windows..."
            $licInfo = Get-CimInstance -ClassName SoftwareLicensingProduct -Filter "PartialProductKey IS NOT NULL" -ErrorAction SilentlyContinue |
                       Where-Object { $_.Name -like "*Windows*" } | Select-Object -First 1

            $statusStr = "Desconhecido"
            if ($licInfo) {
                $statusStr = switch ($licInfo.LicenseStatus) {
                    0 { "Nao Licenciado" }
                    1 { "[OK] Licenciado / Ativado Permanente" }
                    2 { "Periodo de Graca OOB (Pendente)" }
                    3 { "Periodo de Graca OOT" }
                    4 { "Periodo de Graca Nao Genuino" }
                    5 { "Periodo de Notificacao" }
                    default { "Codigo de Status: $($licInfo.LicenseStatus)" }
                }
            }

            $xprOut = & cscript.exe //nologo "$env:SystemRoot\System32\slmgr.vbs" /xpr 2>&1 | Out-String
            $cleanXpr = ($xprOut.Trim() -split "`n") | Where-Object { $_.Trim() -ne "" } | Select-Object -Last 1

            Write-JsonResult -Success ($licInfo.LicenseStatus -eq 1) -Message "Status de Ativacao: $statusStr ($cleanXpr)"
        }

        "UpdateDefenderAndScan" {
            if ($WhatIf) {
                Write-JsonResult -Success $true -Message "[SIMULACAO] O Windows Defender atualizaria suas assinaturas e iniciaria varredura rapida."
                return
            }

            Write-Host "[1/2] Atualizando definicoes de assinaturas do Windows Defender..."
            try {
                Update-MpSignature -ErrorAction Stop
                Write-Host "[2/2] Disparando varredura rapida em segundo plano..."
                Start-MpScan -ScanType QuickScan -ErrorAction SilentlyContinue
                Write-JsonResult -Success $true -Message "Assinaturas do Windows Defender atualizadas e verificacao rapida iniciada!"
            } catch {
                Write-JsonResult -Success $false -Message "Falha ao atualizar Defender: $_"
            }
        }
    }
} catch {
    $errMsg = $_.Exception.Message
    Write-JsonResult -Success $false -Message "Erro ao executar acao '$Action': $errMsg"
}
