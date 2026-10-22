using System.Globalization;
using System.Windows.Data;

namespace AionMeter.App.Controls;

/// <summary>Presentation only: rate suffix and bar width relative to the first displayed row.</summary>
public sealed class OverlayDisplayConverter : IValueConverter, IMultiValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not string text) return value;
        return parameter as string == "clock" ? text.PadLeft(5, '0')
            : text.Replace("/s", "/S", StringComparison.Ordinal);
    }

    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture) =>
        values.Length == 3 && values[0] is double fill && values[1] is double first
            && values[2] is double width && first > 0
            ? Math.Max(0, width) * Math.Clamp(fill / first, 0, 1) : 0d;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
