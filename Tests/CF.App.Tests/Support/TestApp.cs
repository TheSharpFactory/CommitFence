using Avalonia;
using Avalonia.Headless;
using CF.App.Tests.Support;
using Xunit;

[assembly: AvaloniaTestApplication(typeof(TestApp))]
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace CF.App.Tests.Support;

/// <summary>
/// Runs the real app headless: Skia rendering, no window on screen.
/// </summary>
public static class TestApp
{
    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<CF.App.Shell.App>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
            .WithInterFont();
    }
}
