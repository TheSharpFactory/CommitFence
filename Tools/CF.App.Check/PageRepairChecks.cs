using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using AvaloniaEdit.Editing;
using CF.App.Rules.Page;

namespace CF.App.Check;

/// <summary>
/// The checks that are about the page: a removed rule comes back at its own place, Duplicate rule, and the order of
/// the keyboard stops of the page.
/// </summary>
internal sealed class PageRepairChecks
{
    private static readonly string[] _cleanStops =
    {
        "Title of the rule",
        "Rename the rule",
        "The rule is for: if statement",
        "Language: C#",
        "Property: statements in body",
        "Comparison: is 1",
        "Remove the condition: statements in body is 1",
        "Property: body fits on one line",
        "Comparison: is true",
        "Remove the condition: body fits on one line is true",
        "Add a condition",
        "The rule requires. Choose require or forbid",
        "Property: braces",
        "Comparison: is false",
        "the choice of effect",
        "Paste",
        "the code box",
        "TEAM010, No braces around a single-line if",
        "More for TEAM010"
    };

    private static readonly string[] _dirtyButtons = { "Discard", "Save rule" };

    private readonly CheckContext _context;

    public PageRepairChecks(CheckContext context)
    {
        _context = context;
    }

    public void Run()
    {
        _context.Guarded("undo-place", "a removed rule comes back at its own place", UndoAtItsPlace);
        _context.Guarded("duplicate", "Duplicate rule equals rules copy", Duplicate);
        _context.Guarded("duplicate-off", "Duplicate rule keeps a rule that is off, off, as rules copy does", DuplicateOff);
        _context.Guarded("id-one-rule", "add, copy and the page refuse a new id alike", OneRuleForIds);
        _context.Guarded("tab-order", "the keyboard stops of the page are in the design's order", TabOrder);
    }

    private void DuplicateOff()
    {
        var folder = _context.Folders.NewFolder();
        var folderCli = _context.Folders.NewFolder();
        using var app = _context.Open(folder);
        var page = PageEditChecks.OpenPage(app, "TEAM012");
        page.DuplicateRuleCommand.Execute(null);
        app.Pump(400);
        var copy = app.Rules.CurrentPage ?? throw new InvalidOperationException("Duplicate rule opened no page");
        _context.Report.Check("duplicate-off", "the page of the copy shows the switch as the original's: Off", copy.IsDraft && !copy.IsOn && copy.SwitchWord == "Off", $"IsOn {copy.IsOn}, word {copy.SwitchWord}");
        copy.SaveCommand.Execute(null);
        app.Pump(400);
        _context.Cli(folderCli, "rules", "copy", "TEAM012", "TEAM014");
        _context.SameBytes("duplicate-off", "after Save rule the settings file is byte for byte what rules copy TEAM012 TEAM014 writes", folder, folderCli);
        var listed = _context.ListedByCommandLine(folder).FirstOrDefault(rule => rule.Id == "TEAM014");
        _context.Report.Check("duplicate-off", "rules list shows TEAM014 off", listed is { IsOff: true }, "listed " + listed);
    }

    private void OneRuleForIds()
    {
        const string expectedLine = "'bad id' is not a rule id. An id is letters, digits, '-', '_' and '.', with no space.";
        var folder = _context.Folders.NewFolder();
        var folderAdd = _context.Folders.NewFolder();
        var folderCopy = _context.Folders.NewFolder();
        var before = CheckContext.SettingsBytes(folder);
        using var app = _context.Open(folder);
        var draft = new CF.Core.Rules.RuleDraft("bad id", null, "if-statement", CF.Core.Findings.Severity.Block, "T", new List<CF.Core.Rules.ConditionDraft>(), new List<CF.Core.Rules.ConditionDraft> { new("braces", new List<string> { "false" }, false) }, false, new List<string>(), null, null);
        var fromPage = app.Rules.AddRule(draft, null);
        var add = _context.Cli(folderAdd, "rules", "add", "bad id", "--for", "if-statement", "--title", "T", "--require", "braces=false");
        var copy = _context.Cli(folderCopy, "rules", "copy", "TEAM010", "bad id");
        _context.Report.Check("id-one-rule", "the page's Save refuses with the line", fromPage != null && fromPage.Contains(expectedLine), "page said: " + fromPage);
        _context.Report.Check("id-one-rule", "rules add refuses with the same line, exit 3", add.ExitCode == 3 && add.Error.Contains(expectedLine), "said: " + add.Error);
        _context.Report.Check("id-one-rule", "rules copy refuses with the same line, exit 3", copy.ExitCode == 3 && copy.Error.Contains(expectedLine), "said: " + copy.Error);
        var unchanged = before.AsSpan().SequenceEqual(CheckContext.SettingsBytes(folder)) && before.AsSpan().SequenceEqual(CheckContext.SettingsBytes(folderAdd)) && before.AsSpan().SequenceEqual(CheckContext.SettingsBytes(folderCopy));
        _context.Report.Check("id-one-rule", "no settings file changed", unchanged, "a file was written");
    }

    private static bool Named(AppWindow app, string name)
    {
        return app.Find<Control>(control => control.IsEffectivelyVisible && AutomationProperties.GetName(control) == name).Any();
    }

    private void UndoAtItsPlace()
    {
        foreach(var id in new[] { "TEAM010", "TEAM011", "TEAM012", "TEAM013" })
        {
            var folder = _context.Folders.NewFolder();
            var original = CheckContext.SettingsBytes(folder);
            using var app = _context.Open(folder);
            PageEditChecks.OpenPage(app, id).RemoveRuleCommand.Execute(null);
            app.Pump(300);
            var removed = CheckContext.SettingsBytes(folder);
            app.Rules.UndoRemoveCommand.Execute(null);
            app.Pump(300);
            var restored = CheckContext.SettingsBytes(folder);
            _context.Report.Check("undo-place", $"{id}: Remove rule then Undo leaves the settings file byte for byte as it was", !removed.AsSpan().SequenceEqual(original) && restored.AsSpan().SequenceEqual(original), $"original {CheckContext.Hex(original)}, after remove {CheckContext.Hex(removed)}, after undo {CheckContext.Hex(restored)}");
        }
    }

    private void Duplicate()
    {
        var folder = _context.Folders.NewFolder();
        var folderCli = _context.Folders.NewFolder();
        var before = CheckContext.SettingsBytes(folder);
        using var app = _context.Open(folder);
        var page = PageEditChecks.OpenPage(app, "TEAM010");
        page.IsMoreOpen = true;
        app.Pump(400);
        _context.Report.Check("duplicate", "the menu of the page holds Duplicate rule and Remove rule", Named(app, "Duplicate rule") && Named(app, "Remove rule"), "the menu is not drawn with both entries");
        app.Save(_context.Options.PicturesFolder, "app-page-menu-both-1440.png");
        page.DuplicateRuleCommand.Execute(null);
        app.Pump(400);
        var copy = app.Rules.CurrentPage ?? throw new InvalidOperationException("Duplicate rule opened no page");
        var same = copy.IsDraft && copy.Id == "TEAM014" && copy.Title == "Copy of No braces around a single-line if" && copy.ArtifactName == page.ArtifactName && copy.IsForbid == page.IsForbid && copy.Severity == page.Severity && copy.WhereFlow.Count == page.WhereFlow.Count && copy.RequireFlow.Count == page.RequireFlow.Count;
        _context.Report.Check("duplicate", "the page that opens is a new rule TEAM014 titled 'Copy of No braces around a single-line if', with the same sentence and effect", same, $"draft {copy.IsDraft}, id {copy.Id}, title {copy.Title}");
        _context.Report.Check("duplicate", "it is unsaved (Unsaved, Discard and Save rule drawn) and opening it wrote nothing", copy.IsDirty && Named(app, "Discard") && Named(app, "Save rule") && before.AsSpan().SequenceEqual(CheckContext.SettingsBytes(folder)), $"dirty {copy.IsDirty}");
        copy.SaveCommand.Execute(null);
        app.Pump(400);
        _context.Cli(folderCli, "rules", "copy", "TEAM010", "TEAM014");
        _context.SameBytes("duplicate", "after Save rule the settings file is byte for byte what rules copy TEAM010 TEAM014 writes", folder, folderCli);
        var order = string.Join(" ", _context.ListedByCommandLine(folder).Select(rule => rule.Id));
        _context.Report.Check("duplicate", "the copy stands right after TEAM010, is selected, and its page says Saved", order == "TEAM010 TEAM014 TEAM011 TEAM012 TEAM013" && app.Rules.CurrentPage?.Id == "TEAM014" && app.Rules.CurrentPage.IsSaved && !app.Rules.CurrentPage.IsDirty, "order " + order, "current " + app.Rules.CurrentPage?.Id);
    }

    private static string NameOf(Visual? visual)
    {
        if(visual is TextArea)
            return "the code box";

        if(visual is ListBoxItem)
            return "the choice of effect";

        if(visual is not Control control)
            return "?";

        var name = AutomationProperties.GetName(control);
        return string.IsNullOrEmpty(name) ? control.Name ?? control.GetType().Name : name;
    }

    private static Visual? Focused(AppWindow app)
    {
        return app.Window.FocusManager?.GetFocusedElement() as Visual;
    }

    private static List<string> Walk(AppWindow app, RulePageView view, bool forward)
    {
        var modifiers = forward ? Avalonia.Input.RawInputModifiers.None : Avalonia.Input.RawInputModifiers.Shift;
        var names = new List<string> { NameOf(Focused(app)) };
        for(var step = 0; step < 40; step++)
        {
            var before = Focused(app);
            if(before is TextArea)
                app.Press(PhysicalKey.Escape);

            app.Press(PhysicalKey.Tab, modifiers);
            var after = Focused(app);
            if(ReferenceEquals(before, after))
                break;

            if(after == null || !view.IsVisualAncestorOf(after) || names.Contains(NameOf(after)))
            {
                (before as InputElement)?.Focus(NavigationMethod.Tab);
                break;
            }

            names.Add(NameOf(after));
        }

        return names;
    }

    private void TabOrder()
    {
        var folder = _context.Folders.NewFolder();
        using var app = _context.Open(folder, 1440, 900);
        var page = PageEditChecks.OpenPage(app, "TEAM010");
        var view = app.Find<RulePageView>(candidate => candidate.IsEffectivelyVisible).First();
        var emptyStops = _cleanStops.ToList();
        emptyStops.Insert(emptyStops.IndexOf("Paste") + 1, "Load an example");
        CheckWalk(app, view, "in two panes", emptyStops);

        TryItChecks.PutCode(app, page, "x");
        var withClear = _cleanStops.ToList();
        withClear.Insert(withClear.IndexOf("Paste") + 1, "Clear");
        CheckWalk(app, view, "in two panes with code in the box (Clear is drawn)", withClear);

        PageEditChecks.WhereOf(page, 0).SetValues(new List<string> { "2" });
        app.Pump(300);
        var dirty = withClear.ToList();
        dirty.InsertRange(dirty.IndexOf("More for TEAM010"), _dirtyButtons);
        dirty[dirty.IndexOf("Comparison: is 1")] = "Comparison: is 2";
        dirty[dirty.IndexOf("Remove the condition: statements in body is 1")] = "Remove the condition: statements in body is 2";
        CheckWalk(app, view, "in two panes after an edit (Discard and Save rule are drawn)", dirty);
        page.DiscardCommand.Execute(null);
        TryItChecks.PutCode(app, page, string.Empty);

        app.Resize(760, 1000);
        app.Rules.BackCommand.Execute(null);
        app.Pump(300);
        app.Rules.OpenSelectedRule();
        app.Pump(500);
        var onePane = _cleanStops.ToList();
        onePane.Insert(onePane.IndexOf("Paste") + 1, "Load an example");
        onePane.Insert(0, "Back to the rules");
        CheckWalk(app, view, "in one pane (the back button is first)", onePane);
    }

    private void CheckWalk(AppWindow app, RulePageView view, string layout, IReadOnlyList<string> expected)
    {
        app.Window.Focus();
        app.Pump(200);
        view.TabStops()[0].Focus(NavigationMethod.Tab);
        app.Pump(100);
        var forward = Walk(app, view, true);
        var backward = Walk(app, view, false);
        backward.Reverse();
        _context.Report.Check("tab-order", $"{layout}: Tab from the first stop to the last visits the design's order", forward.SequenceEqual(expected), "visited: " + string.Join(" > ", forward), "expected: " + string.Join(" > ", expected));
        _context.Report.Check("tab-order", $"{layout}: Shift+Tab from the last stop back visits the same stops in the opposite order", backward.SequenceEqual(expected), "visited: " + string.Join(" > ", backward));
    }
}
