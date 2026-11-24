using System.Diagnostics;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Threading;
using CF.App.Parts;
using CF.App.Rules;
using CF.App.Rules.Page;

namespace CF.App.Check;

/// <summary>
/// The checks of the page that need the drawn window: what the pickers offer against <c>commitfence catalog</c>, that choosing
/// another rule builds nothing, the keys, the names for screen readers, the one-pane page, the colours of the tokens, and the
/// pictures of the page.
/// </summary>
internal sealed partial class PageLookChecks
{
    private readonly CheckContext _context;

    public PageLookChecks(CheckContext context)
    {
        _context = context;
    }

    public void Run()
    {
        _context.Guarded("page-pickers", "every picker offers exactly what commitfence catalog prints", PickersOfferTheCatalog);
        _context.Guarded("page-select", "choosing each rule in turn, 50 times, builds no control after the first pass", SelectingBuildsNothing);
        _context.Guarded("page-keys", "the keys of the page", KeysOfThePage);
        _context.Guarded("page-names", "the names that a screen reader is given", NamesForScreenReaders);
        _context.Guarded("page-onepane", "in one pane the page comes over the list, with its way back", OnePane);
        _context.Guarded("page-colours", "the tokens are drawn in the design's colours", TokenColours);
        _context.Guarded("pictures", "the pictures of the page are saved", SavePictures);
    }

    [GeneratedRegex(@"^    (?<name>\S+)\s+(?<kind>number|true or false|text)\s+(?<cost>reads the file|needs the build)\s")]
    private static partial Regex PropertyLine();

    private static bool Named(AppWindow app, string name)
    {
        return app.Find<Control>(control => control.IsEffectivelyVisible && AutomationProperties.GetName(control) == name).Any();
    }

    private static Button Token(AppWindow app, string name, object? context = null)
    {
        return app.Find<Button>(button => button.Name == name && button.IsEffectivelyVisible && (context == null || ReferenceEquals(button.DataContext, context))).First();
    }

    /// <summary>
    /// What <c>commitfence catalog csharp</c> prints: for each artifact type its properties with kind of value and cost.
    /// </summary>
    private List<(string Artifact, List<(string Name, string Kind, string Cost)> Properties)> CatalogOf(string folder)
    {
        var artifacts = new List<(string Artifact, List<(string Name, string Kind, string Cost)> Properties)>();
        foreach(var line in _context.Cli(folder, "catalog", "csharp").Output.Split('\n').Select(text => text.TrimEnd('\r')))
        {
            var property = PropertyLine().Match(line);
            if(property.Success)
                artifacts[^1].Properties.Add((property.Groups["name"].Value, property.Groups["kind"].Value, property.Groups["cost"].Value));
            else if(line.StartsWith("  ", StringComparison.Ordinal) && !line.StartsWith("    ", StringComparison.Ordinal))
                artifacts.Add((line.Trim().Split(':')[0].Split(' ')[0], new List<(string Name, string Kind, string Cost)>()));
        }

        return artifacts;
    }

    private static List<string> OfferedBy(PickerViewModel picker, string group)
    {
        var offered = new List<string>();
        var inGroup = false;
        foreach(var item in picker.Items)
        {
            if(item is PickerGroupLabel label)
                inGroup = label.Text == group;
            else if(inGroup && item is PickerEntryViewModel entry)
                offered.Add($"{entry.Key} {(entry.KindText == "one of a list" ? "text" : entry.KindText)}");
        }

        return offered;
    }

    private void PickersOfferTheCatalog()
    {
        var folder = _context.Folders.NewFolder();
        var catalog = CatalogOf(folder);
        using var app = _context.Open(folder);
        app.Rules.NewRuleFromHeadCommand.Execute(null);
        app.Pump(400);
        var page = app.Rules.CurrentPage ?? throw new InvalidOperationException("no draft page");
        page.OpenArtifactPicker(false);
        var artifacts = page.ArtifactPicker.Items.OfType<PickerEntryViewModel>().Select(entry => entry.Key).ToList();
        var expected = catalog.Select(entry => entry.Artifact).ToList();
        _context.Report.Check("page-pickers", "the picker of the artifacts offers " + string.Join(", ", expected), artifacts.SequenceEqual(expected), "offered: " + string.Join(", ", artifacts));
        page.ArtifactPicker.Close();

        foreach(var (artifact, properties) in catalog)
        {
            page.OpenArtifactPicker(false);
            page.ArtifactPicker.ChooseCommand.Execute(page.ArtifactPicker.Items.OfType<PickerEntryViewModel>().First(entry => entry.Key == artifact));
            var condition = page.RequireFlow[0];
            condition.OpenPropertyPicker(false);
            var quick = properties.Where(property => property.Cost == "reads the file").Select(property => $"{property.Name} {property.Kind}").ToList();
            var build = properties.Where(property => property.Cost == "needs the build").Select(property => $"{property.Name} {property.Kind}").ToList();
            var offeredQuick = OfferedBy(condition.PropertyPicker, "Read from the file: runs in quick mode");
            var offeredBuild = OfferedBy(condition.PropertyPicker, "Needs the build: full review only");
            var findRow = condition.PropertyPicker.HasFindRow == properties.Count > 8;
            _context.Report.Check("page-pickers", $"the property picker of {artifact} offers its {properties.Count} properties, each with its kind of value (the picker says one of a list for a text that has a list of values), under the group of its cost; the find row shows from nine entries", offeredQuick.SequenceEqual(quick) && offeredBuild.SequenceEqual(build) && findRow, "reads the file: " + string.Join(", ", offeredQuick), "needs the build: " + string.Join(", ", offeredBuild), "the command line says: " + string.Join(", ", quick) + " | " + string.Join(", ", build));
            condition.PropertyPicker.Close();
        }

        ComparisonPickers(app, page);
    }

    private void ComparisonPickers(AppWindow app, RulePageViewModel page)
    {
        page.OpenArtifactPicker(false);
        page.ArtifactPicker.ChooseCommand.Execute(page.ArtifactPicker.Items.OfType<PickerEntryViewModel>().First(entry => entry.Key == "method"));
        var condition = page.RequireFlow[0];
        var seen = new List<string>();
        foreach(var (property, comparisons, field) in new[] { ("fitsOnOneLine", "is", false), ("lines", "is, is one of", true), ("name", "is, is one of", true) })
        {
            PageEditChecks.ChooseProperty(condition, property);
            condition.OpenComparisonPicker(false);
            var offered = string.Join(", ", condition.ComparisonPicker.Comparisons.Select(entry => entry.Key));
            var values = string.Join(", ", condition.ComparisonPicker.ValueEntries.Select(entry => entry.Key));
            var good = offered == comparisons && condition.ComparisonPicker.ShowsField == field && (field || values == "true, false");
            seen.Add($"{property}: comparisons [{offered}], field {condition.ComparisonPicker.ShowsField}, values [{values}]");
            _context.Report.Check("page-pickers", $"the comparison picker of {property} offers the comparisons [{comparisons}] and {(field ? "a field for the value" : "the values true and false")}", good, seen[^1]);
            condition.ComparisonPicker.Close();
        }

        app.Pump(50);
    }

    private void SelectingBuildsNothing()
    {
        var folder = _context.Folders.NewFolder();
        using var app = _context.Open(folder);
        var host = app.Find<RulesView>(view => true).First();
        var rows = app.Rules.Items.OfType<TeamRuleRowViewModel>().ToList();
        var counts = new List<int>();
        var clock = new Stopwatch();
        var steps = 0;
        for(var pass = 0; pass < 50; pass++)
        {
            foreach(var row in rows)
            {
                clock.Start();
                app.Rules.SelectedItem = row;
                app.Window.UpdateLayout();
                Dispatcher.UIThread.RunJobs();
                clock.Stop();
                steps++;
            }

            if(pass < 2 || pass == 49)
                counts.Add(host.PageControlCount);
        }

        var average = clock.Elapsed.TotalMilliseconds / steps;
        _context.Report.Check("page-select", $"the page host holds {counts[0]} controls after the first pass and {counts[1]} after the second and {counts[2]} after the fiftieth (every visual child under the page host counted); choosing a rule took {average:0.0} ms on average", counts[0] > 0 && counts[1] == counts[0] && counts[2] == counts[0], $"counts: {string.Join(", ", counts)}");
    }

    private void KeysOfThePage()
    {
        var folder = _context.Folders.NewFolder();
        using var app = _context.Open(folder);
        var page = PageEditChecks.OpenPage(app, "TEAM010");
        var title = app.Find<TextBox>(box => box.Name == "TitleBox" && box.IsEffectivelyVisible).First();
        Token(app, "ArtifactButton").Focus(NavigationMethod.Tab);
        app.Pump(60);
        app.Press(PhysicalKey.F2);
        _context.Report.Check("page-keys", "F2 puts the keyboard in the title", title.IsFocused, "title focused " + title.IsFocused);

        var first = PageEditChecks.WhereOf(page, 0);
        var propertyToken = Token(app, "PropertyButton", first);
        propertyToken.Focus(NavigationMethod.Tab);
        app.Pump(60);
        app.Press(PhysicalKey.F4);
        app.Pump(200);
        _context.Report.Check("page-keys", "F4 on a token opens its picker", first.PropertyPicker.IsOpen, "open " + first.PropertyPicker.IsOpen);
        app.Press(PhysicalKey.ArrowDown);
        app.Press(PhysicalKey.ArrowDown);
        app.Press(PhysicalKey.Enter);
        app.Pump(100);
        _context.Report.Check("page-keys", "in the picker Down, Down, Enter chooses the property two entries after the current one, the picker closes and the keyboard is back on the token", first.PropertyName == "fitsOnOneLine" && !first.PropertyPicker.IsOpen && propertyToken.IsFocused, $"property {first.PropertyName}, open {first.PropertyPicker.IsOpen}, token focused {propertyToken.IsFocused}");

        var second = PageEditChecks.WhereOf(page, 1);
        Token(app, "PropertyButton", second).Focus(NavigationMethod.Tab);
        app.Pump(60);
        app.Press(PhysicalKey.Delete);
        _context.Report.Check("page-keys", "Delete on a token removes its condition", page.WhereFlow.OfType<ConditionViewModel>().Count() == 1, "conditions " + page.WhereFlow.OfType<ConditionViewModel>().Count());

        var before = CheckContext.SettingsBytes(folder);
        app.Press(PhysicalKey.S, RawInputModifiers.Control);
        app.Pump(200);
        _context.Report.Check("page-keys", "Ctrl+S saves the rule", !page.IsDirty && !before.AsSpan().SequenceEqual(CheckContext.SettingsBytes(folder)), "dirty " + page.IsDirty);

        app.Resize(760, 1000);
        app.Rules.SelectedItem = PageEditChecks.RowOf(app, "TEAM011");
        app.Rows.FocusEntry(NavigationMethod.Directional);
        app.Pump(100);
        var listOnly = !app.Rules.ShowsPage && app.Find<Border>(border => border.Name == "ListColumn").First().IsVisible;
        app.Press(PhysicalKey.Enter);
        app.Pump(300);
        var over = app.Rules.ShowsPage && !app.Find<Border>(border => border.Name == "ListColumn").First().IsVisible;
        app.Press(PhysicalKey.Escape);
        app.Pump(300);
        var back = !app.Rules.ShowsPage && app.Find<Border>(border => border.Name == "ListColumn").First().IsVisible && app.Rows.IsKeyboardFocusWithin;
        _context.Report.Check("page-keys", "in one pane a selected row opens nothing; Enter opens its page over the list; Esc brings the list back with the keyboard on it", listOnly && over && back, $"list only {listOnly}, page over {over}, back {back}");
    }

    private void NamesForScreenReaders()
    {
        var folder = _context.Folders.NewFolder();
        using var app = _context.Open(folder);
        var page = PageEditChecks.OpenPage(app, "TEAM010");
        var names = new[]
        {
            "The rule is for: if statement", "Language: C#", "Property: statements in body", "Comparison: is 1", "The rule requires. Choose require or forbid",
            "Add a condition", "Remove the condition: statements in body is 1", "More for TEAM010", "Saved", "Title of the rule", "TEAM010, No braces around a single-line if", "When code breaks it"
        };
        foreach(var name in names)
            _context.Report.Check("page-names", $"a part of the page is named \"{name}\"", Named(app, name), "not found among the visible parts");

        PageEditChecks.WhereOf(page, 0).SetValues(new List<string> { "2" });
        app.Pump(100);
        _context.Report.Check("page-names", "after an edit the state is named \"Unsaved changes\"", Named(app, "Unsaved changes") && !Named(app, "Saved"), "named Unsaved changes: " + Named(app, "Unsaved changes"));
        _context.Report.Check("page-names", "the state is a live region", app.Find<Control>(control => control.IsEffectivelyVisible && AutomationProperties.GetName(control) == "Unsaved changes").Any(control => AutomationProperties.GetLiveSetting(control) == AutomationLiveSetting.Polite), "no visible part named Unsaved changes is a live region");
    }

    private void OnePane()
    {
        var folder = _context.Folders.NewFolder();
        using var app = _context.Open(folder, 760, 1000);
        var row = PageEditChecks.RowOf(app, "TEAM010");
        var title = app.Find<TextBlock>(block => block.Classes.Contains("rule-title") && block.DataContext == row).First();
        app.Click(title);
        app.Pump(300);
        var listHidden = !app.Find<Border>(border => border.Name == "ListColumn").First().IsVisible;
        _context.Report.Check("page-onepane", "at 760 wide a click on a row puts its page over the list: the list is not drawn and the back button is", app.Rules.ShowsPage && listHidden && Named(app, "Back to the rules"), $"shows page {app.Rules.ShowsPage}, list hidden {listHidden}");
        app.Click(app.Find<Button>(button => AutomationProperties.GetName(button) == "Back to the rules" && button.IsEffectivelyVisible).First());
        app.Pump(300);
        _context.Report.Check("page-onepane", "the back button brings the list back", !app.Rules.ShowsPage && app.Find<Border>(border => border.Name == "ListColumn").First().IsVisible, "shows page " + app.Rules.ShowsPage);
        app.Rules.NewRuleFromHeadCommand.Execute(null);
        app.Pump(400);
        _context.Report.Check("page-onepane", "New rule opens the page of a draft over the list, with the back button", app.Rules.ShowsPage && app.Rules.CurrentPage?.IsDraft == true && Named(app, "Back to the rules"), "current " + app.Rules.CurrentPage?.Id);
    }

    private void TokenColours()
    {
        var folder = _context.Folders.NewFolder();
        using var app = _context.Open(folder);
        var page = PageEditChecks.OpenPage(app, "TEAM010");
        app.Pump(500);
        using var frame = app.Capture();
        var artifact = Token(app, "ArtifactButton");
        var where = Token(app, "PropertyButton", PageEditChecks.WhereOf(page, 0));
        var require = Token(app, "PropertyButton", page.RequireFlow[0]);
        (int X, int Y) At(Visual visual, double x)
        {
            var point = visual.TranslatePoint(new Point(x, visual.Bounds.Height / 2), app.Window) ?? throw new InvalidOperationException("not on the window");
            return ((int)point.X, (int)point.Y);
        }

        var cases = new[]
        {
            ("the artifact token (for)", "#E6DCFF", At(artifact, 4)),
            ("a token of the where clause (where)", "#CFECFF", At(where, 4)),
            ("the token of the require clause (req)", "#CFF3E0", At(require, 4)),
            ("the sentence panel (well)", "#F7F7F7", At(artifact, -112))
        };
        foreach(var (name, expected, at) in cases)
        {
            var seen = AppWindow.PixelAt(frame, at.X, at.Y);
            _context.Report.Check("page-colours", $"{name} is {expected}", string.Equals(expected, seen, StringComparison.OrdinalIgnoreCase), "the pixel is " + seen);
        }
    }

    private static void Focus(AppWindow app, Control control)
    {
        control.Focus(NavigationMethod.Tab);
        app.Pump(200);
    }

    private void SavePictures()
    {
        var folder = _context.Folders.NewFolder();
        var pictures = _context.Options.PicturesFolder;
        using var app = _context.Open(folder, 1440, 900);
        var saved = new List<(string Path, int Width, int Height)>();
        var page = PageEditChecks.OpenPage(app, "TEAM010");
        PageEditChecks.WhereOf(page, 0).SetValues(new List<string> { "2" });
        app.Pump(400);
        saved.Add((app.Save(pictures, "app-page-unsaved-1440.png"), 1440, 900));
        Focus(app, app.Find<Button>(button => AutomationProperties.GetName(button) == "Save rule" && button.IsEffectivelyVisible).First());
        saved.Add((app.Save(pictures, "app-page-focus-save-1440.png"), 1440, 900));
        page.DiscardCommand.Execute(null);
        app.Window.Focus();
        app.Pump(400);
        saved.Add((app.Save(pictures, "app-page-saved-1440.png"), 1440, 900));

        PageEditChecks.WhereOf(page, 0).OpenPropertyPicker(false);
        app.Pump(400);
        saved.Add((app.Save(pictures, "app-page-picker-1440.png"), 1440, 900));
        PageEditChecks.WhereOf(page, 0).PropertyPicker.Close();
        PageEditChecks.WhereOf(page, 1).OpenComparisonPicker(false);
        app.Pump(400);
        saved.Add((app.Save(pictures, "app-page-picker-comparison-1440.png"), 1440, 900));
        PageEditChecks.WhereOf(page, 1).ComparisonPicker.Close();
        page.IsMoreOpen = true;
        app.Pump(400);
        saved.Add((app.Save(pictures, "app-page-menu-1440.png"), 1440, 900));
        page.IsMoreOpen = false;
        app.Pump(200);

        Focus(app, Token(app, "PropertyButton", PageEditChecks.WhereOf(page, 0)));
        saved.Add((app.Save(pictures, "app-page-focus-token-1440.png"), 1440, 900));
        Focus(app, app.Find<ToggleButton>(button => button.Classes.Contains("cfswitch") && button.IsEffectivelyVisible && ReferenceEquals(button.DataContext, page)).First());
        saved.Add((app.Save(pictures, "app-page-focus-switch-1440.png"), 1440, 900));
        var choice = app.Find<SegmentedList>(list => list.IsEffectivelyVisible && ReferenceEquals(list.DataContext, page)).First();
        Focus(app, choice.ContainerFromIndex(0) ?? throw new InvalidOperationException("no segment"));
        saved.Add((app.Save(pictures, "app-page-focus-segment-1440.png"), 1440, 900));
        Focus(app, app.Find<Button>(button => AutomationProperties.GetName(button) == "Add a condition" && button.IsEffectivelyVisible).First());
        saved.Add((app.Save(pictures, "app-page-focus-add-1440.png"), 1440, 900));
        Focus(app, app.Find<Button>(button => button.Classes.Contains("remove") && button.IsEffectivelyVisible).First());
        saved.Add((app.Save(pictures, "app-page-focus-remove-1440.png"), 1440, 900));

        app.Resize(760, 1000);
        app.Rules.BackCommand.Execute(null);
        app.Pump(300);
        app.Rules.OpenSelectedRule();
        app.Window.Focus();
        app.Pump(400);
        saved.Add((app.Save(pictures, "app-page-760.png"), 760, 1000));
        PageEditChecks.WhereOf(page, 0).SetValues(new List<string> { "2" });
        app.Pump(400);
        saved.Add((app.Save(pictures, "app-page-unsaved-760.png"), 760, 1000));

        var wrong = saved.Where(file => !File.Exists(file.Path) || LookChecks.PngSize(file.Path) != (file.Width, file.Height)).Select(file => file.Path).ToList();
        _context.Report.Check("pictures", $"{saved.Count} pictures of the page saved in {pictures}, each of the size asked for", wrong.Count == 0, wrong.ToArray());
    }
}
