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
    $output | ConvertTo-Json -Depth 10 -Compress
}

$applied = [System.Collections.Generic.List[string]]::new()

try {
    # 1. Assegurar diretorio de rollback
    $rollbackDir = Split-Path -Path $RollbackFile -Parent
    if (-not (Test-Path $rollbackDir)) {
        New-Item -Path $rollbackDir -ItemType Directory -Force | Out-Null
    }

    if ($WhatIf) {
        Write-JsonResult -Success $true -Message "Simulacao: Otimizacoes de desempenho seguro seriam aplicadas sem degradar fontes ou estetica." -AppliedTweaks @(
            "ClearType e Suavizacao de Fontes Preservados",
            "Animacoes e Miniaturas Preservadas",
            "Desativacao de Telemetria e Diagnosticos em Background",
            "Desativacao de Cortana e Dicas do Windows",
            "Otimizacao de MenuShowDelay (150ms)",
            "Ativacao do Sensor de Armazenamento (Storage Sense)"
        )
        return
    }

    # 2. Executar Reversao (Rollback) se solicitado
    if ($Rollback) {
        $reverted = [System.Collections.Generic.List[string]]::new()

        # Restaurar MenuShowDelay padrao do Windows (400ms)
        Set-ItemProperty -Path "HKCU:\Control Panel\Desktop" -Name "MenuShowDelay" -Value "400" -Force -ErrorAction SilentlyContinue
        $reverted.Add("MenuShowDelay restaurado para 400ms")

        # Restaurar Telemetria
        $telemetryPath = "HKLM:\SOFTWARE\Policies\Microsoft\Windows\DataCollection"
        if (Test-Path $telemetryPath) {
            Remove-ItemProperty -Path $telemetryPath -Name "AllowTelemetry" -Force -ErrorAction SilentlyContinue
            $reverted.Add("Politica de telemetria revertida")
        }

        # Restaurar Sugestoes de Conteudo
        $contentDeliveryPath = "HKCU:\Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager"
        if (Test-Path $contentDeliveryPath) {
            Set-ItemProperty -Path $contentDeliveryPath -Name "SystemPaneSuggestionsEnabled" -Value 1 -Force -ErrorAction SilentlyContinue
            Set-ItemProperty -Path $contentDeliveryPath -Name "SubscribedContent-338388Enabled" -Value 1 -Force -ErrorAction SilentlyContinue
            Set-ItemProperty -Path $contentDeliveryPath -Name "SubscribedContent-338389Enabled" -Value 1 -Force -ErrorAction SilentlyContinue
            Set-ItemProperty -Path $contentDeliveryPath -Name "SubscribedContent-353696Enabled" -Value 1 -Force -ErrorAction SilentlyContinue
            $reverted.Add("Sugestoes do Windows restauradas")
        }

        # Restaurar Cortana
        $cortanaPath = "HKLM:\SOFTWARE\Policies\Microsoft\Windows\Windows Search"
        if (Test-Path $cortanaPath) {
            Remove-ItemProperty -Path $cortanaPath -Name "AllowCortana" -Force -ErrorAction SilentlyContinue
            $reverted.Add("Politica de Cortana revertida")
        }

        # Restaurar Storage Sense
        $storageSensePath = "HKCU:\Software\Microsoft\Windows\CurrentVersion\StorageSense\Parameters\StoragePolicy"
        if (Test-Path $storageSensePath) {
            Remove-ItemProperty -Path $storageSensePath -Name "01" -Force -ErrorAction SilentlyContinue
            $reverted.Add("Sensor de Armazenamento restaurado")
        }

        if (Test-Path $RollbackFile) {
            Remove-Item -Path $RollbackFile -Force -ErrorAction SilentlyContinue
        }

        Write-JsonResult -Success $true -Message "Reversao de otimizacoes de performance concluida com sucesso." -AppliedTweaks $reverted
        return
    }

    # 3. Salvar estado atual antes da aplicacao no arquivo de rollback
    try {
        $backupState = @{
            MenuShowDelay = (Get-ItemProperty -Path "HKCU:\Control Panel\Desktop" -Name "MenuShowDelay" -ErrorAction SilentlyContinue).MenuShowDelay
            FontSmoothing = (Get-ItemProperty -Path "HKCU:\Control Panel\Desktop" -Name "FontSmoothing" -ErrorAction SilentlyContinue).FontSmoothing
            Timestamp     = (Get-Date).ToString("o")
        }
        $backupState | ConvertTo-Json -Depth 10 | Out-File -FilePath $RollbackFile -Encoding UTF8 -Force
    } catch { }

    # 4. Preservar ClearType e Efeitos Visuais
    # FontSmoothing = 2 (ClearType ativado)
    Set-ItemProperty -Path "HKCU:\Control Panel\Desktop" -Name "FontSmoothing" -Value "2" -Force
    Set-ItemProperty -Path "HKCU:\Control Panel\Desktop" -Name "FontSmoothingType" -Value 2 -Force
    $applied.Add("ClearType e Suavizacao de Fontes Preservados")

    # MenuShowDelay ajustado para 150ms (resposta rapida sem flicker)
    Set-ItemProperty -Path "HKCU:\Control Panel\Desktop" -Name "MenuShowDelay" -Value "150" -Force
    $applied.Add("MenuShowDelay otimizado para 150ms")

    # 5. Desativar Telemetria Basica e Coleta de Diagnostico sem afetar Defender / Update
    $telemetryPath = "HKLM:\SOFTWARE\Policies\Microsoft\Windows\DataCollection"
    if (-not (Test-Path $telemetryPath)) {
        New-Item -Path $telemetryPath -Force | Out-Null
    }
    Set-ItemProperty -Path $telemetryPath -Name "AllowTelemetry" -Value 0 -Force -Type DWord
    $applied.Add("Telemetria de Diagnosticos Desativada")

    # 6. Desativar Dicas e Sugestoes do Windows
    $contentDeliveryPath = "HKCU:\Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager"
    if (Test-Path $contentDeliveryPath) {
        Set-ItemProperty -Path $contentDeliveryPath -Name "SystemPaneSuggestionsEnabled" -Value 0 -Force
        Set-ItemProperty -Path $contentDeliveryPath -Name "SubscribedContent-338388Enabled" -Value 0 -Force
        Set-ItemProperty -Path $contentDeliveryPath -Name "SubscribedContent-338389Enabled" -Value 0 -Force
        Set-ItemProperty -Path $contentDeliveryPath -Name "SubscribedContent-353696Enabled" -Value 0 -Force
        $applied.Add("Sugestoes e propagandas do Windows desativadas")
    }

    # 7. Desativar Cortana em Segundo Plano
    $cortanaPath = "HKLM:\SOFTWARE\Policies\Microsoft\Windows\Windows Search"
    if (-not (Test-Path $cortanaPath)) {
        New-Item -Path $cortanaPath -Force | Out-Null
    }
    Set-ItemProperty -Path $cortanaPath -Name "AllowCortana" -Value 0 -Force -Type DWord
    $applied.Add("Cortana em segundo plano desativada")

    # 8. Habilitar Sensor de Armazenamento (Storage Sense) para limpeza automatica de temps
    $storageSensePath = "HKCU:\Software\Microsoft\Windows\CurrentVersion\StorageSense\Parameters\StoragePolicy"
    if (-not (Test-Path $storageSensePath)) {
        New-Item -Path $storageSensePath -Force | Out-Null
    }
    Set-ItemProperty -Path $storageSensePath -Name "01" -Value 1 -Force -Type DWord
    $applied.Add("Sensor de Armazenamento ativado")

    Write-JsonResult -Success $true -Message "Otimizacoes de performance aplicadas com sucesso." -AppliedTweaks $applied

} catch {
    $errMsg = $_.Exception.Message
    Write-JsonResult -Success $false -Message "Erro ao aplicar otimizacoes: $errMsg"
}
