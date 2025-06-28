using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace CRD.Utils.UI;

public class UiEnumToBoolConverter : IValueConverter{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture){
        if (value == null || parameter == null)
            return false;

        string enumString = parameter.ToString();
        if (enumString == null)
            return false;

        return value.ToString() == enumString;
    }

    public object ConvertBack(object value, Type targetType, object? parameter, CultureInfo culture){
        if ((bool)value && parameter != null){
            return Enum.Parse(targetType, parameter.ToString() ?? string.Empty);
        }

        return Avalonia.Data.BindingOperations.DoNothing;
    }
}