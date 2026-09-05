<#
.SYNOPSIS
    Test.ps1 - Execucao oficial de testes unitarios do UniFAP Lab Manager.
.DESCRIPTION
    Executa a suite de testes xUnit do projeto UniFAP.LabManager.Tests com saida detalhada.
#>
[CmdletBinding()]
param(
    [string]$Filter = "",
    [switch]$Detailed
)

$ErrorActionPreference = "Stop"

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "   UNIFAP LAB MANAGER - SUITE DE TESTES AUTOMATIZADOS     " -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

$testProject = "src/UniFAP.LabManager.Tests/UniFAP.LabManager.Tests.csproj"
$verbosity = if ($Detailed) { "detailed" } else { "normal" }

$argsList = @("test", $testProject, "--verbosity", $verbosity, "--nologo")
if (![string]::IsNullOrWhiteSpace($Filter)) {
    $argsList += "--filter"
    $argsList += $Filter
}

Write-Host "[INFO] Disparando runner xUnit..." -ForegroundColor Cyan
& dotnet $argsList

if ($LASTEXITCODE -eq 0) {
    Write-Host "`n[SUCESSO] Todos os testes unitarios passaram com exito!" -ForegroundColor Green
}
else {
    Write-Host "`n[FALHA] Um ou mais testes falharam. Verifique os relatorios acima." -ForegroundColor Red
    exit $LASTEXITCODE
}

