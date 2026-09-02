using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using UniFAP.LabManager.App.ViewModels;
using UniFAP.LabManager.Services;

namespace UniFAP.LabManager.App;

public partial class App : Application
{
    private ServiceProvider? _serviceProvider;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var services = new ServiceCollection();

        // Registrar Serviços de Infraestrutura e Domínio
        services.AddUniFapLabManagerServices();

        // Registrar ViewModels
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<DashboardViewModel>();
        services.AddSingleton<PreparationViewModel>();
        services.AddSingleton<ExecutionViewModel>();
        services.AddSingleton<SoftwareCatalogViewModel>();
        services.AddSingleton<MaintenanceViewModel>();
        services.AddSingleton<DiagnosticsViewModel>();
        services.AddSingleton<HistoryViewModel>();
        services.AddSingleton<SettingsViewModel>();
        services.AddSingleton<AboutViewModel>();

        // Registrar Views
        services.AddSingleton<MainWindow>();

        _serviceProvider = services.BuildServiceProvider();

        var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _serviceProvider?.Dispose();
        base.OnExit(e);
    }
}
