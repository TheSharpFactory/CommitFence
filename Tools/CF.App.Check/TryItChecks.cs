using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.VisualTree;
using CF.App.Rules.Page;
using CF.Core.Rules;

namespace CF.App.Check;

/// <summary>
/// The checks of Try it, the code box on the page of a rule, that are about what it says: its verdicts against what
/// <c>commitfence rules try</c> says for the same code, an edit not saved, a rule not saved, every other state, the keys, the
/// names, and that nothing of the code is written. The speed, the height, the colours and the pictures are in
/// <see cref="TryItLookChecks" />.
/// </summary>
internal sealed partial class TryItChecks
{
    /// <summary>
    /// The sample code the Try it checks work on.
    /// </summary>
    public const string Sample = """
        public void Send(Invoice invoice)
        {
            if(invoice == null)
            {
                return;
            }

            if(invoice.Lines.Count == 0)
                return;

            if(invoice.Total < 0)
            {
                log.Warn("Negative total");
                invoice.Total = 0;
            }
        }
        """;

    private const string _needsBuildSentence = "The rule needs what the compiler knows about the code, so it cannot be tried without a build.";

    private static readonly string[] _needsBuildRules = { "TEAM011", "TEAM012" };

    private static readonly int[] _sampleLines = { 3, 8, 11 };

    private readonly CheckContext _context;

    public TryItChecks(CheckContext context)
    {
        _context = context;
    }

    /// <summary>
    /// The sample with <c>public </c> taken off its first line: the reader of the command line does not accept a top-level
    /// public method.
    /// </summary>
    public static string SampleWithoutPublic
    {
        get
        {
            return Sample.Replace("public void", "void", StringComparison.Ordinal);
        }
    }

    public void Run()
    {
        _context.Guarded("try-sample", "the sample without public equals rules try, with its three counts", SampleEqualsTheCommandLine);
        _context.Guarded("try-sample-verbatim", "the sample with public does not parse, as rules try says", VerbatimSample);
        _context.Guarded("try-fragments", "code that is a member or a few statements is read", Fragments);
        _context.Guarded("try-words", "the labels say a property as the page names it", InThePagesWords);
        _context.Guarded("try-unsaved", "an edit not saved equals rules set then rules try", UnsavedEdit);
        _context.Guarded("try-new", "a rule not saved equals rules add then rules try", UnsavedRule);
        _context.Guarded("try-empty", "the empty box shows its placeholder and no label", EmptyBox);
        _context.Guarded("try-parse", "code that does not parse says so with its line", DoesNotParse);
        _context.Guarded("try-none", "code with no artifact of the kind says so", NoArtifact);
        _context.Guarded("try-unfinished", "a sentence not complete says Finish the rule to try it", Unfinished);
        _context.Guarded("try-needs-build", "a rule that needs the build says so in the place of the head's message", NeedsBuild);
        _context.Guarded("try-refused", "an edit that Save would refuse gives Save's message", Refused);
        _context.Guarded("try-kept", "each rule keeps its code while the app runs and nothing is written", Kept);
        _context.Guarded("try-keys", "Tab is four spaces; Esc then Tab leaves the box", Keys);
        _context.Guarded("try-paste-clear", "Paste puts the clipboard in place of the code; Clear empties the box", PasteAndClear);
        _context.Guarded("try-names", "the names a screen reader is given", Names);
        _context.Guarded("try-dropped", "an answer to older text is dropped", OlderAnswerIsDropped);
        new TryItLookChecks(_context).Run();
        new PageRepairChecks(_context).Run();
    }

    /// <summary>
    /// The box of the page that is shown.
    /// </summary>
    public static TryItView ViewOf(AppWindow app, RulePageViewModel page)
    {
        return app.Find<TryItView>(view => view.IsEffectivelyVisible && ReferenceEquals(view.DataContext, page.TryIt)).First();
    }

    /// <summary>
    /// Puts text in the box the way typing or pasting does, and lets the window work until the box has answered.
    /// </summary>
    public static void PutCode(AppWindow app, RulePageViewModel page, string code)
    {
        var view = ViewOf(app, page);
        view.Editor.Document.Text = code;
        app.PumpUntil(() => !page.TryIt.IsBusy, 5000);
        app.Pump(200);
    }

    /// <summary>
    /// The labels drawn inside the code, with the line of the row each stands on.
    /// </summary>
    public static List<(TryLabel Label, int Line)> LabelsInCode(TryItView view)
    {
        var textView = view.Editor.TextArea.TextView;
        var labels = new List<(TryLabel, int)>();
        foreach(var label in textView.GetVisualDescendants().OfType<TryLabel>())
        {
            var top = (label.TranslatePoint(new Avalonia.Point(0, 2), textView)?.Y ?? 0) + textView.VerticalOffset;
            var line = textView.VisualLines.FirstOrDefault(candidate => top >= candidate.VisualTop && top < candidate.VisualTop + candidate.Height);
            labels.Add((label, line?.FirstDocumentLine.LineNumber ?? -1));
        }

        return labels;
    }

    /// <summary>
    /// The words of the labels in the head that are shown.
    /// </summary>
    public static List<string> HeadWords(TryItView view)
    {
        var words = new List<string>();
        foreach(var label in view.GetVisualDescendants().OfType<TryLabel>().Where(label => !label.InCode && label.IsEffectivelyVisible && !view.EmptyBlock.IsVisualAncestorOf(label)))
            words.Add(label.Word);

        return words;
    }

    private static string FormatCount(int count, string one, string many)
    {
        return $"{count} {(count == 1 ? one : many)} it";
    }

    private static void ChooseArtifact(RulePageViewModel page, string name)
    {
        page.OpenArtifactPicker(false);
        page.ArtifactPicker.ChooseCommand.Execute(page.ArtifactPicker.Items.OfType<PickerEntryViewModel>().First(entry => entry.Key == name));
    }

    /// <summary>
    /// What <c>commitfence rules try</c> says about the code, read from what it prints.
    /// </summary>
    private sealed record CliTry(List<(int Line, string Outcome, string Reason)> Verdicts, int Breaks, int Follows, int NotCovered, int? ParseLine, string Output);

    [GeneratedRegex(@"^try\.cs\((?<line>\d+),(?<column>\d+)\): (?<outcome>breaks it|follows it|not covered): (?<reason>.*)$")]
    private static partial Regex VerdictLine();

    [GeneratedRegex(@"^try\.cs\((?<line>\d+),(?<column>\d+)\): does not parse: ")]
    private static partial Regex ParseLine();

    [GeneratedRegex(@"^(?<breaks>\d+) breaks it, (?<follows>\d+) follows it, (?<covered>\d+) not covered\.$")]
    private static partial Regex CountLine();

    private CliTry TryByCommandLine(string folder, string ruleId, string code)
    {
        File.WriteAllText(Path.Combine(folder, "try.cs"), code);
        var output = _context.Cli(folder, "rules", "try", ruleId, "try.cs").Output;
        var verdicts = new List<(int, string, string)>();
        int breaks = 0, follows = 0, notCovered = 0;
        int? parseLine = null;
        foreach(var text in output.Split('\n').Select(line => line.TrimEnd('\r')))
        {
            var verdict = VerdictLine().Match(text);
            var count = CountLine().Match(text);
            var parse = ParseLine().Match(text);
            if(verdict.Success)
                verdicts.Add((int.Parse(verdict.Groups["line"].Value), verdict.Groups["outcome"].Value, verdict.Groups["reason"].Value));
            else if(count.Success)
                (breaks, follows, notCovered) = (int.Parse(count.Groups["breaks"].Value), int.Parse(count.Groups["follows"].Value), int.Parse(count.Groups["covered"].Value));
            else if(parse.Success)
                parseLine = int.Parse(parse.Groups["line"].Value);
        }

        return new CliTry(verdicts, breaks, follows, notCovered, parseLine, output);
    }

    private static string OutcomeText(TryOutcome outcome)
    {
        return outcome == TryOutcome.BreaksIt ? "breaks it" : outcome == TryOutcome.FollowsIt ? "follows it" : "not covered";
    }

    /// <summary>
    /// Whether the box says what the command line says: each artifact's line, outcome and reason, the three counts, and the
    /// labels of the head and of the code.
    /// </summary>
    private static bool SameAs(AppWindow app, RulePageViewModel page, CliTry cli, out string[] seen)
    {
        var tryIt = page.TryIt;
        var mine = tryIt.Verdicts.Select(verdict => (Line: verdict.Span.StartLine, Outcome: OutcomeText(verdict.Outcome), verdict.Reason)).ToList();
        var theirs = cli.Verdicts.Select(verdict => (verdict.Line, verdict.Outcome, verdict.Reason)).ToList();
        var sameVerdicts = mine.SequenceEqual(theirs);
        var sameCounts = (tryIt.Breaks, tryIt.Follows, tryIt.NotCovered) == (cli.Breaks, cli.Follows, cli.NotCovered);
        var view = ViewOf(app, page);
        var head = HeadWords(view);
        var expectedHead = new List<string> { FormatCount(cli.Breaks, "breaks", "break"), FormatCount(cli.Follows, "follows", "follow"), $"{cli.NotCovered} not covered" };
        var inCode = LabelsInCode(view).Select(entry => entry.Line).Order().ToList();
        var expectedLines = cli.Verdicts.Select(verdict => verdict.Line).Order().ToList();
        seen = new[]
        {
            "box: " + string.Join(" | ", mine.Select(entry => $"{entry.Line} {entry.Outcome} {entry.Reason}")),
            "command line: " + string.Join(" | ", theirs.Select(entry => $"{entry.Line} {entry.Outcome} {entry.Reason}")),
            $"counts: box {tryIt.Breaks}/{tryIt.Follows}/{tryIt.NotCovered}, command line {cli.Breaks}/{cli.Follows}/{cli.NotCovered}",
            "head labels: " + string.Join(", ", head),
            "labels in the code stand on lines: " + string.Join(", ", inCode)
        };

        return sameVerdicts && sameCounts && head.SequenceEqual(expectedHead) && inCode.SequenceEqual(expectedLines);
    }

    private void SampleEqualsTheCommandLine()
    {
        var folder = _context.Folders.NewFolder();
        var folderCli = _context.Folders.NewFolder();
        using var app = _context.Open(folder);
        var page = PageEditChecks.OpenPage(app, "TEAM010");
        PutCode(app, page, SampleWithoutPublic);
        var cli = TryByCommandLine(folderCli, "TEAM010", SampleWithoutPublic);
        var same = SameAs(app, page, cli, out var seen);
        _context.Report.Check("try-sample", "for each artifact the line, the outcome and the reason are the command line's, the three counts agree, and the labels stand on the first line of each artifact, in the head and in the code", same && cli.Verdicts.Count == 3, seen);
        _context.Report.Check("try-sample", "line 3 breaks it, line 8 follows it, line 11 is not covered; 1 breaks it, 1 follows it, 1 not covered", cli.Verdicts.Select(verdict => (verdict.Line, verdict.Outcome)).SequenceEqual(new[] { (3, "breaks it"), (8, "follows it"), (11, "not covered") }), cli.Output);
    }

    private void VerbatimSample()
    {
        var folder = _context.Folders.NewFolder();
        var folderCli = _context.Folders.NewFolder();
        using var app = _context.Open(folder);
        var page = PageEditChecks.OpenPage(app, "TEAM010");
        PutCode(app, page, Sample);
        var cli = TryByCommandLine(folderCli, "TEAM010", Sample);
        var same = SameAs(app, page, cli, out var seen);
        _context.Report.Check("try-sample-verbatim", "the sample with public, verbatim: the verdicts, the counts and the labels equal rules try, on lines 3, 8 and 11", same && cli.Verdicts.Select(verdict => verdict.Line).SequenceEqual(_sampleLines), seen);
    }

    private void Fragments()
    {
        var folder = _context.Folders.NewFolder();
        var folderCli = _context.Folders.NewFolder();
        using var app = _context.Open(folder);
        var page = PageEditChecks.OpenPage(app, "TEAM010");
        var cases = new (string Name, string Code, bool Parses)[]
        {
            ("a lone if statement", "if(x) { return; }", true),
            ("an if and a return", "\n  if(x)\n  {\n      return;\n  }\n  return;\n", true),
            ("members of a type with no type round them", "public int Size { get; set; }\n\npublic void Go(int a)\n{\n    if(a > 1) { return; }\n}\n", true),
            ("a whole file with a namespace and a class", "namespace Sample;\n\npublic class A\n{\n    public void Go(int a)\n    {\n        if(a > 1) { return; }\n    }\n}\n", true),
            ("text that is no C# at all", "this is not code at all {{\n", false),
            ("a method that is not closed", "void A() { if(x) { } ", false)
        };
        foreach(var (name, code, parses) in cases)
        {
            PutCode(app, page, code);
            var cli = TryByCommandLine(folderCli, "TEAM010", code);
            var seen = new[] { "command line: " + cli.Output.Trim(), "box: " + page.TryIt.Phase + " " + page.TryIt.ParseText };
            var ok = parses ? SameAs(app, page, cli, out seen) && cli.Verdicts.Count > 0 : page.TryIt.Phase == TryPhase.DoesNotParse && cli.ParseLine != null && page.TryIt.ParseText == $"Does not parse: line {cli.ParseLine}";
            _context.Report.Check("try-fragments", $"{name}: the box says what rules try says, with the lines and columns of the text itself", ok, seen);
        }
    }

    private void InThePagesWords()
    {
        var folder = _context.Folders.NewFolder();
        var folderCli = _context.Folders.NewFolder();
        using var app = _context.Open(folder);
        var page = PageEditChecks.OpenPage(app, "TEAM010");
        PutCode(app, page, Sample);
        var view = ViewOf(app, page);
        var names = page.PropertiesOffered().Select(property => property.Name).Where(name => name.Any(char.IsUpper)).ToList();
        var texts = new List<string>();
        foreach(var (label, _) in LabelsInCode(view))
        {
            texts.Add(label.Word + ": " + label.Reason);
            texts.Add(ToolTip.GetTip(label) as string ?? string.Empty);
            texts.Add(AutomationProperties.GetName(label) ?? string.Empty);
        }

        var notCovered = LabelsInCode(view).Select(entry => entry.Label).First(label => label.Kind == TryKind.NotCovered);
        var cli = TryByCommandLine(folderCli, "TEAM010", Sample);
        _context.Report.Check("try-words", "no label, tooltip or accessible name in the code holds a property named as an identifier", names.Count > 0 && texts.All(text => names.All(name => !text.Contains(name, StringComparison.Ordinal))), "texts: " + string.Join(" | ", texts));
        _context.Report.Check("try-words", "the not-covered label reads 'Not covered: statements in body is 2', its tooltip and its name 'Not covered: statements in body is 2, the rule looks only at 1'", notCovered.Reason == "statements in body is 2" && ToolTip.GetTip(notCovered) as string == "Not covered: statements in body is 2, the rule looks only at 1" && AutomationProperties.GetName(notCovered) == "Not covered: statements in body is 2, the rule looks only at 1", $"reason {notCovered.Reason}", "tip " + ToolTip.GetTip(notCovered));
        _context.Report.Check("try-words", "the command line still prints the identifier", cli.Output.Contains("statementsInBody is 2", StringComparison.Ordinal), cli.Output);
    }

    private void UnsavedEdit()
    {
        var folder = _context.Folders.NewFolder();
        var folderCli = _context.Folders.NewFolder();
        var before = CheckContext.SettingsBytes(folder);
        using var app = _context.Open(folder);
        var page = PageEditChecks.OpenPage(app, "TEAM010");
        PageEditChecks.WhereOf(page, 0).SetValues(new List<string> { "2" });
        PutCode(app, page, SampleWithoutPublic);
        _context.Cli(folderCli, "rules", "set", "TEAM010", "--where", "statementsInBody=2", "--where", "bodyFitsOnOneLine=true");
        var cli = TryByCommandLine(folderCli, "TEAM010", SampleWithoutPublic);
        var same = SameAs(app, page, cli, out var seen);
        _context.Report.Check("try-unsaved", "with statements in body set to 2 and not saved the verdicts, the counts and the labels equal rules set then rules try", same, seen);
        _context.Report.Check("try-unsaved", "the three if statements are all not covered: none has 2 statements in its body and the one that has, on line 11, does not fit on one line", cli.Verdicts.Select(verdict => (verdict.Line, verdict.Outcome)).SequenceEqual(new[] { (3, "not covered"), (8, "not covered"), (11, "not covered") }), cli.Output);
        _context.Report.Check("try-unsaved", "the settings file is as it was and the page still says Unsaved", before.AsSpan().SequenceEqual(CheckContext.SettingsBytes(folder)) && page.IsDirty, $"dirty {page.IsDirty}");
    }

    private void UnsavedRule()
    {
        var folder = _context.Folders.NewFolder();
        var folderCli = _context.Folders.NewFolder();
        var before = CheckContext.SettingsBytes(folder);
        using var app = _context.Open(folder);
        app.Rules.NewRuleFromHeadCommand.Execute(null);
        app.Pump(400);
        var page = app.Rules.CurrentPage ?? throw new InvalidOperationException("New rule opened no page");
        ChooseArtifact(page, "if-statement");
        page.Title = "Braces always";
        PageEditChecks.ChooseProperty(page.RequireFlow[0], "braces");
        PageEditChecks.ChooseValue(page.RequireFlow[0], "true");
        app.Pump(100);
        PutCode(app, page, SampleWithoutPublic);
        _context.Cli(folderCli, "rules", "add", "TEAM014", "--for", "if-statement", "--title", "Braces always", "--require", "braces=true");
        var cli = TryByCommandLine(folderCli, "TEAM014", SampleWithoutPublic);
        var same = SameAs(app, page, cli, out var seen);
        _context.Report.Check("try-new", "a new rule that is not saved: the verdicts, the counts and the labels equal rules add then rules try", same && cli.Verdicts.Count == 3 && (cli.Breaks, cli.Follows) == (1, 2), seen);
        _context.Report.Check("try-new", "the settings file is as it was and the page is still a draft", before.AsSpan().SequenceEqual(CheckContext.SettingsBytes(folder)) && page.IsDraft, $"draft {page.IsDraft}");
    }

    private void EmptyBox()
    {
        var folder = _context.Folders.NewFolder();
        using var app = _context.Open(folder);
        var page = PageEditChecks.OpenPage(app, "TEAM010");
        var view = ViewOf(app, page);
        _context.Report.Check("try-empty", "the box is empty: no label in the head, no message, no hint in line 1, no Clear, and the block that says what to put in it: 'Paste an if statement, or the method around it.', the legend line, the three labels and 'Load an example'", HeadWords(view).Count == 0 && !page.TryIt.ShowsMessage && !view.PlaceholderText.IsEffectivelyVisible && !view.ClearButton.IsEffectivelyVisible && view.EmptyBlock.IsEffectivelyVisible && page.TryIt.BlockSentence == "Paste an if statement, or the method around it." && page.TryIt.BlockLegendLine == "Each if statement gets one of these labels." && view.ExampleButton.IsEffectivelyVisible && view.EmptyBlock.GetVisualDescendants().OfType<TryLabel>().Count() == 3, "block shown: " + view.EmptyBlock.IsEffectivelyVisible, "head: " + string.Join(", ", HeadWords(view)));
        var other = PageEditChecks.OpenPage(app, "TEAM011");
        var otherView = ViewOf(app, other);
        _context.Report.Check("try-empty", "for TEAM011 (method) the block says 'Paste a method, or the class around it.'", other.TryIt.BlockSentence == "Paste a method, or the class around it." && other.TryIt.BlockLegendLine == "Each method gets one of these labels.", "sentence: " + other.TryIt.BlockSentence);
        PutCode(app, other, "x");
        _context.Report.Check("try-empty", "with a character in it the block is gone and Clear is drawn", !otherView.EmptyBlock.IsEffectivelyVisible && otherView.ClearButton.IsEffectivelyVisible, $"block shown {otherView.EmptyBlock.IsEffectivelyVisible}, clear shown {otherView.ClearButton.IsEffectivelyVisible}");
    }

    private void DoesNotParse()
    {
        var folder = _context.Folders.NewFolder();
        var folderCli = _context.Folders.NewFolder();
        const string broken = "void A() { if(x) { } ";
        using var app = _context.Open(folder);
        var page = PageEditChecks.OpenPage(app, "TEAM010");
        PutCode(app, page, broken);
        var cli = TryByCommandLine(folderCli, "TEAM010", broken);
        var view = ViewOf(app, page);
        var label = view.GetVisualDescendants().OfType<TryLabel>().First(candidate => candidate.Kind == TryKind.Warn);
        _context.Report.Check("try-parse", "the head says 'Does not parse: line N' with the line the command line prints, and no label stands in the code", cli.ParseLine != null && page.TryIt.ParseText == $"Does not parse: line {cli.ParseLine}" && label.IsEffectivelyVisible && label.Word == page.TryIt.ParseText && LabelsInCode(view).Count == 0 && HeadWords(view).Count == 1, $"box says {page.TryIt.ParseText}", "command line: " + cli.Output.Trim());
    }

    private void NoArtifact()
    {
        var folder = _context.Folders.NewFolder();
        var folderCli = _context.Folders.NewFolder();
        const string none = "int x = 1;";
        using var app = _context.Open(folder);
        var page = PageEditChecks.OpenPage(app, "TEAM010");
        PutCode(app, page, none);
        var cli = TryByCommandLine(folderCli, "TEAM010", none);
        var view = ViewOf(app, page);
        _context.Report.Check("try-none", "the head says 'No if statement in this code.' and no label stands in the code", cli.Output.Contains("No artifact", StringComparison.Ordinal) && page.TryIt.Message == "No if statement in this code." && view.MessageText.IsEffectivelyVisible && HeadWords(view).Count == 0 && LabelsInCode(view).Count == 0, "message: " + page.TryIt.Message, "command line: " + cli.Output.Trim());
    }

    private void Unfinished()
    {
        var folder = _context.Folders.NewFolder();
        using var app = _context.Open(folder);
        app.Rules.NewRuleFromHeadCommand.Execute(null);
        app.Pump(400);
        var draft = app.Rules.CurrentPage ?? throw new InvalidOperationException("New rule opened no page");
        PutCode(app, draft, SampleWithoutPublic);
        var draftView = ViewOf(app, draft);
        _context.Report.Check("try-unfinished", "a new rule that is not complete: the head says 'Finish the rule to try it.' and the code shows no label", draft.TryIt.Message == "Finish the rule to try it." && draftView.MessageText.IsEffectivelyVisible && LabelsInCode(draftView).Count == 0, "message: " + draft.TryIt.Message);

        var page = PageEditChecks.OpenPage(app, "TEAM010");
        PutCode(app, page, SampleWithoutPublic);
        var view = ViewOf(app, page);
        var withLabels = LabelsInCode(view).Count == 3;
        page.RequireFlow[0].SetValues(new List<string>());
        app.PumpUntil(() => !page.TryIt.IsBusy, 3000);
        app.Pump(200);
        _context.Report.Check("try-unfinished", "a saved rule whose require value is emptied: the same message and no label", withLabels && page.TryIt.Message == "Finish the rule to try it." && LabelsInCode(view).Count == 0, "message: " + page.TryIt.Message, "labels before: " + withLabels);
        page.RequireFlow[0].SetValues(new List<string> { "false" });
        app.PumpUntil(() => !page.TryIt.IsBusy, 3000);
        app.Pump(200);
        _context.Report.Check("try-unfinished", "when the sentence is complete again the labels come back", LabelsInCode(view).Count == 3 && page.TryIt.Phase == TryPhase.Tried, $"phase {page.TryIt.Phase}");
    }

    private void NeedsBuild()
    {
        var folder = _context.Folders.NewFolder();
        var folderCli = _context.Folders.NewFolder();
        using var app = _context.Open(folder);
        foreach(var id in _needsBuildRules)
        {
            var page = PageEditChecks.OpenPage(app, id);
            PutCode(app, page, SampleWithoutPublic);
            var cli = TryByCommandLine(folderCli, id, SampleWithoutPublic);
            var view = ViewOf(app, page);
            var spoken = cli.Output.Contains("cannot be tried without a build", StringComparison.Ordinal) && cli.Output.Contains(_needsBuildSentence[1..^1], StringComparison.Ordinal);
            _context.Report.Check("try-needs-build", $"{id} needs the build: the head says the body's sentence in the place of 'Finish the rule to try it.', as the command line says", spoken && page.TryIt.Phase == TryPhase.NeedsBuild && page.TryIt.Message == _needsBuildSentence && view.MessageText.IsEffectivelyVisible && LabelsInCode(view).Count == 0, "message: " + page.TryIt.Message, "command line: " + cli.Output.Trim());
        }
    }

    private void Refused()
    {
        var folder = _context.Folders.NewFolder();
        using var app = _context.Open(folder);
        var page = PageEditChecks.OpenPage(app, "TEAM011");
        var before = CheckContext.SettingsBytes(folder);
        ChooseArtifact(page, "class");
        PutCode(app, page, SampleWithoutPublic);
        var view = ViewOf(app, page);
        var tried = page.TryIt.Message;
        page.SaveCommand.Execute(null);
        app.Pump(200);
        _context.Report.Check("try-refused", "the edit Save would refuse: the head says Save's message, in the place of the other messages", page.TryIt.Phase == TryPhase.Refused && page.ErrorText != null && tried == page.ErrorText && view.MessageText.IsEffectivelyVisible, "head says: " + tried, "Save says: " + page.ErrorText);
        _context.Report.Check("try-refused", "nothing was saved by trying", before.AsSpan().SequenceEqual(CheckContext.SettingsBytes(folder)), "the settings file changed");
    }

    private void Kept()
    {
        var folder = _context.Folders.NewFolder();
        var before = CheckContext.SettingsBytes(folder);
        var filesBefore = Directory.GetFileSystemEntries(folder).Order().ToList();
        using var app = _context.Open(folder);
        var page = PageEditChecks.OpenPage(app, "TEAM010");
        PutCode(app, page, SampleWithoutPublic);
        var other = PageEditChecks.OpenPage(app, "TEAM013");
        var otherView = ViewOf(app, other);
        var again = PageEditChecks.OpenPage(app, "TEAM010");
        var view = ViewOf(app, again);
        _context.Report.Check("try-kept", "the code is still in the box of TEAM010 after another rule was chosen, with its labels, and the other rule's box is empty", ReferenceEquals(page, again) && view.Editor.Document.Text == SampleWithoutPublic && LabelsInCode(view).Count == 3 && otherView.Editor.Document.Text.Length == 0, $"box holds {view.Editor.Document.Text.Length} characters, the other {otherView.Editor.Document.Text.Length}");
        var filesAfter = Directory.GetFileSystemEntries(folder).Order().ToList();
        _context.Report.Check("try-kept", "nothing of the code was written: the folder holds the same files and the settings file is the same bytes", filesBefore.SequenceEqual(filesAfter) && before.AsSpan().SequenceEqual(CheckContext.SettingsBytes(folder)), string.Join(", ", filesAfter));
    }

    private void Keys()
    {
        var folder = _context.Folders.NewFolder();
        using var app = _context.Open(folder);
        var page = PageEditChecks.OpenPage(app, "TEAM010");
        var view = ViewOf(app, page);
        view.FocusCode();
        app.Pump(200);
        app.Press(PhysicalKey.Tab);
        var tab = view.Editor.Document.Text;
        _context.Report.Check("try-keys", "Tab in the box puts four spaces in the code", tab == "    ", $"the box holds {tab.Length} characters");
        app.Press(PhysicalKey.Escape);
        app.Press(PhysicalKey.Tab);
        var focused = app.Window.FocusManager?.GetFocusedElement() as Visual;
        var inside = focused != null && (ReferenceEquals(focused, view.Editor) || focused.GetVisualAncestors().Contains(view.Editor));
        _context.Report.Check("try-keys", "Esc, then Tab leaves the box and puts nothing in it", !inside && view.Editor.Document.Text == tab, $"focus is inside {inside}, the box holds {view.Editor.Document.Text.Length} characters");
    }

    private void PasteAndClear()
    {
        var folder = _context.Folders.NewFolder();
        using var app = _context.Open(folder);
        var page = PageEditChecks.OpenPage(app, "TEAM010");
        var view = ViewOf(app, page);
        var clipboard = app.Window.Clipboard ?? throw new InvalidOperationException("the window has no clipboard");
        clipboard.SetTextAsync(SampleWithoutPublic).GetAwaiter().GetResult();
        app.Click(view.PasteButton);
        app.PumpUntil(() => view.Editor.Document.Text.Length > 0 && !page.TryIt.IsBusy, 5000);
        app.Pump(200);
        _context.Report.Check("try-paste-clear", "Paste puts the clipboard's text in place of the code, and the box answers", view.Editor.Document.Text == SampleWithoutPublic && page.TryIt.Phase == TryPhase.Tried, $"the box holds {view.Editor.Document.Text.Length} characters, phase {page.TryIt.Phase}");
        app.Click(view.ClearButton);
        app.Pump(300);
        _context.Report.Check("try-paste-clear", "Clear empties the box: the block is back, Clear is gone and there is no label", view.Editor.Document.Text.Length == 0 && view.EmptyBlock.IsEffectivelyVisible && !view.ClearButton.IsEffectivelyVisible && HeadWords(view).Count == 0, $"the box holds {view.Editor.Document.Text.Length} characters");
    }

    private void Names()
    {
        var folder = _context.Folders.NewFolder();
        using var app = _context.Open(folder);
        var page = PageEditChecks.OpenPage(app, "TEAM010");
        PutCode(app, page, SampleWithoutPublic);
        var view = ViewOf(app, page);
        var counts = view.GetVisualDescendants().OfType<StackPanel>().Select(AutomationProperties.GetName).FirstOrDefault(name => name == "1 breaks it, 1 follows it, 1 not covered");
        _context.Report.Check("try-names", "the box is named 'Code to try the rule on', the buttons Paste and Clear, and the head's counts '1 breaks it, 1 follows it, 1 not covered'", AutomationProperties.GetName(view.Editor) == "Code to try the rule on" && AutomationProperties.GetName(view.PasteButton) == "Paste" && AutomationProperties.GetName(view.ClearButton) == "Clear" && counts != null, "box: " + AutomationProperties.GetName(view.Editor), "counts: " + counts);
    }

    private void OlderAnswerIsDropped()
    {
        var folder = _context.Folders.NewFolder();
        var folderCli = _context.Folders.NewFolder();
        using var app = _context.Open(folder);
        var page = PageEditChecks.OpenPage(app, "TEAM010");
        var view = ViewOf(app, page);
        var shown = 0;
        page.TryIt.Shown += () => shown++;
        var last = string.Empty;
        for(var index = 0; index < 20; index++)
        {
            last = SampleWithoutPublic + "\n// " + index;
            view.Editor.Document.Text = last;
        }

        app.PumpUntil(() => !page.TryIt.IsBusy, 5000);
        app.Pump(200);
        var cli = TryByCommandLine(folderCli, "TEAM010", last);
        var same = SameAs(app, page, cli, out var seen);
        _context.Report.Check("try-dropped", "the text changed 20 times in a row: one answer is shown, the one for the last text", shown == 1 && same, new[] { $"answers shown: {shown}" }.Concat(seen).ToArray());
    }
}
