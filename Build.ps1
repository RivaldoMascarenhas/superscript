<#
.SYNOPSIS
    Build.ps1 — Script de compilação oficial do UniFAP Lab Manager.
.DESCRIPTION
    Restaura dependências, valida o ambiente .NET 8.0 LTS e compila todos os projetos da solução em modo Release.
#>
[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [switch]$Clean
)

$ErrorActionPreference = "Stop"

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "   UNIFAP LAB MANAGER — BUILD DE PRODUÇÃO ($Configuration)   " -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

# 1. Verificar .NET SDK
try {
    $dotnetVersion = dotnet --version
    Write-Host "[OK] .NET SDK detectado: $dotnetVersion" -ForegroundColor Green
}
catch {
    Write-Error "[ERRO] .NET SDK não encontrado no PATH do sistema. Instale o .NET 8.0 SDK LTS."
    exit 1
}

# 2. Limpeza opcional
if ($Clean) {
    Write-Host "[INFO] Executando limpeza da solução..." -ForegroundColor Yellow
    dotnet clean UniFAP.LabManager.sln -c $Configuration --nologo
}

# 3. Restauração de pacotes
Write-Host "[INFO] Restaurando pacotes NuGet..." -ForegroundColor Cyan
dotnet restore UniFAP.LabManager.sln --nologo

# 4. Compilação
Write-Host "[INFO] Compilando solução em modo $Configuration..." -ForegroundColor Cyan
dotnet build UniFAP.LabManager.sln -c $Configuration --no-restore --nologo

if ($LASTEXITCODE -eq 0) {
    Write-Host "==========================================================" -ForegroundColor Green
    Write-Host "   COMPILAÇÃO CONCLUÍDA COM ÊXITO!                      " -ForegroundColor Green
    Write-Host "==========================================================" -ForegroundColor Green
}
else {
    Write-Host "==========================================================" -ForegroundColor Red
    Write-Host "   FALHA NA COMPILAÇÃO. Verifique as mensagens acima.   " -ForegroundColor Red
    Write-Host "==========================================================" -ForegroundColor Red
    exit $LASTEXITCODE
}
