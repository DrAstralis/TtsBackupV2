using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace TtsBackup.Wpf.Converters;

public sealed class DepthToMarginConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int depth)
        {
            var left = depth * 12.0;
            return new Thickness(left, 6, 0, 6);
        }

        return new Thickness(0, 6, 0, 6);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}
