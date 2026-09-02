<#
.SYNOPSIS
    Aplica wallpaper institucional globalmente (para todos os usuários e AD) e cria atalhos na Área de Trabalho Pública.
.DESCRIPTION
    Configura HKLM Policies, PersonalizationCSP, Run key para novos logons, Perfil Padrão (Default User),
    e mapeia atalhos para Google Chrome, Firefox, Word, Excel, PowerPoint, Adobe Acrobat Reader, WinRAR, etc.
#>
[CmdletBinding()]
param(
    [string]$WallpaperPath = "C:\ProgramData\UniFAP\LabManager\Assets\unifap_wallpaper.jpg"
)

Write-Host "=== UniFAP - Configuração Global de Wallpaper e Atalhos ===" -ForegroundColor Cyan

# 1. LOCALIZAR OU COPIAR WALLPAPER
$permanentDir = "C:\ProgramData\UniFAP\LabManager\Assets"
if (-not (Test-Path $permanentDir)) {
    New-Item -Path $permanentDir -ItemType Directory -Force | Out-Null
}

$permanentWallpaper = Join-Path $permanentDir "unifap_wallpaper.jpg"

if (-not (Test-Path $permanentWallpaper)) {
    # Procurar wallpaper no projeto
    $candidates = @(
        "$PSScriptRoot\..\assets\branding\wallpaper.jpg",
        "$PSScriptRoot\..\assets\unifap_wallpaper.jpg",
        "C:\Windows\Web\Wallpaper\UniFAP\unifap_wallpaper.jpg"
    )
    foreach ($c in $candidates) {
        if (Test-Path $c) {
            Copy-Item -Path $c -Destination $permanentWallpaper -Force
            break
        }
    }
}

if (Test-Path $permanentWallpaper) {
    Write-Host "[1/2] Aplicando papel de parede global para todos os perfis (Local, Padrão e AD)..." -ForegroundColor Green

    # 1.1 Registro do Perfil Atual (HKCU)
    Set-ItemProperty -Path "HKCU:\Control Panel\Desktop" -Name "Wallpaper" -Value $permanentWallpaper -Force -ErrorAction SilentlyContinue
    Set-ItemProperty -Path "HKCU:\Control Panel\Desktop" -Name "WallpaperStyle" -Value "10" -Force -ErrorAction SilentlyContinue
    Set-ItemProperty -Path "HKCU:\Control Panel\Desktop" -Name "TileWallpaper" -Value "0" -Force -ErrorAction SilentlyContinue

    # 1.2 Políticas Globais de Máquina (HKLM)
    $sysPolicy = "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System"
    if (-not (Test-Path $sysPolicy)) { New-Item -Path $sysPolicy -Force | Out-Null }
    Set-ItemProperty -Path $sysPolicy -Name "Wallpaper" -Value $permanentWallpaper -Force -ErrorAction SilentlyContinue
    Set-ItemProperty -Path $sysPolicy -Name "WallpaperStyle" -Value "4" -Force -ErrorAction SilentlyContinue

    $cspPath = "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\PersonalizationCSP"
    if (-not (Test-Path $cspPath)) { New-Item -Path $cspPath -Force | Out-Null }
    Set-ItemProperty -Path $cspPath -Name "DesktopImagePath" -Value $permanentWallpaper -Force -ErrorAction SilentlyContinue
    Set-ItemProperty -Path $cspPath -Name "DesktopImageUrl" -Value $permanentWallpaper -Force -ErrorAction SilentlyContinue
    Set-ItemProperty -Path $cspPath -Name "DesktopImageStatus" -Value 1 -Type DWord -Force -ErrorAction SilentlyContinue
    Set-ItemProperty -Path $cspPath -Name "LockScreenImagePath" -Value $permanentWallpaper -Force -ErrorAction SilentlyContinue
    Set-ItemProperty -Path $cspPath -Name "LockScreenImageUrl" -Value $permanentWallpaper -Force -ErrorAction SilentlyContinue
    Set-ItemProperty -Path $cspPath -Name "LockScreenImageStatus" -Value 1 -Type DWord -Force -ErrorAction SilentlyContinue

    $lockPolicy = "HKLM:\SOFTWARE\Policies\Microsoft\Windows\Personalization"
    if (-not (Test-Path $lockPolicy)) { New-Item -Path $lockPolicy -Force | Out-Null }
    Set-ItemProperty -Path $lockPolicy -Name "LockScreenImage" -Value $permanentWallpaper -Force -ErrorAction SilentlyContinue

    # 1.3 Chave Run em HKLM (executa no logon de qualquer usuário do AD ou local)
    $runKey = "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Run"
    $runCmd = "powershell.exe -WindowStyle Hidden -NoProfile -ExecutionPolicy Bypass -Command `"Add-Type -TypeDefinition 'using System.Runtime.InteropServices; public class W { [DllImport(\\`"user32.dll\\`")] public static extern int SystemParametersInfo(int u, int p, string v, int f); }'; [W]::SystemParametersInfo(20, 0, '$permanentWallpaper', 3)`""
    Set-ItemProperty -Path $runKey -Name "UniFAP_Wallpaper" -Value $runCmd -Force -ErrorAction SilentlyContinue

    # 1.4 Injetar no perfil Default User
    try {
        reg.exe load HKU\DefaultUser C:\Users\Default\NTUSER.DAT 2>$null | Out-Null
        Set-ItemProperty -Path "Registry::HKEY_USERS\DefaultUser\Control Panel\Desktop" -Name "Wallpaper" -Value $permanentWallpaper -Force -ErrorAction SilentlyContinue
        Set-ItemProperty -Path "Registry::HKEY_USERS\DefaultUser\Control Panel\Desktop" -Name "WallpaperStyle" -Value "10" -Force -ErrorAction SilentlyContinue
        Set-ItemProperty -Path "Registry::HKEY_USERS\DefaultUser\Control Panel\Desktop" -Name "TileWallpaper" -Value "0" -Force -ErrorAction SilentlyContinue
        reg.exe unload HKU\DefaultUser 2>$null | Out-Null
    } catch { }

    # 1.5 Aplicar na sessão ativa imediatamente
    Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;
public class WinWallpaper {
    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    public static extern int SystemParametersInfo(int uAction, int uParam, string lpvParam, int fuWinIni);
}
"@ -ErrorAction SilentlyContinue

    [WinWallpaper]::SystemParametersInfo(20, 0, $permanentWallpaper, 3) | Out-Null
    Write-Host "✓ Papel de parede institucional aplicado com sucesso." -ForegroundColor Green
} else {
    Write-Host "[!] Arquivo de papel de parede não localizado." -ForegroundColor Yellow
}

# 2. CRIAR ATALHOS NA ÁREA DE TRABALHO PÚBLICA (VISÍVEIS PARA TODOS OS USUÁRIOS)
Write-Host "[2/2] Gerando atalhos na Área de Trabalho Pública (C:\Users\Public\Desktop)..." -ForegroundColor Green

$publicDesktop = [Environment]::GetFolderPath("CommonDesktopDirectory")
if (-not $publicDesktop -or -not (Test-Path $publicDesktop)) {
    $publicDesktop = "C:\Users\Public\Desktop"
}
if (-not (Test-Path $publicDesktop)) {
    New-Item -Path $publicDesktop -ItemType Directory -Force | Out-Null
}

$defaultDesktop = "C:\Users\Default\Desktop"
if (-not (Test-Path $defaultDesktop)) {
    New-Item -Path $defaultDesktop -ItemType Directory -Force -ErrorAction SilentlyContinue | Out-Null
}

$wsh = New-Object -ComObject WScript.Shell

function New-PublicShortcut {
    param([string]$Name, [string[]]$Paths)
    foreach ($p in $Paths) {
        if (Test-Path $p) {
            $dest = Join-Path $publicDesktop $Name
            $s = $wsh.CreateShortcut($dest)
            $s.TargetPath = $p
            $s.WorkingDirectory = [System.IO.Path]::GetDirectoryName($p)
            $s.Save()

            # Copiar também para o Desktop Padrão
            Copy-Item -Path $dest -Destination (Join-Path $defaultDesktop $Name) -Force -ErrorAction SilentlyContinue
            Write-Host "   + Atalho criado: $Name" -ForegroundColor Cyan
            return
        }
    }
}

New-PublicShortcut -Name "Google Chrome.lnk" -Paths @(
    "C:\Program Files\Google\Chrome\Application\chrome.exe",
    "C:\Program Files (x86)\Google\Chrome\Application\chrome.exe"
)

New-PublicShortcut -Name "Mozilla Firefox.lnk" -Paths @(
    "C:\Program Files\Mozilla Firefox\firefox.exe",
    "C:\Program Files (x86)\Mozilla Firefox\firefox.exe"
)

New-PublicShortcut -Name "Word.lnk" -Paths @(
    "C:\Program Files\Microsoft Office\root\Office16\WINWORD.EXE",
    "C:\Program Files (x86)\Microsoft Office\root\Office16\WINWORD.EXE",
    "C:\Program Files\Microsoft Office\Office16\WINWORD.EXE"
)

New-PublicShortcut -Name "Excel.lnk" -Paths @(
    "C:\Program Files\Microsoft Office\root\Office16\EXCEL.EXE",
    "C:\Program Files (x86)\Microsoft Office\root\Office16\EXCEL.EXE",
    "C:\Program Files\Microsoft Office\Office16\EXCEL.EXE"
)

New-PublicShortcut -Name "PowerPoint.lnk" -Paths @(
    "C:\Program Files\Microsoft Office\root\Office16\POWERPNT.EXE",
    "C:\Program Files (x86)\Microsoft Office\root\Office16\POWERPNT.EXE",
    "C:\Program Files\Microsoft Office\Office16\POWERPNT.EXE"
)

New-PublicShortcut -Name "Adobe Acrobat.lnk" -Paths @(
    "C:\Program Files\Adobe\Acrobat DC\Acrobat\Acrobat.exe",
    "C:\Program Files\Adobe\Acrobat Reader 64-bit\Reader\AcroRd32.exe",
    "C:\Program Files (x86)\Adobe\Acrobat Reader DC\Reader\AcroRd32.exe",
    "C:\Program Files (x86)\Adobe\Reader 11.0\Reader\AcroRd32.exe"
)

New-PublicShortcut -Name "WinRAR.lnk" -Paths @(
    "C:\Program Files\WinRAR\WinRAR.exe",
    "C:\Program Files (x86)\WinRAR\WinRAR.exe"
)

New-PublicShortcut -Name "VLC media player.lnk" -Paths @(
    "C:\Program Files\VideoLAN\VLC\vlc.exe",
    "C:\Program Files (x86)\VideoLAN\VLC\vlc.exe"
)

New-PublicShortcut -Name "Visual Studio Code.lnk" -Paths @(
    "C:\Program Files\Microsoft VS Code\Code.exe",
    "C:\Program Files (x86)\Microsoft VS Code\Code.exe"
)

# Varrer Menu Iniciar para outros softwares
$startMenu = "C:\ProgramData\Microsoft\Windows\Start Menu\Programs"
if (Test-Path $startMenu) {
    $keywords = @("chrome", "firefox", "word", "excel", "powerpoint", "acrobat", "winrar", "vlc", "code", "autocad", "revit", "pycharm", "intellij", "android studio")
    Get-ChildItem -Path $startMenu -Filter "*.lnk" -Recurse | ForEach-Object {
        $lnkName = $_.Name
        $lower = $lnkName.ToLowerInvariant()
        foreach ($kw in $keywords) {
            if ($lower.Contains($kw)) {
                $target = Join-Path $publicDesktop $lnkName
                if (-not (Test-Path $target)) {
                    Copy-Item -Path $_.FullName -Destination $target -Force -ErrorAction SilentlyContinue
                    Copy-Item -Path $_.FullName -Destination (Join-Path $defaultDesktop $lnkName) -Force -ErrorAction SilentlyContinue
                    Write-Host "   + Sincronizado do Menu Iniciar: $lnkName" -ForegroundColor Cyan
                }
                break
            }
        }
    }
}

Write-Host "=== Processo de Branding e Atalhos Concluído com Sucesso! ===" -ForegroundColor Green
