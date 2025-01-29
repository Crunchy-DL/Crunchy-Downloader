using System;
using System.Collections;
using System.Globalization;
using Avalonia.Data.Converters;

namespace CRD.Utils.UI;

public class UiListHasElementsConverter : IValueConverter{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture){
        if (value is IEnumerable enumerable){
            // Check if the collection has any elements
            foreach (var _ in enumerable){
                return true; // At least one element exists
            }

            return false; // No elements
        }

        // Return false if the input is not a collection or is null
        return false;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture){
        throw new NotSupportedException("ListToBooleanConverter does not support ConvertBack.");
    }
}