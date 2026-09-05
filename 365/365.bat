@echo off
title Instalacao do Office 24
color 0B

echo =======================================================
echo       Instalador Automatizado - Office 2024
echo =======================================================
echo.

:: Verifica se esta rodando como Administrador
net session >nul 2>&1
if %errorLevel% NEQ 0 (
    echo ERRO: Permissao negada!
    echo Por favor, clique com o botao direito neste arquivo .bat e selecione "Executar como administrador".
    pause
    exit /b
)

:: 1. Verifica primeiramente se o script esta rodando direto da pasta com os arquivos (%~dp0)
if exist "%~dp0setup.exe" (
    set "OFFICE_PATH=%~dp0"
    if "%OFFICE_PATH:~-1%"=="\" set "OFFICE_PATH=%OFFICE_PATH:~0,-1%"
) else (
    :: 2. Tenta obter o caminho exato do Desktop via Registro do Windows (funciona com ou sem OneDrive)
    for /f "usebackq tokens=2,*" %%A in (`reg query "HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\User Shell Folders" /v Desktop 2^>nul`) do (
        set "RAW_DESKTOP=%%B"
    )

    :: Expande variaveis de ambiente caso o registro retorne %USERPROFILE%
    if defined RAW_DESKTOP (
        call set "USER_DESKTOP=%RAW_DESKTOP%"
    )

    :: Fallbacks caso a consulta do registro falhe
    if not defined USER_DESKTOP (
        if exist "%OneDrive%\Desktop" (
            set "USER_DESKTOP=%OneDrive%\Desktop"
        ) else if exist "%OneDriveConsumer%\Desktop" (
            set "USER_DESKTOP=%OneDriveConsumer%\Desktop"
        ) else if exist "%OneDriveCommercial%\Desktop" (
            set "USER_DESKTOP=%OneDriveCommercial%\Desktop"
        ) else (
            set "USER_DESKTOP=%USERPROFILE%\Desktop"
        )
    )

    set "OFFICE_PATH=%USER_DESKTOP%\365"

    if not exist "%OFFICE_PATH%\setup.exe" (
        echo ERRO: O arquivo setup.exe nao foi encontrado em "%~dp0" nem em "%OFFICE_PATH%".
        pause
        exit /b
    )
)

echo Pasta de instalacao localizada em:
echo %OFFICE_PATH%
echo.

:: Navega ate a pasta especificada
cd /d "%OFFICE_PATH%"

:: Verifica se os arquivos necessarios estao la
if not exist "setup.exe" (
    echo ERRO: O arquivo setup.exe nao esta na pasta!
    pause
    exit /b
)
if not exist "uninstall.xml" (
    echo ERRO: O arquivo uninstall.xml nao esta na pasta!
    pause
    exit /b
)
if not exist "configuration.xml" (
    echo ERRO: O arquivo configuration.xml nao esta na pasta!
    pause
    exit /b
)

echo Tudo certo! Iniciando o processo em segundo plano...
echo.

echo ===================================================
echo  ETAPA 1: Removendo instalacao anterior do Office...
echo ===================================================
setup.exe /configure uninstall.xml

echo.
echo Desinstalacao concluida. Aguardando alguns segundos antes de prosseguir...
timeout /t 5 /nobreak >nul
echo.

echo ===================================================
echo  ETAPA 2: Instalando o Office (nova versao)...
echo ===================================================
echo O instalador laranja da Microsoft deve aparecer em instantes.
setup.exe /configure configuration.xml

echo.
echo Comando enviado com sucesso! O Office esta sendo instalado.
echo.

echo ===================================================
echo  ETAPA 3: Atualizando atalhos (Word, Excel, PowerPoint)
echo ===================================================
echo Removendo atalhos antigos...

:: Remove atalhos antigos na Area de Trabalho Publica
del /q "%PUBLIC%\Desktop\Word*.lnk" 2>nul
del /q "%PUBLIC%\Desktop\Excel*.lnk" 2>nul
del /q "%PUBLIC%\Desktop\PowerPoint*.lnk" 2>nul
del /q "%PUBLIC%\Desktop\Microsoft Word*.lnk" 2>nul
del /q "%PUBLIC%\Desktop\Microsoft Excel*.lnk" 2>nul
del /q "%PUBLIC%\Desktop\Microsoft PowerPoint*.lnk" 2>nul

:: Remove atalhos antigos na Area de Trabalho do usuario (padrao e OneDrive)
del /q "%USER_DESKTOP%\Word*.lnk" 2>nul
del /q "%USER_DESKTOP%\Excel*.lnk" 2>nul
del /q "%USER_DESKTOP%\PowerPoint*.lnk" 2>nul
del /q "%USER_DESKTOP%\Microsoft Word*.lnk" 2>nul
del /q "%USER_DESKTOP%\Microsoft Excel*.lnk" 2>nul
del /q "%USER_DESKTOP%\Microsoft PowerPoint*.lnk" 2>nul

echo Criando novos atalhos na Area de Trabalho Publica (visivel para todos os usuarios)...

:: Verifica onde ficou instalado o Office
set "OFFICE_EXE_PATH=%ProgramFiles%\Microsoft Office\root\Office16"
if not exist "%OFFICE_EXE_PATH%\WINWORD.EXE" (
    set "OFFICE_EXE_PATH=%ProgramFiles(x86)%\Microsoft Office\root\Office16"
)

if not exist "%OFFICE_EXE_PATH%\WINWORD.EXE" (
    echo AVISO: Nao foi possivel localizar os executaveis do Office para criar os atalhos.
    echo Verifique manualmente o caminho de instalacao.
    goto FIM
)

powershell -NoProfile -ExecutionPolicy Bypass -Command ^
    "$s = New-Object -ComObject WScript.Shell;" ^
    "$l = $s.CreateShortcut('%PUBLIC%\Desktop\Microsoft Word.lnk'); $l.TargetPath = '%OFFICE_EXE_PATH%\WINWORD.EXE'; $l.IconLocation = '%OFFICE_EXE_PATH%\WINWORD.EXE,0'; $l.Save();" ^
    "$l = $s.CreateShortcut('%PUBLIC%\Desktop\Microsoft Excel.lnk'); $l.TargetPath = '%OFFICE_EXE_PATH%\EXCEL.EXE'; $l.IconLocation = '%OFFICE_EXE_PATH%\EXCEL.EXE,0'; $l.Save();" ^
    "$l = $s.CreateShortcut('%PUBLIC%\Desktop\Microsoft PowerPoint.lnk'); $l.TargetPath = '%OFFICE_EXE_PATH%\POWERPNT.EXE'; $l.IconLocation = '%OFFICE_EXE_PATH%\POWERPNT.EXE,0'; $l.Save();"

echo Atalhos atualizados com sucesso!

:FIM
echo.
echo Processo concluido.
echo Pressione qualquer tecla para fechar esta janela...
pause >nul