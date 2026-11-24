using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using CF.App.Rules.Page;

namespace CF.App.Check;

/// <summary>
/// The checks of Try it that are about how it looks and how fast it is: how long it takes to answer while typing, the height
/// of the box and of its rows, the colours it is drawn in, the caret, and its pictures.
/// </summary>
internal sealed class TryItLookChecks
{
    private const int _changes = 50;
    private const double _bottomPadding = 32;
    private const double _rowHeight = 20;

    private readonly CheckContext _context;

    public TryItLookChecks(CheckContext context)
    {
        _context = context;
    }

    public void Run()
    {
        _context.Guarded("try-typing", "typing never waits for the rule's run, and the answer arrives fast enough", Typing);
        _context.Guarded("try-height", "the box takes the height the page has left, with no empty band", Height);
        _context.Guarded("try-rows", "every row of the code is 20 high", Rows);
        _context.Guarded("try-colours", "the box is drawn in the design's colours, light and dark", Colours);
        _context.Guarded("try-caret", "the caret is 2 wide and drawn in ink", Caret);
        _context.Guarded("try-pictures", "the pictures of Try it are saved", Pictures);
    }

    private static double Median(IEnumerable<double> values)
    {
        var sorted = values.Order().ToList();
        return sorted[sorted.Count / 2];
    }

    /// <summary>
    /// A C# file of exactly 500 lines made from the product's own sources: the largest whole files of App/CF.App/Rules that fit, each wrapped in a
    /// namespace of its own under one list of usings, as many as fit, and the rest of the 500 lines filled with comment lines.
    /// It is copied into the scratch folder. It has to parse, not to compile.
    /// </summary>
    private string FiveHundredLines(out string source)
    {
        var root = Path.GetDirectoryName(_context.Options.SampleSettingsPath) ?? throw new InvalidOperationException("no repository folder");
        var usings = new SortedSet<string>(StringComparer.Ordinal);
        var blocks = new List<List<string>>();
        var names = new List<string>();
        foreach(var file in Directory.EnumerateFiles(Path.Combine(root, "App", "CF.App", "Rules"), "*.cs", SearchOption.AllDirectories).OrderByDescending(file => File.ReadAllLines(file).Length).ThenBy(file => file, StringComparer.Ordinal))
        {
            var lines = File.ReadAllLines(file);
            var block = new List<string> { $"namespace Part{blocks.Count}", "{" };
            block.AddRange(lines.Where(line => !line.StartsWith("using ", StringComparison.Ordinal) && !line.StartsWith("namespace ", StringComparison.Ordinal)));
            block.Add("}");
            var newUsings = lines.Where(line => line.StartsWith("using ", StringComparison.Ordinal)).Where(line => !usings.Contains(line)).ToList();
            if(usings.Count + newUsings.Count + 1 + blocks.Sum(entry => entry.Count) + block.Count > 500)
                continue;

            usings.UnionWith(newUsings);
            blocks.Add(block);
            names.Add(Path.GetFileName(file));
        }

        var text = new List<string>(usings) { string.Empty };
        text.AddRange(blocks.SelectMany(entry => entry));
        while(text.Count < 500)
            text.Add("// filler");

        source = $"{names.Count} files of App/CF.App/Rules ({string.Join(", ", names)})";
        var copy = Path.Combine(_context.Folders.Root, "c500.cs");
        File.WriteAllLines(copy, text);

        return File.ReadAllText(copy);
    }

    private static (List<double> Key, List<double> Total, bool Holds) TypeFifty(AppWindow app, RulePageViewModel page, string text)
    {
        var view = TryItChecks.ViewOf(app, page);
        TryItChecks.PutCode(app, page, text);
        var key = new List<double>();
        var total = new List<double>();
        var holds = true;
        for(var index = 0; index < _changes; index++)
        {
            var changed = text + "\n// " + new string('a', index + 1);
            var clock = Stopwatch.StartNew();
            view.Editor.Document.Text = changed;
            holds &= view.Editor.Document.Text == changed && page.TryIt.IsBusy;
            app.Window.UpdateLayout();
            key.Add(clock.Elapsed.TotalMilliseconds);
            while(page.TryIt.IsBusy)
            {
                Dispatcher.UIThread.RunJobs();
                Thread.Yield();
            }

            app.Window.UpdateLayout();
            total.Add(clock.Elapsed.TotalMilliseconds);
            app.Pump(20);
        }

        return (key, total, holds);
    }

    private void Typing()
    {
        var folder = _context.Folders.NewFolder();
        var big = FiveHundredLines(out var source);
        using var app = _context.Open(folder);
        var page = PageEditChecks.OpenPage(app, "TEAM010");
        var (sampleKey, sampleTotal, sampleHolds) = TypeFifty(app, page, TryItChecks.Sample);
        var (bigKey, bigTotal, bigHolds) = TypeFifty(app, page, big);
        var lastBig = page.TryIt.Phase;
        _context.Report.Check("try-typing", "in all 50 changes of the sample and all 50 of the 500 lines the editor held the new text, and the run was still going, when the key press returned", sampleHolds && bigHolds, $"sample {sampleHolds}, 500 lines {bigHolds}");
        _context.Report.Check("try-typing", $"the sample: from a change of the text to the labels, 50 times: median {Median(sampleTotal):0} ms, worst {sampleTotal.Max():0} ms; what the key press itself waits for (the text in the editor and its layout): median {Median(sampleKey):0} ms, worst {sampleKey.Max():0} ms", true);
        _context.Report.Check("try-typing", $"500 lines ({source}): from a change of the text to the labels, 50 times: median {Median(bigTotal):0} ms, worst {bigTotal.Max():0} ms; what the key press itself waits for: median {Median(bigKey):0} ms, worst {bigKey.Max():0} ms; the last answer was {lastBig}", lastBig == TryPhase.Tried);
    }

    private void Height()
    {
        var folder = _context.Folders.NewFolder();
        using var app = _context.Open(folder, 1440, 900);
        var page = PageEditChecks.OpenPage(app, "TEAM010");
        TryItChecks.PutCode(app, page, TryItChecks.Sample);
        CheckHeight(app, page, "1440 by 900", 900, true);
        app.Resize(760, 1000);
        app.Rules.BackCommand.Execute(null);
        app.Pump(300);
        app.Rules.OpenSelectedRule();
        app.Window.Focus();
        app.Pump(500);
        CheckHeight(app, page, "760 by 1000", 1000, false);
    }

    private void CheckHeight(AppWindow app, RulePageViewModel page, string size, double windowHeight, bool noScroll)
    {
        var view = TryItChecks.ViewOf(app, page);
        var box = view.Box;
        var bottom = box.TranslatePoint(new Point(0, box.Bounds.Height), app.Window)?.Y ?? 0;
        var scroller = view.FindAncestorOfType<ScrollViewer>() ?? throw new InvalidOperationException("no scroll viewer");
        var natural = 2 + 36 + 8 + (16 * _rowHeight) + 10;
        _context.Report.Check("try-height", $"at {size} the box ends {_bottomPadding} above the bottom edge of the window, with no empty band under it", Math.Abs(bottom - (windowHeight - _bottomPadding)) <= 1.5, $"the box ends at {bottom}, the window at {windowHeight}");
        _context.Report.Check("try-height", $"at {size} the box is as high as its 16 rows of code at least ({natural})", box.Bounds.Height >= natural - 0.5, $"the box is {box.Bounds.Height} high");
        _context.Report.Check("try-height", $"at {size} the page does not scroll", !noScroll || scroller.Extent.Height <= scroller.Viewport.Height + 0.5, $"content {scroller.Extent.Height}, viewport {scroller.Viewport.Height}");
    }

    private void Rows()
    {
        var folder = _context.Folders.NewFolder();
        using var app = _context.Open(folder);
        var page = PageEditChecks.OpenPage(app, "TEAM010");
        TryItChecks.PutCode(app, page, TryItChecks.Sample);
        var textView = TryItChecks.ViewOf(app, page).Editor.TextArea.TextView;
        var heights = textView.VisualLines.Select(line => line.Height).ToList();
        _context.Report.Check("try-rows", "every row of the 16 rows of the sample is 20 high, the rows with a label too", heights.Count == 16 && heights.All(height => Math.Abs(height - _rowHeight) < 0.1), "heights: " + string.Join(", ", heights.Select(height => height.ToString("0.00"))));
    }

    private static bool AnyNear(WriteableBitmap frame, Point from, double width, double height, string expected, int tolerance)
    {
        for(var y = (int)from.Y; y < (int)(from.Y + height); y++)
        {
            for(var x = (int)from.X; x < (int)(from.X + width); x++)
            {
                if(Near(AppWindow.PixelAt(frame, x, y), expected, tolerance))
                    return true;
            }
        }

        return false;
    }

    private static bool Near(string seen, string expected, int tolerance)
    {
        for(var part = 0; part < 3; part++)
        {
            var one = Convert.ToInt32(seen.Substring(1 + (part * 2), 2), 16);
            var two = Convert.ToInt32(expected.Substring(1 + (part * 2), 2), 16);
            if(Math.Abs(one - two) > tolerance)
                return false;
        }

        return true;
    }

    private void Colours()
    {
        var folder = _context.Folders.NewFolder();
        using var app = _context.Open(folder);
        var page = PageEditChecks.OpenPage(app, "TEAM010");
        TryItChecks.PutCode(app, page, TryItChecks.Sample);
        ColoursOf(app, page, "light", "#FFECEA", "#D42B38", "#8E24AA", "#B3401A", "#525252", "#CDCDCD", "#1C1C1C", "#FFFFFF");
        Application.Current!.RequestedThemeVariant = ThemeVariant.Dark;
        app.Pump(600);
        ColoursOf(app, page, "dark", "#3F2A2A", "#D13B40", "#DDAAF5", "#F2A07E", "#B0B0B0", "#555555", "#EDEDED", "#262626");
        Application.Current.RequestedThemeVariant = ThemeVariant.Light;
        app.Pump(300);
    }

    private void ColoursOf(AppWindow app, RulePageViewModel page, string theme, string band, string bar, string keyword, string text, string number, string border, string focus, string surface)
    {
        var view = TryItChecks.ViewOf(app, page);
        var textView = view.Editor.TextArea.TextView;
        var margin = view.Editor.TextArea.LeftMargins[0];
        Point Window(Visual of, double x, double y)
        {
            return of.TranslatePoint(new Point(x, y), app.Window) ?? throw new InvalidOperationException("not on the window");
        }

        double RowTop(int line)
        {
            return textView.VisualLines.First(candidate => candidate.FirstDocumentLine.LineNumber == line).VisualTop - textView.VerticalOffset;
        }

        Point CharacterAt(int line, int column)
        {
            var at = textView.GetVisualPosition(new AvaloniaEdit.TextViewPosition(line, column), AvaloniaEdit.Rendering.VisualYPosition.LineTop);
            return Window(textView, at.X - textView.HorizontalOffset, RowTop(line));
        }

        var boxLeft = Window(view.Box, 0, 0).X;
        var boxTop = Window(view.Box, 0, 0).Y;
        var boxRight = boxLeft + view.Box.Bounds.Width;
        using(var frame = app.Capture())
        {
            var rest = new[] { AppWindow.PixelAt(frame, (int)boxLeft + 100, (int)boxTop), AppWindow.PixelAt(frame, (int)boxLeft + 100, (int)boxTop + 1), AppWindow.PixelAt(frame, (int)boxLeft, (int)boxTop + 18), AppWindow.PixelAt(frame, (int)boxLeft + 1, (int)boxTop + 18) };
            _context.Report.Check("try-colours", $"{theme}: the border at rest is line-strong ({border}) and 1 wide, on the top edge and the left edge, and the field is the page ({surface})", rest[0] == border && rest[1] == surface && rest[2] == border && rest[3] == surface, "pixels: " + string.Join(" ", rest));

            var rowFour = RowTop(4) + (_rowHeight / 2);
            var bandPixel = AppWindow.PixelAt(frame, (int)Window(textView, textView.Bounds.Width - 6, rowFour).X, (int)Window(textView, 0, rowFour).Y);
            var barAt = Window(margin, 1.5, rowFour);
            var barPixel = AppWindow.PixelAt(frame, (int)barAt.X, (int)barAt.Y);
            var afterBar = Window(margin, 3.5, rowFour);
            var afterBarPixel = AppWindow.PixelAt(frame, (int)afterBar.X, (int)afterBar.Y);
            var clear = Window(textView, 0, RowTop(7) + (_rowHeight / 2));
            var plain = AppWindow.PixelAt(frame, (int)clear.X + 100, (int)clear.Y);
            var rightEdge = AppWindow.PixelAt(frame, (int)boxRight - 3, (int)Window(textView, 0, rowFour).Y);
            _context.Report.Check("try-colours", $"{theme}: the band of a breaking line is diff-del ({band}), reaches the right edge of the box, and a line that does not break is the page ({surface})", bandPixel == band && rightEdge == band && plain == surface, $"band {bandPixel}, at the right edge {rightEdge}, row 7 {plain}");
            _context.Report.Check("try-colours", $"{theme}: the bar is block ({bar}), 3 wide, at the left edge inside the border: the third pixel is the bar, the fourth is the band", barPixel == bar && afterBarPixel == band, $"bar {barPixel}, after it {afterBarPixel}");

            var keywordFrom = CharacterAt(3, 5);
            var stringFrom = CharacterAt(13, 18);
            var numberFrom = Window(margin, 44 - 14 - 8, RowTop(5));
            _context.Report.Check("try-colours", $"{theme}: a keyword (if, line 3) has code-keyword ({keyword}), a string (line 13) has code-string ({text}), and a line number has ink-3 ({number})", AnyNear(frame, keywordFrom, 16, _rowHeight, keyword, 10) && AnyNear(frame, stringFrom, 120, _rowHeight, text, 10) && AnyNear(frame, numberFrom, 8, _rowHeight, number, 10), $"keyword {AnyNear(frame, keywordFrom, 16, _rowHeight, keyword, 10)}, string {AnyNear(frame, stringFrom, 120, _rowHeight, text, 10)}, number {AnyNear(frame, numberFrom, 8, _rowHeight, number, 10)}");
        }

        view.FocusCode();
        app.Pump(300);
        using(var frame = app.Capture())
        {
            var edge = new[] { AppWindow.PixelAt(frame, (int)boxLeft + 100, (int)boxTop), AppWindow.PixelAt(frame, (int)boxLeft + 100, (int)boxTop + 1), AppWindow.PixelAt(frame, (int)boxLeft + 100, (int)boxTop + 2) };
            _context.Report.Check("try-colours", $"{theme}: with the keyboard in the code the border is 2 wide in focus ({focus}), drawn inside", edge[0] == focus && edge[1] == focus && edge[2] == surface, "pixels: " + string.Join(" ", edge));
        }

        view.PasteButton.Focus();
        app.Pump(200);
        var inside = Window(view.Box, view.Box.Bounds.Width / 2, view.Box.Bounds.Height / 2);
        app.Window.MouseMove(inside, Avalonia.Input.RawInputModifiers.None);
        app.Pump(300);
        using(var frame = app.Capture())
        {
            var hover = AppWindow.PixelAt(frame, (int)boxLeft + 100, (int)boxTop);
            _context.Report.Check("try-colours", $"{theme}: under the pointer the border is ink-3 ({number})", hover == number, "pixel: " + hover);
        }

        app.Window.MouseMove(new Point(2, 2), Avalonia.Input.RawInputModifiers.None);
        app.Pump(100);
    }

    private void Caret()
    {
        var folder = _context.Folders.NewFolder();
        using var app = _context.Open(folder);
        var page = PageEditChecks.OpenPage(app, "TEAM010");
        var view = TryItChecks.ViewOf(app, page);
        view.FocusCode();
        app.Pump(100);
        var textView = view.Editor.TextArea.TextView;
        var at = textView.TranslatePoint(new Point(0, _rowHeight / 2), app.Window) ?? throw new InvalidOperationException("not on the window");
        using var frame = app.Capture();
        var pixels = Enumerable.Range(-1, 5).Select(offset => AppWindow.PixelAt(frame, (int)at.X + offset, (int)at.Y)).ToList();
        _context.Report.Check("try-caret", "the caret is drawn in ink (#1C1C1C), 2 wide, at the start of the first row", pixels[1] == "#1C1C1C" && pixels[2] == "#1C1C1C" && pixels[3] != "#1C1C1C" && pixels[0] != "#1C1C1C", "pixels from one before the text: " + string.Join(" ", pixels));
    }

    private void Pictures()
    {
        var folder = _context.Folders.NewFolder();
        var pictures = _context.Options.PicturesFolder;
        var saved = new List<(string Path, int Width, int Height)>();
        using var app = _context.Open(folder, 1440, 900);
        var page = PageEditChecks.OpenPage(app, "TEAM010");
        Take(app, page, pictures, saved, "1440", 1440, 900);
        app.Resize(760, 1000);
        app.Rules.BackCommand.Execute(null);
        app.Pump(300);
        app.Rules.OpenSelectedRule();
        app.Window.Focus();
        app.Pump(500);
        Take(app, page, pictures, saved, "760", 760, 1000);
        using var dark = _context.Open(folder, 1440, 900);
        var darkPage = PageEditChecks.OpenPage(dark, "TEAM010");
        TryItChecks.PutCode(dark, darkPage, TryItChecks.Sample);
        Application.Current!.RequestedThemeVariant = ThemeVariant.Dark;
        dark.Pump(600);
        saved.Add((dark.Save(pictures, "app-try-sample-dark-1440.png"), 1440, 900));
        Application.Current.RequestedThemeVariant = ThemeVariant.Light;
        dark.Pump(300);
        var wrong = saved.Where(file => !File.Exists(file.Path) || LookChecks.PngSize(file.Path) != (file.Width, file.Height)).Select(file => file.Path).ToList();
        _context.Report.Check("try-pictures", $"{saved.Count} pictures of Try it saved in {pictures}, each of the size asked for", wrong.Count == 0, wrong.ToArray());
    }

    private static void Take(AppWindow app, RulePageViewModel page, string pictures, List<(string Path, int Width, int Height)> saved, string size, int width, int height)
    {
        var view = TryItChecks.ViewOf(app, page);
        TryItChecks.PutCode(app, page, string.Empty);
        saved.Add((app.Save(pictures, $"app-try-empty-{size}.png"), width, height));
        TryItChecks.PutCode(app, page, TryItChecks.Sample);
        saved.Add((app.Save(pictures, $"app-try-sample-{size}.png"), width, height));
        TryItChecks.PutCode(app, page, "void A() { if(x) { } ");
        saved.Add((app.Save(pictures, $"app-try-parse-{size}.png"), width, height));
        TryItChecks.PutCode(app, page, TryItChecks.Sample);
        view.FocusCode();
        app.Pump(300);
        saved.Add((app.Save(pictures, $"app-try-focus-code-{size}.png"), width, height));
        view.PasteButton.Focus(Avalonia.Input.NavigationMethod.Tab);
        app.Pump(300);
        saved.Add((app.Save(pictures, $"app-try-focus-paste-{size}.png"), width, height));
        app.Window.Focus();
        app.Pump(200);
    }
}
