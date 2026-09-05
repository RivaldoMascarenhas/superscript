<#
.SYNOPSIS
    Provisiona contas locais com senha de suporte fornecida pelo tecnico.
#>
[CmdletBinding()]
param(
    [string]$SupportUserName = "suporte",
    [string]$SupportDisplayName = "Suporte TI UniFAP",
    $SupportPassword,
    [string]$StudentUserName = "aluno",
    [string]$StudentDisplayName = "Aluno",
    $StudentPassword,
    [switch]$WhatIf
)
$ErrorActionPreference = "Stop"

function Write-JsonResult {
    param([bool]$Success, [string]$Message, [hashtable]$UsersConfigured = @{})
    [PSCustomObject]@{
        Success = $Success
        Message = $Message
        UsersConfigured = $UsersConfigured
        Timestamp = (Get-Date).ToString("o")
    } | ConvertTo-Json -Depth 10 -Compress
}

if ($WhatIf) {
    Write-JsonResult -Success $true -Message "Simulacao: contas locais seriam provisionadas." -UsersConfigured @{ WhatIf = $true }
    return
}

try {
    if ($SupportUserName -notmatch '^[A-Za-z0-9_.-]{1,20}$' -or
        $StudentUserName -notmatch '^[A-Za-z0-9_.-]{1,20}$' -or
        $SupportUserName -ieq $StudentUserName) {
        throw "Os nomes das contas devem ser validos e diferentes."
    }
    if ($SupportPassword -is [string] -and -not [string]::IsNullOrWhiteSpace($SupportPassword)) {
        $SupportPassword = ConvertTo-SecureString $SupportPassword -AsPlainText -Force
    }
    if ($SupportPassword -isnot [Security.SecureString] -or $SupportPassword.Length -eq 0) {
        throw "Informe a senha do suporte. Nao existe senha padrao de contingencia."
    }
    if ($StudentPassword -is [string] -and -not [string]::IsNullOrWhiteSpace($StudentPassword)) {
        $StudentPassword = ConvertTo-SecureString $StudentPassword -AsPlainText -Force
    }

    $support = Get-LocalUser -Name $SupportUserName -ErrorAction SilentlyContinue
    if ($null -eq $support) {
        New-LocalUser -Name $SupportUserName -FullName $SupportDisplayName -Password $SupportPassword -PasswordNeverExpires | Out-Null
    } else {
        Set-LocalUser -Name $SupportUserName -Password $SupportPassword
    }
    Enable-LocalUser -Name $SupportUserName
    $support = Get-LocalUser -Name $SupportUserName
    $admins = Get-LocalGroup -SID 'S-1-5-32-544'
    if (@(Get-LocalGroupMember -Group $admins).SID.Value -notcontains $support.SID.Value) {
        Add-LocalGroupMember -Group $admins -Member $support
    }

    $student = Get-LocalUser -Name $StudentUserName -ErrorAction SilentlyContinue
    if ($null -eq $student) {
        if ($StudentPassword -is [Security.SecureString] -and $StudentPassword.Length -gt 0) {
            New-LocalUser -Name $StudentUserName -FullName $StudentDisplayName -Password $StudentPassword | Out-Null
        } else {
            New-LocalUser -Name $StudentUserName -FullName $StudentDisplayName -NoPassword | Out-Null
        }
    } elseif ($StudentPassword -is [Security.SecureString] -and $StudentPassword.Length -gt 0) {
        Set-LocalUser -Name $StudentUserName -Password $StudentPassword
    } else {
        # ADSI clears the password without exposing a credential in a process command line.
        $localStudent = [ADSI]("WinNT://{0}/{1},user" -f $env:COMPUTERNAME, $StudentUserName)
        $localStudent.SetPassword("")
    }
    Enable-LocalUser -Name $StudentUserName
    $student = Get-LocalUser -Name $StudentUserName
    if (@(Get-LocalGroupMember -Group $admins).SID.Value -contains $student.SID.Value) {
        Remove-LocalGroupMember -Group $admins -Member $student -Confirm:$false
    }
    $users = Get-LocalGroup -SID 'S-1-5-32-545'
    if (@(Get-LocalGroupMember -Group $users).SID.Value -notcontains $student.SID.Value) {
        Add-LocalGroupMember -Group $users -Member $student
    }
    Write-JsonResult -Success $true -Message "Usuarios locais provisionados com sucesso." -UsersConfigured @{
        SupportUser = $SupportUserName
        StudentUser = $StudentUserName
    }
} catch {
    Write-JsonResult -Success $false -Message ("Falha ao provisionar usuarios: " + $_.Exception.Message)
}
