using System.Windows.Input;
using Avalonia.Headless.XUnit;
using CF.App.Rules;
using CF.App.Rules.Page;
using CF.App.Tests.Support;
using CF.Core.Code;
using CF.Core.Findings;
using CF.Core.Rules;
using Xunit;

namespace CF.App.Tests;

/// <summary>
/// The Rules screen: the list of the team's rules and of the rules of .editorconfig, what a change does to the settings file, the
/// page of a rule and its way of saving, the file changing under an open page, and the rules .editorconfig sets. The rule service
/// is the real one over a settings file in a temporary folder; only the listing of .editorconfig is the test's.
/// </summary>
public sealed class RulesScreenTests
{
    [AvaloniaFact]
    public async Task MixedRuleListIntersectsSearchAndEffect()
    {
        var fileRules = new[]
        {
            RulesHost.FileRule("CA1000", "Do not declare static members on generic types", FileRuleState.On, Severity.Block, 4),
            RulesHost.FileRule("CA1001", "Types that own disposable fields should be disposable", FileRuleState.On, Severity.Warn, 8, "warning"),
            RulesHost.FileRule("IDE0005", "Remove unnecessary usings", FileRuleState.Off, null, 12, "silent"),
            RulesHost.FileRule("CS8600", "Converting null literal", FileRuleState.OwnDefault, null, 16, "default")
        };
        using var host = new RulesHost(fileRules: fileRules);
        await host.Loaded;
        var model = host.Model;

        Assert.Equal(new[] { "TEAM001", "TEAM002", "TEAM003", "TEAM004", "CA1000", "CA1001", "IDE0005", "CS8600" }, host.RowIds());
        Assert.Equal(8, model.CountOf(RuleFilter.All));
        Assert.Equal(2, model.CountOf(RuleFilter.Blocks));
        Assert.Equal(3, model.CountOf(RuleFilter.Warns));
        Assert.Equal(2, model.CountOf(RuleFilter.Off));

        // the search reads the id and the title, whatever the case, in both groups
        model.SearchText = "BRACES";
        Assert.Equal(new[] { "TEAM001" }, host.RowIds());
        model.SearchText = "ca100";
        Assert.Equal(new[] { "CA1000", "CA1001" }, host.RowIds());
        model.SearchText = "Dispos";
        Assert.Equal(new[] { "CA1001" }, host.RowIds());
        model.SearchText = "documented";
        Assert.Equal(new[] { "TEAM002", "TEAM003" }, host.RowIds());

        // the search and the effect both have to hold; a rule that is off blocks and warns nothing
        model.SelectedFilter = model.Filters.First(segment => segment.Filter == RuleFilter.Warns);
        Assert.Equal(new[] { "TEAM002" }, host.RowIds());
        model.SelectedFilter = model.Filters.First(segment => segment.Filter == RuleFilter.Blocks);
        Assert.Empty(host.RowIds());
        Assert.True(model.HasNoMatch);

        model.SearchText = string.Empty;
        model.SelectedFilter = model.Filters.First(segment => segment.Filter == RuleFilter.Off);
        Assert.Equal(new[] { "TEAM003", "IDE0005" }, host.RowIds());
    }

    [AvaloniaFact]
    public async Task RuleChangesPersistImmediatelyAndAgreeAcrossViews()
    {
        using var host = new RulesHost();
        await host.Loaded;
        var row = host.Row("TEAM002");
        var page = host.Open("TEAM002");
        var othersBefore = Others(host, "TEAM002");

        row.ToggleSwitchCommand.Execute(null);

        var stored = host.StoredRules().Single(rule => rule.Id == "TEAM002");
        Assert.False(stored.Enabled);
        Assert.Equal(Severity.Warn, stored.Severity);
        Assert.False(row.IsOn);
        Assert.False(page.IsOn);

        page.SelectedEffect = page.Effects.First(entry => entry.Severity == Severity.Block);

        stored = host.StoredRules().Single(rule => rule.Id == "TEAM002");
        Assert.Equal(Severity.Block, stored.Severity);
        Assert.False(stored.Enabled);
        Assert.Equal(Severity.Block, row.Severity);
        Assert.Equal(Severity.Block, page.Severity);
        Assert.False(row.IsOn);
        Assert.False(page.IsDirty);
        Assert.Equal(othersBefore, Others(host, "TEAM002"));
    }

    [AvaloniaFact]
    public async Task RuleEditorSavesTypedConditionsAndDiscardsPendingChanges()
    {
        using var host = new RulesHost();
        await host.Loaded;
        var page = host.Open("TEAM001");
        var first = page.WhereFlow.OfType<ConditionViewModel>().First();

        first.OpenPropertyPicker(false);
        first.PropertyPicker.ChooseCommand.Execute(first.PropertyPicker.Items.OfType<PickerEntryViewModel>().First(entry => entry.Key == "lines"));
        first.SetIsOneOf(true);
        first.SetValues(new[] { "10", "20" });

        Assert.True(page.IsDirty);
        Assert.True(page.SaveCommand.CanExecute(null));
        page.SaveCommand.Execute(null);

        Assert.False(page.IsDirty);
        var stored = host.StoredRules().Single(rule => rule.Id == "TEAM001");
        var lines = stored.Where.Single(condition => condition.PropertyName == "lines");
        Assert.Equal(Comparison.IsOneOf, lines.Comparison);
        Assert.Equal(new[] { "10", "20" }, lines.ExpectedValues.Select(value => value.ToString()));
        Assert.Equal("True", stored.Where.Single(condition => condition.PropertyName == "bodyFitsOnOneLine").ExpectedValues.Single().ToString(), ignoreCase: true);
        Assert.Equal("False", stored.Require.Single(condition => condition.PropertyName == "braces").ExpectedValues.Single().ToString(), ignoreCase: true);

        // an edit that is not finished: Save is off, and leaving the rule and coming back finds the edit still there
        page.AddCondition();
        Assert.True(page.IsDirty);
        Assert.False(page.SaveCommand.CanExecute(null));
        host.Model.SelectedItem = host.Row("TEAM002");
        host.Model.SelectedItem = host.Row("TEAM001");
        Assert.Same(page, host.Model.CurrentPage);
        Assert.True(page.IsDirty);
        Assert.False(page.SaveCommand.CanExecute(null));
        Assert.Equal(3, page.WhereFlow.OfType<ConditionViewModel>().Count());

        page.DiscardCommand.Execute(null);

        Assert.False(page.IsDirty);
        var conditions = page.WhereFlow.OfType<ConditionViewModel>().ToList();
        Assert.Equal(2, conditions.Count);
        var restored = conditions.Single(condition => condition.PropertyName == "lines");
        Assert.True(restored.IsOneOf);
        Assert.Equal(new[] { "10", "20" }, restored.Values);
    }

    [AvaloniaFact]
    public async Task ReloadPreservesDirtyTextAndLastReadableRules()
    {
        using var host = new RulesHost();
        await host.Loaded;
        var model = host.Model;
        var page = host.Open("TEAM001");

        // a clean page follows the file
        File.WriteAllText(host.SettingsPath, SettingsText.FourWithFirstTitled("Title changed outside"));
        host.Settings.Raise();
        Assert.Equal("Title changed outside", page.Title);
        Assert.False(page.IsDirty);

        // a page with a pending edit keeps what the person typed
        page.Title = "Pending title";
        File.WriteAllText(host.SettingsPath, SettingsText.FourWithFirstTitled("Changed again outside"));
        host.Settings.Raise();
        Assert.Equal("Pending title", page.Title);
        Assert.True(page.IsDirty);

        // a file that cannot be read is a problem, and the list of the last good read stays
        File.WriteAllText(host.SettingsPath, SettingsText.Malformed);
        host.Settings.Raise();
        Assert.NotNull(model.BannerText);
        Assert.True(model.CanOpenSettingsFile);
        Assert.Equal(new[] { "TEAM001", "TEAM002", "TEAM003", "TEAM004" }, host.RowIds());

        File.WriteAllText(host.SettingsPath, SettingsText.FourWithFirstTitled("Repaired outside"));
        host.Settings.Raise();
        Assert.Null(model.BannerText);
        Assert.Equal("Pending title", page.Title);

        // the rule that is open is removed by someone else: its page is let go and another rule opens
        host.Service.RemoveRule(host.SettingsPath, "TEAM001");
        host.Settings.Raise();
        Assert.False(model.Holds(page));
        Assert.NotSame(page, model.CurrentPage);
        Assert.NotNull(model.SelectedRule);
        Assert.NotEqual("TEAM001", model.SelectedRule.Id);
        Assert.Equal(new[] { "TEAM002", "TEAM003", "TEAM004" }, host.RowIds());
    }

    [AvaloniaFact]
    public async Task SettingsFailureOffersOnlyAvailableRecovery()
    {
        using(var missing = new RulesHost(settings: null))
        {
            await missing.Loaded;
            Assert.NotNull(missing.Model.BannerText);
            Assert.False(missing.Model.CanOpenSettingsFile);
        }

        using var host = new RulesHost(settings: SettingsText.Malformed);
        await host.Loaded;
        var model = host.Model;
        Assert.NotNull(model.BannerText);
        Assert.True(model.CanOpenSettingsFile);
        var opened = new List<string>();
        model.OpenFileRequested += opened.Add;
        model.OpenSettingsFileCommand.Execute(null);
        Assert.Equal(host.SettingsPath, Assert.Single(opened));

        File.WriteAllText(host.SettingsPath, SettingsText.Four);
        host.Settings.Raise();
        Assert.Null(model.BannerText);
        Assert.Equal(4, host.RowIds().Count);

        // a change the service refuses shows its message and leaves the file as it was
        var before = host.SettingsBytes();
        model.SetEnabled("TEAM999", false);
        Assert.NotNull(model.BannerText);
        Assert.Contains("TEAM999", model.BannerText, StringComparison.Ordinal);
        Assert.Equal(before, host.SettingsBytes());
        Assert.Equal(4, host.StoredRules().Count);
    }

    [AvaloniaFact]
    public async Task RejectedEditKeepsDraftAndOriginalSettings()
    {
        using var host = new RulesHost();
        await host.Loaded;
        var page = host.Open("TEAM001");
        var before = host.SettingsBytes();

        page.OpenArtifactPicker(false);
        page.ArtifactPicker.ChooseCommand.Execute(page.ArtifactPicker.Items.OfType<PickerEntryViewModel>().First(entry => entry.Key == "method"));

        Assert.True(page.HasInvalidCondition);
        Assert.Contains("statements in body", page.InvalidText, StringComparison.Ordinal);
        Assert.True(page.SaveCommand.CanExecute(null));

        page.SaveCommand.Execute(null);

        Assert.True(page.HasError);
        Assert.NotNull(page.ErrorText);
        Assert.True(page.IsDirty);
        Assert.Equal("method", page.ArtifactName);
        Assert.Equal(before, host.SettingsBytes());
    }

    [AvaloniaFact]
    public async Task NewRuleIsWrittenOnlyWhenComplete()
    {
        using var host = new RulesHost();
        await host.Loaded;
        var model = host.Model;

        model.NewRule();
        var page = model.CurrentPage;
        Assert.NotNull(page);
        Assert.True(page.IsDraft);
        Assert.Equal("TEAM005", page.Id);
        Assert.False(page.SaveCommand.CanExecute(null));

        page.Title = "Classes are documented too";
        Assert.False(page.SaveCommand.CanExecute(null));
        page.OpenArtifactPicker(false);
        page.ArtifactPicker.ChooseCommand.Execute(page.ArtifactPicker.Items.OfType<PickerEntryViewModel>().First(entry => entry.Key == "class"));
        Assert.False(page.SaveCommand.CanExecute(null));
        Assert.Equal(4, host.StoredRules().Count);

        page.RequireFlow[0].SetProperty("documentationPresent");
        Assert.True(page.SaveCommand.CanExecute(null));
        page.SaveCommand.Execute(null);

        var stored = host.StoredRules();
        Assert.Equal(new[] { "TEAM001", "TEAM002", "TEAM003", "TEAM004", "TEAM005" }, stored.Select(rule => rule.Id));
        var added = stored[^1];
        Assert.Equal("Classes are documented too", added.Title);
        Assert.Equal("class", added.ArtifactType);
        Assert.Equal("documentationPresent", Assert.Single(added.Require).PropertyName);
        Assert.False(model.IsDraftOpen);
        Assert.Equal("TEAM005", model.SelectedRule?.Id);
        Assert.False(model.CurrentPage!.IsDirty);
    }

    [AvaloniaFact]
    public async Task DuplicatingDisabledRulePreservesItsContentsAndPosition()
    {
        using var host = new RulesHost();
        await host.Loaded;
        var model = host.Model;
        var page = host.Open("TEAM003");
        var source = host.StoredRules().Single(rule => rule.Id == "TEAM003");
        var before = host.SettingsBytes();

        page.DuplicateRuleCommand.Execute(null);

        var copy = model.CurrentPage;
        Assert.NotNull(copy);
        Assert.True(copy.IsDraft);
        Assert.Equal("TEAM005", copy.Id);
        Assert.Equal("Copy of Classes are documented", copy.Title);
        Assert.Equal(before, host.SettingsBytes());

        copy.SaveCommand.Execute(null);

        var stored = host.StoredRules();
        Assert.Equal(new[] { "TEAM001", "TEAM002", "TEAM003", "TEAM005", "TEAM004" }, stored.Select(rule => rule.Id));
        var saved = stored.Single(rule => rule.Id == "TEAM005");
        Assert.False(saved.Enabled);
        Assert.Equal(source.Severity, saved.Severity);
        Assert.Equal(source.ArtifactType, saved.ArtifactType);
        Assert.Equal(Conditions(source), Conditions(saved));
        Assert.Equal("TEAM005", model.SelectedRule?.Id);
        Assert.Equal("TEAM005", host.RowIds()[host.RowIds().ToList().IndexOf("TEAM003") + 1]);
    }

    [AvaloniaFact]
    public async Task UndoRestoresRemovedRuleAtItsOriginalIndex()
    {
        using var host = new RulesHost();
        await host.Loaded;
        var model = host.Model;
        var original = host.StoredRules().Single(rule => rule.Id == "TEAM002");

        model.RemoveRule("TEAM002");

        Assert.Equal(new[] { "TEAM001", "TEAM003", "TEAM004" }, host.StoredRules().Select(rule => rule.Id));
        Assert.True(model.HasUndo);

        model.UndoRemoveCommand.Execute(null);

        var stored = host.StoredRules();
        Assert.Equal(new[] { "TEAM001", "TEAM002", "TEAM003", "TEAM004" }, stored.Select(rule => rule.Id));
        var restored = stored.Single(rule => rule.Id == "TEAM002");
        Assert.Equal(original.Title, restored.Title);
        Assert.Equal(original.Severity, restored.Severity);
        Assert.Equal(original.Message, restored.Message);
        Assert.Equal(original.Enabled, restored.Enabled);
        Assert.Equal(Conditions(original), Conditions(restored));
        Assert.False(model.HasUndo);

        // the undo was used up: another press changes nothing
        var before = host.SettingsBytes();
        model.UndoRemoveCommand.Execute(null);
        Assert.Equal(before, host.SettingsBytes());
    }

    [AvaloniaFact]
    public async Task ScopedRowAndPageDescribeEachPlace()
    {
        using var host = new RulesHost(fileRules: [RulesHost.ScopedRule()]);
        await host.Loaded;
        var row = Assert.Single(host.Model.Items.OfType<FileRuleRowViewModel>());
        Assert.Equal(new List<string> { "Warns for tests/**/*.cs, line 18", "Off for **/Generated/*.cs, line 21, and *.Designer.cs, line 24" }, row.ExceptionLines.Select(line => line.Text));
        host.Model.SelectedItem = row;
        Assert.Equal(new List<string> { "Open at line 14", "Open at line 18", "Open at line 21", "Open at line 24" }, host.Model.FilePage.Places.Select(place => place.ButtonText));
        var opened = 0;
        host.Model.OpenLineRequested += (_, line) => opened = line;
        host.Model.FilePage.Places[1].OpenCommand.Execute(null);
        Assert.Equal(18, opened);
    }

    [AvaloniaFact]
    public async Task FileRulePageTracksTheSourceAndOpensTheRightFile()
    {
        var rule = RulesHost.FileRule("CA1000", "Do not declare static members on generic types", FileRuleState.On, Severity.Block, 4);
        using var host = new RulesHost(fileRules: new[] { rule });
        await host.Loaded;
        var model = host.Model;
        var opened = new List<string>();
        model.OpenLineRequested += (path, line) => opened.Add($"{path}:{line}");
        var row = Assert.Single(model.Items.OfType<FileRuleRowViewModel>());

        model.SelectedItem = row;

        Assert.True(model.ShowsFilePage);
        Assert.Equal("CA1000", model.FilePage.Id);
        var marked = Assert.Single(model.FilePage.Lines, line => line.IsCurrent);
        Assert.Equal(4, marked.Number);
        Assert.Contains("CA1000", marked.Text, StringComparison.Ordinal);
        Assert.Equal("Blocks", Assert.Single(model.FilePage.Settings, setting => setting.IsCurrent).EffectWord);

        row.OpenFileCommand.Execute(null);
        Assert.Equal(Path.Combine(host.RepositoryPath, ".editorconfig") + ":4", Assert.Single(opened));

        // the product does not change that file: the row has no command but the one that opens it, and a change asked for anyway is refused
        var commands = typeof(FileRuleRowViewModel).GetProperties().Where(property => typeof(ICommand).IsAssignableFrom(property.PropertyType)).Select(property => property.Name);
        Assert.Equal(new[] { nameof(FileRuleRowViewModel.OpenFileCommand) }, commands);
        var before = host.SettingsBytes();
        model.SetEnabled("CA1000", false);
        Assert.NotNull(model.BannerText);
        Assert.Equal(before, host.SettingsBytes());

        // the file changes the severity: the same row and the page follow
        host.SetFileRules(new[] { RulesHost.FileRule("CA1000", "Do not declare static members on generic types", FileRuleState.On, Severity.Warn, 4, "warning") });
        var arrived = Signals.FileRulesArrived(model);
        host.EditorConfig.Raise();
        await arrived;
        Assert.Same(row, Assert.Single(model.Items.OfType<FileRuleRowViewModel>()));
        Assert.Equal(Severity.Warn, row.Rule.Severity);
        Assert.Equal("Warns", Assert.Single(model.FilePage.Settings, setting => setting.IsCurrent).EffectWord);

        // the file drops the rule: its row goes and the selection moves to a rule that is still there
        host.SetFileRules(new List<FileRule>());
        arrived = Signals.FileRulesArrived(model);
        host.EditorConfig.Raise();
        await arrived;
        Assert.Empty(model.Items.OfType<FileRuleRowViewModel>());
        Assert.Null(model.SelectedFileRule);
        Assert.IsType<TeamRuleRowViewModel>(model.SelectedItem);
    }

    [AvaloniaFact]
    public async Task SlowFileListingLeavesTeamRulesUsable()
    {
        using var gate = new ManualResetEventSlim();
        var rule = RulesHost.FileRule("CA1000", "Do not declare static members on generic types", FileRuleState.On, Severity.Block, 4);
        using var host = new RulesHost(fileRules: new[] { rule }, prepare: service => service.FileListingGate = gate);
        var model = host.Model;
        try
        {
            await Task.Run(() => host.Service.FileListingStarted.Wait(TestContext.Current.CancellationToken), TestContext.Current.CancellationToken);
            Assert.False(model.FileRulesAreRead);

            model.SearchText = "TEAM00";
            var page = host.Open("TEAM002");
            Assert.Equal(new[] { "TEAM001", "TEAM002", "TEAM003", "TEAM004" }, host.RowIds());

            gate.Set();
            await host.Loaded;

            Assert.True(model.FileRulesAreRead);
            Assert.Equal(new[] { "TEAM001", "TEAM002", "TEAM003", "TEAM004" }, host.RowIds());
            model.SearchText = string.Empty;
            Assert.Contains("CA1000", host.RowIds());
            Assert.Same(page, model.CurrentPage);
            Assert.Equal("TEAM002", model.SelectedRule?.Id);
        }
        finally
        {
            gate.Set();
        }
    }

    private static List<string> Others(RulesHost host, string except)
    {
        return host.StoredRules().Where(rule => rule.Id != except).Select(rule => $"{rule.Id}|{rule.Enabled}|{rule.Severity}|{rule.Title}|{Conditions(rule)}").ToList();
    }

    private static string Conditions(RuleDefinition rule)
    {
        var where = string.Join(";", rule.Where.Select(Describe));
        var require = string.Join(";", rule.Require.Select(Describe));
        return $"{rule.ArtifactType}|{rule.Forbid}|{where}|{require}";
    }

    private static string Describe(RuleCondition condition)
    {
        return $"{condition.PropertyName} {condition.Comparison} {string.Join(",", condition.ExpectedValues.Select(value => value.ToString()))}";
    }
}