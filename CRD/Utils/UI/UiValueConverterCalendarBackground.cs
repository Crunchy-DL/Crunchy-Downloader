using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Avalonia.Styling;

namespace CRD.Utils.UI;

public class UiValueConverterCalendarBackground : IValueConverter{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture){
        if (value is bool boolValue){
            var currentThemeVariant = Application.Current?.RequestedThemeVariant;

            return boolValue ? currentThemeVariant == ThemeVariant.Dark ? new SolidColorBrush(Color.Parse("#583819")) : new SolidColorBrush(Color.Parse("#ffd8a1")) :
                currentThemeVariant == ThemeVariant.Dark ? new SolidColorBrush(Color.Parse("#353535")) : new SolidColorBrush(Color.Parse("#d7d7d7"));
            // return boolValue ? new SolidColorBrush(Color.Parse("#10f5d800")) : new SolidColorBrush(Color.Parse("#10FFFFFF"));
        }

        return new SolidColorBrush(Color.Parse("#10FFFFFF"));
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture){
        throw new NotImplementedException();
    }
}