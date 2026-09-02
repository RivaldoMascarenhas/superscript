using System;
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

                // Gravar registro do Windows (HKCU)
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

                // Aplicar via Windows Shell COM IDesktopWallpaper (Windows 11 / 10 nativo)
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

                // Fallback ou reforço via SystemParametersInfo
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

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError("BrandingService", "Erro ao aplicar branding institucional", ex);
            return false;
        }
    }
}
