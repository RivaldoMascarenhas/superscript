<#
.SYNOPSIS
    Provisionamento Seguro de Usuários Locais para o UniFAP Lab Manager.
.DESCRIPTION
    Cria ou atualiza os usuários 'suporte' (Administrador Local) e 'aluno' (Usuário Padrão).
    Garante isolamento de privilégios para que o usuário 'aluno' NUNCA receba permissões administrativas.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $false)]
    [string]$SupportUserName = "suporte",

    [Parameter(Mandatory = $false)]
    [string]$SupportDisplayName = "Suporte TI UniFAP",

    [Parameter(Mandatory = $false)]
    [System.Security.SecureString]$SupportPassword,

    [Parameter(Mandatory = $false)]
    [string]$StudentUserName = "aluno",

    [Parameter(Mandatory = $false)]
    [string]$StudentDisplayName = "Aluno / Usuário Padrão",

    [Parameter(Mandatory = $false)]
    [System.Security.SecureString]$StudentPassword,

    [Parameter(Mandatory = $false)]
    [switch]$WhatIf
)

$ErrorActionPreference = "Stop"

function Write-JsonResult {
    param(
        [bool]$Success,
        [string]$Message,
        [hashtable]$UsersConfigured = @{}
    )
    $output = [PSCustomObject]@{
        Success         = $Success
        Message         = $Message
        UsersConfigured = $UsersConfigured
        Timestamp       = (Get-Date).ToString("o")
    }
    $output | ConvertTo-Json -Depth 5 -Compress
}

try {
    if ($WhatIf) {
        Write-JsonResult -Success $true -Message "Simulação: Usuário '$SupportUserName' (Administrador) e '$StudentUserName' (Usuário Padrão) seriam provisionados." -UsersConfigured @{
            SupportUser = $SupportUserName
            StudentUser = $StudentUserName
            WhatIf      = $true
        }
        return
    }

    $results = @{}

    # Senhas padrão institucionais se não forem passadas
    if (-not $SupportPassword) {
        $SupportPassword = "UniFAP@Suporte2026!" | ConvertTo-SecureString -AsPlainText -Force
    }
    if (-not $StudentPassword) {
        $StudentPassword = "UniFAP@Aluno2026!" | ConvertTo-SecureString -AsPlainText -Force
    }

    # 1. Provisionar Usuário Suporte (Administrador Local)
    $supportExists = Get-LocalUser -Name $SupportUserName -ErrorAction SilentlyContinue
    if (-not $supportExists) {
        $userParams = @{
            Name                  = $SupportUserName
            FullName              = $SupportDisplayName
            Description           = "Suporte de TI UniFAP"
            PasswordNeverExpires  = $true
            UserMayNotChangePassword = $false
            Password              = $SupportPassword
        }
        New-LocalUser @userParams
        $results[$SupportUserName] = "Criado"
    } else {
        Set-LocalUser -Name $SupportUserName -Password $SupportPassword
        Enable-LocalUser -Name $SupportUserName -ErrorAction SilentlyContinue
        $results[$SupportUserName] = "Atualizado"
    }

    # Adicionar suporte ao grupo Administradores se não estiver
    try {
        Add-LocalGroupMember -Group "Administradores" -Member $SupportUserName -ErrorAction SilentlyContinue
    } catch {
        # Em Windows em inglês ou variante
        Add-LocalGroupMember -Group "Administrators" -Member $SupportUserName -ErrorAction SilentlyContinue
    }

    # 2. Provisionar Usuário Aluno (Usuário Padrão)
    $studentExists = Get-LocalUser -Name $StudentUserName -ErrorAction SilentlyContinue
    if (-not $studentExists) {
        $userParams = @{
            Name                     = $StudentUserName
            FullName                 = $StudentDisplayName
            Description              = "Aluno / Usuario Padrao"
            Password                 = $StudentPassword
            PasswordNeverExpires     = $true
            UserMayNotChangePassword = $true
        }
        New-LocalUser @userParams
        $results[$StudentUserName] = "Criado"
    } else {
        Set-LocalUser -Name $StudentUserName -Password $StudentPassword
        Enable-LocalUser -Name $StudentUserName -ErrorAction SilentlyContinue
        $results[$StudentUserName] = "Atualizado"
    }

    # Assegurar que 'aluno' pertence a 'Usuários' e NUNCA a 'Administradores'
    try {
        Add-LocalGroupMember -Group "Usuários" -Member $StudentUserName -ErrorAction SilentlyContinue
    } catch {
        Add-LocalGroupMember -Group "Users" -Member $StudentUserName -ErrorAction SilentlyContinue
    }

    # Remover explicitamente de Administradores caso tenha sido adicionado por engano
    try {
        Remove-LocalGroupMember -Group "Administradores" -Member $StudentUserName -ErrorAction SilentlyContinue
    } catch {}
    try {
        Remove-LocalGroupMember -Group "Administrators" -Member $StudentUserName -ErrorAction SilentlyContinue
    } catch {}

    Write-JsonResult -Success $true -Message "Usuários locais provisionados com sucesso e privilégios estritamente isolados." -UsersConfigured $results

} catch {
    Write-JsonResult -Success $false -Message "Erro ao provisionar usuários locais: $($_.Exception.Message)"
    exit 1
}
