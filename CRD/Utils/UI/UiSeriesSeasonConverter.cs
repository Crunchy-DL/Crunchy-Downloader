using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Data.Converters;
using CRD.Utils.Structs;
using CRD.Utils.Structs.History;

namespace CRD.Utils.UI;

public class UiSeriesSeasonConverter : IMultiValueConverter{
    public object Convert(IList<object> values, Type targetType, object parameter, CultureInfo culture){
        var series = values.Count > 0 && values[0] is HistorySeries hs ? hs : null;
        var season = values.Count > 1 && values[1] is HistorySeason hsn ? hsn : null;
        return new SeasonDialogArgs(series, season);
    }

    public IList<object> ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}