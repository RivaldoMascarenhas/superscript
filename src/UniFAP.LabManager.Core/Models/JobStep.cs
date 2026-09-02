using System.ComponentModel;
using System.Runtime.CompilerServices;
using UniFAP.LabManager.Core.Enums;

namespace UniFAP.LabManager.Core.Models;

public class JobStep : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public StepType Type { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    private StepStatus _status = StepStatus.Pending;
    public StepStatus Status
    {
        get => _status;
        set
        {
            if (_status != value)
            {
                _status = value;
                OnPropertyChanged();
            }
        }
    }

    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public TimeSpan? Duration => (StartTime.HasValue && EndTime.HasValue) ? EndTime.Value - StartTime.Value : null;

    private string? _errorMessage;
    public string? ErrorMessage
    {
        get => _errorMessage;
        set
        {
            if (_errorMessage != value)
            {
                _errorMessage = value;
                OnPropertyChanged();
            }
        }
    }

    private string? _details;
    public string? Details
    {
        get => _details;
        set
        {
            if (_details != value)
            {
                _details = value;
                OnPropertyChanged();
            }
        }
    }

    public bool IsCritical { get; set; } = true;
}
