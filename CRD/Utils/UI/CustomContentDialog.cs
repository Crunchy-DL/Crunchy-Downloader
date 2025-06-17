using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using FluentAvalonia.UI.Controls;

namespace CRD.Utils.UI;

public class CustomContentDialog : ContentDialog{
    public static readonly StyledProperty<IImage> BackgroundImageProperty =
        AvaloniaProperty.Register<CustomContentDialog, IImage>(nameof(BackgroundImage));

    public static readonly StyledProperty<double> BackgroundImageOpacityProperty =
        AvaloniaProperty.Register<CustomContentDialog, double>(nameof(BackgroundImageOpacity), 0.5);

    public static readonly StyledProperty<double> BackgroundImageBlurRadiusProperty =
        AvaloniaProperty.Register<CustomContentDialog, double>(nameof(BackgroundImageBlurRadius), 10);

    public IImage BackgroundImage{
        get => GetValue(BackgroundImageProperty);
        set => SetValue(BackgroundImageProperty, value);
    }

    public double BackgroundImageOpacity{
        get => GetValue(BackgroundImageOpacityProperty);
        set => SetValue(BackgroundImageOpacityProperty, value);
    }

    public double BackgroundImageBlurRadius{
        get => GetValue(BackgroundImageBlurRadiusProperty);
        set => SetValue(BackgroundImageBlurRadiusProperty, value);
    }

    private Image? _backgroundImage;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e){
        base.OnApplyTemplate(e);

        _backgroundImage = e.NameScope.Find<Image>("BackgroundImageElement");

        if (_backgroundImage is not null){
            _backgroundImage.Effect = new BlurEffect{
                Radius = BackgroundImageBlurRadius
            };
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change){
        base.OnPropertyChanged(change);

        if (change.Property == BackgroundImageBlurRadiusProperty && _backgroundImage?.Effect is BlurEffect blur){
            blur.Radius = BackgroundImageBlurRadius;
        }
    }
}