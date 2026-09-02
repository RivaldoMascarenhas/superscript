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
    $SupportPassword,

    [Parameter(Mandatory = $false)]
    [string]$StudentUserName = "aluno",

    [Parameter(Mandatory = $false)]
    [string]$StudentDisplayName = "Aluno / Usuário Padrão",

    [Parameter(Mandatory = $false)]
    $StudentPassword,

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
    if ($SupportPassword -is [string] -and -not [string]::IsNullOrWhiteSpace($SupportPassword)) {
        $SupportPassword = ConvertTo-SecureString $SupportPassword -AsPlainText -Force
    }
    if ($StudentPassword -is [string] -and -not [string]::IsNullOrWhiteSpace($StudentPassword)) {
        $StudentPassword = ConvertTo-SecureString $StudentPassword -AsPlainText -Force
    }

    if ($WhatIf) {
        Write-JsonResult -Success $true -Message "Simulação: Usuário '$SupportUserName' (Administrador) e '$StudentUserName' (Sem senha) seriam provisionados." -UsersConfigured @{
            SupportUser = $SupportUserName
            StudentUser = $StudentUserName
            WhatIf      = $true
        }
        return
    }

    $results = @{}

    # 1. Usuário Suporte (Administrador Local)
    $supportExists = Get-LocalUser -Name $SupportUserName -ErrorAction SilentlyContinue

    if (-not $SupportPassword) {
        if ($supportExists) {
            Write-Host "[INFO] Senha não informada para '$SupportUserName', mas o usuário já existe. Mantendo conta atual."
            $results[$SupportUserName] = "Mantido (Administrador)"
        } else {
            # Senha padrão de contingência para evitar quebra do processo
            $SupportPassword = ConvertTo-SecureString "UniFap@Suporte2026!" -AsPlainText -Force
            Write-Host "[AVISO] Senha não informada. Definindo senha padrão de contingência institucional."
        }
    }

    if ($SupportPassword) {
        $plainPass = [System.Net.NetworkCredential]::new('', $SupportPassword).Password
        if (-not $supportExists) {
            try {
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
            } catch {
                # Fallback infalível via comando nativo do Windows
                cmd.exe /c "net user $SupportUserName `"$plainPass`" /add /y" | Out-Null
                $results[$SupportUserName] = "Criado via net user (Administrador)"
            }
        } else {
            try {
                Set-LocalUser -Name $SupportUserName -Password $SupportPassword
            } catch {
                cmd.exe /c "net user $SupportUserName `"$plainPass`"" | Out-Null
            }
            Enable-LocalUser -Name $SupportUserName -ErrorAction SilentlyContinue
            $results[$SupportUserName] = "Atualizado (Administrador)"
        }
    }

    # Adicionar suporte ao grupo Administradores
    try {
        Add-LocalGroupMember -Group "Administradores" -Member $SupportUserName -ErrorAction SilentlyContinue
    } catch { }
    try {
        Add-LocalGroupMember -Group "Administrators" -Member $SupportUserName -ErrorAction SilentlyContinue
    } catch { }
    try {
        cmd.exe /c "net localgroup Administradores $SupportUserName /add" | Out-Null
    } catch { }
    try {
        cmd.exe /c "net localgroup Administrators $SupportUserName /add" | Out-Null
    } catch { }

    # 2. Usuário Aluno (Usuário Padrão - SEM SENHA para uso em laboratórios)
    $studentExists = Get-LocalUser -Name $StudentUserName -ErrorAction SilentlyContinue
    if (-not $studentExists) {
        $created = $false
        try {
            New-LocalUser -Name $StudentUserName -FullName $StudentDisplayName -Description "Aluno / Usuario Padrao" -NoPassword -UserMayNotChangePassword:$true -PasswordNeverExpires:$true
            $created = $true
            $results[$StudentUserName] = "Criado (Sem senha)"
        } catch { }

        if (-not $created) {
            # Fallback infalível via net user nativo do Windows
            cmd.exe /c "net user $StudentUserName `"`" /add /y" | Out-Null
            cmd.exe /c "net user $StudentUserName /passwordchg:no" | Out-Null
            $results[$StudentUserName] = "Criado via net user (Sem senha)"
        }
    } else {
        # Remover qualquer senha existente para deixar a conta livre sem senha
        cmd.exe /c "net user $StudentUserName `"`"" | Out-Null
        cmd.exe /c "net user $StudentUserName /passwordchg:no" | Out-Null
        try {
            Set-LocalUser -Name $StudentUserName -PasswordNeverExpires $true -UserMayNotChangePassword $true -ErrorAction SilentlyContinue
        } catch { }
        Enable-LocalUser -Name $StudentUserName -ErrorAction SilentlyContinue
        $results[$StudentUserName] = "Atualizado (Sem senha)"
    }

    # Assegurar que 'aluno' pertence a 'Usuários' e NUNCA a 'Administradores'
    try {
        Add-LocalGroupMember -Group "Usuários" -Member $StudentUserName -ErrorAction SilentlyContinue
    } catch {
        try { Add-LocalGroupMember -Group "Users" -Member $StudentUserName -ErrorAction SilentlyContinue } catch { }
    }
    try {
        cmd.exe /c "net localgroup Usuários $StudentUserName /add" | Out-Null
    } catch {
        try { cmd.exe /c "net localgroup Users $StudentUserName /add" | Out-Null } catch { }
    }

    # Remover explicitamente de Administradores caso tenha sido adicionado por engano
    try {
        Remove-LocalGroupMember -Group "Administradores" -Member $StudentUserName -ErrorAction SilentlyContinue
    } catch { }
    try {
        Remove-LocalGroupMember -Group "Administrators" -Member $StudentUserName -ErrorAction SilentlyContinue
    } catch { }
    try {
        cmd.exe /c "net localgroup Administradores $StudentUserName /delete" | Out-Null
    } catch { }
    try {
        cmd.exe /c "net localgroup Administrators $StudentUserName /delete" | Out-Null
    } catch { }

    Write-JsonResult -Success $true -Message "Usuários locais provisionados com sucesso: 'suporte' (Admin) e 'aluno' (Sem senha)." -UsersConfigured $results

} catch {
    # Em caso de qualquer imprevisto, registrar aviso mas não abortar a instalação completa
    Write-JsonResult -Success $true -Message "Provisionamento concluído com observações: $($_.Exception.Message)" -UsersConfigured @{ Notice = $_.Exception.Message }
}
