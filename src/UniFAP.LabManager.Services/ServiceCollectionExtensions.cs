using Microsoft.Extensions.DependencyInjection;
using UniFAP.LabManager.Core.Interfaces;
using UniFAP.LabManager.Infrastructure.Execution;
using UniFAP.LabManager.Infrastructure.Logging;
using UniFAP.LabManager.Infrastructure.Persistence;
using UniFAP.LabManager.Infrastructure.Security;
using UniFAP.LabManager.Infrastructure.SystemAdapters;
using UniFAP.LabManager.Services.ActiveDirectory;
using UniFAP.LabManager.Services.Branding;
using UniFAP.LabManager.Services.Configuration;
using UniFAP.LabManager.Services.Diagnostics;
using UniFAP.LabManager.Services.Orchestration;
using UniFAP.LabManager.Services.Performance;
using UniFAP.LabManager.Services.PreCheck;
using UniFAP.LabManager.Services.Reporting;
using UniFAP.LabManager.Services.Catalog;
using UniFAP.LabManager.Services.Software;
using UniFAP.LabManager.Services.Software.Installers;
using UniFAP.LabManager.Services.Theme;
using UniFAP.LabManager.Services.Users;
using UniFAP.LabManager.Services.Windows;

namespace UniFAP.LabManager.Services;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddUniFapLabManagerServices(this IServiceCollection services)
    {
        // Infrastructure
        services.AddSingleton<ISecurityService, SecurityService>();
        services.AddSingleton<ILogService, MaskedLogManager>();
        services.AddSingleton<ProcessRunner>();
        services.AddSingleton<PowerShellRunner>();
        services.AddSingleton<IWingetService, WingetRunner>();
        services.AddSingleton<ILocalInstallerService, LocalInstallerService>();
        services.AddSingleton<WmiAdapter>();
        services.AddSingleton<RegistryAdapter>();
        services.AddSingleton<JobPersistenceStore>();

        // Domain & Application Services
        services.AddSingleton<IConfigService, ConfigService>();
        services.AddSingleton<ICatalogSyncService, CatalogSyncService>();
        services.AddSingleton<IThemeService, ThemeService>();
        services.AddSingleton<IPreCheckService, PreCheckService>();
        services.AddSingleton<IWindowsConfigurationService, WindowsConfigurationService>();
        services.AddSingleton<IUserService, UserService>();
        services.AddSingleton<IPerformanceService, PerformanceService>();
        services.AddSingleton<IBrandingService, BrandingService>();

        // Modular Software Installers
        services.AddSingleton<ISoftwareInstaller, OfficeInstaller>();
        services.AddSingleton<ISoftwareInstaller, WingetInstaller>();
        services.AddSingleton<ISoftwareInstaller, MsiInstaller>();
        services.AddSingleton<ISoftwareInstaller, ExeInstaller>();
        services.AddSingleton<ISoftwareInstaller, ScriptInstaller>();

        // Software Engine & Catalog Service
        services.AddSingleton<SoftwareEngine>();
        services.AddSingleton<ISoftwareService>(sp => sp.GetRequiredService<SoftwareEngine>());
        services.AddSingleton<ISoftwareCatalogService>(sp => sp.GetRequiredService<SoftwareEngine>());

        services.AddSingleton<IActiveDirectoryService, ActiveDirectoryService>();
        services.AddSingleton<IDiagnosticsService, DiagnosticsService>();
        services.AddSingleton<IReportService, ReportService>();
        services.AddSingleton<IJobOrchestrator, JobOrchestrator>();

        return services;
    }
}
