using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Data.Converters;
using CRD.Utils.Structs.History;

namespace CRD.Utils.UI;

public class UiUpdateDownloadedHistorySeasonConverter : IMultiValueConverter{
    
    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture){

        if (values[0] is string stringValue1){
            Console.WriteLine(stringValue1);
        }
        
        if (values is[string stringValue, HistorySeries historySeries]){

            return new UpdateDownloadedHistorySeason{
                EpisodeId = stringValue,
                HistorySeries = historySeries
            };
        }
        
        return new UpdateDownloadedHistorySeason{
            EpisodeId = "",
            HistorySeries = null
        };
    }
    
}