using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using UniFAP.LabManager.App.ViewModels;
using UniFAP.LabManager.App.Views;
using UniFAP.LabManager.Core.Interfaces;
using UniFAP.LabManager.Core.Models;

namespace UniFAP.LabManager.App;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly IServiceProvider _serviceProvider;

    public MainWindow(MainViewModel viewModel, IServiceProvider serviceProvider)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _serviceProvider = serviceProvider;
        DataContext = _viewModel;

        // Conectar evento do diálogo modal do Active Directory
        _viewModel.PreparationVM.OnPromptActiveDirectoryCredentials += PromptActiveDirectoryCredentialsAsync;

        // Conectar evento do diálogo modal de senha do usuário suporte
        _viewModel.MaintenanceVM.OnPromptSupportPassword += PromptSupportPasswordAsync;

        // Conectar navegações rápidas da Dashboard
        _viewModel.DashboardVM.OnNavigateToPreparationRequested += () => _viewModel.CurrentViewModel = _viewModel.PreparationVM;
        _viewModel.DashboardVM.OnNavigateToDiagnosticsRequested += () => _viewModel.CurrentViewModel = _viewModel.DiagnosticsVM;
        _viewModel.ExecutionVM.OnReturnToDashboardRequested += () => _viewModel.CurrentViewModel = _viewModel.DashboardVM;

        Loaded += async (s, e) => await _viewModel.InitializeAsync();
    }

    private Task<(bool success, string username, string password)> PromptActiveDirectoryCredentialsAsync(ActiveDirectoryConfig config)
    {
        var tcs = new TaskCompletionSource<(bool success, string username, string password)>();

        Dispatcher.Invoke(() =>
        {
            var adService = _serviceProvider.GetRequiredService<IActiveDirectoryService>();
            var logger = _serviceProvider.GetRequiredService<ILogService>();

            var vm = new ActiveDirectoryDialogViewModel(adService, logger);
            vm.LoadConfig(config);

            var dialog = new ActiveDirectoryDialog(vm)
            {
                Owner = this
            };

            bool? result = dialog.ShowDialog();
            if (result == true)
            {
                tcs.SetResult((true, vm.Username, vm.Password));
            }
            else
            {
                tcs.SetResult((false, string.Empty, string.Empty));
            }
        });

        return tcs.Task;
    }

    private Task<(bool success, string password)> PromptSupportPasswordAsync()
    {
        var tcs = new TaskCompletionSource<(bool success, string password)>();

        Dispatcher.Invoke(() =>
        {
            var dialog = new UserProvisionDialog
            {
                Owner = this
            };

            bool? result = dialog.ShowDialog();
            if (result == true)
            {
                tcs.SetResult((true, dialog.Password));
            }
            else
            {
                tcs.SetResult((false, string.Empty));
            }
        });

        return tcs.Task;
    }
}
