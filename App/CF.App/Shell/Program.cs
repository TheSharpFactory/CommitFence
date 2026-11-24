using Avalonia;
using Avalonia.Fonts.Inter;

namespace CF.App.Shell;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
    }
}