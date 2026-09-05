<#
.SYNOPSIS
    Build.ps1 - Script de compilacao oficial do UniFAP Lab Manager.
.DESCRIPTION
    Restaura dependencias, valida o ambiente .NET 8.0 LTS e compila todos os projetos da solucao em modo Release.
#>
[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [switch]$Clean
)

$ErrorActionPreference = "Stop"

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "   UNIFAP LAB MANAGER - BUILD DE PRODUCAO ($Configuration)   " -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

# 1. Verificar .NET SDK
try {
    $dotnetVersion = dotnet --version
    Write-Host "[OK] .NET SDK detectado: $dotnetVersion" -ForegroundColor Green
}
catch {
    Write-Error "[ERRO] .NET SDK nao encontrado no PATH do sistema. Instale o .NET 8.0 SDK LTS."
    exit 1
}

# 2. Limpeza opcional
if ($Clean) {
    Write-Host "[INFO] Executando limpeza da solucao..." -ForegroundColor Yellow
    dotnet clean UniFAP.LabManager.sln -c $Configuration --nologo
}

# 3. Restauracao de pacotes
Write-Host "[INFO] Restaurando pacotes NuGet..." -ForegroundColor Cyan
dotnet restore UniFAP.LabManager.sln --nologo

# 4. Compilacao
Write-Host "[INFO] Compilando solucao em modo $Configuration..." -ForegroundColor Cyan
dotnet build UniFAP.LabManager.sln -c $Configuration --no-restore --nologo

if ($LASTEXITCODE -eq 0) {
    Write-Host "==========================================================" -ForegroundColor Green
    Write-Host "   COMPILACAO CONCLUIDA COM EXITO!                      " -ForegroundColor Green
    Write-Host "==========================================================" -ForegroundColor Green
}
else {
    Write-Host "==========================================================" -ForegroundColor Red
    Write-Host "   FALHA NA COMPILACAO. Verifique as mensagens acima.   " -ForegroundColor Red
    Write-Host "==========================================================" -ForegroundColor Red
    exit $LASTEXITCODE
}

