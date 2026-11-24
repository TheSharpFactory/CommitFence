using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using CF.App.Parts;
using CF.App.Rules;
using CF.Core.Findings;

namespace CF.App.Check;

/// <summary>
/// The checks of what a person does to the list: the switch and the effect, through the view model, with the mouse and with
/// the keyboard, each against the byte-for-byte result of the command line; a change made by the command line; the banner;
/// and the keyboard of the list.
/// </summary>
internal sealed class RuleChangeChecks
{
    private readonly CheckContext _context;

    public RuleChangeChecks(CheckContext context)
    {
        _context = context;
    }

    public void Run()
    {
        _context.Guarded("switch", "switching TEAM012 on through the view model equals rules enable", SwitchThroughModel);
        _context.Guarded("effect", "setting an effect through the view model equals rules severity", EffectThroughModel);
        _context.Guarded("click", "clicking the switch and the effect menu equals the command line", ClickSwitchAndMenu);
        _context.Guarded("keys", "the keyboard of the list: rows, parts, switch, menu", KeyboardOfTheList);
        _context.Guarded("keys", "the search field and the group heading take their keys", KeysOfSearchAndHeading);
        _context.Guarded("keys", "Ctrl+2, Ctrl+1 and Ctrl+F of the window", KeysOfTheWindow);
        _context.Guarded("names", "the names that a screen reader is given", NamesForScreenReaders);
        _context.Guarded("watch", "a change made by the command line appears in the list", CommandLineChangeAppears);
        _context.Guarded("banner", "a wrong, a missing and a refused settings file give the banner", Banners);
    }

    private static TeamRuleRowViewModel RowOf(AppWindow app, string id)
    {
        return PageEditChecks.RowOf(app, id);
    }

    private void SwitchThroughModel()
    {
        var folderApp = _context.Folders.NewFolder();
        var folderCli = _context.Folders.NewFolder();
        using var app = _context.Open(folderApp);
        var row = RowOf(app, "TEAM012");
        var wasOff = !row.IsOn;
        row.ToggleSwitchCommand.Execute(null);
        app.Pump(100);
        _context.Cli(folderCli, "rules", "enable", "TEAM012");
        var show = _context.Cli(folderApp, "rules", "show", "TEAM012");
        _context.Report.Check("switch", "TEAM012 starts off, and after the switch commitfence rules show TEAM012 does not say it is off", wasOff && !show.Output.Contains("status:", StringComparison.Ordinal) && show.ExitCode == 0, "was off: " + wasOff, show.Output);
        _context.Report.Check("switch", "the row of TEAM012 is on after the switch", row.IsOn, "IsOn is " + row.IsOn);
        _context.SameBytes("switch", "the settings file is byte for byte what rules enable TEAM012 writes on a second copy", folderApp, folderCli);

        row.ToggleSwitchCommand.Execute(null);
        app.Pump(100);
        _context.Cli(folderCli, "rules", "disable", "TEAM012");
        _context.SameBytes("switch", "switching it off again equals rules disable TEAM012", folderApp, folderCli);
    }

    private void EffectThroughModel()
    {
        var folderApp = _context.Folders.NewFolder();
        var folderCli = _context.Folders.NewFolder();
        using var app = _context.Open(folderApp);
        var row = RowOf(app, "TEAM010");
        row.OpenEffectMenu();
        app.Pump(60);
        var menuWords = app.Find<TextBlock>(block => block.Classes.Contains("entry-label")).Select(block => block.Text).ToList();
        _context.Report.Check("effect", "the menu of the effect shows Block the push and Only warn", menuWords.Contains("Block the push") && menuWords.Contains("Only warn"), "entries drawn: " + string.Join(", ", menuWords));
        row.ChooseCommand.Execute(row.Entries.First(entry => entry.Severity == Severity.Warn));
        app.Pump(100);
        _context.Cli(folderCli, "rules", "severity", "TEAM010", "warn");
        var show = _context.Cli(folderApp, "rules", "show", "TEAM010");
        _context.Report.Check("effect", "TEAM010 now only warns: the row says Warns, the menu is closed, rules show says warn", row.EffectWord == "Warns" && !row.IsEffectMenuOpen && show.Output.Contains("warn", StringComparison.Ordinal), "row says " + row.EffectWord, show.Output);
        _context.SameBytes("effect", "the settings file is byte for byte what rules severity TEAM010 warn writes", folderApp, folderCli);

        row.ChooseCommand.Execute(row.Entries.First(entry => entry.Severity == Severity.Block));
        app.Pump(100);
        _context.Cli(folderCli, "rules", "severity", "TEAM010", "block");
        _context.SameBytes("effect", "choosing Block the push again equals rules severity TEAM010 block", folderApp, folderCli);
    }

    private void ClickSwitchAndMenu()
    {
        var folderApp = _context.Folders.NewFolder();
        var folderCli = _context.Folders.NewFolder();
        using var app = _context.Open(folderApp);
        var row = RowOf(app, "TEAM010");
        var container = app.Rows.ContainerFromItem(row) ?? throw new InvalidOperationException("the row of TEAM010 is not drawn");
        var toggle = app.Find<ToggleButton>(button => button.DataContext == row).First();
        app.Click(toggle);
        _context.Cli(folderCli, "rules", "disable", "TEAM010");
        _context.SameBytes("click", "a click on the switch of TEAM010 equals rules disable TEAM010", folderApp, folderCli);
        _context.Report.Check("click", "the click also selects the row", ReferenceEquals(app.Rules.SelectedItem, row) && !container.Classes.Contains("heading"), "selected: " + app.Rules.SelectedRule?.Id);

        var effect = app.Find<Button>(button => button.Classes.Contains("effect") && button.DataContext == row && button.IsEffectivelyVisible).First();
        app.Click(effect);
        app.Pump(200);
        _context.Report.Check("click", "a click on the effect control opens its menu in the overlay layer", row.IsEffectMenuOpen && app.Find<Button>(entry => entry.Classes.Contains("menu-entry")).Count() == 2, "open: " + row.IsEffectMenuOpen);
        var warn = app.Find<Button>(entry => entry.Classes.Contains("menu-entry") && entry.DataContext is EffectMenuEntry menu && menu.Severity == Severity.Warn).First();
        app.Click(warn);
        _context.Cli(folderCli, "rules", "severity", "TEAM010", "warn");
        _context.SameBytes("click", "a click on Only warn equals rules severity TEAM010 warn, and the menu closes", folderApp, folderCli);
        _context.Report.Check("click", "the menu is closed after the choice", !row.IsEffectMenuOpen, "open: " + row.IsEffectMenuOpen);
    }

    private void KeyboardOfTheList()
    {
        var folderApp = _context.Folders.NewFolder();
        var folderCli = _context.Folders.NewFolder();
        using var app = _context.Open(folderApp);
        var openAtStart = app.Rules.SelectedRule?.Id;
        app.Rows.FocusEntry(NavigationMethod.Tab);
        app.Pump(60);
        _context.Report.Check("keys", "the first rule, TEAM010, is already open when Rules is shown, and the keyboard comes into the list on it", openAtStart == "TEAM010" && app.Rules.SelectedRule?.Id == "TEAM010", "open at the start: " + openAtStart, "selected: " + (app.Rules.SelectedRule?.Id ?? app.Rules.SelectedItem?.GetType().Name));
        var row = app.Rules.SelectedRule ?? throw new InvalidOperationException("no rule is selected");
        var ring = app.Rows.ContainerFromItem(row)?.Classes.Contains("ring") == true;
        _context.Report.Check("keys", "the ring is on the row while the keyboard has the row", ring && row.Part == RowPart.Row, "ring class " + ring + ", part " + row.Part);

        app.Press(PhysicalKey.ArrowRight);
        var onEffect = row.Part == RowPart.Effect && row.RingOnEffect;
        app.Press(PhysicalKey.ArrowRight);
        var onSwitch = row.Part == RowPart.Switch && row.RingOnSwitch;
        _context.Report.Check("keys", "Right moves the keyboard from the row to its effect control and then to its switch, with the ring on each", onEffect && onSwitch, "effect " + onEffect + ", switch " + onSwitch);
        app.Press(PhysicalKey.Space);
        _context.Cli(folderCli, "rules", "disable", "TEAM010");
        _context.SameBytes("keys", "Space on the switch equals rules disable TEAM010", folderApp, folderCli);

        app.Press(PhysicalKey.ArrowLeft);
        app.Press(PhysicalKey.ArrowLeft);
        app.Press(PhysicalKey.F4);
        _context.Report.Check("keys", "Left twice goes back to the row; F4 opens the effect menu", row.Part == RowPart.Row && row.IsEffectMenuOpen, "part " + row.Part + ", open " + row.IsEffectMenuOpen);
        app.Press(PhysicalKey.Escape);
        _context.Report.Check("keys", "Esc closes the menu with no change", !row.IsEffectMenuOpen && row.Severity == Severity.Block, "open " + row.IsEffectMenuOpen + ", severity " + row.Severity);
        app.Press(PhysicalKey.ArrowDown, RawInputModifiers.Alt);
        app.Press(PhysicalKey.ArrowDown);
        app.Press(PhysicalKey.Enter);
        _context.Cli(folderCli, "rules", "severity", "TEAM010", "warn");
        _context.SameBytes("keys", "Alt+Down, Down, Enter chooses Only warn and equals rules severity TEAM010 warn", folderApp, folderCli);
        _context.Report.Check("keys", "the menu is closed and the row selected after the choice", !row.IsEffectMenuOpen && ReferenceEquals(app.Rules.SelectedRule, row), "open " + row.IsEffectMenuOpen);
    }

    private void KeysOfSearchAndHeading()
    {
        var folder = _context.Folders.NewFolder();
        using var app = _context.Open(folder);
        var search = app.Find<TextBox>(box => box.Name == "Search").First();
        search.Focus(NavigationMethod.Tab);
        app.Pump(60);
        app.Rules.SearchText = "team";
        app.Press(PhysicalKey.Escape);
        _context.Report.Check("keys", "Esc in the search clears the text", app.Rules.SearchText.Length == 0, "text: " + app.Rules.SearchText);
        search.Focus(NavigationMethod.Tab);
        app.Press(PhysicalKey.ArrowDown);
        _context.Report.Check("keys", "Down in the search selects the first rule and puts the keyboard on the list", app.Rules.SelectedRule?.Id == "TEAM010" && app.Rows.IsKeyboardFocusWithin, "selected: " + app.Rules.SelectedRule?.Id);
        app.Press(PhysicalKey.ArrowUp);
        var onHeading = app.Rules.SelectedItem is GroupHeadingViewModel;
        app.Press(PhysicalKey.ArrowLeft);
        var folded = !app.Rules.Items.OfType<TeamRuleRowViewModel>().Any();
        app.Press(PhysicalKey.ArrowRight);
        var opened = app.Rules.Items.OfType<TeamRuleRowViewModel>().Count() == 4;
        _context.Report.Check("keys", "Up reaches the group heading; Left folds the group and Right opens it", onHeading && folded && opened, "on heading " + onHeading + ", items after Left " + folded + ", after Right " + opened);
    }

    private void KeysOfTheWindow()
    {
        var folder = _context.Folders.NewFolder();
        using var app = _context.Open(folder);
        app.Press(PhysicalKey.Digit2, RawInputModifiers.Control);
        var review = app.Model.ShowsReview && !app.Model.ShowsRules;
        app.Press(PhysicalKey.Digit1, RawInputModifiers.Control);
        var rules = app.Model.ShowsRules && !app.Model.ShowsReview;
        app.Press(PhysicalKey.F, RawInputModifiers.Control);
        var search = app.Find<TextBox>(box => box.Name == "Search").First();
        _context.Report.Check("keys", "Ctrl+2 shows Review, Ctrl+1 shows Rules, Ctrl+F puts the keyboard in the search", review && rules && search.IsFocused, $"review {review}, rules {rules}, search focused {search.IsFocused}");
    }

    private void NamesForScreenReaders()
    {
        var folder = _context.Folders.NewFolder();
        using var app = _context.Open(folder);
        var on = RowOf(app, "TEAM010");
        var off = RowOf(app, "TEAM012");
        var cases = new List<(string What, string? Seen, string Expected)>
        {
            ("a row that is on", RowName(app, on), "TEAM010, No braces around a single-line if. Custom, for if statement in C#. On. Blocks a push."),
            ("a row that is off", RowName(app, off), "TEAM012, Use IClock, not DateTime.Now. Custom, for usage in C#. Off. Blocks a push when on."),
            ("the effect control", EffectName(app, on), "When code breaks it: blocks the push"),
            ("the effect control of a rule that is off", EffectName(app, off), "When code breaks it: blocks a push when on"),
            ("the switch", SwitchName(app, on), "TEAM010, No braces around a single-line if"),
            ("the search", app.Find<TextBox>(box => box.Name == "Search").Select(AutomationProperties.GetName).FirstOrDefault(), "Search by id or title"),
            ("the filter", app.Find<SegmentedList>(list => true).Select(AutomationProperties.GetName).FirstOrDefault(), "Show rules by what they do to a push"),
            ("a filter segment", app.Find<StackPanel>(panel => panel.DataContext is FilterSegmentViewModel segment && segment.Filter == RuleFilter.Blocks).Select(AutomationProperties.GetName).FirstOrDefault(), "Blocks, 3 rules"),
            ("the group heading", app.Find<Button>(button => button.DataContext is GroupHeadingViewModel).Select(AutomationProperties.GetName).FirstOrDefault(), "Custom, 4 rules, written here, expanded")
        };
        foreach(var (what, seen, expected) in cases)
            _context.Report.Check("names", $"{what} is named \"{expected}\"", seen == expected, "named: " + seen);
    }

    private static string? RowName(AppWindow app, TeamRuleRowViewModel row)
    {
        return app.Find<Grid>(grid => grid.Name == "RowRoot" && grid.DataContext == row).Select(AutomationProperties.GetName).FirstOrDefault();
    }

    private static string? EffectName(AppWindow app, TeamRuleRowViewModel row)
    {
        return app.Find<Button>(button => button.Classes.Contains("effect") && button.DataContext == row).Select(AutomationProperties.GetName).FirstOrDefault();
    }

    private static string? SwitchName(AppWindow app, TeamRuleRowViewModel row)
    {
        return app.Find<ToggleButton>(button => button.DataContext == row).Select(AutomationProperties.GetName).FirstOrDefault();
    }

    private void CommandLineChangeAppears()
    {
        var folder = _context.Folders.NewFolder();
        using var app = _context.Open(folder);
        var row = RowOf(app, "TEAM011");
        var offCount = app.Rules.Filters.First(segment => segment.Filter == RuleFilter.Off).Count;
        _context.Cli(folder, "rules", "disable", "TEAM011");
        var seen = app.PumpUntil(() => !row.IsOn, 8000);
        var offAfter = app.Rules.Filters.First(segment => segment.Filter == RuleFilter.Off).Count;
        _context.Report.Check("watch", "after rules disable TEAM011 run in the folder the row is off, with no restart, and the Off count went up by one", seen && offAfter == offCount + 1, $"row on: {row.IsOn}, Off count {offCount} then {offAfter}");
        _context.Cli(folder, "rules", "severity", "TEAM011", "warn");
        var warned = app.PumpUntil(() => row.Severity == Severity.Warn, 8000);
        var ids = string.Join(" ", app.Rules.Items.OfType<TeamRuleRowViewModel>().Select(rule => rule.Id));
        _context.Report.Check("watch", "a second change (rules severity TEAM011 warn) shows too, and the same four rows stay in the list", warned && ids == "TEAM010 TEAM011 TEAM012 TEAM013", $"severity {row.Severity}, rows [{ids}]");
        _context.Cli(folder, "rules", "remove", "TEAM013");
        var removed = app.PumpUntil(() => app.Rules.Items.OfType<TeamRuleRowViewModel>().All(rule => rule.Id != "TEAM013"), 8000);
        _context.Report.Check("watch", "a rule removed by the command line (rules remove TEAM013) leaves the list", removed, "rows: " + string.Join(" ", app.Rules.Items.OfType<TeamRuleRowViewModel>().Select(rule => rule.Id)));
    }

    private void Banners()
    {
        var wrong = _context.Folders.NewFolder();
        File.WriteAllText(Path.Combine(wrong, SampleFolders.SettingsName), "{ \"rules\": [ { \"id\": ");
        using(var app = _context.Open(wrong))
        {
            var drawn = app.Find<TextBlock>(block => block.Text != null && block.Text.Contains(SampleFolders.SettingsName, StringComparison.Ordinal)).Any();
            _context.Report.Check("banner", "a wrong settings file gives the banner with the file and the reason, drawn, and a way to open the file", app.Rules.HasBanner && app.Rules.CanOpenSettingsFile && drawn, "banner: " + app.Rules.BannerText);
            File.Copy(_context.Options.SampleSettingsPath, Path.Combine(wrong, SampleFolders.SettingsName), true);
            var cleared = app.PumpUntil(() => !app.Rules.HasBanner && app.Rules.Items.Count > 1, 8000);
            _context.Report.Check("banner", "when the file is put right the banner goes and the rules come in", cleared, "banner: " + app.Rules.BannerText);
        }

        var missing = _context.Folders.NewFolder();
        File.Delete(Path.Combine(missing, SampleFolders.SettingsName));
        using(var app = _context.Open(missing))
        {
            _context.Report.Check("banner", "a folder without a settings file gives the banner, with no button to open a file that is not there, and no rows", app.Rules.HasBanner && !app.Rules.CanOpenSettingsFile && app.Rules.Items.Count == 0, "banner: " + app.Rules.BannerText + ", items " + app.Rules.Items.Count);
        }

        var open = _context.Folders.NewFolder();
        using(var app = _context.Open(open))
        {
            File.WriteAllText(Path.Combine(open, SampleFolders.SettingsName), "{ \"rules\": [ { \"id\": \"TEAM010\", \"nonsense\": 1 } ] }");
            var shown = app.PumpUntil(() => app.Rules.HasBanner, 8000);
            _context.Report.Check("banner", "a file that goes wrong while the app is open gives the banner and keeps the rules last read", shown && app.Rules.Items.OfType<TeamRuleRowViewModel>().Count() == 4, "banner: " + app.Rules.BannerText);
        }

        var refused = _context.Folders.NewFolder();
        using(var app = _context.Open(refused))
        {
            var before = CheckContext.SettingsBytes(refused);
            app.Rules.SetEnabled("NOPE", true);
            _context.Report.Check("banner", "a change the service refuses (a rule that is not in the file) is shown as the banner and writes nothing", app.Rules.HasBanner && app.Rules.BannerText!.Contains("NOPE", StringComparison.Ordinal) && before.AsSpan().SequenceEqual(CheckContext.SettingsBytes(refused)), "banner: " + app.Rules.BannerText);
        }
    }
}
