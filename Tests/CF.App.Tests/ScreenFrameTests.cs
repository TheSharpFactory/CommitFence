using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using CF.App.Parts;
using CF.App.Shell;
using CF.App.Tests.Support;
using CF.Core.Updates;
using Xunit;

namespace CF.App.Tests;

/// <summary>
/// The rail and the frame of the five screens that come after Rules and Review: the seven screens by name, the frame's two columns
/// and its one, and the lines of a file that wrap.
/// </summary>
public sealed class ScreenFrameTests
{
    private static readonly PhysicalKey[] _digits = [PhysicalKey.Digit1, PhysicalKey.Digit2, PhysicalKey.Digit3, PhysicalKey.Digit4, PhysicalKey.Digit5, PhysicalKey.Digit6, PhysicalKey.Digit7];

    [AvaloniaFact]
    public void RailOpensEachOfTheSevenScreens()
    {
        using var shell = new ShellHost();
        using var host = new ViewHost(new MainWindow { DataContext = shell.Model, Width = 1440, Height = 900 });
        var rail = host.Find<ListBox>(list => list.Name == "Rail")!;

        // seven items, none drawn disabled
        Assert.Equal(7, rail.ItemCount);
        Assert.All(rail.Items.OfType<ListBoxItem>(), item => Assert.True(item.IsEnabled));

        var screens = Enum.GetValues<Screen>();
        for(var index = 0; index < screens.Length; index++)
        {
            // by selection, from the top group and the bottom group alike
            rail.SelectedIndex = index;
            host.Pump();
            Assert.Equal(screens[index], shell.Model.CurrentScreen);
            Assert.Equal(index, rail.SelectedIndex);
            Assert.Single(host.FindAll<Control>(control => control.Name?.EndsWith("Screen", StringComparison.Ordinal) == true && control.IsEffectivelyVisible && control.GetType().Name.EndsWith("View", StringComparison.Ordinal)));
        }

        // and by Ctrl+1 to Ctrl+7, which the keys name in the rail's order
        shell.Model.CurrentScreen = Screen.Rules;
        for(var index = screens.Length - 1; index >= 0; index--)
        {
            host.Press(_digits[index], RawInputModifiers.Control);
            Assert.Equal(screens[index], shell.Model.CurrentScreen);
        }
    }

    [AvaloniaFact]
    public async Task FrameHasThePanelBesideTheMainColumnFrom1304AndOneColumnUnder()
    {
        using var shell = new ShellHost();
        shell.Stats.Report = DashboardPictures.Month();
        shell.Model.CurrentScreen = Screen.Dashboard;
        await shell.Model.Dashboard.AgainCommand.ExecutionTask!;
        var window = new MainWindow { DataContext = shell.Model, Width = 1440, Height = 900 };
        using var host = new ViewHost(window);
        var frame = host.Find<ScreenFrame>(candidate => candidate.IsEffectivelyVisible)!;
        frame.Main = new Border { Height = 200 };
        frame.Panel = new TextBlock { Text = "A panel", Height = 100 };
        host.Pump();

        // two columns: the main column takes the rest (916 at 1440), the panel is 340 and stands to its right
        var main = frame.Regions[1];
        var panel = frame.Regions[2];
        Assert.False(frame.IsOnePane);
        Assert.Equal(916, main.Bounds.Width, 1);
        Assert.Equal(340, panel.Bounds.Width, 1);
        Assert.True(panel.TranslatePoint(new Point(0, 0), frame)!.Value.X >= main.TranslatePoint(new Point(main.Bounds.Width, 0), frame)!.Value.X);

        // one column of 624 at 760, the panel under the main column's content, as wide as it
        window.Width = 760;
        host.Pump();
        Assert.True(frame.IsOnePane);
        Assert.Equal(624, panel.Bounds.Width, 1);
        Assert.Equal(main.TranslatePoint(new Point(0, 0), frame)!.Value.X, panel.TranslatePoint(new Point(0, 0), frame)!.Value.X, 1);
        Assert.True(panel.TranslatePoint(new Point(0, 0), frame)!.Value.Y >= main.TranslatePoint(new Point(0, 200), frame)!.Value.Y);

        // at the window's smallest (760 by 560) with a failed update, the notice takes its own row and every button of it is in view
        // and reached by Tab
        window.Height = 560;
        shell.Updater.Result = FakeUpdater.Available("9.0.0.0");
        shell.Updater.InstallFailure = new UpdateException("The download does not match the signed list of checksums, so nothing was changed.");
        await shell.Model.Updates.CheckAsync(false);
        await shell.Model.Updates.InstallAsync();
        host.Pump();
        var names = new[] { "CloseButton", "ReleasePageButton", "TryAgainButton" };
        var buttons = names.Select(name => host.Find<Button>(button => button.Name == name && button.IsEffectivelyVisible)!).ToList();
        Assert.All(buttons, button =>
        {
            var top = button.TranslatePoint(new Point(0, 0), window)!.Value.Y;
            Assert.True(top >= 0 && top + button.Bounds.Height <= window.Bounds.Height, $"{button.Name} is out of view");
        });
        var reached = new HashSet<string?>();
        for(var tab = 0; tab < 40; tab++)
        {
            host.Press(PhysicalKey.Tab);
            reached.Add((window.FocusManager?.GetFocusedElement() as Control)?.Name);
        }

        Assert.All(names, name => Assert.Contains(name, reached));
    }
}
