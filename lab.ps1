<#
.SYNOPSIS
    lab.ps1 — Web Bootstrapper oficial do UNIFAP Lab Manager.
.DESCRIPTION
    Permite inicializar e executar o UniFAP Lab Manager remotamente em qualquer computador da faculdade com:
    irm https://<servidor-ou-github>/lab.ps1 | iex
#>

[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12 -bor [Net.SecurityProtocolType]::Tls13

# 1. Elevação Automática de Privilégios de Administrador (UAC)
$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Write-Host "[INFO] Solicitando privilégios de Administrador..." -ForegroundColor Yellow
    $scriptUrl = "https://raw.githubusercontent.com/RivaldoMascarenhas/superscript/main/lab.ps1"
    Start-Process powershell.exe -Verb RunAs -ArgumentList "-NoProfile -ExecutionPolicy Bypass -Command `"irm $scriptUrl | iex`""
    exit
}

Clear-Host
Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "       UNIFAP LAB MANAGER — WEB BOOTSTRAPPER LAUNCHER     " -ForegroundColor Cyan
Write-Host "           Centro Universitário Paraíso (UniFAP)          " -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

# 2. Configurações de Origem e Destino
$PackageUrl = "https://github.com/RivaldoMascarenhas/superscript/releases/latest/download/UniFAP-LabManager.zip"
$FallbackIntranetUrl = "http://intranet.unifapce.edu.br/softwares/UniFAP-LabManager.zip"

$InstallDir = "C:\ProgramData\UniFAP\LabManager\App"
$TempZip = "$env:TEMP\UniFAP-LabManager.zip"

# 3. Verificar pré-requisito: .NET 8 Desktop Runtime
Write-Host "[1/4] Verificando Microsoft .NET 8 Desktop Runtime..." -ForegroundColor Yellow
$hasDotNet8 = $false
try {
    $runtimes = dotnet --list-runtimes 2>$null
    if ($runtimes -match "Microsoft\.WindowsDesktop\.App 8\.") {
        $hasDotNet8 = $true
    }
} catch {
    $hasDotNet8 = $false
}

if (-not $hasDotNet8) {
    Write-Host "   -> .NET 8 Desktop Runtime não detectado. Instalando via WinGet..." -ForegroundColor Cyan
    winget install Microsoft.DotNet.DesktopRuntime.8 --silent --accept-package-agreements --accept-source-agreements
    if ($LASTEXITCODE -ne 0) {
        Write-Host "   [AVISO] WinGet encontrou uma advertência. Baixando instalador direto..." -ForegroundColor Yellow
        $dotnetUrl = "https://aka.ms/dotnet/8.0/windowsdesktop-runtime-win-x64.exe"
        $dotnetInstaller = "$env:TEMP\dotnet8-desktop-runtime.exe"
        Invoke-WebRequest -Uri $dotnetUrl -OutFile $dotnetInstaller -UseBasicParsing
        Start-Process -FilePath $dotnetInstaller -ArgumentList "/install /quiet /norestart" -Wait
    }
}
Write-Host "   -> .NET 8 Runtime: OK!" -ForegroundColor Green

# 4. Download do Pacote Mais Recente
Write-Host "[2/4] Baixando a versão mais recente do UniFAP Lab Manager..." -ForegroundColor Yellow
$downloadSuccess = $false

# Tentar download de intranet primeiro se disponível, ou URL principal
$urlsToTry = @($FallbackIntranetUrl, $PackageUrl)
foreach ($url in $urlsToTry) {
    try {
        Write-Host "   -> Tentando obter pacote de: $url" -ForegroundColor DarkGray
        Invoke-WebRequest -Uri $url -OutFile $TempZip -UseBasicParsing -TimeoutSec 30
        if (Test-Path $TempZip) {
            $downloadSuccess = $true
            Write-Host "   -> Download concluído com êxito!" -ForegroundColor Green
            break
        }
    } catch {
        # Continua para a próxima URL
    }
}

# Se for ambiente de desenvolvimento local, usar o dist já gerado
if (-not $downloadSuccess) {
    $localDistZip = Join-Path (Get-Location) "dist\UniFAP-LabManager.zip"
    if (Test-Path $localDistZip) {
        Copy-Item -Path $localDistZip -Destination $TempZip -Force
        $downloadSuccess = $true
        Write-Host "   -> Utilizando pacote local detectado em dist\UniFAP-LabManager.zip" -ForegroundColor Green
    }
}

if (-not $downloadSuccess) {
    Write-Host "[ERRO] Não foi possível baixar o pacote do UniFAP Lab Manager." -ForegroundColor Red
    Write-Host "Certifique-se de publicar o arquivo 'UniFAP-LabManager.zip' no servidor web ou GitHub." -ForegroundColor Yellow
    exit 1
}

# 5. Extração da Aplicação
Write-Host "[3/4] Extraindo aplicação em $InstallDir..." -ForegroundColor Yellow
if (-not (Test-Path $InstallDir)) {
    New-Item -ItemType Directory -Path $InstallDir -Force | Out-Null
}

Expand-Archive -Path $TempZip -DestinationPath $InstallDir -Force
Remove-Item -Path $TempZip -Force -ErrorAction SilentlyContinue
Write-Host "   -> Aplicação extraída e pronta para uso!" -ForegroundColor Green

# 6. Criar Atalho na Área de Trabalho
$exePath = Join-Path $InstallDir "UniFAP.LabManager.App.exe"
if (Test-Path $exePath) {
    $wshShell = New-Object -ComObject WScript.Shell
    $desktopPath = [Environment]::GetFolderPath("Desktop")
    $shortcut = $wshShell.CreateShortcut("$desktopPath\UniFAP Lab Manager.lnk")
    $shortcut.TargetPath = $exePath
    $shortcut.WorkingDirectory = $InstallDir
    $shortcut.Description = "UNIFAP Lab Manager — Centro Universitário Paraíso"
    $shortcut.Save()
}

# 7. Execução do UNIFAP Lab Manager
Write-Host "[4/4] Inicializando o UNIFAP Lab Manager..." -ForegroundColor Cyan
Start-Process -FilePath $exePath -WorkingDirectory $InstallDir

Write-Host "==========================================================" -ForegroundColor Green
Write-Host "   UNIFAP LAB MANAGER INICIADO COM SUCESSO!              " -ForegroundColor Green
Write-Host "==========================================================" -ForegroundColor Green
