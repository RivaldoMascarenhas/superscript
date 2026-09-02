using System.Windows;
using UniFAP.LabManager.App.ViewModels;

namespace UniFAP.LabManager.App.Views;

public partial class ActiveDirectoryDialog : Window
{
    private readonly ActiveDirectoryDialogViewModel _viewModel;

    public ActiveDirectoryDialog(ActiveDirectoryDialogViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;
        _viewModel.OnCloseRequested += (result) =>
        {
            this.DialogResult = result;
            this.Close();
        };
    }

    private void PwdBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        _viewModel.Password = pwdBox.Password;
    }
}
