<#
.SYNOPSIS
    lab.ps1 — Web Bootstrapper oficial do UNIFAP Lab Manager.
.DESCRIPTION
    Permite inicializar e executar o UniFAP Lab Manager remotamente em qualquer computador com:
    irm https://raw.githubusercontent.com/RivaldoMascarenhas/superscript/main/lab.ps1 | iex
#>

# 0. Configuracao de Protocolos de Seguranca de Rede (TLS 1.2 / TLS 1.3)
try {
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12 -bor [Net.SecurityProtocolType]::Tls13
} catch {
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
}

# 1. Elevacao Automatica de Privilegios de Administrador (UAC)
$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Write-Host "[INFO] Solicitando privilegios de Administrador (UAC)..." -ForegroundColor Yellow
    $scriptPath = $MyInvocation.MyCommand.Path
    if ($scriptPath -and (Test-Path $scriptPath)) {
        Start-Process powershell.exe -Verb RunAs -ArgumentList "-NoProfile -ExecutionPolicy Bypass -NoExit -File `"$scriptPath`""
    } else {
        $scriptUrl = "https://raw.githubusercontent.com/RivaldoMascarenhas/superscript/main/lab.ps1"
        Start-Process powershell.exe -Verb RunAs -ArgumentList "-NoProfile -ExecutionPolicy Bypass -NoExit -Command `"irm $scriptUrl | iex`""
    }
    exit
}

Clear-Host
Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "       UNIFAP LAB MANAGER - WEB BOOTSTRAPPER LAUNCHER     " -ForegroundColor Cyan
Write-Host "           Centro Universitario Paraiso (UniFAP)          " -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

# 2. Configuracoes de Origem e Destino
$InstallDir = "C:\ProgramData\UniFAP\LabManager\App"
$TempZip = "$env:TEMP\UniFAP-LabManager.zip"

$urlsToTry = @(
    "https://raw.githubusercontent.com/RivaldoMascarenhas/superscript/main/release/UniFAP-LabManager.zip",
    "https://github.com/RivaldoMascarenhas/superscript/raw/main/release/UniFAP-LabManager.zip",
    "http://intranet.unifapce.edu.br/softwares/UniFAP-LabManager.zip"
)

# 3. Verificar pre-requisito: .NET 8 Desktop Runtime
Write-Host "[1/4] Verificando Microsoft .NET 8 Desktop Runtime..." -ForegroundColor Yellow
$hasDotNet8 = $false

# 3.1 Checagem direta nos diretorios fisicos de runtime
$systemDesktopRuntime = "$env:ProgramFiles\dotnet\shared\Microsoft.WindowsDesktop.App"
$userDesktopRuntime = "$env:USERPROFILE\.dotnet\shared\Microsoft.WindowsDesktop.App"

if (Test-Path $systemDesktopRuntime) {
    $has8 = Get-ChildItem -Path $systemDesktopRuntime -Directory -Filter "8.*" -ErrorAction SilentlyContinue
    if ($has8) {
        $hasDotNet8 = $true
        $env:DOTNET_ROOT = "$env:ProgramFiles\dotnet"
    }
}

if (-not $hasDotNet8 -and (Test-Path $userDesktopRuntime)) {
    $hasUser8 = Get-ChildItem -Path $userDesktopRuntime -Directory -Filter "8.*" -ErrorAction SilentlyContinue
    if ($hasUser8) {
        $hasDotNet8 = $true
        $env:DOTNET_ROOT = "$env:USERPROFILE\.dotnet"
        try { [Environment]::SetEnvironmentVariable("DOTNET_ROOT", "$env:USERPROFILE\.dotnet", "User") } catch {}
        try { [Environment]::SetEnvironmentVariable("DOTNET_ROOT", "$env:USERPROFILE\.dotnet", "Machine") } catch {}
    }
}

# 3.2 Checagem via dotnet CLI se o comando estiver disponivel
if (-not $hasDotNet8) {
    try {
        $runtimes = dotnet --list-runtimes 2>$null
        if ($runtimes -match "Microsoft\.WindowsDesktop\.App 8\.") {
            $hasDotNet8 = $true
            $dotnetCmdPath = (Get-Command dotnet -ErrorAction SilentlyContinue).Source
            if ($dotnetCmdPath) {
                $env:DOTNET_ROOT = Split-Path $dotnetCmdPath
                try { [Environment]::SetEnvironmentVariable("DOTNET_ROOT", $env:DOTNET_ROOT, "User") } catch {}
            }
        }
    } catch {
        $hasDotNet8 = $false
    }
}

# 3.3 Instalacao automatica caso o runtime nao exista
if (-not $hasDotNet8) {
    Write-Host "   -> .NET 8 Desktop Runtime nao detectado. Instalando..." -ForegroundColor Cyan
    $installed = $false
    $dotnetUrl = "https://aka.ms/dotnet/8.0/windowsdesktop-runtime-win-x64.exe"
    $dotnetInstaller = "$env:TEMP\dotnet8-desktop-runtime.exe"

    try {
        Write-Host "   -> Baixando Microsoft .NET 8 Desktop Runtime oficial..." -ForegroundColor DarkGray
        Invoke-WebRequest -Uri $dotnetUrl -OutFile $dotnetInstaller -UseBasicParsing -TimeoutSec 60
        if (Test-Path $dotnetInstaller) {
            $p = Start-Process -FilePath $dotnetInstaller -ArgumentList "/install /quiet /norestart" -Wait -PassThru
            if ($p.ExitCode -eq 0 -or $p.ExitCode -eq 3010) {
                $installed = $true
                $env:DOTNET_ROOT = "$env:ProgramFiles\dotnet"
            }
        }
    } catch {
        $installed = $false
    }

    if (-not $installed) {
        Write-Host "   -> Tentando instalador alternativo via WinGet..." -ForegroundColor Yellow
        winget install Microsoft.DotNet.DesktopRuntime.8 --silent --accept-package-agreements --accept-source-agreements
        $env:DOTNET_ROOT = "$env:ProgramFiles\dotnet"
    }
}
Write-Host "   -> .NET 8 Runtime: [OK]" -ForegroundColor Green

# 4. Obter o Pacote da Aplicacao (Local ou Download)
Write-Host "[2/4] Obtendo a versao mais recente do UniFAP Lab Manager..." -ForegroundColor Yellow
$downloadSuccess = $false

# 4.1 Verificar se existe copia local disponivel (em release/, dist/ ou diretorio atual)
$localCandidates = @(
    (Join-Path (Get-Location) "release\UniFAP-LabManager.zip"),
    (Join-Path (Get-Location) "dist\UniFAP-LabManager.zip"),
    (Join-Path (Get-Location) "UniFAP-LabManager.zip")
)
if ($PSScriptRoot) {
    $localCandidates += (Join-Path $PSScriptRoot "release\UniFAP-LabManager.zip")
    $localCandidates += (Join-Path $PSScriptRoot "dist\UniFAP-LabManager.zip")
    $localCandidates += (Join-Path $PSScriptRoot "..\release\UniFAP-LabManager.zip")
}

foreach ($candidate in $localCandidates) {
    if ((Test-Path $candidate) -and ((Get-Item $candidate).Length -gt 1000000)) {
        Copy-Item -Path $candidate -Destination $TempZip -Force
        $fileSize = (Get-Item $TempZip).Length
        $downloadSuccess = $true
        $sizeMB = [math]::Round($fileSize/1MB, 2)
        Write-Host "   -> Pacote local detectado com exito ($sizeMB MB): $candidate" -ForegroundColor Green
        break
    }
}

# 4.2 Caso nao encontre localmente, baixar via Web
if (-not $downloadSuccess) {
    foreach ($url in $urlsToTry) {
        $wc = $null
        try {
            Write-Host "   -> Baixando pacote de: $url" -ForegroundColor DarkGray
            $wc = New-Object System.Net.WebClient
            $wc.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64)")
            $wc.DownloadFile($url, $TempZip)
            if (Test-Path $TempZip) {
                $fileSize = (Get-Item $TempZip).Length
                if ($fileSize -gt 1000000) {
                    $downloadSuccess = $true
                    $sizeMB = [math]::Round($fileSize/1MB, 2)
                    Write-Host "   -> Download concluido com exito! ($sizeMB MB)" -ForegroundColor Green
                    break
                } else {
                    Remove-Item $TempZip -Force -ErrorAction SilentlyContinue
                }
            }
        } catch {
            # Fallback para Invoke-WebRequest com UserAgent
            try {
                Invoke-WebRequest -Uri $url -OutFile $TempZip -UserAgent "Mozilla/5.0 (Windows NT 10.0; Win64; x64)" -UseBasicParsing -TimeoutSec 60
                if (Test-Path $TempZip) {
                    $fileSize = (Get-Item $TempZip).Length
                    if ($fileSize -gt 1000000) {
                        $downloadSuccess = $true
                        $sizeMB = [math]::Round($fileSize/1MB, 2)
                        Write-Host "   -> Download concluido com exito! ($sizeMB MB)" -ForegroundColor Green
                        break
                    }
                }
            } catch { }
        } finally {
            if ($wc) { $wc.Dispose() }
        }
    }
}

if (-not $downloadSuccess) {
    Write-Host "`n[ERRO] Nao foi possivel baixar ou localizar o pacote do UniFAP Lab Manager." -ForegroundColor Red
    Write-Host "Verifique sua conexao com a internet ou o acesso ao repositorio." -ForegroundColor Yellow
    Read-Host "`nPressione Enter para fechar..."
    exit 1
}

# 5. Extracao da Aplicacao
Write-Host "[3/4] Extraindo aplicacao em $InstallDir..." -ForegroundColor Yellow
if (-not (Test-Path $InstallDir)) {
    New-Item -ItemType Directory -Path $InstallDir -Force | Out-Null
}

Expand-Archive -Path $TempZip -DestinationPath $InstallDir -Force
Remove-Item -Path $TempZip -Force -ErrorAction SilentlyContinue
Write-Host "   -> Aplicacao extraida e pronta para uso!" -ForegroundColor Green

# 6. Criar Atalhos na Area de Trabalho (Publica e do Usuario Atual)
$exePath = Join-Path $InstallDir "UniFAP.LabManager.App.exe"
if (Test-Path $exePath) {
    $wshShell = New-Object -ComObject WScript.Shell

    # Atalho na Area de Trabalho do Usuario Atual
    $userDesktop = [Environment]::GetFolderPath("Desktop")
    if ($userDesktop -and (Test-Path $userDesktop)) {
        $shortcut = $wshShell.CreateShortcut((Join-Path $userDesktop "UniFAP Lab Manager.lnk"))
        $shortcut.TargetPath = $exePath
        $shortcut.WorkingDirectory = $InstallDir
        $shortcut.Description = "UNIFAP Lab Manager - Centro Universitario Paraiso"
        $shortcut.Save()
    }

    # Atalho na Area de Trabalho Publica (visivel a todos os usuarios da maquina e do AD)
    $commonDesktop = [Environment]::GetFolderPath("CommonDesktopDirectory")
    if ($commonDesktop -and (Test-Path $commonDesktop)) {
        $pubShortcut = $wshShell.CreateShortcut((Join-Path $commonDesktop "UniFAP Lab Manager.lnk"))
        $pubShortcut.TargetPath = $exePath
        $pubShortcut.WorkingDirectory = $InstallDir
        $pubShortcut.Description = "UNIFAP Lab Manager - Centro Universitario Paraiso"
        $pubShortcut.Save()
    }
}

# 7. Execucao do UNIFAP Lab Manager
Write-Host "[4/4] Inicializando o UNIFAP Lab Manager..." -ForegroundColor Cyan

# Propagar DOTNET_ROOT para o processo atual e registrar no sistema
if (-not $env:DOTNET_ROOT) {
    if (Test-Path "$env:ProgramFiles\dotnet\shared\Microsoft.WindowsDesktop.App") {
        $env:DOTNET_ROOT = "$env:ProgramFiles\dotnet"
    } elseif (Test-Path "$env:USERPROFILE\.dotnet\shared\Microsoft.WindowsDesktop.App") {
        $env:DOTNET_ROOT = "$env:USERPROFILE\.dotnet"
    }
}
if ($env:DOTNET_ROOT) {
    try { [Environment]::SetEnvironmentVariable("DOTNET_ROOT", $env:DOTNET_ROOT, "User") } catch {}
    try { [Environment]::SetEnvironmentVariable("DOTNET_ROOT", $env:DOTNET_ROOT, "Machine") } catch {}
}

Start-Process -FilePath $exePath -WorkingDirectory $InstallDir

Write-Host "==========================================================" -ForegroundColor Green
Write-Host "   UNIFAP LAB MANAGER INICIADO COM SUCESSO!              " -ForegroundColor Green
Write-Host "==========================================================" -ForegroundColor Green
Start-Sleep -Seconds 3
