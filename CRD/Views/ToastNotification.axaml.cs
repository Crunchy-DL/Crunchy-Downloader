using System;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;

namespace CRD.Views;

public partial class ToastNotification : UserControl{
    public ToastNotification(){
        InitializeComponent();
    }

    private void InitializeComponent(){
        AvaloniaXamlLoader.Load(this);
    }

    public void Show(string message, ToastType type, int durationInSeconds){
        this.FindControl<TextBlock>("MessageText").Text = message;
        SetStyle(type);
        DispatcherTimer timer = new DispatcherTimer{ Interval = TimeSpan.FromSeconds(durationInSeconds) };
        timer.Tick += (sender, args) => {
            timer.Stop();
            this.IsVisible = false;
        };
        timer.Start();
        this.IsVisible = true;
    }

    private void SetStyle(ToastType type){
        var border = this.FindControl<Border>("MessageBorder");
        border.Classes.Clear(); // Clear previous styles
        switch (type){
            case ToastType.Information:
                border.Classes.Add("info");
                break;
            case ToastType.Error:
                border.Classes.Add("error");
                break;
            case ToastType.Warning:
                border.Classes.Add("warning");
                break;
        }
    }
}

public enum ToastType{
    Information,
    Error,
    Warning
}