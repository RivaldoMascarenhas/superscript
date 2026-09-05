using System.Windows.Input;
using UniFAP.LabManager.Core.Enums;
using UniFAP.LabManager.Core.Interfaces;
using UniFAP.LabManager.Core.Models;

namespace UniFAP.LabManager.App.ViewModels;

public class DiagnosticItemViewModel : ViewModelBase
{
    private readonly DiagnosticCheckResult _model;
    private readonly IDiagnosticsService _diagnosticsService;
    private readonly ILogService _logger;

    private HealthStatus _status;
    private string _value;
    private string _message;
    private string? _resolutionHint;
    private bool _isRemediating;
    private string? _remediationFeedback;

    public event Action? OnStatusChanged;

    public DiagnosticItemViewModel(
        DiagnosticCheckResult model,
        IDiagnosticsService diagnosticsService,
        ILogService logger)
    {
        _model = model;
        _diagnosticsService = diagnosticsService;
        _logger = logger;

        _status = model.Status;
        _value = model.Value;
        _message = model.Message;
        _resolutionHint = model.ResolutionHint;

        FixCommand = new AsyncRelayCommand(ExecuteFixAsync, () => CanAutoRemediate && !IsRemediating);
    }

    public string Category => _model.Category;
    public string Name => _model.Name;
    public string? RemediationAction => _model.RemediationAction;
    public string RemediationTitle => _model.RemediationTitle ?? "Corrigir";
    public bool CanAutoRemediate => _model.CanAutoRemediate;

    public HealthStatus Status
    {
        get => _status;
        set
        {
            if (SetProperty(ref _status, value))
            {
                OnPropertyChanged(nameof(ShowRemediationButton));
                OnStatusChanged?.Invoke();
            }
        }
    }

    public string Value
    {
        get => _value;
        set => SetProperty(ref _value, value);
    }

    public string Message
    {
        get => _message;
        set => SetProperty(ref _message, value);
    }

    public string? ResolutionHint
    {
        get => _resolutionHint;
        set => SetProperty(ref _resolutionHint, value);
    }

    public bool IsRemediating
    {
        get => _isRemediating;
        set
        {
            if (SetProperty(ref _isRemediating, value))
            {
                OnPropertyChanged(nameof(ShowRemediationButton));
                (FixCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
            }
        }
    }

    public string? RemediationFeedback
    {
        get => _remediationFeedback;
        set => SetProperty(ref _remediationFeedback, value);
    }

    public bool ShowRemediationButton => CanAutoRemediate && Status != HealthStatus.Good && !IsRemediating;

    public ICommand FixCommand { get; }

    public async Task<DiagnosticRemediationResult?> ExecuteFixAsync()
    {
        if (string.IsNullOrWhiteSpace(RemediationAction) || IsRemediating)
            return null;

        IsRemediating = true;
        RemediationFeedback = "Aplicando correção...";

        try
        {
            _logger.LogInformation("DiagnosticItemViewModel", $"Iniciando auto-remediação para {Name} ({RemediationAction})...");
            var result = await _diagnosticsService.RemediateCheckAsync(RemediationAction);

            if (result.Success)
            {
                Status = result.NewStatus;
                if (!string.IsNullOrEmpty(result.NewValue))
                {
                    Value = result.NewValue;
                }

                if (result.NewStatus == HealthStatus.Good)
                {
                    ResolutionHint = null;
                }

                Message = result.Message;
                RemediationFeedback = $"✓ {result.Message}";
                _logger.LogInformation("DiagnosticItemViewModel", $"Correção aplicada com sucesso para {Name}: {result.Message}");
            }
            else
            {
                RemediationFeedback = $"⚠️ {result.Message}";
                _logger.LogWarning("DiagnosticItemViewModel", $"Correção incompleta ou aviso para {Name}: {result.Message}");
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError("DiagnosticItemViewModel", $"Erro ao executar correção de {Name}", ex);
            RemediationFeedback = $"✕ Falha: {ex.Message}";
            return new DiagnosticRemediationResult { Success = false, Message = ex.Message, NewStatus = Status };
        }
        finally
        {
            IsRemediating = false;
        }
    }
}
