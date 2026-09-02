<#
.SYNOPSIS
    Run.ps1 — Script de inicialização do UniFAP Lab Manager.
.DESCRIPTION
    Verifica privilégios administrativos (UAC) e inicializa a interface desktop WPF com o perfil institucional.
#>
[CmdletBinding()]
param(
    [switch]$NoBuild
)

$ErrorActionPreference = "Stop"

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "   UNIFAP LAB MANAGER — INICIALIZAÇÃO DA APLICAÇÃO        " -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

# 1. Checar se está rodando como Administrador
$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
$principal = New-Object Security.Principal.WindowsPrincipal($identity)
$isAdmin = $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)

if (-not $isAdmin) {
    Write-Host "[AVISO] Recomenda-se executar o UniFAP Lab Manager como Administrador." -ForegroundColor Yellow
    Write-Host "        Algumas rotinas (DISM, criação de usuários e AD) exigem elevação." -ForegroundColor Yellow
}
else {
    Write-Host "[OK] Executando com privilégios de Administrador." -ForegroundColor Green
}

# 2. Executar aplicação WPF
$projectPath = "src/UniFAP.LabManager.App/UniFAP.LabManager.App.csproj"

if ($NoBuild) {
    Write-Host "[INFO] Iniciando aplicação sem compilação prévia..." -ForegroundColor Cyan
    dotnet run --project $projectPath --no-build
}
else {
    Write-Host "[INFO] Compilando e iniciando aplicação WPF..." -ForegroundColor Cyan
    dotnet run --project $projectPath
}
