using System;
using System.Globalization;
using Avalonia.Data.Converters;
using CRD.Downloader;
using CRD.Downloader.Crunchyroll;
using FluentAvalonia.UI.Controls;

namespace CRD.Utils.UI;

public class UiSonarrIdToVisibilityConverter : IValueConverter{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture){
        if (value is string stringValue){
            return CrunchyrollManager.Instance.CrunOptions.SonarrProperties != null && (stringValue.Length > 0 && CrunchyrollManager.Instance.CrunOptions.SonarrProperties.SonarrEnabled);
        }

        return false;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture){
        throw new NotImplementedException("This converter only works for one-way binding");
    }
}