<#
.SYNOPSIS
    Publish.ps1 - Publicacao e empacotamento do UniFAP Lab Manager.
.DESCRIPTION
    Compila e publica a aplicacao WPF e o agente pos-reboot para a pasta dist/ com todas as configuracoes,
    scripts e assets institucionais inclusos para implantacao em pendrives ou rede.
#>
[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [switch]$SelfContained = $false
)

$ErrorActionPreference = "Stop"

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "   UNIFAP LAB MANAGER - PUBLICACAO E EMPACOTAMENTO        " -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

$distDir = Join-Path (Get-Location) "dist"
$appDist = Join-Path $distDir "UniFAP-LabManager"

if (Test-Path $appDist) {
    Write-Host "[INFO] Limpando diretorio de distribuicao anterior..." -ForegroundColor Yellow
    Remove-Item -Path $appDist -Recurse -Force
}

New-Item -ItemType Directory -Path $appDist -Force | Out-Null

$appProject = "src/UniFAP.LabManager.App/UniFAP.LabManager.App.csproj"
$agentProject = "src/UniFAP.LabManager.Agent/UniFAP.LabManager.Agent.csproj"

$selfContainedArg = if ($SelfContained) { "true" } else { "false" }

Write-Host "[INFO] Publicando UniFAP.LabManager.App..." -ForegroundColor Cyan
dotnet publish $appProject -c $Configuration -r $Runtime --self-contained $selfContainedArg -o $appDist --nologo

Write-Host "[INFO] Publicando UniFAP.LabManager.Agent..." -ForegroundColor Cyan
$agentDist = Join-Path $appDist "Agent"
dotnet publish $agentProject -c $Configuration -r $Runtime --self-contained $selfContainedArg -o $agentDist --nologo

# Copiar pastas e arquivos de suporte obrigatorios
Write-Host "[INFO] Copiando arquivos de configuracao, scripts, documentacao e assets institucionais..." -ForegroundColor Cyan

$dirsToCopy = @("config", "assets", "scripts", "themes", "software", "docs")
foreach ($dir in $dirsToCopy) {
    if (Test-Path $dir) {
        $dest = Join-Path $appDist $dir
        Copy-Item -Path $dir -Destination $dest -Recurse -Force
        Write-Host "   -> Copiado diretorio: $dir" -ForegroundColor Green
    }
}

if (Test-Path "README.md") {
    Copy-Item -Path "README.md" -Destination $appDist -Force
    Write-Host "   -> Copiado arquivo: README.md" -ForegroundColor Green
}

# Gerar arquivo ZIP para distribuicao web/intranet
$zipFile = Join-Path $distDir "UniFAP-LabManager.zip"
Write-Host "[INFO] Compactando pacote para distribuicao web/intranet ($zipFile)..." -ForegroundColor Cyan
if (Test-Path $zipFile) { Remove-Item $zipFile -Force }
Compress-Archive -Path "$appDist\*" -DestinationPath $zipFile -Force
Write-Host "   -> Arquivo ZIP gerado com sucesso!" -ForegroundColor Green

# Sincronizar com a pasta release/ para que o bootstrapper lab.ps1 utilize o pacote atualizado
$releaseDir = Join-Path (Get-Location) "release"
if (Test-Path $releaseDir) {
    $releaseZip = Join-Path $releaseDir "UniFAP-LabManager.zip"
    Copy-Item -Path $zipFile -Destination $releaseZip -Force
    Write-Host "   -> Sincronizado com pacote de distribuicao: release\UniFAP-LabManager.zip" -ForegroundColor Green
}

Write-Host "`n==========================================================" -ForegroundColor Green
Write-Host "   PACOTE GERADO COM SUCESSO EM:                          " -ForegroundColor Green
Write-Host "   $appDist" -ForegroundColor White
Write-Host "==========================================================" -ForegroundColor Green

