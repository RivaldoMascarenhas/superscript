<#
.SYNOPSIS
    Run.ps1 - Script de inicializacao do UniFAP Lab Manager.
.DESCRIPTION
    Verifica privilegios administrativos (UAC) e inicializa a interface desktop WPF com o perfil institucional.
#>
[CmdletBinding()]
param(
    [switch]$NoBuild
)

$ErrorActionPreference = "Stop"

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "   UNIFAP LAB MANAGER - INICIALIZACAO DA APLICACAO        " -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

# 1. Checar se esta rodando como Administrador
$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
$principal = New-Object Security.Principal.WindowsPrincipal($identity)
$isAdmin = $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)

if (-not $isAdmin) {
    Write-Host "[AVISO] Recomenda-se executar o UniFAP Lab Manager como Administrador." -ForegroundColor Yellow
    Write-Host "        Algumas rotinas (DISM, criacao de usuarios e AD) exigem elevacao." -ForegroundColor Yellow
}
else {
    Write-Host "[OK] Executando com privilegios de Administrador." -ForegroundColor Green
}

# 2. Executar aplicacao WPF
$projectPath = "src/UniFAP.LabManager.App/UniFAP.LabManager.App.csproj"

if ($NoBuild) {
    Write-Host "[INFO] Iniciando aplicacao sem compilacao previa..." -ForegroundColor Cyan
    dotnet run --project $projectPath --no-build
}
else {
    Write-Host "[INFO] Compilando e iniciando aplicacao WPF..." -ForegroundColor Cyan
    dotnet run --project $projectPath
}

