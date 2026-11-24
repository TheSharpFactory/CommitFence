using Avalonia.Controls;
using CF.App.Rules;
using CF.App.Rules.Page;
using CF.Core.Findings;

namespace CF.App.Check;

/// <summary>
/// The checks of what a person does on the page of a rule: each kind of edit, saved, against the byte-for-byte result of
/// <c>rules set</c> on a second copy; the state "Unsaved" and "Saved"; the switch and the effect; a change made by the command
/// line while the page is open; a new rule against <c>rules add</c>; removing against <c>rules remove</c>, and Undo; and a
/// refusal by the service.
/// </summary>
internal sealed class PageEditChecks
{
    private readonly CheckContext _context;

    public PageEditChecks(CheckContext context)
    {
        _context = context;
    }

    public void Run()
    {
        _context.Guarded("page", "the page of TEAM010 holds what it should", PageOpens);
        _context.Guarded("page-edit", "each kind of edit, saved, equals rules set on a second copy", EachEditEqualsTheCommandLine);
        _context.Guarded("page-state", "Unsaved and Saved follow the edits, Discard restores, Save rule is offered when there is something to save", UnsavedAndSaved);
        _context.Guarded("page-switch", "the switch and the effect are saved at once and the page and the row agree", SwitchAndEffect);
        _context.Guarded("page-watch", "a change made by the command line shows on the open page", ChangeByCommandLine);
        _context.Guarded("page-new", "a new rule equals rules add", NewRule);
        _context.Guarded("page-remove", "removing equals rules remove, and Undo brings it back", RemoveAndUndo);
        _context.Guarded("page-refusal", "a refusal by the service is shown and loses no edit", Refusal);
    }

    public static TeamRuleRowViewModel RowOf(AppWindow app, string id)
    {
        return app.Rules.Items.OfType<TeamRuleRowViewModel>().First(row => row.Id == id);
    }

    public static RulePageViewModel OpenPage(AppWindow app, string id)
    {
        app.Rules.SelectedItem = RowOf(app, id);
        app.Pump(300);

        return app.Rules.CurrentPage ?? throw new InvalidOperationException("no page is open for " + id);
    }

    public static ConditionViewModel WhereOf(RulePageViewModel page, int index)
    {
        return page.WhereFlow.OfType<ConditionViewModel>().ElementAt(index);
    }

    public static void ChooseProperty(ConditionViewModel condition, string name)
    {
        condition.OpenPropertyPicker(false);
        condition.PropertyPicker.ChooseCommand.Execute(condition.PropertyPicker.Items.OfType<PickerEntryViewModel>().First(entry => entry.Key == name));
    }

    public static void ChooseValue(ConditionViewModel condition, string value)
    {
        condition.OpenComparisonPicker(false);
        condition.ComparisonPicker.ChooseValueCommand.Execute(condition.ComparisonPicker.ValueEntries.First(entry => entry.Key == value));
    }

    private static void ChooseKind(RulePageViewModel page, string word)
    {
        page.OpenKindPicker(false);
        page.KindPicker.ChooseCommand.Execute(page.KindPicker.Items.OfType<PickerEntryViewModel>().First(entry => entry.Key == word));
    }

    private static void ChooseArtifact(RulePageViewModel page, string name)
    {
        page.OpenArtifactPicker(false);
        page.ArtifactPicker.ChooseCommand.Execute(page.ArtifactPicker.Items.OfType<PickerEntryViewModel>().First(entry => entry.Key == name));
    }

    private static bool Shows(AppWindow app, string text)
    {
        return app.Find<TextBlock>(block => block.Text == text && block.IsEffectivelyVisible).Any();
    }

    private static bool ShowsButton(AppWindow app, string name)
    {
        return app.Find<Button>(button => Avalonia.Automation.AutomationProperties.GetName(button) == name && button.IsEffectivelyVisible).Any();
    }

    private void PageOpens()
    {
        var folder = _context.Folders.NewFolder();
        using var app = _context.Open(folder);
        var page = OpenPage(app, "TEAM010");
        var words = new[] { "Custom rule", "TEAM010", "if statement", "C#", "statements in body", "is", "1", "body fits on one line", "true", "braces", "false", "For", "where", "require", "Block the push", "Only warn", "Saved" };
        var missing = words.Where(word => !Shows(app, word)).ToList();
        var title = app.Find<TextBox>(box => box.Name == "TitleBox" && box.IsEffectivelyVisible).FirstOrDefault()?.Text;
        _context.Report.Check("page", "TEAM010 is open beside the list and the page draws its tag, id, title, sentence, choice and state", missing.Count == 0 && title == "No braces around a single-line if", "not drawn: " + string.Join(", ", missing), "title: " + title);
        _context.Report.Check("page", "the page is the page of the rule: Block the push is chosen, and Discard and Save rule are not drawn", page.SelectedEffect?.Severity == Severity.Block && !ShowsButton(app, "Discard") && !ShowsButton(app, "Save rule"), "effect: " + page.SelectedEffect?.Label);
    }

    private void EachEditEqualsTheCommandLine()
    {
        Edit("the first property changed to lines", "TEAM010", page => ChooseProperty(WhereOf(page, 0), "lines"), "--where", "lines=1", "--where", "bodyFitsOnOneLine=true");
        Edit("the first comparison changed to is one of 1 and 2", "TEAM010", ChooseOneOf, "--where", "statementsInBody=1,2", "--where", "bodyFitsOnOneLine=true");
        Edit("the second value changed to false", "TEAM010", page => ChooseValue(WhereOf(page, 1), "false"), "--where", "statementsInBody=1", "--where", "bodyFitsOnOneLine=false");
        Edit("a condition added: hasElse is false", "TEAM010", AddHasElse, "--where", "statementsInBody=1", "--where", "bodyFitsOnOneLine=true", "--where", "hasElse=false");
        Edit("the second condition removed", "TEAM010", page => page.RemoveCondition(WhereOf(page, 1)), "--where", "statementsInBody=1");
        Edit("require changed to forbid", "TEAM010", page => ChooseKind(page, "forbid"), "--forbid");
        Edit("the artifact changed to class, its invalid conditions removed", "TEAM011", ChangeToClass, "--for", "class", "--where", "containingType=class", "--require", "declaredAccess=public,private");
        Edit("the title changed", "TEAM010", page => page.Title = "A new title", "--title", "A new title");
    }

    private static void ChooseOneOf(RulePageViewModel page)
    {
        var condition = WhereOf(page, 0);
        condition.OpenComparisonPicker(false);
        condition.ComparisonPicker.ChooseComparisonCommand.Execute(condition.ComparisonPicker.Comparisons.First(entry => entry.Key == "is one of"));
        condition.ComparisonPicker.FieldText = "1, 2";
        condition.ComparisonPicker.CommitField();
    }

    private static void AddHasElse(RulePageViewModel page)
    {
        page.AddCondition();
        var added = WhereOf(page, 2);
        added.PropertyPicker.Close();
        ChooseProperty(added, "hasElse");
        ChooseValue(added, "false");
    }

    private static void ChangeToClass(RulePageViewModel page)
    {
        ChooseArtifact(page, "class");
        if(!page.HasInvalidCondition)
            throw new InvalidOperationException("no condition is invalid after the artifact changed");

        while(page.InvalidCondition is { } invalid)
            page.RemoveCondition(invalid);
    }

    /// <summary>
    /// Makes one edit on the page, saves it, and compares the file with the one <c>rules set</c> writes on a second copy.
    /// </summary>
    private void Edit(string what, string ruleId, Action<RulePageViewModel> edit, params string[] commandLineOptions)
    {
        var folderApp = _context.Folders.NewFolder();
        var folderCli = _context.Folders.NewFolder();
        var before = _context.Cli(folderApp, "rules", "show", ruleId).Output;
        using var app = _context.Open(folderApp);
        var page = OpenPage(app, ruleId);
        edit(page);
        app.Pump(100);
        var wasDirty = page.IsDirty;
        page.SaveCommand.Execute(null);
        app.Pump(200);
        _context.Cli(folderCli, new[] { "rules", "set", ruleId }.Concat(commandLineOptions).ToArray());
        _context.SameBytes("page-edit", $"{what}, saved: the file equals rules set {ruleId} {string.Join(' ', commandLineOptions)}", folderApp, folderCli);
        var shownByApp = _context.Cli(folderApp, "rules", "show", ruleId).Output;
        var shownByCli = _context.Cli(folderCli, "rules", "show", ruleId).Output;
        _context.Report.Check("page-edit", $"{what}: the edit made the page unsaved, the save made it saved, and rules show prints the new rule", wasDirty && !page.IsDirty && shownByApp == shownByCli && shownByApp != before, "dirty before the save: " + wasDirty + ", dirty after: " + page.IsDirty, shownByApp, shownByCli);
    }

    private void UnsavedAndSaved()
    {
        var folder = _context.Folders.NewFolder();
        using var app = _context.Open(folder);
        var page = OpenPage(app, "TEAM010");
        var row = RowOf(app, "TEAM010");
        var first = WhereOf(page, 0);
        first.SetValues(new List<string> { "2" });
        app.Pump(100);
        _context.Report.Check("page-state", "an edit makes the page say Unsaved, with Discard and Save rule, and not Saved", page.IsDirty && Shows(app, "Unsaved") && !Shows(app, "Saved") && ShowsButton(app, "Discard") && ShowsButton(app, "Save rule"), "dirty " + page.IsDirty);
        _context.Report.Check("page-state", "while the page is beside the list the row of the rule does not say Unsaved", !row.ShowsUnsaved && row.HasUnsavedEdits, $"shows {row.ShowsUnsaved}, has edits {row.HasUnsavedEdits}");

        app.Rules.SelectedItem = RowOf(app, "TEAM011");
        app.Pump(200);
        var kept = page.IsDirty && WhereOf(page, 0).ValueText == "2";
        _context.Report.Check("page-state", "choosing another rule keeps the edits, asks no question, and the row of the first rule now says Unsaved", kept && row.ShowsUnsaved, $"kept {kept}, row shows {row.ShowsUnsaved}");
        app.Rules.SelectedItem = row;
        app.Pump(200);

        page.DiscardCommand.Execute(null);
        app.Pump(100);
        _context.Report.Check("page-state", "Discard puts back exactly what the file says and the page says Saved", !page.IsDirty && WhereOf(page, 0).ValueText == "1" && Shows(app, "Saved") && !ShowsButton(app, "Save rule"), "value " + WhereOf(page, 0).ValueText);

        page.AddCondition();
        app.Pump(100);
        _context.Report.Check("page-state", "an empty condition makes Save rule impossible, and says why", page.IsDirty && !page.SaveCommand.CanExecute(null) && page.SaveTip == "Finish the sentence to save it", $"can save {page.SaveCommand.CanExecute(null)}, tip {page.SaveTip}");
        page.RemoveCondition(page.WhereFlow.OfType<ConditionViewModel>().Last());
        app.Pump(100);
        _context.Report.Check("page-state", "taking the empty condition out again leaves nothing to save", !page.IsDirty, "dirty " + page.IsDirty);

        var before = CheckContext.SettingsBytes(folder);
        page.Title = "Another title";
        _context.Report.Check("page-state", "an edit of the title makes Save rule possible", page.SaveCommand.CanExecute(null), "can save " + page.SaveCommand.CanExecute(null));
        page.SaveCommand.Execute(null);
        app.Pump(200);
        _context.Report.Check("page-state", "after Save rule the file changed and the page says Saved", !page.IsDirty && Shows(app, "Saved") && !before.AsSpan().SequenceEqual(CheckContext.SettingsBytes(folder)), "dirty " + page.IsDirty);
    }

    private void SwitchAndEffect()
    {
        var folderApp = _context.Folders.NewFolder();
        var folderCli = _context.Folders.NewFolder();
        using var app = _context.Open(folderApp);
        var page = OpenPage(app, "TEAM010");
        var row = RowOf(app, "TEAM010");
        page.ToggleSwitchCommand.Execute(null);
        app.Pump(100);
        _context.Cli(folderCli, "rules", "disable", "TEAM010");
        _context.SameBytes("page-switch", "the switch of the page equals rules disable TEAM010, at once, with no Save rule", folderApp, folderCli);
        _context.Report.Check("page-switch", "the row and the page both say the rule is off, and the page is not unsaved", !row.IsOn && !page.IsOn && page.SwitchWord == "Off" && !page.IsDirty, $"row on {row.IsOn}, page on {page.IsOn}");

        row.ToggleSwitchCommand.Execute(null);
        app.Pump(100);
        _context.Report.Check("page-switch", "the switch of the row shows on the page", page.IsOn && page.SwitchWord == "On", "page on " + page.IsOn);

        page.SelectedEffect = page.Effects.First(entry => entry.Severity == Severity.Warn);
        app.Pump(100);
        _context.Cli(folderCli, "rules", "enable", "TEAM010");
        _context.Cli(folderCli, "rules", "severity", "TEAM010", "warn");
        _context.SameBytes("page-switch", "the choice Only warn on the page equals rules severity TEAM010 warn, at once", folderApp, folderCli);
        _context.Report.Check("page-switch", "the row says Warns too, and the choice shows the tick on Only warn only", row.EffectWord == "Warns" && page.Effects.Count(entry => entry.IsCurrent) == 1 && page.Effects.First(entry => entry.IsCurrent).Severity == Severity.Warn, "row says " + row.EffectWord);

        row.ChooseCommand.Execute(row.Entries.First(entry => entry.Severity == Severity.Block));
        app.Pump(100);
        _context.Report.Check("page-switch", "a choice made in the row shows on the page", page.SelectedEffect?.Severity == Severity.Block, "page says " + page.SelectedEffect?.Label);
    }

    private void ChangeByCommandLine()
    {
        var folder = _context.Folders.NewFolder();
        using var app = _context.Open(folder);
        var page = OpenPage(app, "TEAM010");
        _context.Cli(folder, "rules", "set", "TEAM010", "--title", "Other");
        var shown = app.PumpUntil(() => page.Title == "Other", 8000);
        _context.Report.Check("page-watch", "a change made by the command line (a new title) shows on the clean page, which stays Saved", shown && !page.IsDirty, "title: " + page.Title);

        WhereOf(page, 0).SetValues(new List<string> { "3" });
        _context.Cli(folder, "rules", "set", "TEAM010", "--title", "Other again");
        app.Pump(1500);
        var kept = page.IsDirty && WhereOf(page, 0).ValueText == "3";
        _context.Report.Check("page-watch", "with an unsaved edit the edit is kept when the file changes, and the page still says Unsaved", kept, $"dirty {page.IsDirty}, value {WhereOf(page, 0).ValueText}");

        _context.Cli(folder, "rules", "remove", "TEAM010");
        var gone = app.PumpUntil(() => app.Rules.CurrentPage?.Id != "TEAM010", 8000);
        _context.Report.Check("page-watch", "a rule removed elsewhere has its page let go of and the next row is opened in its place (no 'Select a rule')", gone && app.Rules.CurrentPage != null && !app.Rules.Holds(page) && !app.Rules.ShowsNoRuleBlock && !Shows(app, "Select a rule"), "current page: " + app.Rules.CurrentPage?.Id);
    }

    private void NewRule()
    {
        var folderApp = _context.Folders.NewFolder();
        var folderCli = _context.Folders.NewFolder();
        using var app = _context.Open(folderApp);
        app.Rules.NewRuleFromHeadCommand.Execute(null);
        app.Pump(400);
        var page = app.Rules.CurrentPage ?? throw new InvalidOperationException("New rule opened no page");
        _context.Report.Check("page-new", "New rule opens the page of a draft with the id TEAM014, an empty first token with its picker open, Unsaved, and Save rule not possible", page.IsDraft && page.Id == "TEAM014" && page.ArtifactIsEmpty && page.ArtifactPicker.IsOpen && page.IsDirty && Shows(app, "Unsaved") && !page.SaveCommand.CanExecute(null), $"draft {page.IsDraft}, id {page.Id}, picker open {page.ArtifactPicker.IsOpen}, can save {page.SaveCommand.CanExecute(null)}");
        ChooseArtifact(page, "if-statement");
        page.Title = "Braces always";
        ChooseProperty(page.RequireFlow[0], "braces");
        app.Pump(100);
        _context.Report.Check("page-new", "with an artifact, a title and a require condition Save rule is possible", page.SaveCommand.CanExecute(null), "can save " + page.SaveCommand.CanExecute(null));
        page.SaveCommand.Execute(null);
        app.Pump(300);
        _context.Cli(folderCli, "rules", "add", "TEAM014", "--for", "if-statement", "--title", "Braces always", "--require", "braces=true");
        _context.SameBytes("page-new", "Save rule writes the file rules add TEAM014 --for if-statement --title \"Braces always\" --require braces=true writes", folderApp, folderCli);
        _context.Report.Check("page-new", "the new rule is selected and its page says Saved", app.Rules.CurrentPage?.Id == "TEAM014" && app.Rules.CurrentPage.IsSaved && !app.Rules.CurrentPage.IsDirty && Shows(app, "Saved"), "current: " + app.Rules.CurrentPage?.Id);
    }

    private void RemoveAndUndo()
    {
        var folderApp = _context.Folders.NewFolder();
        var folderCli = _context.Folders.NewFolder();
        var original = CheckContext.SettingsBytes(folderApp);
        using var app = _context.Open(folderApp);
        var page = OpenPage(app, "TEAM013");
        page.RemoveRuleCommand.Execute(null);
        app.Pump(300);
        _context.Cli(folderCli, "rules", "remove", "TEAM013");
        _context.SameBytes("page-remove", "Remove rule on TEAM013 equals rules remove TEAM013", folderApp, folderCli);
        _context.Report.Check("page-remove", "the banner says TEAM013 removed with Undo, and the row before it is selected", app.Rules.UndoText == "TEAM013 removed." && ShowsButton(app, "Undo") && app.Rules.SelectedRule?.Id == "TEAM012", $"banner {app.Rules.UndoText}, selected {app.Rules.SelectedRule?.Id}");
        app.Rules.UndoRemoveCommand.Execute(null);
        app.Pump(300);
        var restored = CheckContext.SettingsBytes(folderApp);
        _context.Report.Check("page-remove", "Undo brings the file back byte for byte what it was, and the banner goes", restored.AsSpan().SequenceEqual(original) && !app.Rules.HasUndo, $"original {CheckContext.Hex(original)}, now {CheckContext.Hex(restored)}");

        var shownBefore = _context.Cli(folderApp, "rules", "show", "TEAM010").Output;
        OpenPage(app, "TEAM010").RemoveRuleCommand.Execute(null);
        app.Pump(300);
        app.Rules.UndoRemoveCommand.Execute(null);
        app.Pump(300);
        var shownAfter = _context.Cli(folderApp, "rules", "show", "TEAM010").Output;
        var order = string.Join(" ", _context.ListedByCommandLine(folderApp).Select(rule => rule.Id));
        _context.Report.Check("page-remove", "Undo of the first rule brings it back with all its parts (rules show is the same), at its own place in the file", shownBefore == shownAfter && order == "TEAM010 TEAM011 TEAM012 TEAM013", "order now: " + order);
    }

    private void Refusal()
    {
        var folder = _context.Folders.NewFolder();
        var folderCli = _context.Folders.NewFolder();
        using var app = _context.Open(folder);
        var page = OpenPage(app, "TEAM011");
        var before = CheckContext.SettingsBytes(folder);
        ChooseArtifact(page, "class");
        app.Pump(100);
        var named = page.HasInvalidCondition && page.InvalidText.Contains("class has no property", StringComparison.Ordinal) && page.InvalidText.Contains("containing type declared access", StringComparison.Ordinal);
        _context.Report.Check("page-refusal", "with the artifact changed to class the page names the condition class does not have, and draws the line", named && Shows(app, page.InvalidText), "line: " + page.InvalidText);
        page.SaveCommand.Execute(null);
        app.Pump(200);
        var cli = _context.Cli(folderCli, "rules", "set", "TEAM011", "--for", "class");
        var firstLine = cli.Error.Split('\n')[0].Trim();
        _context.Report.Check("page-refusal", "Save rule is refused by the service: the page shows the message rules set prints, and draws it", page.ErrorText != null && page.ErrorText.Contains(firstLine, StringComparison.Ordinal) && Shows(app, page.ErrorText), "page says: " + page.ErrorText, "command line says: " + firstLine);
        _context.Report.Check("page-refusal", "the edits are still there, the page is still unsaved, and the file is unchanged", page.IsDirty && page.ArtifactName == "class" && before.AsSpan().SequenceEqual(CheckContext.SettingsBytes(folder)), $"dirty {page.IsDirty}, artifact {page.ArtifactName}");
    }
}
