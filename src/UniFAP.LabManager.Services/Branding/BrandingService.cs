using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using UniFAP.LabManager.Core.Interfaces;

namespace UniFAP.LabManager.Services.Branding;

[ComImport]
[Guid("B92B56A9-8B55-4E49-9B86-3DEEB046AE87")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IDesktopWallpaper
{
    void SetWallpaper([MarshalAs(UnmanagedType.LPWStr)] string? monitorID, [MarshalAs(UnmanagedType.LPWStr)] string wallpaper);
    [return: MarshalAs(UnmanagedType.LPWStr)]
    string GetWallpaper([MarshalAs(UnmanagedType.LPWStr)] string? monitorID);
    [return: MarshalAs(UnmanagedType.LPWStr)]
    string GetMonitorDevicePathAt(uint monitorIndex);
    uint GetMonitorDevicePathCount();
    void GetMonitorRECT([MarshalAs(UnmanagedType.LPWStr)] string? monitorID, out IntPtr rect);
    void SetBackgroundColor(uint color);
    uint GetBackgroundColor();
    void SetPosition(int position);
    int GetPosition();
    void SetSlideshow(IntPtr items);
    IntPtr GetSlideshow();
    void SetSlideshowOptions(int options, uint slideshowTick);
    void GetSlideshowOptions(out int options, out uint slideshowTick);
    void AdvanceSlideshow([MarshalAs(UnmanagedType.LPWStr)] string? monitorID, int direction);
    int GetStatus();
    bool Enable();
}

[ComImport]
[Guid("C2CF3110-460E-4fc1-B9D0-8A1C0C9CC4BD")]
internal class DesktopWallpaperClass
{
}

public class BrandingService : IBrandingService
{
    private readonly IConfigService _configService;
    private readonly ILogService _logger;

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern int SystemParametersInfo(int uAction, int uParam, string lpvParam, int fuWinIni);

    private const int SPI_SETDESKWALLPAPER = 20;
    private const int SPIF_UPDATEINIFILE = 0x01;
    private const int SPIF_SENDCHANGE = 0x02;

    public BrandingService(IConfigService configService, ILogService logger)
    {
        _configService = configService;
        _logger = logger;
    }

    public string GetWallpaperPath()
    {
        string relativePath = _configService.Branding.Wallpaper.Path.Replace('/', Path.DirectorySeparatorChar);
        string basePath = AppDomain.CurrentDomain.BaseDirectory;
        string fullPath = Path.GetFullPath(Path.Combine(basePath, relativePath));

        if (File.Exists(fullPath))
            return fullPath;

        // Subir nos diretórios pais até encontrar a raiz do repositório
        var dir = new DirectoryInfo(basePath);
        while (dir != null)
        {
            string candidate = Path.Combine(dir.FullName, relativePath);
            if (File.Exists(candidate)) return Path.GetFullPath(candidate);
            dir = dir.Parent;
        }

        // Tentar no diretório de trabalho atual
        string currentCandidate = Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, relativePath));
        if (File.Exists(currentCandidate)) return currentCandidate;

        // Tentar em C:\ProgramData\UniFAP\LabManager
        string programDataCandidate = Path.Combine(@"C:\ProgramData\UniFAP\LabManager", relativePath);
        if (File.Exists(programDataCandidate)) return programDataCandidate;

        string fixedAssets = @"C:\ProgramData\UniFAP\LabManager\Assets\unifap_wallpaper.jpg";
        if (File.Exists(fixedAssets)) return fixedAssets;

        return fullPath;
    }

    public async Task<bool> ApplyBrandingAsync(bool dryRun = false)
    {
        await Task.Yield();
        _logger.LogInformation("BrandingService", $"Aplicando identidade visual institucional UniFAP [DryRun: {dryRun}]");

        if (dryRun)
        {
            _logger.LogInformation("BrandingService", "[DRY-RUN] Simulação: Wallpaper e informações institucionais seriam configurados.");
            return true;
        }

        try
        {
            var branding = _configService.Branding;

            // 1. Aplicar Wallpaper Institucional
            if (branding.Wallpaper.Enabled)
            {
                string wallpaperPath = GetWallpaperPath();
                if (!File.Exists(wallpaperPath))
                {
                    _logger.LogError("BrandingService", $"Arquivo de wallpaper não encontrado em nenhum dos diretórios pesquisados. Caminho base: {wallpaperPath}");
                    return false;
                }

                // Copiar o wallpaper para ProgramData permanente
                string permanentDir = @"C:\ProgramData\UniFAP\LabManager\Assets";
                Directory.CreateDirectory(permanentDir);
                string permanentWallpaperPath = Path.Combine(permanentDir, "unifap_wallpaper.jpg");
                try
                {
                    File.Copy(wallpaperPath, permanentWallpaperPath, true);
                    wallpaperPath = permanentWallpaperPath;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("BrandingService", $"Não foi possível copiar wallpaper para ProgramData: {ex.Message}");
                }

                _logger.LogInformation("BrandingService", $"Configurando papel de parede: {wallpaperPath}");

                // 1.1 Gravar registro do Windows no perfil atual (HKCU)
                try
                {
                    using var key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop", true);
                    if (key != null)
                    {
                        key.SetValue("WallpaperStyle", "10"); // Fill
                        key.SetValue("TileWallpaper", "0");
                        key.SetValue("Wallpaper", wallpaperPath);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("BrandingService", $"Erro ao registrar wallpaper no HKCU: {ex.Message}");
                }

                // 1.2 Gravar registro de Políticas do Windows (HKLM) para TODOS os usuários (incluindo domínio AD)
                try
                {
                    using var sysPolicyKey = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System", true);
                    if (sysPolicyKey != null)
                    {
                        sysPolicyKey.SetValue("Wallpaper", wallpaperPath);
                        sysPolicyKey.SetValue("WallpaperStyle", "4"); // Fill
                    }

                    using var cspKey = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\PersonalizationCSP", true);
                    if (cspKey != null)
                    {
                        cspKey.SetValue("DesktopImagePath", wallpaperPath);
                        cspKey.SetValue("DesktopImageUrl", wallpaperPath);
                        cspKey.SetValue("DesktopImageStatus", 1, RegistryValueKind.DWord);
                        cspKey.SetValue("LockScreenImagePath", wallpaperPath);
                        cspKey.SetValue("LockScreenImageUrl", wallpaperPath);
                        cspKey.SetValue("LockScreenImageStatus", 1, RegistryValueKind.DWord);
                    }

                    using var lockPolicyKey = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Policies\Microsoft\Windows\Personalization", true);
                    if (lockPolicyKey != null)
                    {
                        lockPolicyKey.SetValue("LockScreenImage", wallpaperPath);
                    }

                    // Gravar chave Run em HKLM para garantir aplicação a qualquer novo logon de usuário (Local ou AD)
                    using var runKey = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);
                    if (runKey != null)
                    {
                        string runCmd = $"powershell.exe -WindowStyle Hidden -NoProfile -ExecutionPolicy Bypass -Command \"Add-Type -TypeDefinition 'using System.Runtime.InteropServices; public class W {{ [DllImport(\\\"user32.dll\\\")] public static extern int SystemParametersInfo(int u, int p, string v, int f); }}'; [W]::SystemParametersInfo(20, 0, '{wallpaperPath.Replace("'", "''")}', 3)\"";
                        runKey.SetValue("UniFAP_Wallpaper", runCmd);
                    }

                    _logger.LogInformation("BrandingService", "Políticas globais de wallpaper gravadas em HKLM e PersonalizationCSP.");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("BrandingService", $"Erro ao gravar wallpaper global em HKLM: {ex.Message}");
                }

                // 1.3 Injetar wallpaper no Perfil Padrão (C:\Users\Default\NTUSER.DAT) para novos usuários
                try
                {
                    var pLoad = Process.Start(new ProcessStartInfo
                    {
                        FileName = "reg.exe",
                        Arguments = "load HKU\\DefaultUser C:\\Users\\Default\\NTUSER.DAT",
                        CreateNoWindow = true,
                        UseShellExecute = false
                    });
                    pLoad?.WaitForExit(3000);

                    using var defKey = Registry.Users.OpenSubKey(@"DefaultUser\Control Panel\Desktop", true);
                    if (defKey != null)
                    {
                        defKey.SetValue("WallpaperStyle", "10");
                        defKey.SetValue("TileWallpaper", "0");
                        defKey.SetValue("Wallpaper", wallpaperPath);
                    }
                    defKey?.Dispose();

                    var pUnload = Process.Start(new ProcessStartInfo
                    {
                        FileName = "reg.exe",
                        Arguments = "unload HKU\\DefaultUser",
                        CreateNoWindow = true,
                        UseShellExecute = false
                    });
                    pUnload?.WaitForExit(3000);
                }
                catch { }

                // 1.4 Aplicar via Windows Shell COM IDesktopWallpaper (sessão ativa)
                try
                {
                    var desktopWallpaper = (IDesktopWallpaper)new DesktopWallpaperClass();
                    desktopWallpaper.SetPosition(4); // 4 = Fill
                    desktopWallpaper.SetWallpaper(null, wallpaperPath); // null = todos os monitores
                    _logger.LogInformation("BrandingService", "Papel de parede aplicado com sucesso via Windows Shell IDesktopWallpaper.");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("BrandingService", $"IDesktopWallpaper COM falhou, executando fallback: {ex.Message}");
                }

                // 1.5 Fallback ou reforço via SystemParametersInfo
                try
                {
                    SystemParametersInfo(SPI_SETDESKWALLPAPER, 0, wallpaperPath, SPIF_UPDATEINIFILE | SPIF_SENDCHANGE);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("BrandingService", $"SystemParametersInfo falhou: {ex.Message}");
                }
            }

            // 2. Aplicar OEM Information (Propriedades do Sistema)
            try
            {
                using var oemKey = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\OEMInformation", true);
                if (oemKey != null)
                {
                    oemKey.SetValue("Manufacturer", branding.OemInfo.Manufacturer ?? "Centro Universitário Paraíso - UNIFAP");
                    oemKey.SetValue("SupportPhone", branding.OemInfo.SupportPhone ?? "TI Institucional");
                    oemKey.SetValue("SupportURL", branding.OemInfo.SupportUrl ?? "https://www.unifap.edu.br");
                    _logger.LogInformation("BrandingService", "Informações OEM registradas com sucesso.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning("BrandingService", $"Não foi possível gravar OEMInformation (pode requerer elevação): {ex.Message}");
            }

            // 3. Criar Atalhos na Área de Trabalho Pública para todos os usuários
            await CreateDesktopShortcutsAsync();

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError("BrandingService", "Erro ao aplicar branding institucional", ex);
            return false;
        }
    }

    public async Task<int> CreateDesktopShortcutsAsync()
    {
        await Task.Yield();
        int createdCount = 0;

        try
        {
            string publicDesktop = Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory);
            if (string.IsNullOrWhiteSpace(publicDesktop) || !Directory.Exists(publicDesktop))
            {
                publicDesktop = @"C:\Users\Public\Desktop";
            }
            Directory.CreateDirectory(publicDesktop);

            string defaultDesktop = @"C:\Users\Default\Desktop";
            try { Directory.CreateDirectory(defaultDesktop); } catch { }

            string userDesktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);

            // Mapeamento de softwares essenciais com seus caminhos executáveis conhecidos
            var softwareDefinitions = new (string shortcutName, string[] candidatePaths)[]
            {
                ("Google Chrome.lnk", new[]
                {
                    @"C:\Program Files\Google\Chrome\Application\chrome.exe",
                    @"C:\Program Files (x86)\Google\Chrome\Application\chrome.exe"
                }),
                ("Mozilla Firefox.lnk", new[]
                {
                    @"C:\Program Files\Mozilla Firefox\firefox.exe",
                    @"C:\Program Files (x86)\Mozilla Firefox\firefox.exe"
                }),
                ("Word.lnk", new[]
                {
                    @"C:\Program Files\Microsoft Office\root\Office16\WINWORD.EXE",
                    @"C:\Program Files (x86)\Microsoft Office\root\Office16\WINWORD.EXE",
                    @"C:\Program Files\Microsoft Office\Office16\WINWORD.EXE",
                    @"C:\Program Files (x86)\Microsoft Office\Office16\WINWORD.EXE",
                    @"C:\Program Files\Microsoft Office\Office15\WINWORD.EXE"
                }),
                ("Excel.lnk", new[]
                {
                    @"C:\Program Files\Microsoft Office\root\Office16\EXCEL.EXE",
                    @"C:\Program Files (x86)\Microsoft Office\root\Office16\EXCEL.EXE",
                    @"C:\Program Files\Microsoft Office\Office16\EXCEL.EXE",
                    @"C:\Program Files (x86)\Microsoft Office\Office16\EXCEL.EXE",
                    @"C:\Program Files\Microsoft Office\Office15\EXCEL.EXE"
                }),
                ("PowerPoint.lnk", new[]
                {
                    @"C:\Program Files\Microsoft Office\root\Office16\POWERPNT.EXE",
                    @"C:\Program Files (x86)\Microsoft Office\root\Office16\POWERPNT.EXE",
                    @"C:\Program Files\Microsoft Office\Office16\POWERPNT.EXE",
                    @"C:\Program Files (x86)\Microsoft Office\Office16\POWERPNT.EXE",
                    @"C:\Program Files\Microsoft Office\Office15\POWERPNT.EXE"
                }),
                ("Adobe Acrobat.lnk", new[]
                {
                    @"C:\Program Files\Adobe\Acrobat DC\Acrobat\Acrobat.exe",
                    @"C:\Program Files\Adobe\Acrobat Reader 64-bit\Reader\AcroRd32.exe",
                    @"C:\Program Files (x86)\Adobe\Acrobat Reader DC\Reader\AcroRd32.exe",
                    @"C:\Program Files (x86)\Adobe\Reader 11.0\Reader\AcroRd32.exe"
                }),
                ("WinRAR.lnk", new[]
                {
                    @"C:\Program Files\WinRAR\WinRAR.exe",
                    @"C:\Program Files (x86)\WinRAR\WinRAR.exe"
                }),
                ("VLC media player.lnk", new[]
                {
                    @"C:\Program Files\VideoLAN\VLC\vlc.exe",
                    @"C:\Program Files (x86)\VideoLAN\VLC\vlc.exe"
                }),
                ("Visual Studio Code.lnk", new[]
                {
                    @"C:\Program Files\Microsoft VS Code\Code.exe",
                    @"C:\Program Files (x86)\Microsoft VS Code\Code.exe"
                })
            };

            // Criar atalhos baseados nos executáveis encontrados
            foreach (var item in softwareDefinitions)
            {
                string? targetExe = null;
                foreach (var candidate in item.candidatePaths)
                {
                    if (File.Exists(candidate))
                    {
                        targetExe = candidate;
                        break;
                    }
                }

                if (!string.IsNullOrEmpty(targetExe))
                {
                    string targetLnk = Path.Combine(publicDesktop, item.shortcutName);
                    if (CreateShortcut(targetLnk, targetExe))
                    {
                        createdCount++;
                        // Replicar no Desktop padrão e do usuário ativo
                        try { File.Copy(targetLnk, Path.Combine(defaultDesktop, item.shortcutName), true); } catch { }
                        if (!string.IsNullOrEmpty(userDesktop) && Directory.Exists(userDesktop))
                        {
                            try { File.Copy(targetLnk, Path.Combine(userDesktop, item.shortcutName), true); } catch { }
                        }
                    }
                }
            }

            // Varrer o Menu Iniciar em busca de atalhos já existentes dos pacotes instalados
            string startMenuPrograms = @"C:\ProgramData\Microsoft\Windows\Start Menu\Programs";
            if (Directory.Exists(startMenuPrograms))
            {
                var searchKeywords = new[] { "chrome", "firefox", "word", "excel", "powerpoint", "acrobat", "winrar", "vlc", "code", "autocad", "revit", "pycharm", "intellij", "android studio" };
                var lnkFiles = Directory.GetFiles(startMenuPrograms, "*.lnk", SearchOption.AllDirectories);

                foreach (var lnk in lnkFiles)
                {
                    string fileName = Path.GetFileName(lnk);
                    string lower = fileName.ToLowerInvariant();

                    foreach (var kw in searchKeywords)
                    {
                        if (lower.Contains(kw))
                        {
                            string destLnk = Path.Combine(publicDesktop, fileName);
                            if (!File.Exists(destLnk))
                            {
                                try
                                {
                                    File.Copy(lnk, destLnk, true);
                                    createdCount++;
                                    try { File.Copy(lnk, Path.Combine(defaultDesktop, fileName), true); } catch { }
                                    if (!string.IsNullOrEmpty(userDesktop) && Directory.Exists(userDesktop))
                                    {
                                        try { File.Copy(lnk, Path.Combine(userDesktop, fileName), true); } catch { }
                                    }
                                }
                                catch { }
                            }
                            break;
                        }
                    }
                }
            }

            _logger.LogInformation("BrandingService", $"Total de atalhos de programas configurados na Área de Trabalho Pública: {createdCount}");
        }
        catch (Exception ex)
        {
            _logger.LogWarning("BrandingService", $"Erro ao criar atalhos de desktop: {ex.Message}");
        }

        return createdCount;
    }

    private bool CreateShortcut(string shortcutPath, string targetPath)
    {
        try
        {
            Type? shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType != null)
            {
                dynamic shell = Activator.CreateInstance(shellType)!;
                dynamic shortcut = shell.CreateShortcut(shortcutPath);
                shortcut.TargetPath = targetPath;
                shortcut.WorkingDirectory = Path.GetDirectoryName(targetPath);
                shortcut.Save();
                return true;
            }
        }
        catch { }

        // Fallback via PowerShell
        try
        {
            string psCmd = $"$ws = New-Object -ComObject WScript.Shell; $s = $ws.CreateShortcut('{shortcutPath.Replace("'", "''")}'); $s.TargetPath = '{targetPath.Replace("'", "''")}'; $s.WorkingDirectory = '{Path.GetDirectoryName(targetPath)?.Replace("'", "''")}'; $s.Save();";
            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{psCmd}\"",
                CreateNoWindow = true,
                UseShellExecute = false
            };
            var proc = Process.Start(psi);
            proc?.WaitForExit(3000);
            return File.Exists(shortcutPath);
        }
        catch
        {
            return false;
        }
    }
}
