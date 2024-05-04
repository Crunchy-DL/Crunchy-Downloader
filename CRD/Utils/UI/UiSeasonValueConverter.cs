using System;
using System.Globalization;
using Avalonia.Data.Converters;
using FluentAvalonia.UI.Controls;

namespace CRD.Utils.UI;

public class UiSeasonValueConverter : IValueConverter{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture){

        if (value is string stringValue){
            var parsed = int.TryParse(stringValue, out int seasonNum);
            if (parsed)
                return $"Season {seasonNum}";
        }
  
        return "Specials";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture){
        throw new NotImplementedException();
    }
}