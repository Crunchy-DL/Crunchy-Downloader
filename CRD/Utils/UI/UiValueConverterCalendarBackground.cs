using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using FluentAvalonia.UI.Controls;

namespace CRD.Utils.UI;

public class UiValueConverterCalendarBackground : IValueConverter{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture){
        if (value is bool boolValue){
            return boolValue ? new SolidColorBrush(Color.Parse("#10f5d800")) : new SolidColorBrush(Color.Parse("#10FFFFFF"));
        }

        return new SolidColorBrush(Color.Parse("#10FFFFFF"));
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture){
        throw new NotImplementedException();
    }
}