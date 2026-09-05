using System.Windows.Controls;
using UniFAP.LabManager.App.ViewModels;

namespace UniFAP.LabManager.App.Views;

public partial class MaintenanceView : UserControl
{
    public MaintenanceView()
    {
        InitializeComponent();
        DataContextChanged += (s, e) =>
        {
            if (e.NewValue is MaintenanceViewModel vm)
            {
                vm.OnLogAppended += () => Dispatcher.Invoke(() => MaintenanceLogScrollViewer?.ScrollToEnd());
            }
        };
    }
}
