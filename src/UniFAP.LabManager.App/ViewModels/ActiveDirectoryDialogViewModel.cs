using System.Security;
using System.Windows.Input;
using UniFAP.LabManager.Core.Interfaces;
using UniFAP.LabManager.Core.Models;

namespace UniFAP.LabManager.App.ViewModels;

public class ActiveDirectoryDialogViewModel : ViewModelBase
{
    private readonly IActiveDirectoryService _adService;
    private readonly ILogService _logger;

    private string _domain = "intranet.unifapce.edu.br";
    private string _domainController = string.Empty;
    private string _ouPath = "Padrão do Domínio (Automático)";
    private string _username = string.Empty;
    private string _password = string.Empty;
    private bool _isValidating = false;
    private bool _isValidated = false;
    private string _validationMessage = string.Empty;
    private bool _dialogResult = false;

    public string Domain
    {
        get => _domain;
        set => SetProperty(ref _domain, value);
    }

    public string DomainController
    {
        get => _domainController;
        set => SetProperty(ref _domainController, value);
    }

    public string OuPath
    {
        get => _ouPath;
        set => SetProperty(ref _ouPath, value);
    }

    public string Username
    {
        get => _username;
        set => SetProperty(ref _username, value);
    }

    public string Password
    {
        get => _password;
        set => SetProperty(ref _password, value);
    }

    public bool IsValidating
    {
        get => _isValidating;
        set => SetProperty(ref _isValidating, value);
    }

    public bool IsValidated
    {
        get => _isValidated;
        set => SetProperty(ref _isValidated, value);
    }

    public string ValidationMessage
    {
        get => _validationMessage;
        set => SetProperty(ref _validationMessage, value);
    }

    public bool DialogResult
    {
        get => _dialogResult;
        set => SetProperty(ref _dialogResult, value);
    }

    public ICommand ValidatePreRequisitesCommand { get; }
    public ICommand ConfirmCommand { get; }
    public ICommand CancelCommand { get; }

    public event Action<bool>? OnCloseRequested;

    public ActiveDirectoryDialogViewModel(IActiveDirectoryService adService, ILogService logger)
    {
        _adService = adService;
        _logger = logger;

        ValidatePreRequisitesCommand = new AsyncRelayCommand(ValidatePreRequisitesAsync);
        ConfirmCommand = new RelayCommand(Confirm, () => !string.IsNullOrWhiteSpace(Username) && !string.IsNullOrWhiteSpace(Password));
        CancelCommand = new RelayCommand(Cancel);
    }

    public void LoadConfig(ActiveDirectoryConfig config)
    {
        Domain = config.Domain;
        DomainController = config.DomainController;
        OuPath = string.IsNullOrWhiteSpace(config.ComputerOu) ? "Padrão do Domínio (Automático)" : config.ComputerOu;
        _ = ValidatePreRequisitesAsync();
    }

    public async Task ValidatePreRequisitesAsync()
    {
        IsValidating = true;
        ValidationMessage = "Testando conexão com servidores de domínio UniFAP...";
        try
        {
            var res = await _adService.ValidateDomainPreRequisitesAsync(Domain, DomainController);
            IsValidated = res.Success;
            ValidationMessage = res.Message;
        }
        catch (Exception ex)
        {
            IsValidated = false;
            ValidationMessage = $"Erro: {ex.Message}";
        }
        finally
        {
            IsValidating = false;
        }
    }

    private void Confirm()
    {
        DialogResult = true;
        OnCloseRequested?.Invoke(true);
    }

    private void Cancel()
    {
        DialogResult = false;
        OnCloseRequested?.Invoke(false);
    }
}
