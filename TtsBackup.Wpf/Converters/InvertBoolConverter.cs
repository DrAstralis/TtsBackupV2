using System;
using System.Globalization;
using System.Windows.Data;

namespace TtsBackup.Wpf.Converters;

/// <summary>
/// Converts a bool to its negation. Intended for simple XAML bindings like IsEnabled.
/// </summary>
public sealed class InvertBoolConverter : IValueConverter
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
