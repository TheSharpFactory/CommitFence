using System.Collections.Concurrent;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using CF.App.Rules;
using CF.App.Rules.Page;
using CF.App.Tests.Support;
using CF.Core.Code;
using CF.Core.Rules;
using Xunit;

namespace CF.App.Tests;

/// <summary>
/// Try it on the page of a rule: the code in the box and what the rule, as the page holds it now, says about it.
/// </summary>
public sealed class TryItTests
{
    private const string _snippet = """
        class C
        {
            void M(bool a)
            {
                if(a) { F(); }
                if(a) { F(); }
                if(a) F();
            }

            void F()
            {
            }
        }
        """;

    [AvaloniaFact]
    public async Task PreviewUsesTheUnsavedRuleAndNeverPersistsCode()
    {
        using var host = new RulesHost();
        await host.Loaded;
        var page = host.Open("TEAM001");
        var tryIt = page.TryIt;
        var before = host.SettingsBytes();

        // the saved rule: two ifs have braces, which it forbids, and one has none
        await SetCodeAsync(tryIt, _snippet);
        Assert.Equal(TryPhase.Tried, tryIt.Phase);
        Assert.Equal((2, 1), (tryIt.Breaks, tryIt.Follows));
        Assert.Equal(new[] { 5, 6 }, tryIt.Verdicts.Where(verdict => verdict.Outcome == TryOutcome.BreaksIt).Select(verdict => verdict.Span.StartLine));

        // the same code against the rule with an edit that is not saved: the verdicts turn round, and the reason is in the page's words
        page.RequireFlow[0].SetValues(new[] { "true" });
        await Signals.Idle(tryIt);
        Assert.True(page.IsDirty);
        Assert.Equal((1, 2), (tryIt.Breaks, tryIt.Follows));
        var breaking = Assert.Single(tryIt.Verdicts, verdict => verdict.Outcome == TryOutcome.BreaksIt);
        Assert.Equal(7, breaking.Span.StartLine);
        Assert.Contains("braces", tryIt.InPageWords(breaking.Reason), StringComparison.Ordinal);
        Assert.Equal(before, host.SettingsBytes());

        // a rule that is not saved yet
        host.Model.NewRule();
        var draft = host.Model.CurrentPage!;
        draft.Title = "Ifs have braces";
        draft.OpenArtifactPicker(false);
        draft.ArtifactPicker.ChooseCommand.Execute(draft.ArtifactPicker.Items.OfType<PickerEntryViewModel>().First(entry => entry.Key == "if-statement"));
        draft.RequireFlow[0].SetProperty("braces");
        await SetCodeAsync(draft.TryIt, _snippet);
        Assert.Equal(TryPhase.Tried, draft.TryIt.Phase);
        Assert.Equal((1, 2), (draft.TryIt.Breaks, draft.TryIt.Follows));

        // each page keeps its own code while another is open
        host.Model.SelectedItem = host.Row("TEAM002");
        Assert.Equal(string.Empty, host.Model.CurrentPage!.TryIt.Code);
        host.Model.SelectedItem = host.Row("TEAM001");
        Assert.Equal(_snippet, tryIt.Code);
        host.Model.NewRule();
        Assert.Equal(_snippet, host.Model.CurrentPage!.TryIt.Code);

        // nothing of the code was written
        Assert.Equal(before, host.SettingsBytes());
        Assert.Equal(new[] { host.SettingsPath }, Directory.GetFileSystemEntries(host.RepositoryPath));
    }

    [AvaloniaFact]
    public async Task PreviewReplacesOldVerdictsWithTheCurrentOutcome()
    {
        var span = new SourceSpan(5, 1, 5, 20);
        var tried = TryAnswer.Of(new RuleTryResult(TryStatus.Tried, new[] { new ArtifactVerdict(TryOutcome.BreaksIt, span, "braces is true"), new ArtifactVerdict(TryOutcome.FollowsIt, span, string.Empty) }, string.Empty, null, null));
        var answers = new Dictionary<string, TryAnswer>
        {
            ["break 1"] = tried,
            ["break 2"] = tried,
            ["break 3"] = tried,
            ["break 4"] = tried,
            ["break 5"] = tried,
            ["break 6"] = tried,
            ["garbage"] = TryAnswer.Of(new RuleTryResult(TryStatus.DoesNotParse, new List<ArtifactVerdict>(), "does not parse", 3, 1)),
            ["nothing"] = TryAnswer.Of(new RuleTryResult(TryStatus.Tried, new List<ArtifactVerdict>(), string.Empty, null, null)),
            ["build"] = TryAnswer.Of(new RuleTryResult(TryStatus.NeedsBuild, new List<ArtifactVerdict>(), "the rule needs the build", null, null)),
            ["refused"] = TryAnswer.Refused("The property is not one of the artifact.")
        };
        var plan = new TryPlan("sentence", code => answers[code]);
        var tryIt = new TryItViewModel();
        tryIt.SentenceChanged("if statement", new[] { "braces" }, plan);

        async Task Check(string code, TryPhase phase)
        {
            await SetCodeAsync(tryIt, code);
            Assert.Equal(phase, tryIt.Phase);
            if(phase == TryPhase.Tried)
            {
                Assert.Equal((1, 1, 0), (tryIt.Breaks, tryIt.Follows, tryIt.NotCovered));
                Assert.Equal(2, tryIt.Verdicts.Count);
            }
            else
            {
                Assert.Equal((0, 0, 0), (tryIt.Breaks, tryIt.Follows, tryIt.NotCovered));
                Assert.Empty(tryIt.Verdicts);
            }
        }

        await Check("break 1", TryPhase.Tried);
        await Check("garbage", TryPhase.DoesNotParse);
        Assert.Contains("line 3", tryIt.ParseText, StringComparison.Ordinal);
        await Check("break 2", TryPhase.Tried);
        await Check("nothing", TryPhase.NoArtifact);
        Assert.Contains("if statement", tryIt.Message, StringComparison.Ordinal);
        await Check("break 3", TryPhase.Tried);
        await Check("build", TryPhase.NeedsBuild);
        Assert.Contains("build", tryIt.Message, StringComparison.Ordinal);
        await Check("break 4", TryPhase.Tried);
        await Check("refused", TryPhase.Refused);
        Assert.Contains("not one of the artifact", tryIt.Message, StringComparison.Ordinal);
        await Check("break 5", TryPhase.Tried);

        // the sentence is not finished: the old verdicts go, and a finished sentence brings the answer back
        tryIt.SentenceChanged("if statement", new[] { "braces" }, null);
        await Signals.Idle(tryIt);
        Assert.Equal(TryPhase.Unfinished, tryIt.Phase);
        Assert.Equal((0, 0, 0), (tryIt.Breaks, tryIt.Follows, tryIt.NotCovered));
        Assert.Empty(tryIt.Verdicts);
        tryIt.SentenceChanged("if statement", new[] { "braces" }, plan);
        await Signals.Idle(tryIt);
        Assert.Equal(TryPhase.Tried, tryIt.Phase);
        Assert.Equal((1, 1, 0), (tryIt.Breaks, tryIt.Follows, tryIt.NotCovered));
    }

    [AvaloniaFact]
    public async Task PreviewPublishesOnlyTheLatestText()
    {
        using var gate = new ManualResetEventSlim();
        using var firstStarted = new ManualResetEventSlim();
        var asked = new ConcurrentQueue<string>();
        var span = new SourceSpan(1, 1, 1, 2);
        var plan = new TryPlan("sentence", code =>
        {
            asked.Enqueue(code);
            if(code != "old text")
                return TryAnswer.Of(new RuleTryResult(TryStatus.Tried, new[] { new ArtifactVerdict(TryOutcome.FollowsIt, span, string.Empty), new ArtifactVerdict(TryOutcome.FollowsIt, span, string.Empty) }, string.Empty, null, null));

            firstStarted.Set();
            gate.Wait();
            return TryAnswer.Of(new RuleTryResult(TryStatus.Tried, new[] { new ArtifactVerdict(TryOutcome.BreaksIt, span, string.Empty) }, string.Empty, null, null));
        });
        var tryIt = new TryItViewModel();
        tryIt.SentenceChanged("if statement", new List<string>(), plan);
        var seen = new List<(int Breaks, int Follows)>();
        tryIt.Shown += () => seen.Add((tryIt.Breaks, tryIt.Follows));

        tryIt.Code = "old text";
        await Task.Run(() => firstStarted.Wait(TestContext.Current.CancellationToken), TestContext.Current.CancellationToken);
        tryIt.Code = "new text";
        gate.Set();
        await Signals.Idle(tryIt);

        Assert.Equal(new[] { "old text", "new text" }, asked.ToArray());
        Assert.Equal((0, 2), Assert.Single(seen));
        Assert.Equal((0, 2), (tryIt.Breaks, tryIt.Follows));
    }

    [AvaloniaFact]
    public async Task PasteReplacesTheSnippetAndClearResetsThePreview()
    {
        using var host = new RulesHost();
        await host.Loaded;
        var page = host.Open("TEAM001");
        var view = new TryItView { DataContext = page.TryIt };
        using var window = new ViewHost(view, 900, 700);
        await SetCodeAsync(page.TryIt, "// code that is in the box before the paste");
        var clipboard = window.Window.Clipboard ?? throw new InvalidOperationException("the window has no clipboard");
        await clipboard.SetTextAsync(_snippet);

        view.PasteButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await Signals.Until(page.TryIt, () => page.TryIt.Code == _snippet);
        await Signals.Idle(page.TryIt);

        Assert.Equal(_snippet, view.Editor.Document.Text);
        Assert.Equal(TryPhase.Tried, page.TryIt.Phase);
        Assert.Equal((2, 1), (page.TryIt.Breaks, page.TryIt.Follows));

        view.ClearButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await Signals.Idle(page.TryIt);

        Assert.Equal(string.Empty, view.Editor.Document.Text);
        Assert.Equal(string.Empty, page.TryIt.Code);
        Assert.Equal((0, 0, 0), (page.TryIt.Breaks, page.TryIt.Follows, page.TryIt.NotCovered));
        Assert.Empty(page.TryIt.Verdicts);
        Assert.Equal(TryPhase.Empty, page.TryIt.Phase);
    }

    private static async Task SetCodeAsync(TryItViewModel tryIt, string code)
    {
        var shown = Signals.Shown(tryIt);
        tryIt.Code = code;
        await shown;
        await Signals.Idle(tryIt);
    }
}
