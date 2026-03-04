using System;
using Avalonia;
using System.Linq;
using ReactiveUI.Avalonia;

namespace CRD;

sealed class Program{
    [STAThread]
    public static void Main(string[] args){
        var isHeadless = args.Contains("--headless");

        BuildAvaloniaApp(isHeadless).StartWithClassicDesktopLifetime(args);
    }
    
    public static AppBuilder BuildAvaloniaApp(bool isHeadless){
        var builder = AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace()
            .UseReactiveUI(_ => { });

        if (isHeadless){
            Console.WriteLine("Running in headless mode...");
        }

        return builder;
    }
}