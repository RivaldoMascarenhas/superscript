<#
.SYNOPSIS
    Rotina de Diagnóstico e Reparo do Windows (DISM / SFC) para UniFAP Lab Manager.
.DESCRIPTION
    Executa verificação e restauração de integridade dos arquivos de sistema e da imagem do Windows.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $false)]
    [ValidateSet("ScanOnly", "FullRepair")]
    [string]$Mode = "ScanOnly",

    [Parameter(Mandatory = $false)]
    [switch]$WhatIf
)

$ErrorActionPreference = "Continue"

function Write-JsonResult {
    param(
        [bool]$Success,
        [string]$Message,
        [hashtable]$Details = @{}
    )
    $output = [PSCustomObject]@{
        Success   = $Success
        Message   = $Message
        Details   = $Details
        Timestamp = (Get-Date).ToString("o")
    }
    $output | ConvertTo-Json -Depth 10 -Compress
}

try {
    if ($WhatIf) {
        Write-JsonResult -Success $true -Message "Simulacao: Verificacao e reparo de integridade do Windows (DISM/SFC) seriam executados." -Details @{
            WhatIf = $true
            Mode   = $Mode
        }
        return
    }

    $details = @{}

    if ($Mode -eq "FullRepair") {
        # 1. DISM RestoreHealth
        $dismOutput = & dism.exe /Online /Cleanup-Image /RestoreHealth 2>&1 | Out-String
        $details["DISM"] = $dismOutput.Trim()

        # 2. SFC ScanNow
        $sfcOutput = & sfc.exe /scannow 2>&1 | Out-String
        $details["SFC"] = $sfcOutput.Trim()

        Write-JsonResult -Success $true -Message "Reparo do Windows (DISM + SFC) concluido." -Details $details
    } else {
        # ScanOnly
        $dismOutput = & dism.exe /Online /Cleanup-Image /CheckHealth 2>&1 | Out-String
        $details["DISM_CheckHealth"] = $dismOutput.Trim()

        Write-JsonResult -Success $true -Message "Verificacao de integridade do Windows concluida." -Details $details
    }
} catch {
    $errMsg = $_.Exception.Message
    Write-JsonResult -Success $false -Message "Erro ao executar reparo do Windows: $errMsg"
}
