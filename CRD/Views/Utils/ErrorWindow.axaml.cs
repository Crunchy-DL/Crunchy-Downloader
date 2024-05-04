using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace CRD.Views.Utils;

public partial class ErrorWindow : Window{
    public ErrorWindow(){
        InitializeComponent();
    }

    private void Close_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e){
        Close();
    }

    private void InitializeComponent(){
        AvaloniaXamlLoader.Load(this);
    }

    public void SetErrorMessage(string message){
        this.FindControl<TextBlock>("ErrorMessage").Text = message;
    }
}