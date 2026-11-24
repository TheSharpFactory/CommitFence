using System.Reflection;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using CF.App.Parts;
using CF.App.Rules;
using CF.App.Rules.Page;
using CF.Composition;
using CF.Core.Findings;
using CF.Core.Rules;
using CF.Core.Services;

namespace CF.App.Check;

/// <summary>
/// One rule of .editorconfig as <c>commitfence rules list</c> prints it: its id, its state word, its effect word, its title and
/// the line of the file.
/// </summary>
internal sealed record ListedFileRule(string Id, string State, string Effect, string Title, int Line);

/// <summary>
/// The checks that are about the rules of .editorconfig in the list and on their page.
/// </summary>
internal sealed partial class FileRuleChecks
{
    private const string _ide0055Refusal = "IDE0055 is set in .editorconfig, line 378; the product does not change that file.";

    private static readonly int[] _ide0055Line = { 378 };
    private static readonly string[] _errorWord = { "error" };
    private static readonly string[] _openAtLineButton = { "OpenAtLine" };

    private readonly CheckContext _context;

    public FileRuleChecks(CheckContext context)
    {
        _context = context;
    }

    [GeneratedRegex(@"^(?<id>\S+)\s+(?<state>on|off|default|not understood)\s+(?<effect>blocks|warns)?\s*(?<title>.*?)\s*(?:setting '[^']*', )?line (?<line>\d+)$")]
    private static partial Regex FileLine();

    public void Run()
    {
        _context.Guarded("file-rows", "the second group's rows equal commitfence rules list", RowsEqualCommandLine);
        _context.Guarded("file-counts", "the counts of the heading and of the filter cover both groups", Counts);
        _context.Guarded("file-search", "the search finds by id and by title across both groups", Search);
        _context.Guarded("file-states", "each state of a rule of the file is shown in the command line's words", States);
        _context.Guarded("file-no-control", "a row of the file has no menu and no switch, and the keys skip it", NoControl);
        _context.Guarded("file-refusal", "a try to change a rule of the file through the view model is refused with the body's line", Refusal);
        _context.Guarded("file-page", "the page of IDE0055 shows the lines round 378, marks 378 and error", PageOfIde0055);
        _context.Guarded("file-live", "a change of .editorconfig on disk shows without a restart", ChangedOnDisk);
        _context.Guarded("file-loading", "the window is usable before the group arrives", UsableWhileReading);
        _context.Guarded("file-open", "Open file and line N ask for the .editorconfig of the repository", OpenTheFile);
        _context.Guarded("file-held", "the heading of the group at the top stays in view", HeldHeading);
        _context.Guarded("file-tab", "Open file is the stop between the filter and the list", TabOrder);
        _context.Guarded("file-repairs", "the repairs of the file rules: order, outlined buttons, the line link, the width", Repairs);
        _context.Guarded("file-pictures", "pictures of the second group and of a file rule's page", Pictures);
    }

    public static List<ListedFileRule> ListedByCommandLine(CheckContext context, string folder)
    {
        var rules = new List<ListedFileRule>();
        foreach(var line in context.Cli(folder, "rules", "list").Output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var match = FileLine().Match(line);
            if(match.Success)
                rules.Add(new ListedFileRule(match.Groups["id"].Value, match.Groups["state"].Value, match.Groups["effect"].Value, match.Groups["title"].Value, int.Parse(match.Groups["line"].Value, System.Globalization.CultureInfo.InvariantCulture)));
        }

        return rules;
    }

    public static ListedFileRule Presented(FileRuleRowViewModel row)
    {
        var state = row.ShowsEffect ? "on" : row.IsOffInFile ? "off" : row.StateWord;
        var effect = row.MarkIsBlock ? "blocks" : row.MarkIsWarn ? "warns" : string.Empty;

        return new ListedFileRule(row.Id, state, effect, row.Title, row.Rule.Line);
    }

    public static List<ListedFileRule> ShownFileRows(AppWindow app)
    {
        return app.Rules.Items.OfType<FileRuleRowViewModel>().Select(Presented).ToList();
    }

    public static string EditorConfigOfThisRepository(CheckContext context)
    {
        return Path.Combine(Path.GetDirectoryName(context.Options.SampleSettingsPath)!, ".editorconfig");
    }

    public static string FolderWithRepositoryFile(CheckContext context)
    {
        var folder = context.Folders.NewFolder();
        File.Copy(EditorConfigOfThisRepository(context), Path.Combine(folder, ".editorconfig"));

        return folder;
    }

    public static AppWindow OpenReady(CheckContext context, string folder, double width = 1440, double height = 900)
    {
        var app = context.Open(folder, width, height);
        app.PumpUntil(() => app.Rules.FileRulesAreRead, 60000);
        app.Pump(300);

        return app;
    }

    private static string Describe(IEnumerable<ListedFileRule> rules)
    {
        return string.Join(" | ", rules.Select(rule => $"{rule.Id} {rule.State} {rule.Effect} {rule.Title} line {rule.Line}"));
    }

    private void RowsEqualCommandLine()
    {
        var folder = FolderWithRepositoryFile(_context);
        using var app = OpenReady(_context, folder);
        var listed = ListedByCommandLine(_context, folder);
        var shown = ShownFileRows(app);
        _context.Report.Check("file-rows", "the rows of the second group equal commitfence rules list (id, on or off, blocks or warns, title, line), in order", listed.Count > 50 && Describe(listed) == Describe(shown), $"command line: {listed.Count} rules", $"the window:   {shown.Count} rules", "first difference: " + FirstDifference(listed, shown));
        var heading = app.Rules.Items.OfType<GroupHeadingViewModel>().Last();
        _context.Report.Check("file-rows", "the second heading is .editorconfig, with the lock note, Open file and a count of every rule", heading.IsEditorConfig && heading.Name == ".editorconfig" && heading.Count == listed.Count && heading.ShowsCount && heading.HasOpenFile && heading.NoteHasLock && heading.Note == "On and off are set in the file", $"heading {heading.Name} {heading.Count} {heading.Note}");
        _context.Report.Check("file-rows", "the heading is named as expected: .editorconfig, N rules, on and off set in the file, expanded", heading.SpokenName == $".editorconfig, {listed.Count} rules, on and off set in the file, expanded", heading.SpokenName);
        var order = app.Rules.Items.Select(item => item is GroupHeadingViewModel group ? group.Name : item is TeamRuleRowViewModel ? "team" : item is FileRuleRowViewModel ? "file" : "note").ToList();
        var groups = string.Join(",", order.Where(word => word is "Custom" or ".editorconfig"));
        _context.Report.Check("file-rows", "the Custom group comes first, then .editorconfig", groups == "Custom,.editorconfig" && order.IndexOf(".editorconfig") > order.LastIndexOf("team"), groups);
    }

    private static string FirstDifference(List<ListedFileRule> listed, List<ListedFileRule> shown)
    {
        for(var index = 0; index < Math.Max(listed.Count, shown.Count); index++)
        {
            var one = index < listed.Count ? Describe(new[] { listed[index] }) : "(none)";
            var other = index < shown.Count ? Describe(new[] { shown[index] }) : "(none)";
            if(one != other)
                return $"#{index}: cli [{one}] window [{other}]";
        }

        return "none";
    }

    private void Counts()
    {
        var folder = FolderWithRepositoryFile(_context);
        using var app = OpenReady(_context, folder);
        var team = _context.ListedByCommandLine(folder);
        var file = ListedByCommandLine(_context, folder);
        var expected = new Dictionary<string, int>
        {
            ["All"] = team.Count + file.Count,
            ["Blocks"] = team.Count(rule => rule.Blocks) + file.Count(rule => rule.State == "on" && rule.Effect == "blocks"),
            ["Warns"] = team.Count(rule => rule.Warns) + file.Count(rule => rule.State == "on" && rule.Effect == "warns"),
            ["Off"] = team.Count(rule => rule.IsOff) + file.Count(rule => rule.State == "off")
        };
        foreach(var segment in app.Rules.Filters)
        {
            app.Rules.SelectedFilter = segment;
            app.Pump(150);
            var shownFile = ShownFileRows(app);
            var wanted = file.Where(rule => segment.Label switch
            {
                "Blocks" => rule.State == "on" && rule.Effect == "blocks",
                "Warns" => rule.State == "on" && rule.Effect == "warns",
                "Off" => rule.State == "off",
                _ => true
            }).ToList();
            _context.Report.Check("file-counts", $"filter {segment.Label}: the count is {expected[segment.Label]} (both groups) and the second group shows the {wanted.Count} rules that pass it", segment.Count == expected[segment.Label] && Describe(shownFile) == Describe(wanted), $"count {segment.Count}, expected {expected[segment.Label]}", $"shown {shownFile.Count}, wanted {wanted.Count}");
        }
    }

    private void Search()
    {
        var folder = FolderWithRepositoryFile(_context);
        using var app = OpenReady(_context, folder);
        var team = _context.ListedByCommandLine(folder);
        var file = ListedByCommandLine(_context, folder);
        foreach(var text in new[] { "IDE0055", "ide0055", "Fix formatting", "primary constructor", "TEAM01", "IClock", "zzzz" })
        {
            app.Rules.SearchText = text;
            app.Pump(150);
            var wantedFile = file.Where(rule => rule.Id.Contains(text, StringComparison.OrdinalIgnoreCase) || rule.Title.Contains(text, StringComparison.OrdinalIgnoreCase)).ToList();
            var wantedTeam = team.Where(rule => rule.Id.Contains(text, StringComparison.OrdinalIgnoreCase) || rule.Title.Contains(text, StringComparison.OrdinalIgnoreCase)).ToList();
            var shownTeam = app.Rules.Items.OfType<TeamRuleRowViewModel>().Select(row => row.Id).ToList();
            var same = Describe(ShownFileRows(app)) == Describe(wantedFile) && string.Join(" ", shownTeam) == string.Join(" ", wantedTeam.Select(rule => rule.Id));
            var none = wantedFile.Count + wantedTeam.Count == 0;
            _context.Report.Check("file-search", $"search '{text}': {wantedTeam.Count} rule(s) of the team and {wantedFile.Count} of the file{(none ? "; the page says nothing matches" : string.Empty)}", same && app.Rules.HasNoMatch == none, $"team {string.Join(" ", shownTeam)}", $"file {ShownFileRows(app).Count}, wanted {wantedFile.Count}", "no match " + app.Rules.HasNoMatch);
        }

        app.Rules.SearchText = string.Empty;
    }

    private void States()
    {
        var text = string.Join('\n', "root = true", "", "[*.cs]",
            "dotnet_diagnostic.CA1000.severity = error",
            "dotnet_diagnostic.CA1001.severity = warning",
            "dotnet_diagnostic.CA1002.severity = suggestion",
            "dotnet_diagnostic.CA1003.severity = none",
            "dotnet_diagnostic.CA1004.severity = silent",
            "dotnet_diagnostic.CA1005.severity = default",
            "dotnet_diagnostic.CA1006.severity = nonsense",
            "dotnet_diagnostic.ZZ9999.severity = error",
            "") + "\n";
        var folder = _context.Folders.NewFolder();
        File.WriteAllText(Path.Combine(folder, ".editorconfig"), text);
        Directory.CreateDirectory(Path.Combine(folder, "sub"));
        File.WriteAllText(Path.Combine(folder, "sub", ".editorconfig"), "[*.cs]\ndotnet_diagnostic.CA1000.severity = none\n");
        using var app = OpenReady(_context, folder);
        var listed = ListedByCommandLine(_context, folder);
        var shown = ShownFileRows(app);
        _context.Report.Check("file-states", "five severity words, default, a word not understood and a rule without a title: the rows equal the command line's lines", listed.Count == 8 && Describe(listed) == Describe(shown), "cli: " + Describe(listed), "window: " + Describe(shown));
        var states = string.Join(",", shown.Select(rule => rule.State + "/" + rule.Effect));
        _context.Report.Check("file-states", "the states are on blocks, on warns, on warns, off, off, default, not understood, on blocks", states == "on/blocks,on/warns,on/warns,off/,off/,default/,not understood/,on/blocks", states);
        var rows = app.Rules.Items.OfType<FileRuleRowViewModel>().ToList();
        _context.Report.Check("file-states", "the rule without a title has none and the one the SDK knows (CA1000) has its title", rows.Last().Title.Length == 0 && rows[0].Title.Length > 0, string.Join(" | ", rows.Select(row => row.Id + ":" + row.Title.Length)));
        _context.Report.Check("file-states", "default and not understood say the command line's word in the effect's place and keep their line; an off rule says Off in the file and has no line link", rows[5].ShowsStateWord && rows[5].StateWord == "default" && rows[6].StateWord == "not understood" && rows[5].ShowsLine && rows[3].IsOffInFile && !rows[3].ShowsLine && rows[0].ShowsEffect && rows[0].EffectWord == "Blocks" && rows[1].EffectWord == "Warns", "words " + rows[5].StateWord + ", " + rows[6].StateWord);
        var cliNotes = _context.Cli(folder, "rules", "list").Output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Where(line => line.Contains("sub-folder", StringComparison.Ordinal)).ToList();
        var notes = app.Rules.Items.OfType<GroupNoteViewModel>().Select(note => note.Text).ToList();
        _context.Report.Check("file-states", "the note about .editorconfig files in sub-folders is the command line's, under the heading", cliNotes.Count == 1 && notes.SequenceEqual(cliNotes), "cli: " + string.Join("; ", cliNotes), "window: " + string.Join("; ", notes));
        var order = app.Rules.Items.Select(item => item.GetType().Name).ToList();
        _context.Report.Check("file-states", "the note stands right after the heading, before the rows", order.IndexOf(nameof(GroupNoteViewModel)) == order.LastIndexOf(nameof(GroupHeadingViewModel)) + 1, string.Join(",", order.Skip(order.Count - 12)));

        var none = _context.Folders.NewFolder();
        using var withoutFile = OpenReady(_context, none);
        var noFileNotes = withoutFile.Rules.Items.OfType<GroupNoteViewModel>().Select(note => note.Text).ToList();
        var heading = withoutFile.Rules.Items.OfType<GroupHeadingViewModel>().Last();
        _context.Report.Check("file-states", "no .editorconfig: under the heading, in the expected words, with no count and no Open file", noFileNotes.Contains("No .editorconfig in invoicing.") && !heading.ShowsCount && !heading.HasOpenFile, "notes: " + string.Join("; ", noFileNotes));

        var broken = _context.Folders.NewFolder();
        Directory.CreateDirectory(Path.Combine(broken, ".editorconfig"));
        var cliError = _context.Cli(broken, "rules", "list").Error.Trim();
        using var unreadable = OpenReady(_context, broken);
        var problem = unreadable.Rules.Items.OfType<GroupNoteViewModel>().Select(note => note.Text).ToList();
        _context.Report.Check("file-states", "a file that cannot be read: under the heading, the command line's own sentence", cliError.Length > 0 && problem.Count == 1 && problem[0] == cliError, "cli: " + cliError, "window: " + string.Join("; ", problem));
    }

    private void NoControl()
    {
        var folder = FolderWithRepositoryFile(_context);
        using var app = OpenReady(_context, folder);
        var on = app.Rules.Items.OfType<FileRuleRowViewModel>().First(row => row.Id == "IDE0055");
        var off = app.Rules.Items.OfType<FileRuleRowViewModel>().First(row => !row.ShowsStateWord && row.IsOffInFile);
        foreach(var row in new[] { on, off })
        {
            app.Rules.SelectedItem = row;
            app.Rows.ScrollIntoView(row);
            app.Pump(200);
            var container = app.Rows.ContainerFromItem(row) ?? throw new InvalidOperationException("the row is not drawn");
            var buttons = container.GetVisualDescendants().OfType<Button>().Where(button => button.IsEffectivelyVisible).ToList();
            var noEffect = !buttons.Any(button => button.Classes.Contains("effect")) && !container.GetVisualDescendants().OfType<ToggleButton>().Any() && !container.GetLogicalDescendants().OfType<Popup>().Any();
            _context.Report.Check("file-no-control", $"{row.Id}: the row has no effect control, no menu and no switch ({(row.IsOffInFile ? "off in the file" : "on")})", noEffect && buttons.Count(button => button.Classes.Contains("linelink")) == (row.IsOffInFile ? 0 : 1), "buttons: " + string.Join(", ", buttons.Select(button => string.Join(".", button.Classes))));
        }

        var before = CheckContext.SettingsBytes(folder);
        app.Rules.SelectedItem = on;
        app.Rows.FocusEntry(NavigationMethod.Directional);
        app.Press(PhysicalKey.ArrowRight);
        var onLine = on.Part == RowPart.Line && on.RingOnLine;
        app.Press(PhysicalKey.ArrowRight);
        var staysOnLine = on.Part == RowPart.Line;
        app.Press(PhysicalKey.ArrowLeft);
        var backOnRow = on.Part == RowPart.Row;
        _context.Report.Check("file-no-control", "Right from a rule that is on goes to the line of the file (not to an effect control), stays there, and Left goes back", onLine && staysOnLine && backOnRow, $"on line {onLine}, stays {staysOnLine}, back {backOnRow}");
        app.Press(PhysicalKey.Space);
        app.Press(PhysicalKey.F4);
        _context.Report.Check("file-no-control", "Space and F4 on the row change nothing and open no menu", before.AsSpan().SequenceEqual(CheckContext.SettingsBytes(folder)) && app.Rules.BannerText == null && app.Rules.SelectedFileRule == on, "banner " + app.Rules.BannerText);
        app.Rules.SelectedItem = off;
        app.Rows.FocusEntry(NavigationMethod.Directional);
        app.Press(PhysicalKey.ArrowRight);
        _context.Report.Check("file-no-control", "Right on a rule the file switches off moves nowhere", off.Part == RowPart.Row, "part " + off.Part);
    }

    private void Refusal()
    {
        var folder = FolderWithRepositoryFile(_context);
        using var app = OpenReady(_context, folder);
        var before = CheckContext.SettingsBytes(folder);
        var cli = _context.Cli(folder, "rules", "disable", "IDE0055");
        app.Rules.SetEnabled("IDE0055", false);
        var bannerOne = app.Rules.BannerText;
        app.Rules.BannerText = null;
        app.Rules.SetSeverity("IDE0055", Severity.Warn);
        var bannerTwo = app.Rules.BannerText;
        var change = app.Rules.ChangeRule("IDE0055", new RuleChange("Another title", null, null, null, null, null, null, null));
        _context.Report.Check("file-refusal", "switching off a rule of the file is refused with the body's line, the one the command line gives", cli.ExitCode == 3 && cli.Error.Trim() == _ide0055Refusal && bannerOne == _ide0055Refusal, "cli: " + cli.Error.Trim(), "window: " + bannerOne);
        _context.Report.Check("file-refusal", "changing its effect and changing it as a whole are refused with the same line", bannerTwo == _ide0055Refusal && change == _ide0055Refusal, "effect: " + bannerTwo, "change: " + change);
        _context.Report.Check("file-refusal", "no settings file was written", before.AsSpan().SequenceEqual(CheckContext.SettingsBytes(folder)), "the file changed");
    }

    private void PageOfIde0055()
    {
        var folder = FolderWithRepositoryFile(_context);
        var fileLines = File.ReadAllLines(Path.Combine(folder, ".editorconfig"));
        using var app = OpenReady(_context, folder);
        var row = app.Rules.Items.OfType<FileRuleRowViewModel>().First(candidate => candidate.Id == "IDE0055");
        app.Rules.SelectedItem = row;
        app.Rows.ScrollIntoView(row);
        app.Pump(400);
        var page = app.Rules.FilePage;
        var numbers = string.Join(" ", page.Lines.Select(line => line.Number));
        var marked = page.Lines.Where(line => line.IsCurrent).Select(line => line.Number).ToList();
        _context.Report.Check("file-page", "the lines shown are the file's lines round 378, three before and three after", numbers == "375 376 377 378 379 380 381" && page.Lines.All(line => line.Text == fileLines[line.Number - 1]), "numbers: " + numbers);
        _context.Report.Check("file-page", "the marked line is 378 and it is the line that sets IDE0055", marked.SequenceEqual(_ide0055Line) && page.Lines.First(line => line.IsCurrent).Text.Contains("IDE0055", StringComparison.Ordinal), "marked: " + string.Join(",", marked));
        var current = page.Settings.Where(setting => setting.IsCurrent).Select(setting => setting.Words).ToList();
        _context.Report.Check("file-page", "the table has three rows and the one marked 'In the file now' is error", page.Settings.Count == 3 && current.SequenceEqual(_errorWord), "marked: " + string.Join(",", current));
        var view = app.Find<RulesView>(_ => true).First().ShownFilePage();
        var texts = view?.GetVisualDescendants().OfType<TextBlock>().Select(block => block.Text ?? string.Join(string.Empty, block.Inlines?.OfType<Avalonia.Controls.Documents.Run>().Select(run => run.Text) ?? Array.Empty<string>())).ToList() ?? new List<string>();
        var drawn = view != null && texts.Contains("IDE0055") && texts.Contains("Fix formatting") && texts.Contains("On, set in the file") && texts.Contains("Open at line 378") && texts.Contains("line 378 sets this rule") && texts.Contains("In the file now") && texts.Contains(".editorconfig rule");
        _context.Report.Check("file-page", "the page is drawn with the tag, the id, the state as a sentence, the title, the line's head and the mark", drawn, "texts: " + string.Join(" | ", texts.Where(text => text.Length > 0).Take(40)));
        var controls = view?.GetVisualDescendants().OfType<Button>().Where(button => button.IsEffectivelyVisible).Select(button => button.Name ?? string.Join(".", button.Classes)).ToList() ?? new List<string>();
        var noChange = view != null && controls.SequenceEqual(_openAtLineButton) && !view.GetVisualDescendants().OfType<ToggleButton>().Any() && !view.GetVisualDescendants().OfType<TextBox>().Any() && !texts.Any(text => text.Contains("saved", StringComparison.OrdinalIgnoreCase) || text.Contains("Changed here", StringComparison.Ordinal) || text.Contains("When code breaks it", StringComparison.Ordinal));
        _context.Report.Check("file-page", "the page has one button, Open at line 378, and no control, no text box and no sentence about a change", noChange, "buttons: " + string.Join(", ", controls));
        _context.Report.Check("file-page", "one view serves every file rule: choosing another binds the same page", ReferenceEquals(view?.DataContext, page) && CountOfFilePages(app) == 1, "pages " + CountOfFilePages(app));
        var other = app.Rules.Items.OfType<FileRuleRowViewModel>().First(candidate => candidate.Id != "IDE0055" && candidate.ShowsEffect && candidate.Rule.Severity == Severity.Warn);
        app.Rules.SelectedItem = other;
        app.Pump(200);
        _context.Report.Check("file-page", "the state is read as On, set in the file", page.StateSpokenName == "On, set in the file", page.StateSpokenName);
        _context.Report.Check("file-page", $"a rule that warns ({other.Id}) marks the row 'warning, suggestion'; one that is off marks 'silent, none' and reads 'Off, set in the file'", page.Rule?.Id == other.Id && page.Settings.Single(setting => setting.IsCurrent).Words == "warning, suggestion" && FirstOffMarks(app) == "silent, none" && page.StateSpokenName == "Off, set in the file", "marked " + page.Settings.Single(setting => setting.IsCurrent).Words);
    }

    private static string FirstOffMarks(AppWindow app)
    {
        var off = app.Rules.Items.OfType<FileRuleRowViewModel>().First(candidate => candidate.IsOffInFile);
        app.Rules.SelectedItem = off;
        app.Pump(200);

        return app.Rules.FilePage.Settings.Single(setting => setting.IsCurrent).Words;
    }

    private static int CountOfFilePages(AppWindow app)
    {
        return app.Find<FileRulePageView>(_ => true).Count();
    }

    private void ChangedOnDisk()
    {
        var folder = FolderWithRepositoryFile(_context);
        using var app = OpenReady(_context, folder);
        var path = Path.Combine(folder, ".editorconfig");
        var before = app.Rules.Items.OfType<FileRuleRowViewModel>().First(row => row.Id == "IDE0055");
        app.Rules.SelectedItem = before;
        app.Pump(200);
        File.WriteAllText(path, File.ReadAllText(path).Replace("dotnet_diagnostic.IDE0055.severity = error", "dotnet_diagnostic.IDE0055.severity = warning", StringComparison.Ordinal));
        var followed = app.PumpUntil(() => app.Rules.Items.OfType<FileRuleRowViewModel>().First(row => row.Id == "IDE0055").EffectWord == "Warns", 60000);
        var row = app.Rules.Items.OfType<FileRuleRowViewModel>().First(candidate => candidate.Id == "IDE0055");
        _context.Report.Check("file-live", "after .editorconfig is changed on disk the row says Warns, with no restart", followed && ReferenceEquals(row, before), "word " + row.EffectWord);
        _context.Report.Check("file-live", "the selected row stays selected and its page follows: 'warning, suggestion' is marked", app.Rules.SelectedFileRule?.Id == "IDE0055" && app.Rules.FilePage.Settings.Single(setting => setting.IsCurrent).Words == "warning, suggestion", "marked " + app.Rules.FilePage.Settings.SingleOrDefault(setting => setting.IsCurrent)?.Words);
        File.Delete(path);
        var gone = app.PumpUntil(() => !app.Rules.Items.OfType<FileRuleRowViewModel>().Any(), 60000);
        _context.Report.Check("file-live", "after the file is deleted the group says there is none", gone && app.Rules.Items.OfType<GroupNoteViewModel>().Any(note => note.Text == "No .editorconfig in invoicing."), "rows " + app.Rules.Items.OfType<FileRuleRowViewModel>().Count());
    }

    private void UsableWhileReading()
    {
        var folder = FolderWithRepositoryFile(_context);
        var gate = new ManualResetEventSlim(false);
        var services = StandIn(_context.Services, gate);
        using var app = new AppWindow(services, folder, 1440, 900);
        app.Pump(500);
        var heading = app.Rules.Items.OfType<GroupHeadingViewModel>().Last();
        _context.Report.Check("file-loading", "while the listing is not done, the heading stands alone with 'Reading .editorconfig' and no count", heading.IsEditorConfig && heading.Note == "Reading .editorconfig" && !heading.ShowsCount && !app.Rules.FileRulesAreRead && !app.Rules.Items.OfType<FileRuleRowViewModel>().Any(), $"note {heading.Note}, count shown {heading.ShowsCount}");
        var team = app.Rules.Items.OfType<TeamRuleRowViewModel>().ToList();
        app.Rules.SelectedItem = team[1];
        app.Pump(300);
        _context.Report.Check("file-loading", "the team's four rules are in the list and a team rule can be opened while the file's group is still being read", team.Count == 4 && app.Rules.CurrentPage?.Id == "TEAM011" && app.Rules.ShowsPage, "page " + app.Rules.CurrentPage?.Id);
        app.Rules.SearchText = "TEAM012";
        app.Pump(200);
        _context.Report.Check("file-loading", "the search works meanwhile", app.Rules.Items.OfType<TeamRuleRowViewModel>().Count() == 1, "rows " + app.Rules.Items.OfType<TeamRuleRowViewModel>().Count());
        app.Rules.SearchText = string.Empty;
        app.Rules.SelectedItem = app.Rules.Items.OfType<TeamRuleRowViewModel>().First(row => row.Id == "TEAM011");
        app.Pump(200);
        gate.Set();
        var arrived = app.PumpUntil(() => app.Rules.FileRulesAreRead, 60000);
        app.Pump(300);
        _context.Report.Check("file-loading", "when the listing is done the group arrives with its count and its rows, and the open team page stays open", arrived && heading.ShowsCount && heading.Count > 50 && app.Rules.Items.OfType<FileRuleRowViewModel>().Any() && app.Rules.CurrentPage?.Id == "TEAM011", $"count {heading.Count}");
    }

    private static CommitFenceServices StandIn(CommitFenceServices real, ManualResetEventSlim gate)
    {
        var proxy = DispatchProxy.Create<IRuleService, WaitingRules>();
        ((WaitingRules)(object)proxy).Set(real.Rules, gate);

        return new CommitFenceServices(real.ProductInfo, real.Clock, real.Languages, proxy, real.Review, real.Setup, real.Folders, real.Updater, real.UpdateChecks);
    }

    private void OpenTheFile()
    {
        var folder = FolderWithRepositoryFile(_context);
        using var app = OpenReady(_context, folder);
        var asked = new List<string>();
        app.Rules.OpenFileRequested += asked.Add;
        var heading = app.Rules.Items.OfType<GroupHeadingViewModel>().Last();
        heading.OpenFileCommand.Execute(null);
        var row = app.Rules.Items.OfType<FileRuleRowViewModel>().First(candidate => candidate.Id == "IDE0055");
        row.OpenFileCommand.Execute(null);
        app.Rules.FilePage.OpenFileCommand.Execute(null);
        var expected = Path.Combine(folder, ".editorconfig");
        _context.Report.Check("file-open", "Open file, the line of a row and Open at line N each ask the system to open the repository's .editorconfig", asked.Count == 3 && asked.All(path => Path.GetFullPath(path) == Path.GetFullPath(expected)) && File.Exists(asked[0]), "asked: " + string.Join("; ", asked));
        var launcher = TopLevel.GetTopLevel(app.Window)?.Launcher;
        _context.Report.Check("file-open", "in the check the window's launcher is not the person's own program (nothing was started on this machine)", launcher != null && launcher.GetType().Name.Contains("Noop", StringComparison.OrdinalIgnoreCase), "launcher: " + launcher?.GetType().FullName);
    }

    private void HeldHeading()
    {
        var folder = FolderWithRepositoryFile(_context);
        using var app = OpenReady(_context, folder);
        var view = app.Find<RulesView>(_ => true).First();
        var scroller = app.Rows.GetVisualDescendants().OfType<ScrollViewer>().First();
        _context.Report.Check("file-held", "at the top of the list no heading is held (the first is in its own place)", view.StuckHeading == null, "held: " + view.StuckHeading?.Name);
        scroller.Offset = new Vector(0, 1000);
        app.Pump(400);
        _context.Report.Check("file-held", "scrolled into the second group, its heading is held at the top, with its name, count and Open file", view.StuckHeading is { IsEditorConfig: true }, "held: " + view.StuckHeading?.Name);
        scroller.Offset = new Vector(0, 150);
        app.Pump(400);
        _context.Report.Check("file-held", "scrolled a little, the Custom heading is held, over its own rules", view.StuckHeading is { IsCustom: true }, "held: " + view.StuckHeading?.Name);
        scroller.Offset = new Vector(0, 0);
        app.Pump(400);
        _context.Report.Check("file-held", "back at the top it is let go", view.StuckHeading == null, "held: " + view.StuckHeading?.Name);
    }

    private void TabOrder()
    {
        var folder = FolderWithRepositoryFile(_context);
        using var app = OpenReady(_context, folder);
        var segmented = app.Find<SegmentedList>(_ => true).First();
        app.Window.Focus();
        segmented.GetVisualDescendants().OfType<ListBoxItem>().First(item => item.IsSelected).Focus(NavigationMethod.Tab);
        app.Pump(100);
        var names = new List<string>();
        for(var step = 0; step < 6; step++)
        {
            app.Press(PhysicalKey.Tab);
            var focused = app.Window.FocusManager?.GetFocusedElement();
            var inList = focused is Visual visual && app.Rows.IsVisualAncestorOf(visual);
            var word = focused is Control control ? Avalonia.Automation.AutomationProperties.GetName(control) is { Length: > 0 } name ? name : control.GetType().Name : "?";
            names.Add(inList && word != "Open file" ? "the list" : word);
            if(names[^1] == "the list")
                break;
        }

        _context.Report.Check("file-tab", "from the filter, Tab reaches Open file and then the list", names.Count >= 2 && names[0] == "Open file" && names[1] == "the list", "visited: " + string.Join(" > ", names));
        app.Press(PhysicalKey.Tab, Avalonia.Input.RawInputModifiers.Shift);
        var back = app.Window.FocusManager?.GetFocusedElement();
        _context.Report.Check("file-tab", "Shift+Tab from the list goes back to the filter, not to Open file", back is Visual backVisual && !app.Rows.IsVisualAncestorOf(backVisual) && backVisual.FindAncestorOfType<SegmentedList>(true) != null, "focus: " + back?.GetType().Name);
    }

    private void Pictures()
    {
        var folder = FolderWithRepositoryFile(_context);
        var pictures = _context.Options.PicturesFolder;
        var saved = new List<string>();
        foreach(var (width, height, tag) in new[] { (1440, 900, "1440"), (760, 1000, "760") })
        {
            using var app = OpenReady(_context, folder, width, height);
            var scroller = app.Rows.GetVisualDescendants().OfType<ScrollViewer>().First();
            scroller.Offset = new Vector(0, 1000);
            app.Pump(400);
            saved.Add(app.Save(pictures, $"app-filerules-held-{tag}.png"));
            var onRule = app.Rules.Items.OfType<FileRuleRowViewModel>().First(row => row.Id == "IDE0055");
            app.Rules.SelectedItem = onRule;
            app.Rows.ScrollIntoView(onRule);
            app.Pump(300);
            if(app.Rules.IsOnePane)
            {
                saved.Add(app.Save(pictures, $"app-filerule-row-{tag}.png"));
                app.Rules.OpenSelectedRule();
                app.Pump(500);
            }

            saved.Add(app.Save(pictures, $"app-filerule-page-{tag}.png"));
            if(app.Rules.IsOnePane)
            {
                app.Rules.BackCommand.Execute(null);
                app.Pump(300);
            }

            app.Rows.FocusEntry(NavigationMethod.Directional);
            app.Press(PhysicalKey.ArrowUp);
            app.Press(PhysicalKey.ArrowDown);
            saved.Add(app.Save(pictures, $"app-filerule-focus-row-{tag}.png"));
            app.Press(PhysicalKey.ArrowRight);
            saved.Add(app.Save(pictures, $"app-filerule-focus-line-{tag}.png"));
            app.Press(PhysicalKey.ArrowLeft);
            var off = app.Rules.Items.OfType<FileRuleRowViewModel>().First(row => row.IsOffInFile);
            app.Rules.SelectedItem = off;
            app.Rows.ScrollIntoView(off);
            app.Pump(300);
            saved.Add(app.Save(pictures, $"app-filerule-off-row-{tag}.png"));
            if(app.Rules.IsOnePane)
            {
                app.Rules.OpenSelectedRule();
                app.Pump(500);
                saved.Add(app.Save(pictures, $"app-filerule-off-page-{tag}.png"));
                app.Rules.BackCommand.Execute(null);
                app.Pump(300);
            }

            Application.Current!.RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Dark;
            app.Rules.SelectedItem = onRule;
            app.Rows.ScrollIntoView(onRule);
            app.Pump(400);
            saved.Add(app.Save(pictures, $"app-filerule-dark-{tag}.png"));
            Application.Current.RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Light;
            app.Pump(200);
        }

        _context.Report.Check("file-pictures", "the pictures are saved", saved.All(File.Exists) && saved.Count >= 14, string.Join(", ", saved.Select(Path.GetFileName)));
    }

    private class WaitingRules : DispatchProxy
    {
        private IRuleService? _real;
        private ManualResetEventSlim? _gate;

        public void Set(IRuleService real, ManualResetEventSlim gate)
        {
            _real = real;
            _gate = gate;
        }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if(targetMethod?.Name == nameof(IRuleService.ListFileRules))
                _gate!.Wait();

            try
            {
                return targetMethod!.Invoke(_real, args);
            }
            catch(TargetInvocationException exception) when(exception.InnerException != null)
            {
                throw exception.InnerException;
            }
        }
    }
}
