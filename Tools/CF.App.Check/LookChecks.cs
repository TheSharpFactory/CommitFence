using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Styling;
using Avalonia.VisualTree;
using CF.App.Rules;

namespace CF.App.Check;

/// <summary>
/// The colours of the drawn window against the app's colour tokens, and the pictures of the screens.
/// </summary>
internal sealed class LookChecks
{
    private readonly CheckContext _context;

    public LookChecks(CheckContext context)
    {
        _context = context;
    }

    public void Run()
    {
        _context.Guarded("colours", "the colours of the drawn window equal the design's tokens, light and dark", ColoursAreTheTokens);
        _context.Guarded("pictures", "the pictures are saved at 1440 by 900 and 760 by 1000", SavePictures);
    }

    public static (int Width, int Height) PngSize(string path)
    {
        var bytes = File.ReadAllBytes(path);
        var width = (bytes[16] << 24) | (bytes[17] << 16) | (bytes[18] << 8) | bytes[19];
        var height = (bytes[20] << 24) | (bytes[21] << 16) | (bytes[22] << 8) | bytes[23];

        return (width, height);
    }

    private static Control ContainerOf(AppWindow app, string id)
    {
        var row = app.Rules.Items.OfType<TeamRuleRowViewModel>().First(candidate => candidate.Id == id);
        return app.Rows.ContainerFromItem(row) ?? throw new InvalidOperationException("the row of " + id + " is not drawn");
    }

    private static (int X, int Y) Where(AppWindow app, Visual visual, double x, double y)
    {
        var point = visual.TranslatePoint(new Point(x, y), app.Window) ?? throw new InvalidOperationException("not on the window");
        return ((int)point.X, (int)point.Y);
    }

    private void ColoursAreTheTokens()
    {
        var folder = _context.Folders.NewFolder();
        using var app = _context.Open(folder);
        var team010 = app.Rules.Items.OfType<TeamRuleRowViewModel>().First(row => row.Id == "TEAM010");
        app.Rules.SelectedItem = team010;
        app.Pump(600);
        CheckThemeColours(app, "light", "#F5F5F5", "#FFFFFF", "#2450E6", "#1C1C1C", "#FFFFFF", "#767676");

        Application.Current!.RequestedThemeVariant = ThemeVariant.Dark;
        app.Pump(600);
        CheckThemeColours(app, "dark", "#202020", "#262626", "#8FA8FF", "#EDEDED", "#1F1F1F", "#8C8C8C");
        Application.Current.RequestedThemeVariant = ThemeVariant.Light;
        app.Pump(300);
    }

    /// <summary>
    /// The values written here are the app's tokens, for light then dark: side, page, here,
    /// on, on-knob and off.
    /// </summary>
    private void CheckThemeColours(AppWindow app, string theme, string side, string page, string here, string on, string onKnob, string off)
    {
        using var frame = app.Capture();
        var rows = app.Rows;
        var bottom = Where(app, rows, rows.Bounds.Width / 2, rows.Bounds.Height - 24);
        var pagePane = app.Find<Border>(border => border.Name == "PagePane").First();
        var pageAt = Where(app, pagePane, pagePane.Bounds.Width / 2, pagePane.Bounds.Height / 2);
        var team010 = ContainerOf(app, "TEAM010");
        var bar = team010.GetVisualDescendants().OfType<Border>().First(border => border.Name == "Bar");
        var barAt = Where(app, bar, bar.Bounds.Width / 2, bar.Bounds.Height / 2);
        var onSwitch = team010.GetVisualDescendants().OfType<ToggleButton>().First();
        var onAt = Where(app, onSwitch, 7, onSwitch.Bounds.Height / 2);
        var knobOn = onSwitch.GetVisualDescendants().OfType<Border>().First(border => border.Name == "Knob");
        var knobOnAt = Where(app, knobOn, knobOn.Bounds.Width / 2, knobOn.Bounds.Height / 2);
        var offSwitch = ContainerOf(app, "TEAM012").GetVisualDescendants().OfType<ToggleButton>().First();
        var knobOff = offSwitch.GetVisualDescendants().OfType<Border>().First(border => border.Name == "Knob");
        var knobOffAt = Where(app, knobOff, knobOff.Bounds.Width / 2, knobOff.Bounds.Height / 2);

        var seen = new[]
        {
            ("the list column (side)", side, AppWindow.PixelAt(frame, bottom.X, bottom.Y)),
            ("the page (page)", page, AppWindow.PixelAt(frame, pageAt.X, pageAt.Y)),
            ("the bar of the selected row (here)", here, AppWindow.PixelAt(frame, barAt.X, barAt.Y)),
            ("the track of a switch that is on (on)", on, AppWindow.PixelAt(frame, onAt.X, onAt.Y)),
            ("the knob of a switch that is on (on-knob)", onKnob, AppWindow.PixelAt(frame, knobOnAt.X, knobOnAt.Y)),
            ("the knob of a switch that is off (off)", off, AppWindow.PixelAt(frame, knobOffAt.X, knobOffAt.Y))
        };
        foreach(var (name, expected, actual) in seen)
        {
            _context.Report.Check("colours", $"{theme}: {name} is {expected}", string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase), "the pixel is " + actual);
        }
    }

    private void SavePictures()
    {
        var folder = _context.Folders.NewFolder();
        var pictures = _context.Options.PicturesFolder;
        using var app = _context.Open(folder, 1440, 900);
        var team010 = app.Rules.Items.OfType<TeamRuleRowViewModel>().First(row => row.Id == "TEAM010");
        var saved = new List<(string Path, int Width, int Height)>();

        app.Rules.SelectedItem = team010;
        app.Pump(500);
        saved.Add((app.Save(pictures, "app-rules-1440.png"), 1440, 900));
        team010.OpenEffectMenu();
        app.Pump(300);
        saved.Add((app.Save(pictures, "app-rules-effect-menu-1440.png"), 1440, 900));
        team010.CloseEffectMenu();
        KeyboardFocus(app, team010);
        saved.Add((app.Save(pictures, "app-rules-focus-1440.png"), 1440, 900));
        app.Press(PhysicalKey.ArrowRight);
        app.Press(PhysicalKey.ArrowRight);
        saved.Add((app.Save(pictures, "app-rules-focus-switch-1440.png"), 1440, 900));
        app.Press(PhysicalKey.ArrowLeft);
        app.Press(PhysicalKey.ArrowLeft);

        app.Window.Focus();
        Application.Current!.RequestedThemeVariant = ThemeVariant.Dark;
        app.Pump(500);
        saved.Add((app.Save(pictures, "app-rules-dark-1440.png"), 1440, 900));
        Application.Current.RequestedThemeVariant = ThemeVariant.Light;
        app.Pump(300);

        app.Model.ShowReview();
        app.Pump(300);
        saved.Add((app.Save(pictures, "app-review-1440.png"), 1440, 900));
        app.Model.ShowRules();
        app.Pump(300);

        app.Resize(760, 1000);
        saved.Add((app.Save(pictures, "app-rules-760.png"), 760, 1000));
        team010.OpenEffectMenu();
        app.Pump(300);
        saved.Add((app.Save(pictures, "app-rules-effect-menu-760.png"), 760, 1000));
        team010.CloseEffectMenu();
        KeyboardFocus(app, team010);
        saved.Add((app.Save(pictures, "app-rules-focus-760.png"), 760, 1000));

        NoRulePictures(saved);
        var wrong = saved.Where(file => !File.Exists(file.Path) || PngSize(file.Path) != (file.Width, file.Height)).Select(file => file.Path).ToList();
        _context.Report.Check("pictures", $"{saved.Count} pictures saved in {pictures}, each of the size asked for", wrong.Count == 0, wrong.ToArray());
    }

    /// <summary>
    /// The block that stands in the page pane when the repository has no rule at all.
    /// </summary>
    private void NoRulePictures(List<(string Path, int Width, int Height)> saved)
    {
        var empty = _context.Folders.NewFolder();
        File.WriteAllText(Path.Combine(empty, SampleFolders.SettingsName), "{ \"rules\": [] }\n");
        using var bare = _context.Open(empty, 1440, 900);
        bare.PumpUntil(() => bare.Rules.ShowsNoRuleBlock, 20000);
        bare.Pump(500);
        saved.Add((bare.Save(_context.Options.PicturesFolder, "app-rules-none-1440.png"), 1440, 900));
        Application.Current!.RequestedThemeVariant = ThemeVariant.Dark;
        bare.Pump(500);
        saved.Add((bare.Save(_context.Options.PicturesFolder, "app-rules-none-dark-1440.png"), 1440, 900));
        Application.Current.RequestedThemeVariant = ThemeVariant.Light;
        bare.Pump(300);
    }

    private static void KeyboardFocus(AppWindow app, TeamRuleRowViewModel row)
    {
        app.Rules.SelectedItem = null;
        app.Rows.FocusEntry(NavigationMethod.Tab);
        app.Pump(60);
        app.Press(PhysicalKey.ArrowDown);
        if(!ReferenceEquals(app.Rules.SelectedItem, row))
            app.Rules.SelectedItem = row;

        app.Pump(300);
    }
}
