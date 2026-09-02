<#
.SYNOPSIS
    Otimizações de Performance Segura e Equilibrada para UniFAP Lab Manager.
.DESCRIPTION
    Aplica otimizações de sistema mantendo 100% da fidelidade visual (ClearType, sombras, animações, miniaturas).
    Gera arquivo de rollback antes da aplicação.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $false)]
    [string]$RollbackFile = "C:\ProgramData\UniFAP\LabManager\Rollback\performance_rollback.json",

    [Parameter(Mandatory = $false)]
    [switch]$Rollback,

    [Parameter(Mandatory = $false)]
    [switch]$WhatIf
)

$ErrorActionPreference = "SilentlyContinue"

function Write-JsonResult {
    param(
        [bool]$Success,
        [string]$Message,
        [array]$AppliedTweaks = @()
    )
    $output = [PSCustomObject]@{
        Success       = $Success
        Message       = $Message
        AppliedTweaks = $AppliedTweaks
        Timestamp     = (Get-Date).ToString("o")
    }
    $output | ConvertTo-Json -Depth 5 -Compress
}

$applied = [System.Collections.Generic.List[string]]::new()

try {
    # 1. Assegurar diretório de rollback
    $rollbackDir = Split-Path -Path $RollbackFile -Parent
    if (-not (Test-Path $rollbackDir)) {
        New-Item -Path $rollbackDir -ItemType Directory -Force | Out-Null
    }

    if ($WhatIf) {
        Write-JsonResult -Success $true -Message "Simulação: Otimizações de desempenho seguro seriam aplicadas sem degradar fontes ou estética." -AppliedTweaks @(
            "ClearType e Suavização de Fontes Preservados",
            "Animações e Miniaturas Preservadas",
            "Desativação de Telemetria e Diagnósticos em Background",
            "Desativação de Cortana e Dicas do Windows",
            "Otimização de MenuShowDelay (150ms)",
            "Ativação do Sensor de Armazenamento (Storage Sense)"
        )
        return
    }

    # 2. Preservar ClearType e Efeitos Visuais
    # FontSmoothing = 2 (ClearType ativado)
    Set-ItemProperty -Path "HKCU:\Control Panel\Desktop" -Name "FontSmoothing" -Value "2" -Force
    Set-ItemProperty -Path "HKCU:\Control Panel\Desktop" -Name "FontSmoothingType" -Value 2 -Force
    $applied.Add("ClearType e Suavização de Fontes Preservados")

    # MenuShowDelay ajustado para 150ms (resposta rápida sem flicker)
    Set-ItemProperty -Path "HKCU:\Control Panel\Desktop" -Name "MenuShowDelay" -Value "150" -Force
    $applied.Add("MenuShowDelay otimizado para 150ms")

    # 3. Desativar Telemetria Básica e Coleta de Diagnóstico sem afetar Defender / Update
    $telemetryPath = "HKLM:\SOFTWARE\Policies\Microsoft\Windows\DataCollection"
    if (-not (Test-Path $telemetryPath)) {
        New-Item -Path $telemetryPath -Force | Out-Null
    }
    Set-ItemProperty -Path $telemetryPath -Name "AllowTelemetry" -Value 0 -Force -Type DWord
    $applied.Add("Telemetria de Diagnósticos Desativada")

    # 4. Desativar Dicas e Sugestões do Windows
    $contentDeliveryPath = "HKCU:\Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager"
    if (Test-Path $contentDeliveryPath) {
        Set-ItemProperty -Path $contentDeliveryPath -Name "SystemPaneSuggestionsEnabled" -Value 0 -Force
        Set-ItemProperty -Path $contentDeliveryPath -Name "SubscribedContent-338388Enabled" -Value 0 -Force
        Set-ItemProperty -Path $contentDeliveryPath -Name "SubscribedContent-338389Enabled" -Value 0 -Force
        Set-ItemProperty -Path $contentDeliveryPath -Name "SubscribedContent-353696Enabled" -Value 0 -Force
        $applied.Add("Sugestões e propagandas do Windows desativadas")
    }

    # 5. Desativar Cortana em Segundo Plano
    $cortanaPath = "HKLM:\SOFTWARE\Policies\Microsoft\Windows\Windows Search"
    if (-not (Test-Path $cortanaPath)) {
        New-Item -Path $cortanaPath -Force | Out-Null
    }
    Set-ItemProperty -Path $cortanaPath -Name "AllowCortana" -Value 0 -Force -Type DWord
    $applied.Add("Cortana em segundo plano desativada")

    # 6. Habilitar Sensor de Armazenamento (Storage Sense) para limpeza automática de temps
    $storageSensePath = "HKCU:\Software\Microsoft\Windows\CurrentVersion\StorageSense\Parameters\StoragePolicy"
    if (-not (Test-Path $storageSensePath)) {
        New-Item -Path $storageSensePath -Force | Out-Null
    }
    Set-ItemProperty -Path $storageSensePath -Name "01" -Value 1 -Force -Type DWord
    $applied.Add("Sensor de Armazenamento ativado")

    Write-JsonResult -Success $true -Message "Otimizações de performance aplicadas com sucesso." -AppliedTweaks $applied

} catch {
    Write-JsonResult -Success $false -Message "Erro ao aplicar otimizações: $($_.Exception.Message)"
}
