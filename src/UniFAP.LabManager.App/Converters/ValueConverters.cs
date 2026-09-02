using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using UniFAP.LabManager.Core.Enums;

namespace UniFAP.LabManager.App.Converters;

public class BooleanToVisibilityConverter : IValueConverter
{
    public bool Invert { get; set; } = false;

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool boolVal = value is bool b && b;
        if (Invert) boolVal = !boolVal;
        return boolVal ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is Visibility v && v == Visibility.Visible;
    }
}

public class StepStatusToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is StepStatus stepStatus)
        {
            return stepStatus switch
            {
                StepStatus.Succeeded => new SolidColorBrush(Color.FromRgb(16, 185, 129)), // Emerald
                StepStatus.Running => new SolidColorBrush(Color.FromRgb(37, 99, 235)),   // Primary Blue
                StepStatus.Warning => new SolidColorBrush(Color.FromRgb(245, 158, 11)),  // Amber
                StepStatus.Failed => new SolidColorBrush(Color.FromRgb(239, 68, 68)),    // Red
                StepStatus.Skipped => new SolidColorBrush(Color.FromRgb(148, 163, 184)), // Slate
                _ => new SolidColorBrush(Color.FromRgb(100, 116, 139))                   // Muted
            };
        }

        if (value is SoftwareInstallStatus swStatus)
        {
            return swStatus switch
            {
                SoftwareInstallStatus.Installed => new SolidColorBrush(Color.FromRgb(16, 185, 129)),
                SoftwareInstallStatus.Installing => new SolidColorBrush(Color.FromRgb(37, 99, 235)),
                SoftwareInstallStatus.Warning => new SolidColorBrush(Color.FromRgb(245, 158, 11)),
                SoftwareInstallStatus.Failed => new SolidColorBrush(Color.FromRgb(239, 68, 68)),
                _ => new SolidColorBrush(Color.FromRgb(100, 116, 139))
            };
        }

        if (value is HealthStatus health)
        {
            return health switch
            {
                HealthStatus.Good => new SolidColorBrush(Color.FromRgb(16, 185, 129)),
                HealthStatus.Warning => new SolidColorBrush(Color.FromRgb(245, 158, 11)),
                HealthStatus.Critical => new SolidColorBrush(Color.FromRgb(239, 68, 68)),
                _ => new SolidColorBrush(Color.FromRgb(148, 163, 184))
            };
        }

        return new SolidColorBrush(Color.FromRgb(100, 116, 139));
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}

public class StepStatusToIconConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is StepStatus stepStatus)
        {
            return stepStatus switch
            {
                StepStatus.Succeeded => "✓",
                StepStatus.Running => "⏳",
                StepStatus.Warning => "⚠",
                StepStatus.Failed => "✗",
                StepStatus.Skipped => "○",
                _ => "○"
            };
        }

        if (value is SoftwareInstallStatus swStatus)
        {
            return swStatus switch
            {
                SoftwareInstallStatus.Installed => "✓",
                SoftwareInstallStatus.Installing => "⏳",
                SoftwareInstallStatus.Warning => "⚠",
                SoftwareInstallStatus.Failed => "✗",
                _ => "○"
            };
        }

        if (value is PreCheckStatus pcStatus)
        {
            return pcStatus switch
            {
                PreCheckStatus.Passed => "✓",
                PreCheckStatus.Warning => "⚠",
                PreCheckStatus.Failed => "✗",
                _ => "○"
            };
        }

        return "○";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}

public class InverseBooleanConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is bool b ? !b : true;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is bool b ? !b : false;
    }
}
