using Avalonia;
using Avalonia.Headless;
using CF.App.Parts;
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
        // no animation waits on the clock: the tests ask for less motion, so every length of the design's motion is 0
        Motion.Reduced = () => true;
        return AppBuilder.Configure<CF.App.Shell.App>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
            .WithInterFont();
    }
}
