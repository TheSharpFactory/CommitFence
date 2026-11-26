using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using CF.App.Rules.Page;
using CF.App.Tests.Support;
using CF.Core.Rules;
using Xunit;

namespace CF.App.Tests;

/// <summary>
/// The head of a rule's page in its three states, a range as one token, and how the tokens of a clause wrap.
/// </summary>
public sealed class RulesHeadAndTokensTests
{
    private const string _rangeSettings = """
        {"rules":[{"id":"R","language":"csharp","for":"method","severity":"warn","title":"Few parameters",
            "require":{"parameterCount":{"atLeast":1,"atMost":5}}}]}
        """;

    [AvaloniaFact]
    public async Task ARangeIsOneTokenReadFromTheSettingsFileAndWrittenBackToIt()
    {
        using var host = new RulesHost(_rangeSettings);
        await host.Loaded;
        var page = host.Open("R");

        // the file holds one object with two bounds; the page holds one token
        var range = Assert.Single(page.RequireFlow);
        Assert.True(range.IsRange);
        Assert.Equal("is at least", range.ComparisonWord);
        Assert.Equal(["1", "5"], range.Values);
        Assert.False(page.IsDirty);

        // the number picker names it "is between", with two boxes; keeping them changes the token and the page writes the object again
        range.OpenComparisonPicker(false);
        Assert.Equal("is between", range.ComparisonPicker.Comparisons.Single(entry => entry.IsCurrent).Name);
        Assert.True(range.ComparisonPicker.ShowsRange);
        range.ComparisonPicker.RangeHighText = "9";
        Assert.True(range.ComparisonPicker.CommitRange());
        Assert.True(page.IsDirty);
        page.SaveCommand.Execute(null);

        Assert.Contains("\"parameterCount\": { \"atLeast\": 1, \"atMost\": 9 }", File.ReadAllText(host.SettingsPath), StringComparison.Ordinal);
        Assert.False(page.IsDirty);
        Assert.True(Assert.Single(page.RequireFlow).IsRange);
        Assert.Equal(["parameterCount is at least 1", "parameterCount is at most 9"], host.StoredRules().Single().Require.Select(RuleSentence.Condition));

        // the ends cannot be reversed: the reason is given and nothing is kept
        var again = Assert.Single(page.RequireFlow);
        again.OpenComparisonPicker(false);
        again.ComparisonPicker.RangeLowText = "12";
        Assert.False(again.ComparisonPicker.CommitRange());
        Assert.Equal("The lower end must not be above the upper end.", again.ComparisonPicker.RefusalText);
        Assert.Equal(["1", "9"], again.Values);
        again.ComparisonPicker.Escape();

        // a clause holds no third comparison on a property that has a range: its picker does not offer the property again
        page.AddRequirement();
        var added = page.RequireFlow[^1];
        added.OpenPropertyPicker(false);
        Assert.True(added.PropertyPicker.Items.OfType<PickerEntryViewModel>().Single(entry => entry.Key == "parameterCount").IsDisabled);
    }

    [AvaloniaFact]
    public void AConditionMovesWholeToTheNextRowAndNeverWrapsInsideItself()
    {
        // step 1: a condition that does not fit in what is left of its line moves whole to the next line
        var flow = new TokenFlow { ItemSpacing = 8, LineSpacing = 8 };
        var first = new Border { Width = 300, Height = 36 };
        var second = new Border { Width = 300, Height = 36 };
        flow.Children.Add(first);
        flow.Children.Add(second);
        Lay(flow, 400);
        Assert.Equal(new Point(0, 0), first.Bounds.Position);
        Assert.Equal(new Point(0, 44), second.Bounds.Position);

        // step 2: a condition is one row: wider than its line, it does not wrap between its property and its comparison; the comparison is
        // offered what the property leaves, and the value inside it shortens to fit (see ValueShortening)
        var property = new Border { Width = 100, Height = 36 };
        var tail = new Border { Width = 350, Height = 36 };
        var condition = new ConditionFlow();
        condition.Children.Add(property);
        condition.Children.Add(tail);
        Lay(condition, 460);
        Assert.Equal(new Point(102, 0), tail.Bounds.Position);
        Lay(condition, 400);
        Assert.Equal(0, tail.Bounds.Y);
        Assert.Equal(298, tail.DesiredSize.Width);
        Assert.Equal(36, condition.DesiredSize.Height);
    }

    [Fact]
    public void AValueTooLongForItsRowIsShortenedAndItsWholeTextIsKept()
    {
        // every character is 10 wide, and the box of "+N" with its gap is 40
        static double Measure(string text)
        {
            return text.Length * 10;
        }

        static double More(int count)
        {
            return 40;
        }

        var names = new List<string> { "DisposeAsync", "StartAsync" };

        // it fits: nothing is cut
        Assert.Equal(new ValueShortening.Shown("DisposeAsync, StartAsync", 0), ValueShortening.Shorten(names, true, 240, Measure, More));

        // a list that does not fit keeps its first value and counts the rest; the whole text stays the whole list
        var list = ValueShortening.Shorten(names, true, 170, Measure, More);
        Assert.Equal(new ValueShortening.Shown("DisposeAsync", 1), list);
        Assert.Equal("DisposeAsync, StartAsync", ValueShortening.Whole(names, true));

        // a first value that does not fit either is cut at its end
        var cut = ValueShortening.Shorten(names, true, 120, Measure, More);
        Assert.Equal(1, cut.More);
        Assert.Contains("…", cut.Text, StringComparison.Ordinal);
        Assert.True(Measure(cut.Text) <= 120 - 40);

        // a path keeps its first folder and its last name
        const string path = @"C:\invoicing\src\Payments\Refunds\Payments";
        Assert.Equal(@"C:\invoicing\…\Payments", ValueShortening.CutMiddle(path, 240, Measure));
        Assert.Equal(path, ValueShortening.Whole([path], false));

        // a name with no separator keeps its start and its end
        var name = ValueShortening.CutMiddle("AVeryLongMethodNameAsync", 100, Measure);
        Assert.True(Measure(name) <= 100);
        Assert.StartsWith("AVer", name, StringComparison.Ordinal);
        Assert.EndsWith("ync", name, StringComparison.Ordinal);
    }

    [Fact]
    public void AListTokenShowsItsFirstValueWholeAndCutsOnlyAFirstValueTooWideForItsRoom()
    {
        static double Measure(string text)
        {
            return text.Length * 10;
        }

        static double More(int count)
        {
            return 40;
        }

        // "internal" and the badge fit, the whole list does not: the first value is whole, never "in…l"
        var access = new List<string> { "internal", "private" };
        Assert.Equal(new ValueShortening.Shown("internal", 1), ValueShortening.Shorten(access, true, 120, Measure, More));

        // only a first value too wide for the room is cut: a name at its end, a path in the middle with its first folder and last name
        var names = new List<string> { "DisposeAsync", "StartAsync" };
        Assert.Equal(new ValueShortening.Shown("Dispo…", 1), ValueShortening.Shorten(names, true, 100, Measure, More));
        var paths = new List<string> { @"C:\invoicing\src\Payments\Refunds", @"C:\invoicing\src\Billing" };
        Assert.Equal(new ValueShortening.Shown(@"C:\invoicing\…\Refunds", 1), ValueShortening.Shorten(paths, true, 270, Measure, More));
    }

    [Fact]
    public void ShorteningNeverLengthensAValueOrLeavesItWiderThanItsRoom()
    {
        static double Measure(string text)
        {
            return text.Length * 10;
        }

        static double More(int count)
        {
            return 40;
        }

        // one character of room: a short name becomes an ellipsis alone, never longer than itself
        Assert.Equal("…", ValueShortening.CutMiddle("ab", 10, Measure));
        Assert.True(ValueShortening.CutMiddle("abcdef", 30, Measure).Length <= 6);

        // the list's first value and its "+N" are fitted together; when the badge alone leaves no room, an ellipsis stands alone
        var names = new List<string> { "DisposeAsync", "StartAsync" };
        Assert.Equal(new ValueShortening.Shown("…", 0), ValueShortening.Shorten(names, true, 30, Measure, More));
        var fitted = ValueShortening.Shorten(names, true, 100, Measure, More);
        Assert.Equal(1, fitted.More);
        Assert.True(Measure(fitted.Text) + More(1) <= 100);
    }

    [AvaloniaFact]
    public async Task TheHeadSaysSavedUnsavedOrCannotSaveAndTheLineUnderTheTitleIsNeverEmpty()
    {
        using var host = new RulesHost(_rangeSettings);
        await host.Loaded;
        var page = host.Open("R");
        var seen = new List<string>();

        // saved: both buttons are off
        seen.Add(page.HeadLine);
        Assert.True(page.IsSavedState);
        Assert.Equal("Saved", page.SaveStateWord);
        Assert.Equal("Kept in commitfence.json.", page.HeadLine);
        Assert.False(page.IsUnsaved);
        Assert.False(page.SaveCommand.CanExecute(null));

        // a path being typed counts, and one the page would refuse cannot be saved: the state says so, Save is off, and the line names the field
        page.AddPath();
        var path = page.Paths[0];
        path.Open();
        Assert.True(page.IsSavedState);
        path.FieldText = "C:\\invoicing\\src";
        Assert.Equal(SaveStateKind.CannotSave, page.SaveState);
        Assert.Equal("Cannot save", page.SaveStateWord);
        Assert.True(page.IsUnsaved);
        Assert.False(page.SaveCommand.CanExecute(null));
        Assert.Equal("Cannot save: the path in within is not valid.", page.HeadLine);
        Assert.StartsWith("Path C:\\invoicing\\src: Write the path from the root of ", page.SaveBlockerText, StringComparison.Ordinal);
        seen.Add(page.HeadLine);
        path.FieldText = "src/Billing";
        Assert.Equal(SaveStateKind.Unsaved, page.SaveState);
        Assert.True(page.SaveCommand.CanExecute(null));
        path.Cancel();
        Assert.True(page.IsSavedState);

        // an edit of the title is unsaved while Save works; with no title it cannot be saved, and the line says what to do
        page.Title = "Fewer parameters";
        Assert.Equal("Unsaved", page.SaveStateWord);
        Assert.Equal("Changed since it was saved. Save writes it to commitfence.json.", page.HeadLine);
        Assert.True(page.SaveCommand.CanExecute(null));
        page.Title = string.Empty;
        Assert.Equal("Cannot save", page.SaveStateWord);
        Assert.Equal("Cannot save: the rule needs a title.", page.HeadLine);
        Assert.False(page.SaveCommand.CanExecute(null));
        Assert.Null(page.SaveBlockerTarget);
        page.Title = "Fewer parameters";

        // the range no method can meet: the line names the clause and the reason, and following it goes to the token
        var range = page.RequireFlow[0];
        range.SetValues(["50", "5"]);
        Assert.Equal("Cannot save", page.SaveStateWord);
        Assert.Equal("Cannot save: the parameter count requirement cannot be met.", page.HeadLine);
        Assert.Equal("\"parameter count is between 50 and 5\" cannot be met: the lower end is above the upper end. Change either number to save the rule.", page.RequireReason);
        Assert.Null(page.WhereReason);
        Assert.True(page.HeadLineIsBad);
        Assert.True(page.IsUnsaved);
        Assert.Same(range, page.SaveBlockerTarget);
        seen.Add(page.HeadLine);

        // discarding goes back to saved
        page.DiscardCommand.Execute(null);
        Assert.True(page.IsSavedState);
        seen.Add(page.HeadLine);
        Assert.DoesNotContain(seen, line => line.Length == 0);
    }

    [AvaloniaFact]
    public async Task ARangeKeepsItsStoredEndsAndTheHeadSaysWhatItsPickerHolds()
    {
        const string signed = """
            {"rules":[{"id":"R","language":"csharp","for":"method","severity":"warn","title":"Few parameters",
                "require":{"parameterCount":{"atLeast":-1,"atMost":5}}}]}
            """;
        using var host = new RulesHost(signed);
        await host.Loaded;
        var page = host.Open("R");
        var range = Assert.Single(page.RequireFlow);

        // opening the picker and dismissing it leaves a signed end as the file holds it, and the page is still saved
        range.OpenComparisonPicker(false);
        Assert.Equal("-1", range.ComparisonPicker.RangeLowText);
        Assert.True(page.IsSavedState);
        range.ComparisonPicker.Close();
        Assert.Equal(["-1", "5"], range.Values);
        Assert.True(page.IsSavedState);

        // an end typed in the picker is unsaved while the picker holds it and Save works; one that is no number cannot be saved and Save is off
        range.OpenComparisonPicker(false);
        range.ComparisonPicker.RangeHighText = "9";
        Assert.Equal("Unsaved", page.SaveStateWord);
        Assert.True(page.SaveCommand.CanExecute(null));
        range.ComparisonPicker.RangeHighText = "-";
        Assert.Equal("Cannot save", page.SaveStateWord);
        Assert.Equal("Cannot save: the range of parameter count is not valid.", page.HeadLine);
        Assert.Equal("Range of parameter count: Type a number.", page.SaveBlockerText);
        Assert.False(page.SaveCommand.CanExecute(null));
        Assert.Same(range, page.SaveBlockerTarget);
        range.ComparisonPicker.RangeHighText = "9";
        page.SaveCommand.Execute(null);
        Assert.Equal(["-1", "9"], range.Values);
        Assert.True(page.IsSavedState);

        // the setup writing the file switches Save off, and the word says so instead of "Unsaved"
        page.Title = "Fewer parameters";
        Assert.Equal("Unsaved", page.SaveStateWord);
        page.IsSetupWriting = true;
        Assert.Equal("Cannot save", page.SaveStateWord);
        Assert.False(page.SaveCommand.CanExecute(null));
        page.IsSetupWriting = false;
        Assert.Equal("Unsaved", page.SaveStateWord);

        // a range made one-sided keeps the end its comparison is about: the upper for "at most", the lower for "at least"
        range.SetComparison(Comparison.AtMost);
        Assert.Equal(["9"], range.Values);
        range.MakeRange();
        range.SetComparison(Comparison.AtLeast);
        Assert.Equal(["0"], range.Values);
    }

    private static void Lay(Control control, double width)
    {
        control.Measure(new Size(width, double.PositiveInfinity));
        control.Arrange(new Rect(0, 0, width, control.DesiredSize.Height));
    }
}
