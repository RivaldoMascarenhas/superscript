<#
.SYNOPSIS
    Test.ps1 — Execução oficial de testes unitários do UniFAP Lab Manager.
.DESCRIPTION
    Executa a suíte de testes xUnit do projeto UniFAP.LabManager.Tests com saída detalhada.
#>
[CmdletBinding()]
param(
    [string]$Filter = "",
    [switch]$Detailed
)

$ErrorActionPreference = "Stop"

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "   UNIFAP LAB MANAGER — SUÍTE DE TESTES AUTOMATIZADOS     " -ForegroundColor Cyan
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
    Write-Host "`n[SUCESSO] Todos os testes unitários passaram com êxito!" -ForegroundColor Green
}
else {
    Write-Host "`n[FALHA] Um ou mais testes falharam. Verifique os relatórios acima." -ForegroundColor Red
    exit $LASTEXITCODE
}
