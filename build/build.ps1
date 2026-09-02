<#
.SYNOPSIS
    Script de Build, Testes e Publicação do UniFAP Lab Manager.
.DESCRIPTION
    Compila a solução completa em Release, executa a suíte de testes xUnit,
    e gera o pacote distribuível em dist/UniFAP-LabManager.
#>
[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [string]$OutputDir = "$PSScriptRoot\..\dist\UniFAP-LabManager"
)

$ErrorActionPreference = "Stop"
$root = Resolve-Path "$PSScriptRoot\.."

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "     UNIFAP LAB MANAGER — BUILD E PUBLICAÇÃO AUTOMATIZADA " -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

# 1. Localizar dotnet
$dotnetCmd = if (Test-Path "$env:USERPROFILE\.dotnet\dotnet.exe") {
    "$env:USERPROFILE\.dotnet\dotnet.exe"
} else {
    "dotnet"
}

Write-Host "`n[1/5] Restaurando dependências..." -ForegroundColor Yellow
& $dotnetCmd restore "$root\UniFAP.LabManager.sln"

Write-Host "`n[2/5] Compilando solução ($Configuration)..." -ForegroundColor Yellow
& $dotnetCmd build "$root\UniFAP.LabManager.sln" -c $Configuration --no-restore

Write-Host "`n[3/5] Executando testes unitários automatizados..." -ForegroundColor Yellow
& $dotnetCmd test "$root\src\UniFAP.LabManager.Tests\UniFAP.LabManager.Tests.csproj" -c $Configuration --no-build

Write-Host "`n[4/5] Publicando binários da aplicação e do agente..." -ForegroundColor Yellow
$publishDir = "$root\dist\UniFAP-LabManager"
if (Test-Path $publishDir) {
    Remove-Item $publishDir -Recurse -Force | Out-Null
}
New-Item -ItemType Directory -Path $publishDir -Force | Out-Null

# Publicar App Principal
& $dotnetCmd publish "$root\src\UniFAP.LabManager.App\UniFAP.LabManager.App.csproj" -c $Configuration -o $publishDir --no-build

# Publicar Agente Pós-Reboot
& $dotnetCmd publish "$root\src\UniFAP.LabManager.Agent\UniFAP.LabManager.Agent.csproj" -c $Configuration -o "$publishDir\Agent" --no-build

Write-Host "`n[5/5] Copiando arquivos declarativos, temas, scripts e documentação..." -ForegroundColor Yellow
Copy-Item -Path "$root\config" -Destination "$publishDir\config" -Recurse -Force
Copy-Item -Path "$root\themes" -Destination "$publishDir\themes" -Recurse -Force
Copy-Item -Path "$root\assets" -Destination "$publishDir\assets" -Recurse -Force
Copy-Item -Path "$root\scripts" -Destination "$publishDir\scripts" -Recurse -Force
Copy-Item -Path "$root\docs" -Destination "$publishDir\docs" -Recurse -Force
Copy-Item -Path "$root\software" -Destination "$publishDir\software" -Recurse -Force

Write-Host "`n==========================================================" -ForegroundColor Green
Write-Host "✓ BUILD CONCLUÍDO COM SUCESSO!" -ForegroundColor Green
Write-Host "Pacote distribuível gerado em: $publishDir" -ForegroundColor Green
Write-Host "==========================================================" -ForegroundColor Green
