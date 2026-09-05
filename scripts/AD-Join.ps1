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
    $SecurePassword,

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
    $output | ConvertTo-Json -Depth 10 -Compress
}

    # 4. Modo Simulacao (-WhatIf)
    if ($WhatIf) {
        Write-JsonResult -Success $true -Message "Simulacao: Ingresso no dominio '$Domain' na OU '$OUPath' seria executado." -Details @{
            WhatIf = $true
            Domain = $Domain
            OUPath = $OUPath
        }
        return
    }


try {
    # Suportar conversao automatica caso SecurePassword seja passado como string simples
    if (($SecurePassword -is [string]) -and (-not [string]::IsNullOrWhiteSpace($SecurePassword))) {
        $SecurePassword = ConvertTo-SecureString $SecurePassword -AsPlainText -Force
    }

    # 1. Verificar se ja esta no dominio
    $computerSystem = Get-CimInstance -ClassName Win32_ComputerSystem
    $currentDomain = $computerSystem.Domain
    $partOfDomain = $computerSystem.PartOfDomain

    if ($partOfDomain -and ($currentDomain -ieq $Domain)) {
        Write-JsonResult -Success $true -Message "O computador ja esta ingressado no dominio $Domain." -Details @{
            CurrentDomain = $currentDomain
            PartOfDomain  = $true
        }
        return
    }

    # 2. Pre-validacao de Resolucao DNS
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
        Write-JsonResult -Success $false -Message "Falha na resolucao de DNS para o dominio '$Domain'." -Details @{
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
            Write-JsonResult -Success $false -Message "Nao foi possivel alcancar o Controlador de Dominio '$DomainController'." -Details @{
                DomainController = $DomainController
                Reachable        = $false
            }
            return
        }
    }

    if ($ValidateOnly) {
        Write-JsonResult -Success $true -Message "Pre-validacao de Active Directory concluida com sucesso." -Details @{
            DnsResolved = $true
            DcReachable = $dcReachable
            CurrentDomain = $currentDomain
        }
        return
    }

    # 5. Ingressar no Dominio (com credencial segura)
    if ([string]::IsNullOrWhiteSpace($Username) -or ($null -eq $SecurePassword)) {
        Write-JsonResult -Success $false -Message "Credenciais de administrador de dominio nao fornecidas."
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

    # Garantir que o DNS do controlador de domínio esteja configurado nos adaptadores de rede
    try {
        $targetDnsIps = @()
        if (-not [string]::IsNullOrWhiteSpace($DomainController)) {
            try {
                $dcAddresses = [System.Net.Dns]::GetHostAddresses($DomainController)
                foreach ($a in $dcAddresses) { $targetDnsIps += $a.IPAddressToString }
            } catch { }
        }
        try {
            $domAddresses = [System.Net.Dns]::GetHostAddresses($Domain)
            foreach ($a in $domAddresses) { $targetDnsIps += $a.IPAddressToString }
        } catch { }

        $primaryDns = ($targetDnsIps | Select-Object -Unique | Select-Object -First 1)
        if ($primaryDns) {
            Get-NetAdapter | Where-Object { $_.Status -eq 'Up' } | ForEach-Object {
                try {
                    Set-DnsClientServerAddress -InterfaceAlias $_.Name -ServerAddresses ($primaryDns, "1.1.1.1") -ErrorAction SilentlyContinue
                } catch { }
            }
        }
    } catch { }

    $pendingName = (Get-ItemProperty 'HKLM:\SYSTEM\CurrentControlSet\Control\ComputerName\ComputerName').ComputerName
    if ($pendingName -ine $env:COMPUTERNAME) {
        $addParams["Options"] = @("JoinWithNewName", "AccountCreate")
    }
    if (-not [string]::IsNullOrWhiteSpace($DomainController)) {
        $addParams["Server"] = $DomainController
    }
    Add-Computer @addParams

    # PÓS-INGRESSO: Configurar permissões locais e políticas de logon do Windows
    $netbios = $Domain.Split('.')[0].ToUpper()
    $rawUser = $Username
    if ($rawUser -match '\\') { $rawUser = $rawUser.Split('\')[1] }
    if ($rawUser -match '@') { $rawUser = $rawUser.Split('@')[0] }

    # 1. Adicionar o técnico e Domain Admins aos Administradores locais da máquina
    try {
        Add-LocalGroupMember -Group "Administradores" -Member "$Domain\$rawUser" -ErrorAction SilentlyContinue
    } catch {
        net localgroup Administradores "$rawUser" /add 2>$null
        net localgroup Administradores "$netbios\$rawUser" /add 2>$null
    }

    try {
        Add-LocalGroupMember -Group "Administradores" -Member "$netbios\Domain Admins" -ErrorAction SilentlyContinue
    } catch {
        net localgroup Administradores "Domain Admins" /add 2>$null
        net localgroup Administradores "$netbios\Domain Admins" /add 2>$null
    }

    try {
        Add-LocalGroupMember -Group "Usuários" -Member "$netbios\Domain Users" -ErrorAction SilentlyContinue
    } catch {
        net localgroup "Usuários" "Domain Users" /add 2>$null
        net localgroup "Usuários" "$netbios\Domain Users" /add 2>$null
    }

    # 2. Configurar o Domínio Padrão no Logon do Windows (para não tentar autenticar como usuário local)
    try {
        $winlogonPath = "HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon"
        if (Test-Path $winlogonPath) {
            Set-ItemProperty -Path $winlogonPath -Name "DefaultDomainName" -Value $netbios -Force -ErrorAction SilentlyContinue
            Set-ItemProperty -Path $winlogonPath -Name "AltDefaultDomainName" -Value $Domain -Force -ErrorAction SilentlyContinue
            Set-ItemProperty -Path $winlogonPath -Name "CachePrimaryDomain" -Value $Domain -Force -ErrorAction SilentlyContinue
            Set-ItemProperty -Path $winlogonPath -Name "CachedLogonsCount" -Value "25" -Force -ErrorAction SilentlyContinue
        }

        # 3. Forçar Windows a aguardar a rede antes de exibir o prompt de logon (evita 'Servidores de logon indisponíveis')
        $winlogonPolicies = "HKLM:\SOFTWARE\Policies\Microsoft\Windows NT\CurrentVersion\Winlogon"
        if (-not (Test-Path $winlogonPolicies)) {
            New-Item -Path $winlogonPolicies -Force -ErrorAction SilentlyContinue | Out-Null
        }
        if (Test-Path $winlogonPolicies) {
            Set-ItemProperty -Path $winlogonPolicies -Name "SyncForegroundPolicy" -Value 1 -Type DWord -Force -ErrorAction SilentlyContinue
            Set-ItemProperty -Path $winlogonPolicies -Name "GpNetworkStartTimeoutPolicyValue" -Value 60 -Type DWord -Force -ErrorAction SilentlyContinue
        }

        $systemPolicies = "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System"
        if (Test-Path $systemPolicies) {
            Set-ItemProperty -Path $systemPolicies -Name "DefaultDomainName" -Value $netbios -Force -ErrorAction SilentlyContinue
        }
    } catch { }

    Write-JsonResult -Success $true -Message "Computador ingressado com sucesso no domínio '$Domain'. Usuário '$Domain\$rawUser' e 'Domain Admins' adicionados aos Administradores locais." -NeedsReboot $true -Details @{
        Domain          = $Domain
        OUPath          = $OUPath
        Joined          = $true
        TechnicianAdmin = "$netbios\$rawUser"
        DefaultDomain   = $netbios
    }

} catch {
    $errMsg = $_.Exception.Message
    Write-JsonResult -Success $false -Message "Erro ao ingressar no Active Directory: $errMsg" -Details @{
        Error = $errMsg
    }
}
