using Avalonia.Headless.XUnit;
using CF.App.Rules.Page;
using CF.App.Tests.Support;
using CF.Core.Findings;
using CF.Core.Review;
using CF.Core.Rules;
using Xunit;

namespace CF.App.Tests;

/// <summary>
/// What the rule page adds: where the rule applies, what a finding says and why, several requirements, and the comparisons the
/// property's type offers. The real rule service writes the settings file; only the files of the repository are the test's.
/// </summary>
public sealed class RulesPageFieldsTests
{
    private static readonly string[] _numberComparisons = ["Is", "IsOneOf", "IsNot", "AtMost", "AtLeast", "Between"];
    private static readonly string[] _textComparisons = ["Is", "IsOneOf", "IsNot", "Matches"];

    [AvaloniaFact]
    public async Task ComparisonEntryAndTokenSayTheSameWordsByTheNumberOfValues()
    {
        using var host = new RulesHost();
        await host.Loaded;
        var page = host.Open("TEAM001");
        page.AddRequirement();
        var text = page.RequireFlow[^1];
        text.IsJustAdded = false;
        text.SetProperty("name");
        text.SetComparison(Comparison.IsNot);
        text.SetValues(["Run"]);

        // one value: "is not" on the entry and on the token
        text.OpenComparisonPicker(false);
        var entry = text.ComparisonPicker.Comparisons.Single(candidate => candidate.Key == nameof(Comparison.IsNot));
        Assert.Equal("is not", entry.Name);
        Assert.Equal("is not", text.ComparisonWord);

        // a second value typed in the list: the entry says "is none of" at once, and the token when the list is kept
        text.ComparisonPicker.AddRowCommand.Execute(null);
        text.ComparisonPicker.Rows[^1].Text = "Start";
        Assert.Equal("is none of", entry.Name);
        Assert.True(text.ComparisonPicker.CommitList());
        Assert.Equal("is none of", text.ComparisonWord);
        text.OpenComparisonPicker(false);
        Assert.Equal("is none of", text.ComparisonPicker.Comparisons.Single(candidate => candidate.Key == nameof(Comparison.IsNot)).Name);

        // choosing "is not" for a property that holds two values keeps both, under the same name
        text.SetComparison(Comparison.Is);
        text.SetValues(["Run", "Start"]);
        text.SetComparison(Comparison.IsNot);
        Assert.Equal("is none of", text.ComparisonWord);
    }

    [AvaloniaFact]
    public async Task PathsAreAddedEditedAndSaved()
    {
        using var host = new RulesHost();
        await host.Loaded;
        host.Model.PathFiles = new FakeLanguageService { Tracked = ["src/Billing/A.cs", "src/Billing/B.cs", "src/Other/C.cs"] };
        var page = host.Open("TEAM001");
        Assert.IsType<EveryFileItem>(page.WithinFlow[0]);

        page.AddPath();
        var path = Assert.Single(page.Paths);
        path.Open();

        // a pattern the picker refuses keeps nothing, and says how to write it
        path.FieldText = "src\\Billing\\**";
        Assert.False(path.Commit());
        Assert.Equal("Write the path from the root of invoicing, with / between folders: src/Billing/**.", path.Problem);
        path.FieldText = "../x/**";
        Assert.Equal("A path stays inside invoicing, so .. cannot be part of it.", path.Problem);
        path.FieldText = "src/{A,B}.cs";
        Assert.Equal("Only *, ** and ? are wildcards here.", path.Problem);

        // a pattern that is kept says what it names
        path.FieldText = "src/Billing/**";
        Assert.Equal("2", path.MatchesCount);
        Assert.True(path.Commit());
        page.AddPath();
        var second = page.Paths[1];
        second.Open();
        second.FieldText = "src/Other/**";
        Assert.True(second.Commit());

        Assert.True(page.IsDirty);
        Assert.Equal("or", Assert.IsType<AddPathItem>(page.WithinFlow[^1]).Text);
        page.SaveCommand.Execute(null);
        Assert.Equal(["src/Billing/**", "src/Other/**"], host.StoredRules().Single(rule => rule.Id == "TEAM001").Paths);

        // an empty field removes the path, and the last one brings back "every file"
        var first = page.Paths[0];
        first.Open();
        first.FieldText = " ";
        Assert.True(first.Commit());
        Assert.Single(page.Paths);
        page.Paths[0].RemoveCommand.Execute(null);
        Assert.IsType<EveryFileItem>(page.WithinFlow[0]);
        page.SaveCommand.Execute(null);
        Assert.Empty(host.StoredRules().Single(rule => rule.Id == "TEAM001").Paths);
    }

    [AvaloniaFact]
    public async Task MessageAndReasonAreSavedAndShownOnAnOpenFinding()
    {
        string saved;
        using(var host = new RulesHost())
        {
            await host.Loaded;
            var page = host.Open("TEAM001");
            Assert.Equal("Uses the title: No braces around a single-line if", page.MessagePlaceholder);

            // spaces at either end are taken off, and a field of spaces is empty
            page.Message = "  Write the if on one line.  ";
            page.Why = "Short ifs read better.";
            Assert.True(page.IsDirty);
            page.SaveCommand.Execute(null);
            var rule = host.StoredRules().Single(candidate => candidate.Id == "TEAM001");
            Assert.Equal("Write the if on one line.", rule.Message);
            Assert.Equal("Short ifs read better.", rule.Why);

            page.Why = "   ";
            page.SaveCommand.Execute(null);
            Assert.Null(host.StoredRules().Single(candidate => candidate.Id == "TEAM001").Why);
            page.Why = "Short ifs read better.";
            page.SaveCommand.Execute(null);

            // a bar moved from the message to the reason is a change, though both texts joined read the same
            page.Message = "Use A|B";
            page.Why = "C";
            page.SaveCommand.Execute(null);
            page.Message = "Use A";
            page.Why = "B|C";
            Assert.True(page.IsDirty);
            Assert.True(page.SaveCommand.CanExecute(null));
            page.Message = "Write the if on one line.";
            page.Why = "Short ifs read better.";
            page.SaveCommand.Execute(null);
            saved = File.ReadAllText(host.SettingsPath);
        }

        using var review = new ReviewHost(settings: saved);
        review.Review.Result = _ => Samples.Result(ReviewOutcome.Blocked, [Samples.Finding("a.cs", 3, "TEAM001", Severity.Block, "Write the if on one line.")], [Samples.Reviewed("a.cs")]);
        await review.RunAsync();
        var finding = Assert.Single(Assert.Single(review.Model.FileGroups).Findings);
        Assert.True(finding.HasWhy);
        Assert.Equal("Short ifs read better.", finding.WhyText);
    }

    [AvaloniaFact]
    public async Task RequirementsAreAddedAndRemovedButNeverTheLast()
    {
        using var host = new RulesHost();
        await host.Loaded;
        var page = host.Open("TEAM002");
        var only = Assert.Single(page.RequireFlow);
        Assert.False(only.CanRemove);

        // a new requirement that is dismissed with Esc goes again
        page.AddRequirement();
        Assert.Equal(2, page.RequireFlow.Count);
        Assert.All(page.RequireFlow, condition => Assert.True(condition.CanRemove));
        page.RequireFlow[1].PropertyPicker.Escape();
        Assert.Single(page.RequireFlow);
        Assert.False(only.CanRemove);

        page.AddRequirement();
        var added = page.RequireFlow[1];
        added.SetProperty("parameterCount");
        added.SetComparison(Comparison.AtMost);
        added.SetValues(["5"]);
        Assert.Equal("is at most", added.ComparisonWord);
        Assert.IsType<AddRequirementItem>(page.RequireItems[^1]);
        page.SaveCommand.Execute(null);
        var stored = host.StoredRules().Single(rule => rule.Id == "TEAM002");
        Assert.Equal(["documentationPresent is true", "parameterCount is at most 5"], stored.Require.Select(RuleSentence.Condition));

        // removing one leaves the other; the last one cannot be removed
        page.RemoveCondition(page.RequireFlow[0]);
        Assert.Single(page.RequireFlow);
        page.RemoveCondition(page.RequireFlow[0]);
        Assert.Single(page.RequireFlow);
        page.SaveCommand.Execute(null);
        Assert.Equal(["parameterCount is at most 5"], host.StoredRules().Single(rule => rule.Id == "TEAM002").Require.Select(RuleSentence.Condition));
    }

    [AvaloniaFact]
    public async Task ComparisonPickerOffersWhatFitsAndARangeIsWrittenAsOneObject()
    {
        using var host = new RulesHost();
        await host.Loaded;
        var page = host.Open("TEAM002");
        page.AddRequirement();
        var number = page.RequireFlow[1];
        number.SetProperty("parameterCount");
        number.OpenComparisonPicker(false);
        Assert.Equal(_numberComparisons, number.ComparisonPicker.Comparisons.Select(entry => entry.Key));
        Assert.True(number.ComparisonPicker.ShowsField);

        page.AddRequirement();
        var text = page.RequireFlow[2];
        text.SetProperty("name");
        text.OpenComparisonPicker(false);
        Assert.Equal(_textComparisons, text.ComparisonPicker.Comparisons.Select(entry => entry.Key));
        Assert.Contains("a pattern", text.ComparisonPicker.Comparisons.Select(entry => entry.KindText));

        // a yes or no property has one comparison, so its picker holds only the two values
        var flag = page.RequireFlow[0];
        flag.OpenComparisonPicker(false);
        Assert.False(flag.ComparisonPicker.ShowsComparisons);
        Assert.Equal(["true", "false"], flag.ComparisonPicker.ValueEntries.Select(entry => entry.Key));

        // several values of "is not" read "is none of", and a value that is kept in a list is one entry per value
        text.SetComparison(Comparison.IsNot);
        text.SetValues(["Run", "Start"]);
        Assert.Equal("is none of", text.ComparisonWord);
        text.SetValues(["Run"]);
        Assert.Equal("is not", text.ComparisonWord);
        text.SetComparison(Comparison.Matches);
        text.SetValues(["*Async"]);
        Assert.Equal("matches", text.ComparisonWord);

        // a range is one token, written as one object with its two bounds and read back as one token; one no code can meet is refused
        number.MakeRange();
        number.SetValues(["1", "5"]);
        page.RemoveCondition(text);
        Assert.False(page.HasInvalidCondition);
        page.SaveCommand.Execute(null);
        Assert.Contains("\"parameterCount\": { \"atLeast\": 1, \"atMost\": 5 }", File.ReadAllText(host.SettingsPath), StringComparison.Ordinal);
        number = page.RequireFlow[1];
        Assert.Equal(2, page.RequireFlow.Count);
        Assert.True(number.IsRange);
        Assert.Equal(["1", "5"], number.Values);
        Assert.False(page.IsDirty);

        number.SetValues(["50", "5"]);
        Assert.True(page.HasInvalidCondition);
        Assert.Contains("\"parameter count is between 50 and 5\" cannot be met: the lower end is above the upper end. Change either number to save the rule.", page.InvalidText, StringComparison.Ordinal);
        Assert.True(number.ShowsInvalid);
        Assert.False(page.SaveCommand.CanExecute(null));
        Assert.Equal("Fix the sentence to save it.", page.SaveTip);
        Assert.Equal("Cannot save: the parameter count requirement cannot be met.", page.HeadLine);
        Assert.Equal("Require clause: \"parameter count is between 50 and 5\" cannot be met: the lower end is above the upper end. Change either number to save the rule.", page.InvalidSpokenName);

        // two conditions on one property that a range cannot hold are refused on the page before they are saved
        number.SetComparison(Comparison.AtLeast);
        number.SetValues(["1"]);
        page.AddRequirement();
        var other = page.RequireFlow[2];
        other.SetProperty("parameterCount");
        other.SetComparison(Comparison.IsNot);
        other.SetValues(["3"]);
        Assert.False(page.SaveCommand.CanExecute(null));
        Assert.Equal("A clause can hold one condition per property, or \"is at least\" with \"is at most\" as a range. Join the other conditions on parameterCount into one, or move one to where.", page.InvalidText);
    }

    [AvaloniaFact]
    public async Task EscapeInTheComparisonPickerPutsBackTheComparisonAndTheValue()
    {
        using var host = new RulesHost();
        await host.Loaded;
        var page = host.Open("TEAM002");
        page.AddRequirement();
        var number = page.RequireFlow[1];
        number.SetProperty("parameterCount");
        number.SetValues(["5"]);

        // choosing a comparison and then leaving with Esc changes nothing
        number.OpenComparisonPicker(false);
        number.ComparisonPicker.ChooseComparisonCommand.Execute(number.ComparisonPicker.Comparisons.Single(entry => entry.Key == nameof(Comparison.AtMost)));
        Assert.Equal(Comparison.AtMost, number.Comparison);
        number.ComparisonPicker.Escape();
        Assert.Equal(Comparison.Is, number.Comparison);
        Assert.Equal(["5"], number.Values);
        Assert.False(number.ComparisonPicker.IsOpen);

        // closing it any other way keeps the comparison
        number.OpenComparisonPicker(false);
        number.ComparisonPicker.ChooseComparisonCommand.Execute(number.ComparisonPicker.Comparisons.Single(entry => entry.Key == nameof(Comparison.AtMost)));
        number.ComparisonPicker.Close();
        Assert.Equal(Comparison.AtMost, number.Comparison);
        Assert.Equal(["5"], number.Values);
    }
}
