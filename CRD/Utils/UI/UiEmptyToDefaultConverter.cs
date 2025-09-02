using System.Globalization;
using Avalonia.Data.Converters;

namespace CRD.Utils.UI;

public class UiEmptyToDefaultConverter: IValueConverter{
    public object? Convert(object? value, System.Type targetType, object? parameter, CultureInfo culture){
        var s = value as string;
        var fallback = parameter as string ?? string.Empty;
        return string.IsNullOrEmpty(s) ? fallback : s!;
    }

    public object? ConvertBack(object? value, System.Type targetType, object? parameter, CultureInfo culture) => value!;
}