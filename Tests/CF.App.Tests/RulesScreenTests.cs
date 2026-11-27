using System.Windows.Input;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using CF.App.Parts;
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
    private static readonly string[] _boundWords = { "is at least" };
    private static readonly string[] _parameterBounds = { "parameterCount is at least 1", "parameterCount is at most 5" };

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
    public void ScopedOnlyRuleShowsItsEffectAndScope()
    {
        var production = new FileRulePlace("src/**.cs", "error", FileRuleState.On, Severity.Block, 7, [], _ => true);
        var rule = new FileRule("CA1822", "Mark members as static", ".editorconfig", "C#", null, [production]);
        var row = new FileRuleRowViewModel(rule, _ => { });

        Assert.Equal("Blocks", row.EffectWord);
        Assert.False(row.IsUnset);
        Assert.Equal("Blocks only in src/**.cs; not set elsewhere", Assert.Single(row.OtherEffects).Spoken);
        Assert.Equal("CA1822, Mark members as static. Blocks only in src/**.cs; not set elsewhere. Set at line 7.", row.SpokenName);

        row.Update(new FileRule(rule.Id, rule.Title, rule.File, rule.Language, null, [production, production with { Pattern = "tests/**.cs", Line = 10 }]));
        Assert.Equal(2, row.VisiblePlaces.Count);
        Assert.False(row.Varies);
        Assert.Equal("not set elsewhere", row.ElsewhereText);
        Assert.Contains("not set elsewhere", row.SpokenName, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public void TwoWarningPlacesOpenTheirOwnLines()
    {
        var main = new FileRulePlace("*.cs", "warning", FileRuleState.On, Severity.Warn, 4, [], _ => true);
        var payments = main with { Pattern = "src/Payments/**", Line = 9 };
        var opened = new List<int>();
        var rule = new FileRule("CA2007", "Consider calling ConfigureAwait", ".editorconfig", "C#", main, [payments]);
        var row = new FileRuleRowViewModel(rule, opened.Add);

        Assert.Equal("Warns", row.EffectWord);
        Assert.False(row.Varies);
        Assert.Equal(2, row.VisiblePlaces.Count);
        Assert.False(row.ShowsLine);
        Assert.Null(row.ElsewhereText);
        row.MoveRight();
        row.OpenFileCommand.Execute(null);
        row.MoveRight();
        row.OpenFileCommand.Execute(null);
        Assert.Collection(opened, line => Assert.Equal(9, line), line => Assert.Equal(4, line));
    }

    [AvaloniaFact]
    public void PlaceLinksFollowKeyboardOrderAndOpenTheirOwnLines()
    {
        var main = new FileRulePlace("*.cs", "warning", FileRuleState.On, Severity.Warn, 4, [], _ => true);
        var production = new FileRulePlace("src/**.cs", "error", FileRuleState.On, Severity.Block, 7, [], _ => true);
        var other = Enumerable.Range(8, 4).Select(line => production with { Line = line }).Prepend(production).ToList();
        var rule = new FileRule("CA1822", "Mark members as static", ".editorconfig", "C#", main, other);
        var opened = new List<int>();
        var row = new FileRuleRowViewModel(rule, opened.Add);
        var opensPage = false;
        row.OpenPageRequested += () => opensPage = true;

        Assert.Equal("line 7, line 8, line 4", string.Join(", ", row.VisiblePlaces.Select(place => place.LineText)));
        Assert.Equal("3 more places", row.MorePlacesText);
        for(var index = 0; index < row.VisiblePlaces.Count; index++)
        {
            row.MoveRight();
            row.OpenFileCommand.Execute(null);
        }

        Assert.Equal("7, 8, 4", string.Join(", ", opened));
        row.MoveRight();
        row.OpenFileCommand.Execute(null);
        Assert.True(opensPage);
        row.MoveLeft();
        row.OpenFileCommand.Execute(null);
        Assert.Equal(4, opened[^1]);
        row.ResetPart();
        Assert.Equal(RowPart.Row, row.Part);
    }

    [AvaloniaFact]
    public async Task AFilterDimsNonMatchingPlacesWithoutChangingTheRow()
    {
        var rule = RulesHost.ScopedRule();
        using var host = new RulesHost(fileRules: [rule]);
        await host.Loaded;
        var model = host.Model;
        var row = Assert.Single(model.Items.OfType<FileRuleRowViewModel>());
        var places = row.VisiblePlaces;
        var spoken = row.SpokenName;

        Assert.Equal("line 18, line 21, line 24, line 14", string.Join(", ", places.Select(place => place.LineText)));
        Assert.Contains("blocks everywhere else, line 14", spoken, StringComparison.Ordinal);
        foreach(var filter in model.Filters)
        {
            Assert.True(filter.Count > 0);
            model.SelectedFilter = filter;
            Assert.Same(row, Assert.Single(model.Items.OfType<FileRuleRowViewModel>()));
            Assert.Equal("Varies", row.EffectWord);
            Assert.False(row.MarkIsBlock);
            Assert.Same(places, row.VisiblePlaces);
            Assert.Equal(filter.Filter switch
            {
                RuleFilter.All => [false, false, false, false],
                RuleFilter.Blocks => [true, true, true, false],
                RuleFilter.Warns => [false, true, true, true],
                RuleFilter.Off => [true, false, false, true],
                _ => throw new InvalidOperationException()
            }, row.VisiblePlaces.Select(place => place.IsDimmed));
            if(filter.Filter == RuleFilter.All)
                Assert.Equal(spoken, row.SpokenName);
            else
                Assert.Contains(" Also: ", row.SpokenName, StringComparison.Ordinal);
        }
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
    public async Task EditingTheTitleKeepsComparisonsThePickerDoesNotOffer()
    {
        const string settings = """
            {"rules":[{"id":"R","language":"csharp","for":"method","severity":"warn","title":"Original title",
                "where":{"name":{"not":["Run"]}},
                "require":{"parameterCount":{"atLeast":1,"atMost":5}}}]}
            """;
        using var host = new RulesHost(settings);
        await host.Loaded;
        var page = host.Open("R");

        Assert.Equal("is none of", Assert.Single(page.WhereFlow.OfType<ConditionViewModel>()).ComparisonWord);
        Assert.Equal(_boundWords, page.RequireFlow.Select(condition => condition.ComparisonWord));
        Assert.True(Assert.Single(page.RequireFlow).IsRange);
        page.Title = "New title";
        Assert.True(page.SaveCommand.CanExecute(null));
        page.SaveCommand.Execute(null);

        var rule = Assert.Single(host.StoredRules());
        Assert.Equal("New title", rule.Title);
        Assert.Equal("name is none of Run", RuleSentence.Condition(Assert.Single(rule.Where)));
        Assert.Equal(_parameterBounds, rule.Require.Select(RuleSentence.Condition));
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
        Assert.False(page.SaveCommand.CanExecute(null));
        Assert.Equal("Fix the sentence to save it.", page.SaveTip);

        page.SaveCommand.Execute(null);

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
    public async Task DraftStaysFirstAndDoesNotCountUnderAnyFilterOrSearch()
    {
        using var host = new RulesHost();
        await host.Loaded;
        var model = host.Model;
        var previous = model.SelectedItem;
        var counts = model.Filters.Select(filter => filter.Count).ToArray();
        var custom = model.Items.OfType<GroupHeadingViewModel>().First();
        model.NewRule();
        var draft = Assert.IsType<DraftRuleRowViewModel>(model.Items[1]);
        draft.Page.Id = "  SEC-004";
        Assert.Same(draft, model.Items[1]);
        foreach(var filter in model.Filters)
        {
            model.SelectedFilter = filter;
            model.SearchText = "does not match";
            Assert.Same(draft, model.Items[1]);
            Assert.Equal(0, custom.Count);
            Assert.Equal(counts, model.Filters.Select(segment => segment.Count));
        }

        model.SearchText = "";
        model.SelectedFilter = model.Filters[0];
        model.DiscardDraft();
        Assert.DoesNotContain(draft, model.Items);
        Assert.Same(previous, model.SelectedItem);
    }

    [AvaloniaFact]
    public async Task DraftIdIsCheckedWhileTypingAndAgainstTheFileWhenSaved()
    {
        using var host = new RulesHost();
        await host.Loaded;
        var model = host.Model;
        using var window = new ViewHost(new RulesView { DataContext = model }, 1440, 900);
        model.DuplicateRule("TEAM001");
        window.Pump();

        var page = Assert.IsType<RulePageViewModel>(model.CurrentPage);
        var field = Assert.IsType<TextBox>(window.Find<TextBox>(box => AutomationProperties.GetName(box) == "Rule ID" && box.IsEffectivelyVisible));
        Assert.True(field.IsFocused);
        Assert.Equal(page.Id, field.SelectedText);
        window.Type("team001");
        Assert.Equal("Save adds it to commitfence.json. Until then it checks nothing.", page.HeadLine);
        Assert.Empty(page.SaveBlockerText);
        Assert.Null(page.SaveTip);
        Assert.Null(window.Find<TextBlock>(text => text.Name == "IdText" && text.IsEffectivelyVisible));
        Assert.Contains("TEAM001 is taken by", page.IdLine, StringComparison.Ordinal);
        Assert.Contains("TEAM001", AutomationProperties.GetHelpText(field), StringComparison.Ordinal);
        Assert.False(page.SaveCommand.CanExecute(null));
        Assert.True(field.IsFocused);

        page.Id = "";
        Assert.Equal("A rule needs an ID.", page.IdLine);
        page.Id = "SEC 004";
        Assert.Equal("An ID uses letters, digits, - and _ only.", page.IdLine);
        page.Id = "SEC/004";
        Assert.Equal("An ID uses letters, digits, - and _ only.", page.IdLine);

        page.Id = "SEC_004-v2";
        Assert.True(page.SaveCommand.CanExecute(null));
        host.Service.CopyRule(host.SettingsPath, "TEAM002", "sec_004-v2");
        var before = host.SettingsBytes();
        page.SaveCommand.Execute(null);

        Assert.Same(page, model.CurrentPage);
        Assert.Equal("sec_004-v2 is taken by “Copy of Methods are documented”.", page.IdLine);
        Assert.False(page.SaveCommand.CanExecute(null));
        Assert.Equal(before, host.SettingsBytes());

        page.Id = "SEC-005";
        page.SaveCommand.Execute(null);
        Assert.Contains(host.StoredRules(), rule => rule.Id == "SEC-005");
        Assert.False(model.CurrentPage!.IsDraft);
    }

    [AvaloniaFact]
    public async Task GroupCountsAndNamesFollowFilterAndSearch()
    {
        using var host = new RulesHost(fileRules: [RulesHost.ScopedRule()]);
        await host.Loaded;
        var model = host.Model;
        var groups = model.Items.OfType<GroupHeadingViewModel>().ToList();
        Assert.Equal("Custom, 4 rules, expanded", groups[0].SpokenName);
        model.SelectedFilter = model.Filters.Single(filter => filter.Filter == RuleFilter.Blocks);
        Assert.Equal("Custom, 1 of 4 rules shown, expanded", groups[0].SpokenName);
        Assert.Equal(".editorconfig, 1 of 1 rules shown, expanded", groups[1].SpokenName);
        model.SearchText = "IDE0055";
        Assert.Equal(0, groups[0].Count);
        Assert.Equal(1, groups[1].Count);
        Assert.Equal("Custom, 0 of 4 rules shown, expanded", groups[0].SpokenName);
        groups[1].Toggle();
        Assert.Equal(".editorconfig, 1 of 1 rules shown, collapsed", groups[1].SpokenName);
        groups[1].Toggle();
        Assert.Equal(".editorconfig, 1 of 1 rules shown, expanded", groups[1].SpokenName);
    }

    [AvaloniaFact]
    public async Task TakenIdCanBeRepairedFromTheFieldOrLink()
    {
        using var host = new RulesHost();
        await host.Loaded;
        var model = host.Model;
        using var window = new ViewHost(new RulesView { DataContext = model }, 1440, 900);
        model.DuplicateRule("TEAM001");
        window.Pump();
        var page = model.CurrentPage!;
        var field = window.Find<TextBox>(box => box.Name == "IdBox" && box.IsEffectivelyVisible)!;
        var fix = window.Find<Button>(button => button.Name == "IdFix" && button.IsEffectivelyVisible);
        Assert.Null(fix);

        page.Id = "TEAM001";
        window.Pump();
        Assert.True(host.Row("TEAM001").HasIdClash);
        Assert.Equal("Use TEAM005", page.IdFixLabel);
        Assert.True(field.Focus(NavigationMethod.Tab));
        window.Press(PhysicalKey.Tab);
        fix = window.Find<Button>(button => button.Name == "IdFix" && button.IsEffectivelyVisible)!;
        Assert.True(fix.IsFocused);
        window.Press(PhysicalKey.Tab, RawInputModifiers.Shift);
        Assert.True(field.IsFocused);
        window.Press(PhysicalKey.Enter, RawInputModifiers.Alt);
        Assert.Equal("TEAM005", page.Id);
        Assert.False(host.Row("TEAM001").HasIdClash);
        Assert.True(field.IsFocused);
        Assert.Equal(page.Id.Length, field.CaretIndex);

        page.Id = "SEC 004";
        window.Pump();
        window.Press(PhysicalKey.Tab);
        window.Press(PhysicalKey.Space);
        Assert.Equal("SEC-004", page.Id);
        Assert.True(field.IsFocused);
    }

    [AvaloniaFact]
    public async Task RefusedSaveFocusesTheIdAndTheSavedIdIsPlainText()
    {
        using var host = new RulesHost();
        await host.Loaded;
        var model = host.Model;
        using var window = new ViewHost(new RulesView { DataContext = model }, 1440, 900);
        model.DuplicateRule("TEAM001");
        window.Pump();
        var page = model.CurrentPage!;
        page.Id = string.Empty;
        var title = window.Find<TextBox>(box => box.Name == "TitleBox" && box.IsEffectivelyVisible)!;
        Assert.True(title.Focus(NavigationMethod.Tab));
        model.SaveOpenRule();
        var field = window.Find<TextBox>(box => box.Name == "IdBox" && box.IsEffectivelyVisible)!;
        Assert.True(field.IsFocused);
        var save = window.Find<Button>(button => button.IsEffectivelyVisible && AutomationProperties.GetName(button) == "Save rule")!;
        var peer = ControlAutomationPeer.CreatePeerForElement(save);
        Assert.True(peer.IsEnabled());
        Assert.Equal("A rule needs an ID. Press Save rule to go there.", peer.GetHelpText());
        Assert.True(save.Focus(NavigationMethod.Tab));
        window.Pump();
        Assert.True(save.FindDescendantOfType<FocusRing>()!.IsVisible);
        window.Press(PhysicalKey.Space);
        Assert.True(field.IsFocused);

        page.Id = "SEC-004";
        model.SaveOpenRule();
        window.Pump();
        var label = window.Find<TextBlock>(text => text.Name == "IdText" && text.IsEffectivelyVisible)!;
        Assert.Equal("SEC-004", label.Text);
        Assert.False(label.Focusable);
        Assert.True(string.IsNullOrEmpty(AutomationProperties.GetHelpText(label)));
        Assert.Null(ToolTip.GetTip(label));
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
        Assert.Equal("TEAM005", host.RowIds()[^1]);
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
        Assert.Collection(row.VisiblePlaces,
            place => Assert.Equal("warns in tests/**/*.cs, line 18", place.SpokenName),
            place => Assert.Equal("off in **/Generated/*.cs, line 21", place.SpokenName),
            place => Assert.Equal("off in *.Designer.cs, line 24", place.SpokenName),
            place => Assert.Equal("blocks everywhere else, line 14", place.SpokenName));
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

        // The row can open the source or the rule page, and the file still refuses a change.
        var commands = typeof(FileRuleRowViewModel).GetProperties().Where(property => typeof(ICommand).IsAssignableFrom(property.PropertyType)).Select(property => property.Name);
        Assert.Equal("OpenFileCommand, OpenPageCommand", string.Join(", ", commands.OrderBy(name => name, StringComparer.Ordinal)));
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

    [AvaloniaTheory]
    [InlineData("quiet", "Blocks:error|Warns:warning|Off:suggestion, silent, none")]
    [InlineData("standard", "Blocks:error|Warns:warning, suggestion|Off:silent, none")]
    [InlineData("strict", "Blocks:error, warning|Warns:suggestion|Off:silent, none")]
    public async Task FileRulePageTableFollowsTheStrictnessInForce(string strictness, string expectedRows)
    {
        var settings = SettingsText.Four.Replace("\"rules\": [", $"\"strictness\": \"{strictness}\",\n    \"rules\": [", StringComparison.Ordinal);
        var severity = strictness == "strict" ? Severity.Block : Severity.Warn;
        var rule = RulesHost.FileRule("CA1000", "Do not declare static members on generic types", FileRuleState.On, severity, 4, "warning");
        using var host = new RulesHost(settings, new[] { rule });
        await host.Loaded;

        host.Model.SelectedItem = Assert.Single(host.Model.Items.OfType<FileRuleRowViewModel>());

        var rows = host.Model.FilePage.Settings;
        Assert.Equal(expectedRows, string.Join("|", rows.Select(row => $"{row.EffectWord}:{row.Words}")));
        Assert.Contains("warning", Assert.Single(rows, row => row.IsCurrent).Words, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public async Task NewAndDuplicateGoOnWhenARuleIdHasAVeryLongNumber()
    {
        var settings = SettingsText.Four.Replace("TEAM004", "TEAM99999999999999999999", StringComparison.Ordinal);
        using var host = new RulesHost(settings);
        await host.Loaded;

        host.Model.NewRule();
        Assert.Equal("TEAM004", host.Model.CurrentPage?.Id);

        host.Open("TEAM001").DuplicateRuleCommand.Execute(null);
        Assert.Equal("TEAM004", host.Model.CurrentPage?.Id);
    }

    [AvaloniaFact]
    public async Task NewNeverProposesAnIdThatExistsWhateverItsLength()
    {
        var settings = SettingsText.Four.Replace("TEAM004", "TEAM999999999999999", StringComparison.Ordinal).Replace("TEAM003", "TEAM1000000000000000", StringComparison.Ordinal);
        using var host = new RulesHost(settings);
        await host.Loaded;

        host.Model.NewRule();

        Assert.Equal("TEAM1000000000000001", host.Model.CurrentPage?.Id);
    }

    [AvaloniaFact]
    public async Task DuplicateKeepsANewRuleThatWasBegun()
    {
        using var host = new RulesHost();
        await host.Loaded;
        host.Model.NewRule();
        var begun = host.Model.CurrentPage!;
        begun.Title = "Half written";

        host.Open("TEAM001").DuplicateRuleCommand.Execute(null);

        Assert.Same(begun, host.Model.CurrentPage);
        Assert.Equal(RulesViewModel.UnfinishedDraftText, host.Model.BannerText);

        host.Model.DiscardDraft();
        host.Open("TEAM001").DuplicateRuleCommand.Execute(null);

        Assert.Equal("Copy of " + host.StoredRules().Single(rule => rule.Id == "TEAM001").Title, host.Model.CurrentPage?.Title);
        Assert.Null(host.Model.BannerText);
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
