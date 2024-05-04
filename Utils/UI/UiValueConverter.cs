using System;
using System.Globalization;
using Avalonia.Data.Converters;
using FluentAvalonia.UI.Controls;

namespace CRD.Utils.UI;

public class UiValueConverter : IValueConverter{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture){
        if (value is bool boolValue){
            return boolValue ? Symbol.Pause : Symbol.Play;
        }

        return null; // Or return a default value
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture){
        if (value is Symbol sym)
        {
            return sym == Symbol.Pause;
        }
        return false;
    }
}