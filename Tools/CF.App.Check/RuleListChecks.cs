using Avalonia.Controls;
using CF.App.Rules;

namespace CF.App.Check;

/// <summary>
/// The checks of what the list shows: its rows against the command line, the search, the filter and its counts, and the
/// two layouts.
/// </summary>
internal sealed class RuleListChecks
{
    private readonly CheckContext _context;

    public RuleListChecks(CheckContext context)
    {
        _context = context;
    }

    public void Run()
    {
        _context.Guarded("rows", "the rows shown equal commitfence rules list", RowsEqualCommandLine);
        _context.Guarded("search", "the search finds by id and by title", SearchFinds);
        _context.Guarded("filter", "each filter shows the right rows and the counts are right", FilterShows);
        _context.Guarded("layout", "two panes from 1304 wide with the list column 540 to 656, one pane under it", LayoutFollowsWidth);
    }

    private static string Describe(IEnumerable<ListedRule> rules)
    {
        return string.Join(" | ", rules.Select(rule => $"{rule.Id} {rule.Severity} {rule.Title}{(rule.IsOff ? " (off)" : string.Empty)}"));
    }

    private static List<ListedRule> ShownRows(AppWindow app)
    {
        return app.Rules.Items.OfType<TeamRuleRowViewModel>()
            .Select(row => new ListedRule(row.Id, row.IsBlocks ? "block" : "warn", row.Title, !row.IsOn))
            .ToList();
    }

    private static string ShownIds(AppWindow app)
    {
        return string.Join(" ", app.Rules.Items.OfType<TeamRuleRowViewModel>().Select(row => row.Id));
    }

    private void RowsEqualCommandLine()
    {
        var folder = _context.Folders.NewFolder();
        using var app = _context.Open(folder);
        var listed = _context.ListedByCommandLine(folder);
        var shown = ShownRows(app);
        var drawn = app.Find<TextBlock>(block => block.Text != null).Select(block => block.Text).ToList();
        var missing = listed.Where(rule => !drawn.Contains(rule.Title)).Select(rule => rule.Id).ToList();
        var same = Describe(listed) == Describe(shown);
        _context.Report.Check("rows", "the rows shown equal commitfence rules list (ids, order, effect, off, titles), and each title is drawn", same && missing.Count == 0 && listed.Count > 0,
            "command line: " + Describe(listed),
            "the window:   " + Describe(shown),
            "titles not drawn: " + string.Join(", ", missing));
    }

    private void SearchFinds()
    {
        var folder = _context.Folders.NewFolder();
        using var app = _context.Open(folder);
        var listed = _context.ListedByCommandLine(folder);
        var cases = new List<(string Text, string Expected)>
        {
            ("TEAM012", "TEAM012"),
            ("team012", "TEAM012"),
            ("iclock", "TEAM012"),
            ("braces around", "TEAM010"),
            ("TEAM01", string.Join(" ", listed.Select(rule => rule.Id))),
            ("zzz", string.Empty)
        };
        foreach(var (text, expected) in cases)
        {
            app.Rules.SearchText = text;
            app.Pump(60);
            _context.Report.Check("search", $"searching \"{text}\" shows [{expected}]", ShownIds(app) == expected, "shown: [" + ShownIds(app) + "]");
        }

        app.Rules.SearchText = "zzz";
        app.Pump(100);
        var message = app.Find<TextBlock>(block => block.Text != null && block.Text.StartsWith("No rule matches", StringComparison.Ordinal)).FirstOrDefault();
        _context.Report.Check("search", "a search that finds nothing says \"No rule matches\" with the typed text", app.Rules.HasNoMatch && message != null && message.Text!.Contains("zzz", StringComparison.Ordinal), "message drawn: " + message?.Text);

        app.Rules.SearchText = "TEAM01";
        app.Rules.SelectedFilter = app.Rules.Filters.First(segment => segment.Filter == RuleFilter.Off);
        app.Pump(60);
        _context.Report.Check("search", "the search and the filter together: TEAM01 under Off shows TEAM012", ShownIds(app) == "TEAM012", "shown: [" + ShownIds(app) + "]");
    }

    private void FilterShows()
    {
        var folder = _context.Folders.NewFolder();
        using var app = _context.Open(folder);
        var listed = _context.ListedByCommandLine(folder);
        var expected = new Dictionary<RuleFilter, IReadOnlyList<ListedRule>>
        {
            [RuleFilter.All] = listed,
            [RuleFilter.Blocks] = listed.Where(rule => rule.Blocks).ToList(),
            [RuleFilter.Warns] = listed.Where(rule => rule.Warns).ToList(),
            [RuleFilter.Off] = listed.Where(rule => rule.IsOff).ToList()
        };
        foreach(var segment in app.Rules.Filters)
        {
            var want = expected[segment.Filter];
            app.Rules.SelectedFilter = segment;
            app.Pump(60);
            var countDrawn = app.Find<TextBlock>(block => block.Classes.Contains("segment-count")).Select(block => block.Text).ToList();
            var ids = string.Join(" ", want.Select(rule => rule.Id));
            _context.Report.Check("filter", $"{segment.Label}: shows [{ids}] and counts {want.Count}", ShownIds(app) == ids && segment.Count == want.Count && countDrawn.Contains(want.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                "shown: [" + ShownIds(app) + "], count " + segment.Count,
                "counts drawn: " + string.Join(", ", countDrawn));
        }

        app.Rules.SelectedFilter = app.Rules.Filters.First(segment => segment.Filter == RuleFilter.Warns);
        app.Pump(60);
        var note = app.Find<TextBlock>(block => block.Text == "No custom rule warns.").FirstOrDefault();
        _context.Report.Check("filter", "a filter that lets no custom rule through says so under the heading (\"No custom rule warns.\")", note != null, "no such line drawn");
    }

    private void LayoutFollowsWidth()
    {
        var folder = _context.Folders.NewFolder();
        using var app = _context.Open(folder, 1440, 900);
        var columns = app.Find<Grid>(grid => grid.Name == "Columns").First();
        var widths = new List<string>();
        var ok = true;
        foreach(var (width, twoPanes, listWidth) in new[] { (1440.0, true, 656.0), (1304.0, true, 540.0), (1380.0, true, 596.0), (1303.0, false, 0.0), (760.0, false, 0.0) })
        {
            app.Resize(width, 900);
            var onePane = app.Rules.IsOnePane;
            var column = columns.ColumnDefinitions[0].Width;
            var good = onePane == !twoPanes && (twoPanes ? Math.Abs(column.Value - listWidth) < 0.5 && column.IsAbsolute : column.IsStar);
            widths.Add($"{width}: one pane {onePane}, list column {column}");
            ok &= good;
        }

        _context.Report.Check("layout", "at 1440 the list column is 656, at 1304 540, at 1380 596 (window less 784), under 1304 one pane", ok, widths.ToArray());
    }
}
