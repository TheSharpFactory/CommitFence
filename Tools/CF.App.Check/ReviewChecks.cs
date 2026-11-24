using System.Text.RegularExpressions;
using Avalonia.Controls;

namespace CF.App.Check;

/// <summary>
/// The checks of the Review screen for the changes a push would send: what the window says about a push against what
/// <c>commitfence review --push</c> prints for the same repository, in every state of the verdict, the running review, the way
/// to a rule, the whole-screen states, the keys and the pictures. The repositories are made in the scratch folder, each with a
/// remote of its own.
/// </summary>
internal sealed partial class ReviewChecks
{
    private const int _waitSeconds = 300;

    private static readonly string[] _collapsedFiles = { "(collapsed)" };

    private readonly CheckContext _context;
    private readonly string _parent;

    public ReviewChecks(CheckContext context)
    {
        _context = context;
        _parent = Path.Combine(context.Folders.Root, "review");
    }

    /// <summary>
    /// One finding as the command line prints it, or as the window shows it.
    /// </summary>
    private sealed record Found(string File, int Line, string RuleId, bool Blocks, string Message);

    /// <summary>
    /// What <c>review --push</c> printed.
    /// </summary>
    private sealed record Printed(int ExitCode, List<Found> Findings, string Summary, string Output, string Error);

    [GeneratedRegex(@"^(?<file>.+?)\((?<line>\d+),(?<column>\d+)\): (?<severity>error|warning) (?<id>\S+): (?<message>.*)$")]
    private static partial Regex FindingLine();

    [GeneratedRegex(@"\s\[[^\]]*\]$")]
    private static partial Regex ProjectSuffix();

    [GeneratedRegex(@"(?<reviewed>\d+) of (?<files>\d+) files reviewed")]
    private static partial Regex FilesReviewed();

    [GeneratedRegex(@"^\d+(\.\d)? s, \d\d:\d\d$")]
    private static partial Regex DurationAndTime();

    public void Run()
    {
        Case("review-push", "review --push prints what review --hook pre-push prints for the line git would feed it", PushEqualsHook);
        Case("review-push-reasons", "no remote and a detached HEAD each give a plain reason and exit 3", PushReasons);
        Case("review-refused", "the refused push, every check on its window", Refused);
        Case("review-warned", "the push with a warning only", Warned);
        Case("review-clean", "the clean push", Clean);
        Case("review-incomplete", "the review that cannot finish", Incomplete);
        Case("review-running", "the window is usable while a review runs", Running);
        Case("review-settings", "a settings file that cannot be read", SettingsBroken);
        Case("review-no-push", "the screen with no push to review", NoPush);
        Case("review-folder", "the review of a folder, the count by rule and a rule chosen", FolderReview);
    }

    /// <summary>
    /// Runs one case. The environment variable CF_REVIEW_CASES, a list of case names, narrows a run to them while the cases are being
    /// worked on; a run for the record leaves it unset.
    /// </summary>
    private void Case(string name, string text, Action body)
    {
        var only = Environment.GetEnvironmentVariable("CF_REVIEW_CASES");
        if(string.IsNullOrEmpty(only) || only.Split(',').Contains(name))
            _context.Guarded(name, text, body);
    }

    private Printed Cli(string folder)
    {
        var result = _context.Cli(folder, "review", "--push");
        var findings = new List<Found>();
        foreach(var line in result.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var match = FindingLine().Match(line);
            if(match.Success)
                findings.Add(new Found(match.Groups["file"].Value, int.Parse(match.Groups["line"].Value, System.Globalization.CultureInfo.InvariantCulture), match.Groups["id"].Value, match.Groups["severity"].Value == "error", ProjectSuffix().Replace(match.Groups["message"].Value, string.Empty)));
        }

        var lines = result.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return new Printed(result.ExitCode, findings, lines.Length == 0 ? string.Empty : lines[^1], result.Output, result.Error);
    }

    private string Make(string name, PushKind kind)
    {
        return PushRepositories.Make(_parent, name, kind, _context.Options.SampleSettingsPath);
    }

    private static Button RunButton(AppWindow app)
    {
        // before a review the block in the sheet holds the run button and the bar does not draw its own
        return app.Find<Button>(button => (button.Name == "RunButton" || button.Name == "BlockRunButton") && button.IsEffectivelyVisible).FirstOrDefault() ?? app.Find<Button>(button => button.Name == "RunButton").First();
    }

    /// <summary>
    /// Shows Review and presses the run button with the mouse, then lets the window work until the review is shown.
    /// </summary>
    private static bool PressRunAndWait(AppWindow app)
    {
        app.Model.ShowReview();
        app.Pump(300);
        app.Click(RunButton(app));
        return app.PumpUntil(() => app.Model.Review.HasResult && !app.Model.Review.IsBusy, _waitSeconds * 1000);
    }

    private static List<Found> ShownFindings(AppWindow app)
    {
        return app.Model.Review.FileGroups.SelectMany(group => group.Findings.Select(finding => new Found(group.Path, finding.Line ?? 0, finding.RuleId, finding.IsBlock, finding.Message))).ToList();
    }

    private static string Describe(IEnumerable<Found> findings)
    {
        return string.Join(" | ", findings.OrderBy(found => found.File, StringComparer.Ordinal).ThenBy(found => found.Line).ThenBy(found => found.RuleId, StringComparer.Ordinal).Select(found => $"{found.File}:{found.Line} {found.RuleId} {(found.Blocks ? "Blocks" : "Warns")} {found.Message}"));
    }

    private static List<string> Texts(AppWindow app)
    {
        return app.Find<TextBlock>(block => block.IsEffectivelyVisible).Select(block => block.Text ?? string.Join(string.Empty, block.Inlines?.OfType<Avalonia.Controls.Documents.Run>().Select(run => run.Text) ?? Array.Empty<string>())).ToList();
    }

    private static string Pieces(AppWindow app)
    {
        return string.Join(" | ", app.Model.Review.Verdict.Counts.Select(piece => $"{piece.Kind}:{piece.Text}"));
    }

    private static Border RailDot(AppWindow app)
    {
        return app.Find<Border>(border => border.CornerRadius.TopLeft == 10 && border.Height == 20 && border.MinWidth == 20).First();
    }

    private static bool HasRunStyle(AppWindow app, string style)
    {
        return RunButton(app).Classes.Contains(style);
    }

    /// <summary>
    /// What the window and the command line must agree on for one push: the findings (file, line, rule, effect, message), the files
    /// with findings in order with their tallies, the panel and the exit code.
    /// </summary>
    private void SameAsCommandLine(string caseName, AppWindow app, Printed cli, int expectedExit)
    {
        var review = app.Model.Review;
        var shown = ShownFindings(app);
        _context.Report.Check(caseName, $"the command line exits {expectedExit}", cli.ExitCode == expectedExit, $"exit {cli.ExitCode}", cli.Output);
        _context.Report.Check(caseName, "every finding of the command line is in the window under its file, with its rule, line, message and effect, and none more", Describe(cli.Findings) == Describe(shown), "command line: " + Describe(cli.Findings), "window:       " + Describe(shown));
        var expectedFiles = cli.Findings.GroupBy(found => found.File, StringComparer.Ordinal).OrderByDescending(group => group.Count()).ThenBy(group => group.Key, StringComparer.Ordinal).ToList();
        var expectedOrder = string.Join(", ", expectedFiles.Select(group => $"{group.Key} {group.Count(found => found.Blocks)}/{group.Count(found => !found.Blocks)}"));
        var shownOrder = string.Join(", ", review.FileGroups.Select(group => $"{group.Path} {group.BlockCount}/{group.WarnCount}"));
        _context.Report.Check(caseName, "the files with findings are the command line's, most findings first, each with its blocking and warning tally", expectedOrder == shownOrder, "command line: " + expectedOrder, "window:       " + shownOrder);
        var files = FilesReviewed().Match(cli.Summary);
        var fileCount = files.Success ? int.Parse(files.Groups["files"].Value, System.Globalization.CultureInfo.InvariantCulture) : -1;
        var panelPaths = review.PanelFiles.Select(file => file.Path).Concat(review.HasCleanSummary ? _collapsedFiles : Array.Empty<string>()).Count();
        if(review.HasCleanPush)
        {
            _context.Report.Check(caseName, "a clean push has no panel: the main column lists as many files as the command line's summary says were reviewed, by path, under the line that counts them", !review.HasPanel && review.CleanPushFiles.Count == fileCount && review.CleanPushLine == $"{fileCount} files reviewed, none with findings" && review.CleanPushFiles.SequenceEqual(review.CleanPushFiles.Order(StringComparer.Ordinal)), $"summary: {cli.Summary}", $"list: {review.CleanPushFiles.Count} files, line '{review.CleanPushLine}'");
            return;
        }

        _context.Report.Check(caseName, "the panel lists as many files as the command line's summary says were reviewed, and its head counts them and the files with findings", panelPaths == fileCount && review.PanelHead == $"{fileCount} in this push, {expectedFiles.Count} with findings", $"summary: {cli.Summary}", $"panel: {panelPaths} files, head '{review.PanelHead}'");
    }

    private void FileTally(string caseName, AppWindow app)
    {
        var withFindings = app.Model.Review.PanelFiles.Where(file => file.HasFindings).Select(file => $"{file.Path} {file.BlockText}/{file.WarnText}").ToList();
        var groups = app.Model.Review.FileGroups.Select(group => $"{group.Path} {group.BlockText}/{group.WarnText}").ToList();
        _context.Report.Check(caseName, "the panel's files with findings are the files of the list, in the same order and with the same tally", withFindings.SequenceEqual(groups), string.Join(", ", withFindings), string.Join(", ", groups));
    }
}
