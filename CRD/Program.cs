using System;
using Avalonia;
using System.Linq;

namespace CRD;

sealed class Program{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args){
        var isHeadless = args.Contains("--headless");

        BuildAvaloniaApp(isHeadless).StartWithClassicDesktopLifetime(args);
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    // public static AppBuilder BuildAvaloniaApp()
    //     => AppBuilder.Configure<App>()
    //         .UsePlatformDetect()
    //         .WithInterFont()
    //         .LogToTrace();

    public static AppBuilder BuildAvaloniaApp(bool isHeadless){
        var builder = AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();

        if (isHeadless){
            Console.WriteLine("Running in headless mode...");
        }

        return builder;
    }
}