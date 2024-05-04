using System;
using System.Globalization;
using Avalonia.Data.Converters;
using FluentAvalonia.UI.Controls;

namespace CRD.Utils.UI;

public class UiIntToVisibilityConverter : IValueConverter{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture){
        if (value is int intValue){
            // Return Visible if intValue is greater than or equal to 1, otherwise Collapsed
            return intValue >= 1 ? true : false;
        }

        return false;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture){
        throw new NotImplementedException("This converter only works for one-way binding");
    }
}