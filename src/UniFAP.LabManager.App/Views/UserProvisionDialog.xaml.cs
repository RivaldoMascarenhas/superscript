using System.Windows;

namespace UniFAP.LabManager.App.Views;

public partial class UserProvisionDialog : Window
{
    public string Password { get; private set; } = string.Empty;

    public UserProvisionDialog()
    {
        InitializeComponent();
        txtPassword.Focus();
    }

    private void ConfirmButton_Click(object sender, RoutedEventArgs e)
    {
        string pass = txtPassword.Password;
        string confirm = txtConfirmPassword.Password;

        if (string.IsNullOrWhiteSpace(pass))
        {
            txtError.Text = "⚠️ A senha do administrador 'suporte' não pode estar em branco.";
            txtError.Visibility = Visibility.Visible;
            txtPassword.Focus();
            return;
        }

        if (pass.Length < 4)
        {
            txtError.Text = "⚠️ A senha deve conter pelo menos 4 caracteres.";
            txtError.Visibility = Visibility.Visible;
            txtPassword.Focus();
            return;
        }

        if (pass != confirm)
        {
            txtError.Text = "⚠️ A senha e a confirmação de senha não coincidem.";
            txtError.Visibility = Visibility.Visible;
            txtConfirmPassword.Focus();
            return;
        }

        Password = pass;
        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
