using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using CF.App.Exemptions;
using CF.App.Parts;
using CF.App.Rules.Page;
using CF.App.Shell;
using CF.App.Tests.Support;
using CF.Core.Code;
using CF.Core.Findings;
using CF.Core.Services;
using Xunit;

namespace CF.App.Tests;

/// <summary>
/// The Exemptions screen over a fake scan: the summary, the files in order and the words of a row for each form, the views that narrow
/// the list (those with no reason, a rule chosen, the search), and where "Narrow the rule" lands on Rules and how it comes back.
/// </summary>
public sealed class ExemptionsScreenTests
{
    [AvaloniaFact]
    public async Task SummaryFilesInOrderAndTheWordsOfARowForEachFormAndForNoReasonAndFoldingOverTwentyHold()
    {
        using var shell = new ShellHost(SettingsText.Exempted, FileRules());
        var model = shell.Model.Exemptions;
        using var host = new ViewHost(new ExemptionsView { DataContext = model }, 1440, 900);
        await model.ShownAsync();
        host.Pump();

        // the head line, the summary with its numbers marked and its button, the panel's head
        Assert.Equal("In the last commit of main, 4f2a91c", model.Meta);
        Assert.Equal("**28** lines set **8** rules aside, in **17** files", model.SummaryMarked);
        Assert.Equal("2 have no reason", model.ButtonText);
        Assert.Equal("8 rules", model.RulesHead);
        Assert.Equal("17 files, most exemptions first", model.FilesLine);

        // the files, the one with most first, equal ones by path; only the first file starts open, with its rows closed
        var files = model.Items.OfType<ExemptionFileViewModel>().ToList();
        Assert.Equal(["Dependencies/CF.Adapters/Updates/ReleaseUpdater.cs", "App/CF.App/Rules/RulesViewModel.FileRules.cs", "App/CF.App/Review/SentencePieces.axaml.cs"], files.Take(3).Select(file => file.Path));
        Assert.Equal(["5", "3", "2"], files.Take(3).Select(file => file.TallyNumber));
        Assert.Equal("exemptions", files[0].TallyWords);
        Assert.Same(files[0], Assert.Single(files, file => file.IsOpen));
        Assert.All(files[0].Rows, row => Assert.False(row.IsOpen));

        // a row: the reason, the rule's title, the form of a pragma or an attribute, the line; the rule is a team rule or a rule of the file
        var first = files[0].Rows[1];
        Assert.Equal("Copied from Sharp Factory's updater, kept as it is there", first.Reason);
        Assert.Equal("No braces around a single-line if", first.RuleTitle);
        Assert.Equal("4 lines", first.LineText);
        Assert.Equal("Lines 208, 241, 263 and 302", first.LinesMeta);
        Assert.True(first.Rule.IsCustom);

        // each form, and no reason: an allow comment with none is a finding; a pragma or an attribute with none is not
        shell.Exemptions.Answer = () => ExemptionScan.Of("main", new string('a', 40), [
            Entry("src/A.cs", 5, "TEAM001", ExemptionForm.AllowComment, "Generated code keeps its braces"),
            Entry("src/A.cs", 9, "TEAM001", ExemptionForm.AllowComment, null),
            Entry("src/A.cs", 20, "CA1822", ExemptionForm.Pragma, null),
            Entry("src/A.cs", 30, "CA1031", ExemptionForm.SuppressMessage, "The caller reports it")]);
        await model.ShownAsync();
        host.Pump();
        var rows = model.Items.OfType<ExemptionRowViewModel>().ToList();
        Assert.Equal([false, true, false, false], rows.Select(row => row.IsFinding));
        Assert.Equal([false, false, true, false], rows.Select(row => row.HasQuietNoReason));
        Assert.Equal([null, null, "#pragma warning disable", "[SuppressMessage]"], rows.Select(row => row.FormText));
        Assert.Equal("No reason given", rows[1].NoReason);
        Assert.Equal("No braces around a single-line if", rows[1].RuleTitle);
        Assert.Equal("**This line is a finding:** an exemption needs a reason. Write why after the rule: `// commitfence:allow TEAM001: ` and the reason.", rows[1].HintMarked);
        Assert.False(rows[3].Rule.IsKnown);
        var fileHeader = Assert.Single(host.FindAll<Button>(button => button.Classes.Contains("exfile")));
        Assert.Equal(("1 finding", "src/A.cs, 4 exemptions, expanded, 1 finding"),
            (Assert.Single(host.FindAll<TextBlock>(text => text.IsEffectivelyVisible && text.Text == "1 finding"
                && ReferenceEquals(text.DataContext, fileHeader.DataContext))).Text, AutomationProperties.GetName(fileHeader)));

        // "Open at line n" resolves the path against the top of the working tree the scan read, not the folder the app was opened on
        var work = Path.Combine(Path.GetTempPath(), "shop-top");
        shell.Exemptions.Answer = () => ExemptionScan.Of("main", new string('a', 40), [Entry("src/A.cs", 5, "TEAM001", ExemptionForm.AllowComment, "Kept")], work);
        await model.ShownAsync();
        var opened = new List<(string Path, int Line)>();
        model.OpenLineRequested += (path, line) => opened.Add((path, line));
        model.Items.OfType<ExemptionRowViewModel>().Single().Actions[0].Command.Execute(null);
        Assert.Equal([(Path.Combine(work, "src", "A.cs"), 5)], opened);

        // over twenty exemptions every file but the first starts folded, and the list of a long scan recycles its rows
        shell.Exemptions.Answer = () => ExemptionSamples.Many(60, 8);
        await model.ShownAsync();
        host.Pump();
        files = model.Items.OfType<ExemptionFileViewModel>().ToList();
        Assert.Equal(50, files.Count);
        Assert.Equal([true], files.Where(file => file.IsOpen).Select(file => file.IsOpen));
        Assert.StartsWith("And 10 more files with 8 exemptions each.", Assert.IsType<ExemptionTailViewModel>(model.Items[^1]).Text, StringComparison.Ordinal);
        model.ToggleAllCommand.Execute(null);
        host.Pump();
        Assert.True(model.Items.Count > 400);
        Assert.InRange(host.FindAll<Button>(button => button.Classes.Contains("exrow")).Count, 1, 60);
    }

    [AvaloniaFact]
    public async Task RowsMergeByRuleAndTrimmedReasonAndSearchKeepsEveryMatchingRowsLine()
    {
        using var shell = new ShellHost(SettingsText.Exempted, FileRules());
        var model = shell.Model.Exemptions;
        using var host = new ViewHost(new ExemptionsView { DataContext = model }, 1440, 900);
        shell.Exemptions.Answer = () => ExemptionScan.Of("main", new string('a', 40), [
            Entry("src/A.cs", 40, "TEAM001", ExemptionForm.AllowComment, "Kept "),
            Entry("src/A.cs", 20, "TEAM001", ExemptionForm.AllowComment, null),
            Entry("src/A.cs", 10, "TEAM001", ExemptionForm.AllowComment, "Kept"),
            Entry("src/A.cs", 30, "TEAM011", ExemptionForm.AllowComment, "Kept"),
            Entry("src/A.cs", 50, "TEAM001", ExemptionForm.AllowComment, null)]);
        await model.ShownAsync();
        host.Pump();

        var file = Assert.Single(model.Items.OfType<ExemptionFileViewModel>());
        Assert.Equal(("5", "exemptions"), (file.TallyNumber, file.TallyWords));
        Assert.Equal(["2 lines", "2 lines", "line 30"], file.Rows.Select(row => row.LineText));
        Assert.Equal("Lines 10 and 40", file.Rows[0].LinesMeta);
        Assert.Equal("Lines 20 and 50", file.Rows[1].LinesMeta);
        Assert.Equal("No reason given", file.Rows[1].NoReason);
        var merged = file.Rows[0];
        var row = host.FindAll<Button>(button => button.Classes.Contains("exrow") && ReferenceEquals(button.DataContext, file.Rows[0])).Single();
        Assert.Equal("TEAM001, Kept, 2 lines: 10 and 40, folded", AutomationProperties.GetName(row));
        row.Command!.Execute(null);
        host.Pump();
        Assert.Equal("TEAM001, Kept, 2 lines: 10 and 40, expanded", AutomationProperties.GetName(row));
        var excerpt = Assert.Single(host.FindAll<FileLines>(lines => lines.IsEffectivelyVisible));
        Assert.Same(merged.ExcerptLines, excerpt.ItemsSource);
        Assert.Equal(10, Assert.Single(merged.ExcerptLines, line => line.IsCurrent).Number);
        Assert.Equal(["Open at line 10", "Open at line 40", "Open the rule"], merged.Actions.Select(action => action.Text));

        model.SearchText = "Kept ";
        file = Assert.Single(model.Items.OfType<ExemptionFileViewModel>());
        Assert.Equal(("2", "of 5 exemptions"), (file.TallyNumber, file.TallyWords));
        Assert.False(file.HasFindings);
        Assert.Equal("Lines 10 and 40", Assert.Single(file.Rows).LinesMeta);

        model.SearchText = string.Empty;
        shell.Exemptions.Answer = () => ExemptionScan.Of("main", new string('a', 40), Enumerable.Range(10, 9)
            .Select(line => Entry("src/A.cs", line, "TEAM001", ExemptionForm.AllowComment, "Kept")).ToList());
        await model.ShownAsync();
        merged = Assert.Single(model.Items.OfType<ExemptionRowViewModel>());
        Assert.Equal("Lines 10, 11, 12 and 6 more", merged.LinesMeta);
        Assert.Equal(["Open at line 10", "Open at line 11", "Open at line 12", "Open at line 13", "Open at line 14", "Open at line 15", "Show all 9 lines", "Open the rule"],
            merged.Actions.Select(action => action.Text));
        var showAll = Assert.Single(merged.Actions, action => action.IsQuiet);
        merged.IsOpen = true;
        host.Pump();
        var showAllButton = host.Find<Button>(button => button.IsEffectivelyVisible && ReferenceEquals(button.DataContext, showAll))!;
        Assert.True(showAllButton.Focus(NavigationMethod.Tab));
        host.Press(PhysicalKey.Enter);
        host.Pump();
        Assert.Equal(["Open at line 16", "Open at line 17", "Open at line 18", "Open the rule"], merged.Actions.Skip(6).Select(action => action.Text));
        var next = host.Find<Button>(button => button.IsEffectivelyVisible && ReferenceEquals(button.DataContext, merged.Actions[6]))!;
        Assert.True(next.IsFocused);
        host.Press(PhysicalKey.Tab);
        Assert.Equal("Open at line 17", AutomationProperties.GetName(Assert.IsType<Button>(host.Window.FocusManager?.GetFocusedElement())));
    }

    [AvaloniaFact]
    public async Task FindingsOpenOnTheirOwnLinesAndRepeatedEntriesOnALineHaveOneAction()
    {
        using var shell = new ShellHost(SettingsText.Exempted, FileRules());
        shell.Exemptions.Answer = () => ExemptionScan.Of("main", new string('a', 40), [
            Entry("src/A.cs", 10, "TEAM001", ExemptionForm.Pragma, null),
            Entry("src/A.cs", 20, "TEAM001", ExemptionForm.AllowComment, null),
            Entry("src/A.cs", 30, "TEAM001", ExemptionForm.SuppressMessage, "Kept"),
            Entry("src/A.cs", 30, "TEAM001", ExemptionForm.SuppressMessage, "Kept")]);
        var model = shell.Model.Exemptions;
        await model.ShownAsync();

        var file = Assert.Single(model.Items.OfType<ExemptionFileViewModel>());
        Assert.Equal("1 finding", file.FindingsText);
        Assert.Equal("4", file.TallyNumber);
        var finding = Assert.Single(file.Rows, row => row.IsFinding);
        Assert.Equal(20, Assert.Single(finding.ExcerptLines, line => line.IsCurrent).Number);
        Assert.Equal("Open at line 20", Assert.Single(finding.Actions, action => action.OpensLine).Text);
        Assert.Contains(file.Rows, row => row.HasQuietNoReason && row.LineText == "line 10");
        var repeated = Assert.Single(file.Rows, row => row.HasReason);
        Assert.Equal("line 30", repeated.LineText);
        Assert.Equal("Open at line 30", Assert.Single(repeated.Actions, action => action.OpensLine).Text);
    }

    [AvaloniaFact]
    public async Task RulesAreGroupedBySourceAndTheKeyboardMovesAcrossHeadings()
    {
        using var shell = new ShellHost(SettingsText.Exempted, FileRules());
        var model = shell.Model.Exemptions;
        using var host = new ViewHost(new ExemptionsView { DataContext = model }, 760, 1000);
        await model.ShownAsync();
        host.Pump();

        Assert.Empty(VisibleGroupNames());
        var toggle = host.Find<Button>(button => button.Name == "PanelToggleButton")!;
        toggle.Command!.Execute(null);
        host.Pump();

        Assert.Equal(["Custom rules, 4", "Rules of .editorconfig, 4"], VisibleGroupNames());
        var rows = host.FindAll<Button>(button => button.IsEffectivelyVisible && button.Classes.Contains("exrule"));
        Assert.Equal(["TEAM001", "TEAM013", "TEAM011", "TEAM012", "CA1822", "CA1051", "IDE0051", "IDE0055"],
            rows.Select(button => Assert.IsType<ExemptionRuleRowViewModel>(button.DataContext).RuleId));
        Assert.EndsWith(", Custom rule", AutomationProperties.GetName(rows[3]), StringComparison.Ordinal);
        Assert.EndsWith(", Rule of .editorconfig", AutomationProperties.GetName(rows[4]), StringComparison.Ordinal);
        var bars = host.FindAll<ExemptionBar>(bar => bar.IsEffectivelyVisible).ToDictionary(bar => Assert.IsType<ExemptionRuleRowViewModel>(bar.DataContext).RuleId);
        Assert.Equal(bars["TEAM001"].Bounds.Width, bars["TEAM013"].Bounds.Width);
        rows[3].Focus();
        host.Press(PhysicalKey.ArrowDown);
        Assert.True(rows[4].IsFocused);
        host.Press(PhysicalKey.ArrowUp);
        Assert.True(rows[3].IsFocused);
        host.Press(PhysicalKey.Tab, Avalonia.Input.RawInputModifiers.Shift);
        Assert.True(toggle.IsFocused);

        shell.Exemptions.Answer = () => ExemptionScan.Of("main", new string('a', 40), [
            Entry("src/A.cs", 5, "CA1822", ExemptionForm.Pragma, "Kept"),
            Entry("src/A.cs", 6, "OLD001", ExemptionForm.Pragma, "Kept")]);
        await model.ShownAsync();
        host.Pump();
        Assert.Equal(["Rules of .editorconfig, 1", "Rules the repository does not have, 1"], VisibleGroupNames());

        IEnumerable<string?> VisibleGroupNames()
        {
            return host.FindAll<TextBlock>(block => block.IsEffectivelyVisible && block.DataContext is ExemptionRuleGroupHeading
                && AutomationProperties.GetName(block) != null).Select(AutomationProperties.GetName);
        }
    }

    [AvaloniaTheory]
    [InlineData(56, 1.0 / 7, 46, 10)]
    [InlineData(21, 6.0 / 7, 4, 17)]
    [InlineData(84, 1.0 / 7, 72, 12)]
    public void TheBarMakesBothSegmentsVisibleWithinTheSameLength(double available, double redFraction, double neutral, double red)
    {
        var bar = new ExemptionBar { Fraction = 1, RedFraction = redFraction };
        var widths = bar.SegmentWidths(available);
        Assert.Equal(neutral, widths.Neutral, 8);
        Assert.Equal(red, widths.Red, 8);
    }

    [AvaloniaFact]
    public async Task NoReasonNarrowsTheListAndClearRestoresItAndARuleChosenNarrowsTheFilesAndEscEndsIt()
    {
        using var shell = new ShellHost(SettingsText.Exempted, FileRules());
        var model = shell.Model.Exemptions;
        using var host = new ViewHost(new ExemptionsView { DataContext = model }, 1440, 900);
        await model.ShownAsync();
        host.Pump();

        // "2 have no reason": the list is those two, each open, and the clear button says how many it shows again
        host.Find<Button>(button => button.Name == "WithoutReasonButton")!.Command!.Execute(null);
        host.Pump();
        Assert.Equal("**2** exemptions have no reason", model.SummaryMarked);
        Assert.Equal(["App/CF.App/Review/ReviewViewModel.cs", "Dependencies/CF.Adapters/Git/GitRunner.cs"], model.Items.OfType<ExemptionFileViewModel>().Select(file => file.Path));
        Assert.All(model.Items.OfType<ExemptionRowViewModel>(), row => Assert.True(row.IsOpen && row.IsFinding));
        Assert.Equal("Show all 28 exemptions", model.ClearName);
        host.Find<Button>(button => button.Name == "ClearFilterButton")!.Command!.Execute(null);
        host.Pump();
        Assert.False(model.HasFilter);
        Assert.Equal(17, model.Items.OfType<ExemptionFileViewModel>().Count());

        // a rule chosen in the panel: the other rows keep their words, the files are its own, and "The rule chosen" shows
        model.RuleRows.Single(row => row.RuleId == "TEAM001").ChooseCommand.Execute(null);
        host.Pump();
        Assert.Equal("**7** lines set TEAM001 aside, in **4** files", model.SummaryMarked);
        Assert.Equal("1 of 8 rules shown", model.RulesHead);
        Assert.Equal("4 files with exemptions of TEAM001, most first", model.FilesLine);
        Assert.Equal(["TEAM001"], model.RuleRows.Where(row => row.IsChosen).Select(row => row.RuleId));
        Assert.Equal(7, model.RuleRows.Count(row => row.IsSteppedBack));
        Assert.Equal("1 without a reason", model.RuleRows.Single(row => row.RuleId == "TEAM011").WithoutReasonText);
        var chosen = model.Chosen!;
        Assert.Equal("Custom rule", chosen.TagText);
        Assert.Equal("Narrow the rule", chosen.ButtonText);
        Assert.Equal("7 lines set it aside, 5 of them in Dependencies/CF.Adapters/Updates. Narrow the rule opens it on Rules with a new condition, so it stops reaching code it should leave alone.", chosen.Aside);
        Assert.Equal(["For if statement", "where statements in body is 1", "and body fits on one line is true", "require braces is false"], chosen.Sentence.Select(piece => piece.Spoken));
        var updater = model.Items.OfType<ExemptionFileViewModel>().First();
        Assert.Equal(("4", "of 5 exemptions"), (updater.TallyNumber, updater.TallyWords));
        Assert.True(host.Find<Border>(border => border.Name == "ChosenBox")!.IsEffectivelyVisible);

        // a rule of the file has no sentence and its button opens it; Esc ends the choice
        model.RuleRows.Single(row => row.RuleId == "CA1822").ChooseCommand.Execute(null);
        Assert.Equal("Set in .editorconfig, line 8.", model.Chosen!.SetIn);
        Assert.Equal("Open the rule", model.Chosen.ButtonText);
        host.Find<TextBox>(box => box.Name == "Search")!.Focus();
        host.Pump();
        host.Press(PhysicalKey.Escape);
        Assert.False(model.HasFilter);
        Assert.Null(model.Chosen);
        Assert.False(host.Find<Border>(border => border.Name == "ChosenBox")?.IsEffectivelyVisible ?? false);
    }

    [AvaloniaFact]
    public async Task NarrowTheRuleLandsOnRulesWithANewEmptyConditionAndTheStripAndBackReturnsWithTheRuleChosen()
    {
        using var shell = new ShellHost(SettingsText.Exempted, FileRules());
        var model = shell.Model.Exemptions;
        var settings = Path.Combine(shell.RepositoryPath, "commitfence.json");
        var before = File.ReadAllText(settings);
        using var host = new ViewHost(new MainWindow { DataContext = shell.Model, Width = 1440, Height = 900 });
        shell.Model.CurrentScreen = Screen.Exemptions;
        await model.ScanAsync();
        host.Pump();
        model.RuleRows.Single(row => row.RuleId == "TEAM001").ChooseCommand.Execute(null);

        // the rule opens on Rules, selected, with one empty condition added to its where clause, its property picker open, and the strip
        model.Chosen!.ActCommand.Execute(null);
        host.Pump();
        Assert.Equal(Screen.Rules, shell.Model.CurrentScreen);
        var page = shell.Model.Rules.CurrentPage!;
        Assert.Equal("TEAM001", page.Id);
        Assert.Equal("**From Exemptions.** 7 lines set this rule aside, 5 of them in Dependencies/CF.Adapters/Updates. Add a condition so it stops reaching them; nothing is saved until Save rule.", page.OriginText);
        var conditions = page.WhereFlow.OfType<ConditionViewModel>().ToList();
        Assert.Equal(3, conditions.Count);
        Assert.True(conditions[^1].IsEmpty);
        Assert.True(conditions[^1].PropertyPicker.IsOpen);
        Assert.True(page.IsDirty);
        Assert.Equal(before, File.ReadAllText(settings));
        Assert.NotNull(host.Find<Button>(button => button.IsEffectivelyVisible && button.GetValue(Avalonia.Automation.AutomationProperties.NameProperty) == "Back to Exemptions"));

        // "Back to Exemptions" returns with the rule still chosen
        page.BackToOriginCommand.Execute(null);
        await model.ScanAsync();
        host.Pump();
        Assert.Equal(Screen.Exemptions, shell.Model.CurrentScreen);
        Assert.Equal("TEAM001", model.Chosen?.RuleId);

        // discarding the edit takes the strip away
        shell.Model.ShowRules();
        page.DiscardCommand.Execute(null);
        Assert.False(page.HasOrigin);
        Assert.Equal(2, page.WhereFlow.OfType<ConditionViewModel>().Count());
    }

    [AvaloniaFact]
    public async Task TheSearchNarrowsByIdPathAndReasonAndSaysWhenNothingMatches()
    {
        using var shell = new ShellHost(SettingsText.Exempted, FileRules());
        var model = shell.Model.Exemptions;
        using var host = new ViewHost(new ExemptionsView { DataContext = model }, 1440, 900);
        await model.ShownAsync();
        host.Pump();

        // by the rule's id: every file that has one, with only those exemptions in its tally
        model.SearchText = "team011";
        Assert.Equal(["Dependencies/CF.Adapters/Git/GitRunner.cs", "Dependencies/CF.Adapters/Updates/ReleaseUpdater.cs", "Dependencies/CF.Core/Review/ExemptionFilter.cs"], model.Items.OfType<ExemptionFileViewModel>().Select(file => file.Path));
        var updater = model.Items.OfType<ExemptionFileViewModel>().Single(file => file.Path.EndsWith("ReleaseUpdater.cs", StringComparison.Ordinal));
        Assert.Equal(("1", "of 5 exemptions"), (updater.TallyNumber, updater.TallyWords));

        // by the path, by the reason
        model.SearchText = "Motion";
        Assert.Empty(model.Items.OfType<ExemptionFileViewModel>());
        model.SearchText = "FileRules";
        Assert.Equal(["App/CF.App/Rules/RulesViewModel.FileRules.cs"], model.Items.OfType<ExemptionFileViewModel>().Select(file => file.Path));
        model.SearchText = "plug-in contract";
        Assert.Equal(["App/CF.App/Rules/RulesViewModel.FileRules.cs"], model.Items.OfType<ExemptionFileViewModel>().Select(file => file.Path));

        // nothing found: the words stand in place of the files, and "Clear the search" brings them back
        model.SearchText = "zzz";
        host.Pump();
        Assert.True(model.HasNothingFound);
        Assert.Equal("“zzz”", model.NothingFoundText);
        Assert.Empty(model.Items.OfType<ExemptionFileViewModel>());
        Assert.True(host.Find<StackPanel>(panel => panel.Name == "NothingFound")!.IsEffectivelyVisible);
        Assert.Equal("No exemption matches", host.FindAll<TextBlock>(block => block.IsEffectivelyVisible && block.Text == "No exemption matches").Single().Text);
        model.ClearSearchCommand.Execute(null);
        host.Pump();
        Assert.False(model.HasNothingFound);
        Assert.Equal(17, model.Items.OfType<ExemptionFileViewModel>().Count());
    }

    private static ExemptionEntry Entry(string file, int line, string rule, ExemptionForm form, string? reason)
    {
        return new ExemptionEntry(file, line, rule, form, reason, form == ExemptionForm.AllowComment && reason == null, [new FileLine(line, "// the line")]);
    }

    private static IReadOnlyList<FileRule> FileRules()
    {
        return
        [
            RulesHost.FileRule("CA1822", "Mark members as static", FileRuleState.On, Severity.Warn, 8, "warning"),
            RulesHost.FileRule("IDE0055", "Fix formatting", FileRuleState.On, Severity.Block, 14),
            RulesHost.FileRule("CA1051", "Do not declare visible instance fields", FileRuleState.On, Severity.Warn, 20),
            RulesHost.FileRule("IDE0051", "Remove unused private members", FileRuleState.On, Severity.Warn, 26)
        ];
    }
}
