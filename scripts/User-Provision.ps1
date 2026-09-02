<#
.SYNOPSIS
    Provisionamento Seguro de Usuários Locais para o UniFAP Lab Manager.
.DESCRIPTION
    Cria ou atualiza os usuários:
      - 'suporte': Administrador Local com senha definida pelo técnico.
      - 'aluno': Usuário Padrão SEM SENHA para acesso livre às aulas nos laboratórios.
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
        Write-JsonResult -Success $true -Message "Simulação: Usuário '$SupportUserName' (Administrador) e '$StudentUserName' (Sem senha) seriam provisionados." -UsersConfigured @{
            SupportUser = $SupportUserName
            StudentUser = $StudentUserName
            WhatIf      = $true
        }
        return
    }

    $results = @{}

    # 1. Usuário Suporte (Administrador Local - Senha Solicitada ao Técnico)
    if (-not $SupportPassword) {
        throw "A senha para o administrador local '$SupportUserName' é obrigatória e deve ser fornecida pelo técnico."
    }

    $supportExists = Get-LocalUser -Name $SupportUserName -ErrorAction SilentlyContinue
    if (-not $supportExists) {
        $userParams = @{
            Name                     = $SupportUserName
            FullName                 = $SupportDisplayName
            Description              = "Suporte de TI UniFAP"
            PasswordNeverExpires     = $true
            UserMayNotChangePassword = $false
            Password                 = $SupportPassword
        }
        New-LocalUser @userParams
        $results[$SupportUserName] = "Criado (Administrador)"
    } else {
        Set-LocalUser -Name $SupportUserName -Password $SupportPassword
        Enable-LocalUser -Name $SupportUserName -ErrorAction SilentlyContinue
        $results[$SupportUserName] = "Atualizado (Administrador)"
    }

    # Adicionar suporte ao grupo Administradores se não estiver
    try {
        Add-LocalGroupMember -Group "Administradores" -Member $SupportUserName -ErrorAction SilentlyContinue
    } catch {
        # Em Windows em inglês ou variante
        Add-LocalGroupMember -Group "Administrators" -Member $SupportUserName -ErrorAction SilentlyContinue
    }

    # 2. Usuário Aluno (Usuário Padrão - SEM SENHA para uso em laboratórios)
    $studentExists = Get-LocalUser -Name $StudentUserName -ErrorAction SilentlyContinue
    if (-not $studentExists) {
        $userParams = @{
            Name                     = $StudentUserName
            FullName                 = $StudentDisplayName
            Description              = "Aluno / Usuario Padrao"
            PasswordNeverExpires     = $true
            UserMayNotChangePassword = $true
        }
        New-LocalUser @userParams
        $results[$StudentUserName] = "Criado (Sem senha)"
    } else {
        # Remover qualquer senha existente para deixar a conta livre sem senha
        try {
            cmd.exe /c "net user $StudentUserName `"`"" | Out-Null
        } catch {
            Set-LocalUser -Name $StudentUserName -Password ([System.Security.SecureString]::new()) -ErrorAction SilentlyContinue
        }
        Set-LocalUser -Name $StudentUserName -PasswordNeverExpires $true -UserMayNotChangePassword $true -ErrorAction SilentlyContinue
        Enable-LocalUser -Name $StudentUserName -ErrorAction SilentlyContinue
        $results[$StudentUserName] = "Atualizado (Sem senha)"
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

    Write-JsonResult -Success $true -Message "Usuários locais provisionados com sucesso: 'suporte' (Admin) e 'aluno' (Sem senha)." -UsersConfigured $results

} catch {
    Write-JsonResult -Success $false -Message "Erro ao provisionar usuários locais: $($_.Exception.Message)"
    exit 1
}
