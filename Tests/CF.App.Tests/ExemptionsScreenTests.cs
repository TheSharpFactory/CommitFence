using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using CF.App.Exemptions;
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

        // the files, the one with most first, equal ones by path; at most twenty-eight lines: every file starts open, the first with its first row open
        var files = model.Items.OfType<ExemptionFileViewModel>().ToList();
        Assert.Equal(["Dependencies/CF.Adapters/Updates/ReleaseUpdater.cs", "App/CF.App/Rules/RulesViewModel.FileRules.cs", "App/CF.App/Review/SentencePieces.axaml.cs"], files.Take(3).Select(file => file.Path));
        Assert.Equal(["5", "3", "2"], files.Take(3).Select(file => file.TallyNumber));
        Assert.Equal("exemptions", files[0].TallyWords);
        Assert.Equal([true, false, false, false, false], files[0].Rows.Select(row => row.IsOpen));

        // a row: the reason, the rule's title, the form of a pragma or an attribute, the line; the rule is a team rule or a rule of the file
        var first = files[0].Rows[1];
        Assert.Equal("Copied from Sharp Factory's updater, kept as it is there", first.Reason);
        Assert.Equal("No braces around a single-line if", first.RuleTitle);
        Assert.Equal("line 208", first.LineText);
        Assert.True(first.Rule.IsCustom);

        // each form, and no reason: an allow comment with none is a finding; a pragma or an attribute with none is not
        shell.Exemptions.Answer = () => ExemptionScan.Of("main", new string('a', 40), [
            Entry("src/A.cs", 5, "TEAM001", ExemptionForm.AllowComment, "Generated code keeps its braces"),
            Entry("src/A.cs", 9, "TEAM001", ExemptionForm.AllowComment, null),
            Entry("src/A.cs", 20, "CA1822", ExemptionForm.Pragma, null),
            Entry("src/A.cs", 30, "CA1031", ExemptionForm.SuppressMessage, "The caller reports it")]);
        await model.ShownAsync();
        var rows = model.Items.OfType<ExemptionRowViewModel>().ToList();
        Assert.Equal([false, true, false, false], rows.Select(row => row.IsFinding));
        Assert.Equal([false, false, true, false], rows.Select(row => row.HasQuietNoReason));
        Assert.Equal([null, null, "#pragma warning disable", "[SuppressMessage]"], rows.Select(row => row.FormText));
        Assert.Equal("No reason given", rows[1].NoReason);
        Assert.Equal("A finding: an exemption needs a reason", rows[1].FindingLead);
        Assert.Equal("Write why after the rule: `// commitfence:allow TEAM001: ` and the reason. Until then this line is a finding of its own.", rows[1].HintMarked);
        Assert.False(rows[3].Rule.IsKnown);

        // "Open at line n" resolves the path against the top of the working tree the scan read, not the folder the app was opened on
        var work = Path.Combine(Path.GetTempPath(), "shop-top");
        shell.Exemptions.Answer = () => ExemptionScan.Of("main", new string('a', 40), [Entry("src/A.cs", 5, "TEAM001", ExemptionForm.AllowComment, "Kept")], work);
        await model.ShownAsync();
        var opened = new List<(string Path, int Line)>();
        model.OpenLineRequested += (path, line) => opened.Add((path, line));
        model.Items.OfType<ExemptionRowViewModel>().Single().OpenLineCommand.Execute(null);
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
