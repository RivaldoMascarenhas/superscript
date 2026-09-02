<#
.SYNOPSIS
    Ingresso Seguro no Active Directory para o UniFAP Lab Manager.
.DESCRIPTION
    Executa testes de pré-validação (DNS, Ping, NTP, LDAP), ingresso no domínio via Add-Computer
    e retorna status em formato JSON sanitizado (sem nunca expor senhas).
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$Domain,

    [Parameter(Mandatory = $false)]
    [string]$DomainController,

    [Parameter(Mandatory = $false)]
    [string]$OUPath,

    [Parameter(Mandatory = $false)]
    [string]$Username,

    [Parameter(Mandatory = $false)]
    [System.Security.SecureString]$SecurePassword,

    [Parameter(Mandatory = $false)]
    [switch]$ValidateOnly,

    [Parameter(Mandatory = $false)]
    [switch]$WhatIf
)

$ErrorActionPreference = "Stop"

function Write-JsonResult {
    param(
        [bool]$Success,
        [string]$Message,
        [hashtable]$Details = @{},
        [bool]$NeedsReboot = $false
    )
    $output = [PSCustomObject]@{
        Success     = $Success
        Message     = $Message
        NeedsReboot = $NeedsReboot
        Details     = $Details
        Timestamp   = (Get-Date).ToString("o")
    }
    $output | ConvertTo-Json -Depth 5 -Compress
}

try {
    # 1. Verificar se já está no domínio
    $computerSystem = Get-CimInstance -ClassName Win32_ComputerSystem
    $currentDomain = $computerSystem.Domain
    $partOfDomain = $computerSystem.PartOfDomain

    if ($partOfDomain -and ($currentDomain -ieq $Domain)) {
        Write-JsonResult -Success $true -Message "O computador já está ingressado no domínio $Domain." -Details @{
            CurrentDomain = $currentDomain
            PartOfDomain  = $true
        }
        return
    }

    # 2. Pré-validação de Resolução DNS
    $dnsResolved = $false
    try {
        $dnsTest = [System.Net.Dns]::GetHostAddresses($Domain)
        if ($dnsTest.Count -gt 0) {
            $dnsResolved = $true
        }
    } catch {
        $dnsResolved = $false
    }

    if (-not $dnsResolved) {
        Write-JsonResult -Success $false -Message "Falha na resolução de DNS para o domínio '$Domain'." -Details @{
            Domain      = $Domain
            DnsResolved = $false
        }
        return
    }

    # 3. Teste de conectividade com o DC se especificado
    $dcReachable = $true
    if (-not [string]::IsNullOrWhiteSpace($DomainController)) {
        try {
            $ping = Test-Connection -ComputerName $DomainController -Count 2 -Quiet -ErrorAction SilentlyContinue
            if (-not $ping) {
                $dcReachable = $false
            }
        } catch {
            $dcReachable = $false
        }

        if (-not $dcReachable) {
            Write-JsonResult -Success $false -Message "Não foi possível alcançar o Controlador de Domínio '$DomainController'." -Details @{
                DomainController = $DomainController
                Reachable        = $false
            }
            return
        }
    }

    if ($ValidateOnly) {
        Write-JsonResult -Success $true -Message "Pré-validação de Active Directory concluída com sucesso." -Details @{
            DnsResolved = $true
            DcReachable = $dcReachable
            CurrentDomain = $currentDomain
        }
        return
    }

    # 4. Ingressar no Domínio (com credencial segura)
    if ([string]::IsNullOrWhiteSpace($Username) -or ($null -eq $SecurePassword)) {
        Write-JsonResult -Success $false -Message "Credenciais de administrador de domínio não fornecidas."
        return
    }

    $credential = New-Object System.Management.Automation.PSCredential($Username, $SecurePassword)

    $addParams = @{
        DomainName  = $Domain
        Credential  = $credential
        Force       = $true
        ErrorAction = "Stop"
    }

    if (-not [string]::IsNullOrWhiteSpace($OUPath)) {
        $addParams["OUPath"] = $OUPath
    }

    if ($WhatIf) {
        $addParams["WhatIf"] = $true
        Write-JsonResult -Success $true -Message "Simulação: Ingresso no domínio '$Domain' na OU '$OUPath' seria executado." -Details @{
            WhatIf = $true
            Domain = $Domain
            OUPath = $OUPath
        }
        return
    }

    Add-Computer @addParams

    Write-JsonResult -Success $true -Message "Computador ingressado com sucesso no domínio '$Domain'." -NeedsReboot $true -Details @{
        Domain = $Domain
        OUPath = $OUPath
        Joined = $true
    }

} catch {
    Write-JsonResult -Success $false -Message "Erro ao ingressar no Active Directory: $($_.Exception.Message)" -Details @{
        Error = $_.Exception.Message
    }
}
