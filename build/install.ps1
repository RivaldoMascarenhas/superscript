<#
.SYNOPSIS
    Script de Instalação Corporativa do UniFAP Lab Manager.
.DESCRIPTION
    Instala o UniFAP Lab Manager em 'C:\Program Files\UniFAP\LabManager'
    e inicializa a estrutura de dados persistentes em 'C:\ProgramData\UniFAP\LabManager'.
#>
[CmdletBinding()]
param(
    [string]$SourceDir = "$PSScriptRoot\..\dist\UniFAP-LabManager",
    [string]$InstallDir = "C:\Program Files\UniFAP\LabManager",
    [string]$DataDir = "C:\ProgramData\UniFAP\LabManager"
)

$ErrorActionPreference = "Stop"

# Verifica se está rodando como Administrador
$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Write-Error "Este script de instalação deve ser executado com privilégios de Administrador."
    exit 1
}

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "   INSTALADOR CORPORATIVO — UNIFAP LAB MANAGER           " -ForegroundColor Cyan
Write-Host "   Centro Universitário Paraíso - UNIFAP                  " -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

# 1. Criar diretórios de destino
Write-Host "`n[1/4] Criando estrutura de pastas corporativa..." -ForegroundColor Yellow
if (-not (Test-Path $InstallDir)) {
    New-Item -ItemType Directory -Path $InstallDir -Force | Out-Null
}
if (-not (Test-Path $DataDir)) {
    New-Item -ItemType Directory -Path $DataDir -Force | Out-Null
}
New-Item -ItemType Directory -Path "$DataDir\Logs", "$DataDir\Jobs", "$DataDir\Reports", "$DataDir\Rollback" -Force | Out-Null

# 2. Copiar binários e arquivos
Write-Host "[2/4] Copiando binários e arquivos de configuração..." -ForegroundColor Yellow
if (Test-Path $SourceDir) {
    Copy-Item -Path "$SourceDir\*" -Destination $InstallDir -Recurse -Force
} else {
    Write-Warning "Diretório de distribuição não encontrado em $SourceDir. Execute build.ps1 primeiro."
    exit 1
}

# 3. Criar Atalho na Área de Trabalho e Menu Iniciar
Write-Host "[3/4] Criando atalhos institucionais..." -ForegroundColor Yellow
$wshShell = New-Object -ComObject WScript.Shell

$appExe = "$InstallDir\UniFAP.LabManager.App.exe"
if (Test-Path $appExe) {
    # Atalho Desktop Público
    $desktopShortcut = $wshShell.CreateShortcut("$env:PUBLIC\Desktop\UniFAP Lab Manager.lnk")
    $desktopShortcut.TargetPath = $appExe
    $desktopShortcut.WorkingDirectory = $InstallDir
    $desktopShortcut.Description = "Gerenciador e Preparador de Computadores do Centro Universitário Paraíso - UNIFAP"
    $desktopShortcut.Save()

    # Atalho Menu Iniciar Público
    $startMenuDir = "$env:ProgramData\Microsoft\Windows\Start Menu\Programs\UniFAP"
    if (-not (Test-Path $startMenuDir)) { New-Item -ItemType Directory -Path $startMenuDir -Force | Out-Null }
    $startShortcut = $wshShell.CreateShortcut("$startMenuDir\UniFAP Lab Manager.lnk")
    $startShortcut.TargetPath = $appExe
    $startShortcut.WorkingDirectory = $InstallDir
    $startShortcut.Save()
}

# 4. Registrar Agente Pós-Reboot no Registro Run (para detecção de jobs pendentes)
Write-Host "[4/4] Registrando Agente de Retomada Pós-Reboot..." -ForegroundColor Yellow
$agentExe = "$InstallDir\Agent\UniFAP.LabManager.Agent.exe"
if (Test-Path $agentExe) {
    Set-ItemProperty -Path "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Run" -Name "UniFAPLabManagerAgent" -Value "`"$agentExe`"" -Force
}

Write-Host "`n==========================================================" -ForegroundColor Green
Write-Host "✓ INSTALAÇÃO CONCLUÍDA COM SUCESSO!" -ForegroundColor Green
Write-Host "O UniFAP Lab Manager já pode ser acessado na Área de Trabalho." -ForegroundColor Green
Write-Host "==========================================================" -ForegroundColor Green
