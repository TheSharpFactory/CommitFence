using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using CF.App.Parts;
using CF.App.Review;

namespace CF.App.Check;

internal sealed partial class ReviewChecks
{
    private static Color ThemeColour(string key)
    {
        var found = Application.Current!.TryGetResource(key, Application.Current.ActualThemeVariant, out var value);
        return found && value is ISolidColorBrush brush ? brush.Color : default;
    }

    private static string Hex(Color colour)
    {
        return $"#{colour.R:X2}{colour.G:X2}{colour.B:X2}";
    }

    private static bool SameLine(IEnumerable<Visual> parts, AppWindow app)
    {
        var centres = parts.Select(part => app.CenterOf(part).Y).ToList();
        return centres.Count < 2 || centres.Max() - centres.Min() <= 8;
    }

    /// <summary>
    /// The four repairs of R1's pictures: the panel's ground and head, the second button's one look, the pieces of a sentence that
    /// never break, and the thin scroll bar with the columns where the layout expects them.
    /// </summary>
    private void Repairs(AppWindow app)
    {
        var review = app.Model.Review;
        app.Model.ShowReview();
        app.Resize(1440, 900);
        ScrollToTop(app);
        app.MovePointerAway();
        var side = app.Find<FilesPanel>(panel => panel.Name == "SidePanel").First();
        var ground = side.GetVisualDescendants().OfType<Border>().First(border => border.IsEffectivelyVisible && border.CornerRadius.TopLeft == 16);
        var colour = ground.Background is ISolidColorBrush brush ? brush.Color : default;
        var title = side.GetVisualDescendants().OfType<TextBlock>().First(block => block.IsEffectivelyVisible && block.Text == "Files");
        var count = side.GetVisualDescendants().OfType<TextBlock>().First(block => block.IsEffectivelyVisible && block.Text == review.PanelHead);
        _context.Report.Check("review-repairs", "the panel of Files stands on the side ground, radius 16, padding 8 12 8 8, and its head is one line (Files and the count side by side)", colour == ThemeColour("side") && colour.A == 255 && ground.Padding == new Thickness(8, 12, 8, 8) && SameLine(new Visual[] { title, count }, app) && app.CenterOf(count).X > app.CenterOf(title).X, $"ground {Hex(colour)}, padding {ground.Padding}, title y {app.CenterOf(title).Y}, count y {app.CenterOf(count).Y}");
        var run = RunButton(app);
        var icons = run.GetVisualDescendants().OfType<Icon>().Where(icon => icon.IsEffectivelyVisible).ToList();
        using(var frame = app.Capture())
        {
            var inside = app.CenterOf(run);
            var pixel = AppWindow.PixelAt(frame, (int)(inside.X - (run.Bounds.Width / 2) + 8), (int)inside.Y);
            _context.Report.Check("review-repairs", "at 1440 the run button says 'Review again' with no shield and the outlined second look (the page colour inside, the pointer away)", review.RunText == "Review again" && icons.Count == 0 && HasRunStyle(app, "outlined") && pixel == Hex(ThemeColour("page")), $"{icons.Count} icons, pixel {pixel}, page {Hex(ThemeColour("page"))}");
        }

        var sentence = Sentences(app);
        _context.Report.Check("review-repairs", "at 1440 every piece of a sentence stands on one line: a word with its token (For, where, and, require)", sentence.Count >= 4 && sentence.All(piece => SameLine(piece, app)), $"{sentence.Count} pieces");
        var thumb = Thumbs(app);
        var frameGrid = app.Find<Grid>(grid => grid.Name == "Frame").First();
        var main = app.Find<StackPanel>(panel => panel.Name == "Main").First();
        var mainRight = main.TranslatePoint(new Point(main.Bounds.Width, 0), app.Window)?.X ?? double.NaN;
        var panelRight = side.TranslatePoint(new Point(side.Bounds.Width, 0), app.Window)?.X ?? double.NaN;
        _context.Report.Check("review-repairs", "the sheet's scroll bar thumb is 6 wide; at 1440 the main column ends at 1036 and the panel at 1408", thumb.Count > 0 && thumb.All(width => Math.Abs(width - 6) < 0.5) && Math.Abs(mainRight - 1036) <= 1 && Math.Abs(panelRight - 1408) <= 1, $"thumb {string.Join(",", thumb)}, main ends {mainRight}, panel ends {panelRight}, frame {frameGrid.Bounds.Width}");
        app.Resize(760, 1000);
        ScrollToTop(app);
        app.MovePointerAway();
        var narrow = Sentences(app);
        var narrowRun = RunButton(app);
        _context.Report.Check("review-repairs", "at 760 the sentence's pieces stand on one line each (the lead word with its token), the column is 624 wide, and the run button has no shield", narrow.Count >= 4 && narrow.All(piece => SameLine(piece, app)) && Math.Abs(frameGrid.Bounds.Width - 624) <= 1 && !narrowRun.GetVisualDescendants().OfType<Icon>().Any(icon => icon.IsEffectivelyVisible) && HasRunStyle(app, "outlined"), $"{narrow.Count} pieces, column {frameGrid.Bounds.Width}");
        app.Resize(1440, 900);
        ScrollToTop(app);
        Sheet(app).Offset = new Vector(0, 420);
        app.Pump(500);
        app.Save(_context.Options.PicturesFolder, "review-refused-scrolled-1440.png");
        ScrollToTop(app);
    }

    /// <summary>
    /// The visible parts of each piece of a finding's sentence: the lead word and its token or tokens.
    /// </summary>
    private static List<List<Visual>> Sentences(AppWindow app)
    {
        var pieces = new List<List<Visual>>();
        foreach(var panel in app.Find<StackPanel>(candidate => candidate.IsEffectivelyVisible && candidate.DataContext is SentencePieceViewModel && candidate.Orientation == Avalonia.Layout.Orientation.Horizontal && candidate.Spacing == 6))
        {
            var parts = panel.Children.Where(child => child.IsEffectivelyVisible && child.Bounds.Width > 0).Cast<Visual>().ToList();
            if(parts.Count > 0)
                pieces.Add(parts);
        }

        return pieces;
    }

    private static List<double> Thumbs(AppWindow app)
    {
        var sheet = Sheet(app);
        return sheet.GetVisualDescendants().OfType<Avalonia.Controls.Primitives.ScrollBar>().Where(bar => bar.IsEffectivelyVisible && bar.Orientation == Avalonia.Layout.Orientation.Vertical).SelectMany(bar => bar.GetVisualDescendants().OfType<Border>().Where(border => border.Name == "Bar")).Select(border => border.Bounds.Width).ToList();
    }
}
