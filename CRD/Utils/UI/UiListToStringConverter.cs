using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia.Data.Converters;

namespace CRD.Utils.UI;

public class UiListToStringConverter : IValueConverter{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture){
        if (value is List<string> list){
            return string.Join(", ", list);
        }
        
        return "";
    }

 
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture){
        if (value is string str){
            return str.Split(new[]{ ", " }, StringSplitOptions.None).ToList();
        }

        return new List<string>();
    }
}
