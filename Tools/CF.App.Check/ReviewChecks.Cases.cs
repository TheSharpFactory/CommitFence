using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Styling;
using CF.App.Parts;
using CF.App.Review;
using CF.App.Rules;
using CF.Composition;
using CF.Core.Changes;
using CF.Core.Review;
using CF.Core.Services;

namespace CF.App.Check;

internal sealed partial class ReviewChecks
{
    private void PushEqualsHook()
    {
        var folder = Make("hook", PushKind.Refused);
        var push = _context.Cli(folder, "review", "--push");
        var line = $"refs/heads/main {PushRepositories.Head(folder)} refs/heads/main {PushRepositories.RemoteTip(folder)}\n";
        var hook = _context.CommandLine.RunWithInput(folder, line, "review", "--hook", "pre-push", "origin", PushRepositories.RemoteUrl(folder));
        var pushText = string.Join("\n", push.Output.Split('\n').Skip(1));
        var hookText = string.Join("\n", hook.Output.Split('\n').Skip(1));
        _context.Report.Check("review-push", "review --push prints the same text as review --hook pre-push does for the line git would feed it, after the first line, and exits the same", pushText == hookText && push.ExitCode == hook.ExitCode && push.ExitCode == 1, $"--push (exit {push.ExitCode}): {pushText}", $"--hook (exit {hook.ExitCode}): {hookText}");
    }

    private void PushReasons()
    {
        var none = PushRepositories.MakeWithoutRemote(_parent, "no-remote", _context.Options.SampleSettingsPath);
        var noRemote = _context.Cli(none, "review", "--push");
        _context.Report.Check("review-push-reasons", "no remote: 'No push to review: ' and a plain reason, exit 3", noRemote.ExitCode == 3 && noRemote.Output.Contains("No push to review: This repository has no remote", StringComparison.Ordinal), $"exit {noRemote.ExitCode}", noRemote.Output);
        var detached = Make("detached", PushKind.Clean);
        PushRepositories.DetachHead(detached);
        var result = _context.Cli(detached, "review", "--push");
        _context.Report.Check("review-push-reasons", "a detached HEAD: 'No push to review: ' and a plain reason, exit 3", result.ExitCode == 3 && result.Output.Contains("No push to review: No branch is checked out", StringComparison.Ordinal), $"exit {result.ExitCode}", result.Output);
        var fresh = Make("new-branch", PushKind.Clean);
        PushRepositories.NewBranch(fresh, "feature");
        var branch = _context.Cli(fresh, "review", "--push");
        _context.Report.Check("review-push-reasons", "a branch never pushed is a new branch on the remote: it is reviewed as refs/heads/feature, with the five files of the commit not on the remote", branch.ExitCode == 0 && branch.Output.Contains("refs/heads/feature: 5 changed files", StringComparison.Ordinal), $"exit {branch.ExitCode}", branch.Output);
    }

    private void Refused()
    {
        var folder = Make("refused", PushKind.Refused);
        using var app = _context.Open(folder);
        app.Model.ShowReview();
        app.Pump(1000);
        BeforeTheRun(app);
        Pictures(app, "before");
        var done = PressRunAndWait(app);
        _context.Report.Check("review-refused", "the review is shown after the run button is pressed", done, "the window did not show a result in time");
        var cli = Cli(folder);
        SameAsCommandLine("review-refused", app, cli, 1);
        RefusedVerdict(app);
        FileTally("review-refused", app);
        Pictures(app, "refused");
        KeyboardPictures(app);
        Application.Current!.RequestedThemeVariant = ThemeVariant.Dark;
        app.Pump(500);
        Pictures(app, "refused-dark");
        Application.Current.RequestedThemeVariant = ThemeVariant.Light;
        app.Pump(500);
        _context.Guarded("review-tab", "the tab order and the keys of the panel", () => TabOrder(app));
        _context.Guarded("review-code-source", "the lines shown are the pushed commit's", () => CodeSource(app, folder));
        _context.Guarded("review-finding-body", "the open finding", () => FindingBody(app, folder));
        _context.Guarded("review-open-rule", "Open the rule", () => OpenTheRule(app));
        _context.Guarded("review-fold", "fold and open, and the panel's files", () => Folding(app));
        _context.Guarded("review-one-pane", "one pane", () => OnePane(app));
        _context.Guarded("review-repairs", "the four repairs of R1's pictures", () => Repairs(app));
    }

    private void BeforeTheRun(AppWindow app)
    {
        var review = app.Model.Review;
        var texts = Texts(app);
        var items = app.Find<ListBoxItem>(item => item.DataContext is ReviewChoiceViewModel).ToList();
        _context.Report.Check("review-before", "'No review yet' and 'Run one to see what a push would meet.' are drawn, with no badge, no panel, no files and no count on the rail", texts.Contains("No review yet") && texts.Contains("Run one to see what a push would meet.") && !review.Verdict.HasBadge && !review.HasPanel && !review.HasFiles && !RailDot(app).IsEffectivelyVisible, "texts: " + string.Join(" | ", texts.Take(20)));
        _context.Report.Check("review-before", "the run button says 'Review now' and is a main button", RunButton(app).IsEffectivelyVisible && review.RunText == "Review now" && HasRunStyle(app, "main") && !HasRunStyle(app, "outlined"), review.RunText);
        _context.Report.Check("review-before", "the choice has 'The changes' chosen and 'A folder' drawn and enabled", items.Count == 2 && items[0].IsSelected && items[0].IsEnabled && items[1].IsEnabled && !items[1].IsSelected, string.Join(", ", items.Select(item => $"{((ReviewChoiceViewModel)item.DataContext!).Label} enabled {item.IsEnabled} selected {item.IsSelected}")));
    }

    private void RefusedVerdict(AppWindow app)
    {
        var review = app.Model.Review;
        var verdict = review.Verdict;
        var texts = Texts(app);
        var blockPiece = app.Find<Border>(border => border.Classes.Contains("piece") && border.Classes.Contains("block") && border.IsEffectivelyVisible).ToList();
        var warnPiece = app.Find<Border>(border => border.Classes.Contains("piece") && border.Classes.Contains("warn") && border.IsEffectivelyVisible).ToList();
        _context.Report.Check("review-refused", "the verdict is 'Push refused' with the filled pieces '2 findings block it' and '1 only warns', drawn", verdict.Kind == VerdictKind.Refused && verdict.Heading == "Push refused" && Pieces(app) == "Block:2 findings block it | Warn:1 only warns" && blockPiece.Count == 1 && warnPiece.Count == 1 && texts.Contains("Push refused") && texts.Contains("2 findings block it") && texts.Contains("1 only warns"), Pieces(app));
        var facts = verdict.Facts.Select(fact => fact.Text).ToList();
        _context.Report.Check("review-refused", "the facts are '5 files' and the duration and the time of day; no number of commits, lines or kind of review is made up", facts.Count == 2 && facts[0] == "5 files" && DurationAndTime().IsMatch(facts[1]), string.Join(" | ", facts));
        var panel = review.PanelFiles;
        _context.Report.Check("review-refused", "the panel lists the five files, the three without findings say 'No findings'", panel.Count == 5 && panel.Count(file => file.IsClean) == 3 && texts.Count(text => text == "No findings") == 3, string.Join(", ", panel.Select(file => file.Path + (file.IsClean ? " clean" : " findings"))));
        _context.Report.Check("review-refused", "the run button says 'Review again' and is a second button; the rail's count is 2 with its tooltip", review.RunText == "Review again" && HasRunStyle(app, "outlined") && !HasRunStyle(app, "main") && review.BlockingCount == 2 && RailDot(app).IsEffectivelyVisible && review.BlockingText == "2 findings block the last push", $"{review.RunText}, count {review.BlockingCount}");
        var starts = review.FileGroups.All(group => group.IsOpen) && review.FileGroups.SelectMany(group => group.Findings).Where(finding => finding.IsBlock).All(finding => finding.IsOpen) && review.FileGroups.SelectMany(group => group.Findings).Where(finding => !finding.IsBlock).All(finding => !finding.IsOpen);
        _context.Report.Check("review-refused", "at most 20 findings: every file starts open, and the blocking findings; the warning one starts folded", starts && review.OpenAllText == "Fold all", review.OpenAllText);
    }

    private void Warned()
    {
        var folder = Make("warned", PushKind.Warned);
        using var app = _context.Open(folder);
        var done = PressRunAndWait(app);
        var cli = Cli(folder);
        SameAsCommandLine("review-warned", app, cli, 0);
        var review = app.Model.Review;
        _context.Report.Check("review-warned", "the verdict is 'Push allowed' with the green piece 'Nothing blocks it' and the filled piece '1 only warns'; no count on the rail", done && review.Verdict.Kind == VerdictKind.AllowedWithWarnings && review.Verdict.Heading == "Push allowed" && Pieces(app) == "Allow:Nothing blocks it | Warn:1 only warns" && review.BlockingCount == 0 && !RailDot(app).IsEffectivelyVisible, Pieces(app));
        Pictures(app, "warned");
    }

    private void Clean()
    {
        var folder = Make("clean", PushKind.Clean);
        using var app = _context.Open(folder);
        var done = PressRunAndWait(app);
        var cli = Cli(folder);
        SameAsCommandLine("review-clean", app, cli, 0);
        var review = app.Model.Review;
        var texts = Texts(app);
        _context.Report.Check("review-clean", "the verdict is 'Push allowed' with the green piece 'No findings'; no file with findings; no panel; the main column lists the five files by path, each with its tick, under the line '5 files reviewed, none with findings'; no count on the rail", done && review.Verdict.Kind == VerdictKind.AllowedClean && Pieces(app) == "Allow:No findings" && !review.HasFiles && !review.HasPanel && review.CleanPushFiles.Count == 5 && texts.Contains("5 files reviewed, none with findings") && texts.Count(text => text == "No findings") == 1 && !RailDot(app).IsEffectivelyVisible, Pieces(app), string.Join(" | ", texts.Take(30)));
        Pictures(app, "clean");
    }

    private void Incomplete()
    {
        var folder = Make("incomplete", PushKind.Incomplete);
        using var app = _context.Open(folder);
        var done = PressRunAndWait(app);
        var cli = Cli(folder);
        var review = app.Model.Review;
        var cliReason = cli.Output.Split('\n').FirstOrDefault(line => line.Contains("not reviewed: the build cannot evaluate the project", StringComparison.Ordinal)) ?? string.Empty;
        var piece = review.Verdict.Counts.Count > 0 ? review.Verdict.Counts[0].Text : string.Empty;
        _context.Report.Check("review-incomplete", "the command line exits 2 and says the build cannot evaluate the project", cli.ExitCode == 2 && cliReason.Length > 0, $"exit {cli.ExitCode}", cli.Output);
        var rows = review.PanelFiles;
        _context.Report.Check("review-incomplete", "no file of the panel says 'No findings': each of the five says it was not reviewed, with a reason", rows.Count == 5 && rows.All(file => file.IsNotReviewed && file.NotReviewedReason.Length > 0) && !Texts(app).Contains("No findings"), string.Join(", ", rows.Select(file => file.Path + (file.IsNotReviewed ? " not reviewed" : " clean"))));
        _context.Report.Check("review-incomplete", "the verdict is 'Review incomplete' with one plain piece of two sentences, the reasons listed under it (the build first, the first one open), and 'Review again' as a main button; no filled piece; no count on the rail", done && review.Verdict.Kind == VerdictKind.Incomplete && review.Verdict.Heading == "Review incomplete" && review.Verdict.Counts.Count == 1 && review.Verdict.Counts[0].IsPlain && piece == "The review could not finish, so the push was not allowed. Fix what is listed below, then review again." && review.Reasons.Count > 0 && review.Reasons[0].Failed == "The build cannot evaluate the project" && review.Reasons[0].IsOpen && review.ReasonsLine.EndsWith(", the build first", StringComparison.Ordinal) && review.RunText == "Review again" && HasRunStyle(app, "main") && review.BlockingCount == 0 && !RailDot(app).IsEffectivelyVisible, piece);
        Pictures(app, "incomplete");
    }

    /// <summary>
    /// The review service is a stand-in that waits for a signal before it answers, so that the window can be looked at while a
    /// review is still running.
    /// </summary>
    private sealed class WaitingReviewService : IReviewService
    {
        private readonly IReviewService _inner;
        private int _calls;

        public WaitingReviewService(IReviewService inner)
        {
            _inner = inner;
        }

        public ManualResetEventSlim Signal { get; } = new ManualResetEventSlim(false);

        public int Calls
        {
            get { return Volatile.Read(ref _calls); }
        }

        public ReviewResult ReviewFiles(string settingsPath, IReadOnlyList<FileToReview> files)
        {
            return _inner.ReviewFiles(settingsPath, files);
        }

        public ReviewResult ReviewChanges(string folder, ReviewScope scope, string? settingsPath)
        {
            Interlocked.Increment(ref _calls);
            Signal.Wait();
            return _inner.ReviewChanges(folder, scope, settingsPath);
        }

        public PushScopeAnswer FindPushScope(string folder)
        {
            return _inner.FindPushScope(folder);
        }
    }

    private void Running()
    {
        var folder = Make("running", PushKind.Refused);
        var real = _context.Services;
        var waiting = new WaitingReviewService(real.Review);
        var services = new CommitFenceServices(real.ProductInfo, real.Clock, real.Languages, real.Rules, waiting, real.Setup, real.Folders, real.Updater, real.UpdateChecks);
        using var app = new AppWindow(services, folder, 1440, 900);
        try
        {
            RunningCases(app, waiting);
        }
        finally
        {
            waiting.Signal.Set();
        }
    }

    private void RunningCases(AppWindow app, WaitingReviewService waiting)
    {
        app.Model.ShowReview();
        app.Pump(600);
        app.Click(RunButton(app));
        app.PumpUntil(() => waiting.Calls == 1, 20000);
        app.Pump(300);
        var review = app.Model.Review;
        var texts = Texts(app);
        var ring = app.Find<TurningRing>(candidate => candidate.IsEffectivelyVisible).Count();
        _context.Report.Check("review-running", "while it waits: 'Reviewing' with the turning ring, no count piece, no fact and no bar; the run button says 'Reviewing'", review.IsBusy && review.Verdict.IsRunning && review.Verdict.Heading == "Reviewing" && !review.Verdict.HasCounts && !review.Verdict.HasFacts && texts.Count(text => text == "Reviewing") == 2 && ring >= 2 && !app.Find<ProgressBar>(_ => true).Any() && review.RunText == "Reviewing", $"texts: {string.Join(" | ", texts.Take(20))}", $"rings {ring}");
        Pictures(app, "running");
        app.Press(PhysicalKey.Digit1, RawInputModifiers.Control);
        var team010 = app.Rules.Items.OfType<TeamRuleRowViewModel>().First(row => row.Id == "TEAM010");
        app.Rules.SelectedItem = team010;
        app.Pump(600);
        var rulesWork = app.Model.ShowsRules && app.Rules.CurrentPage?.Id == "TEAM010";
        app.Press(PhysicalKey.Digit2, RawInputModifiers.Control);
        app.Pump(300);
        _context.Report.Check("review-running", "while it waits: Ctrl+1 shows the rules, TEAM010 can be chosen and its page opens, Ctrl+2 shows Review with 'Reviewing' still there", rulesWork && app.Model.ShowsReview && review.Verdict.IsRunning && review.IsBusy, $"rules work {rulesWork}, review shown {app.Model.ShowsReview}");
        app.Click(RunButton(app));
        review.RunCommand.Execute(null);
        app.Pump(600);
        _context.Report.Check("review-running", "two more presses (the mouse and the command) start no second review, and the command cannot run", waiting.Calls == 1 && !review.RunCommand.CanExecute(null), $"asked {waiting.Calls} times");
        waiting.Signal.Set();
        var done = app.PumpUntil(() => review.HasResult && !review.IsBusy, _waitSeconds * 1000);
        _context.Report.Check("review-running", "when it is let go the verdict shows the result and the button says 'Review again'; the stand-in was asked once", done && review.Verdict.Kind == VerdictKind.Refused && review.RunText == "Review again" && waiting.Calls == 1, $"{review.Verdict.Heading}, {review.RunText}, asked {waiting.Calls}");
    }

    private void SettingsBroken()
    {
        var folder = PushRepositories.MakeWithBrokenSettings(_parent, "broken-settings", _context.Options.SampleSettingsPath);
        var cli = _context.Cli(folder, "review", "--push");
        using var app = _context.Open(folder);
        app.Model.ShowReview();
        app.Pump(600);
        app.Click(RunButton(app));
        app.PumpUntil(() => app.Model.Review.HasBanner && !app.Model.Review.IsBusy, _waitSeconds * 1000);
        var review = app.Model.Review;
        var texts = Texts(app);
        var open = app.Find<Button>(button => button.IsEffectivelyVisible && Avalonia.Automation.AutomationProperties.GetName(button) == "Open the file").Any();
        _context.Report.Check("review-settings", "the command line exits 3 with a sentence about commitfence.json in the tree under review", cli.ExitCode == 3 && cli.Error.Contains("commitfence.json (in the tree under review)", StringComparison.Ordinal), $"exit {cli.ExitCode}", cli.Error);
        _context.Report.Check("review-settings", "the window shows the banner with that sentence and a quiet 'Open the file' button; the verdict is back to 'No review yet'; no count on the rail", review.BannerText == cli.Error.Trim() && texts.Contains(cli.Error.Trim()) && open && review.Verdict.Kind == VerdictKind.NoReview && review.BlockingCount == 0 && !review.HasResult, "banner: " + review.BannerText, "command line: " + cli.Error.Trim());
        Pictures(app, "settings");
    }

    private void NoPush()
    {
        var none = PushRepositories.MakeWithoutRemote(_parent, "no-push", _context.Options.SampleSettingsPath);
        var cli = _context.Cli(none, "review", "--push");
        var reason = cli.Output.Split('\n').Select(line => line.Trim()).FirstOrDefault(line => line.StartsWith("No push to review: ", StringComparison.Ordinal))?.Substring("No push to review: ".Length) ?? string.Empty;
        using(var app = _context.Open(none))
        {
            app.Model.ShowReview();
            app.Pump(600);
            app.PumpUntil(() => app.Model.Review.Verdict.Kind == VerdictKind.NoPush, 20000);
            app.Pump(300);
            var review = app.Model.Review;
            _context.Report.Check("review-no-push", "a repository with no remote: 'No push to review' with the reason the command line prints; the run button says 'Review now'", review.Verdict.Kind == VerdictKind.NoPush && review.Verdict.Heading == "No push to review" && review.Verdict.Lead == reason && reason.Length > 0 && review.RunText == "Review now" && Texts(app).Contains(reason), $"window: {review.Verdict.Lead}", $"command line: {reason}");
            app.Click(RunButton(app));
            app.PumpUntil(() => !review.IsBusy, 30000);
            app.Pump(300);
            _context.Report.Check("review-no-push", "a press shows the reason again and leaves no result", review.Verdict.Kind == VerdictKind.NoPush && review.Verdict.Lead == reason && !review.HasResult && review.RunText == "Review now", review.Verdict.Heading);
            Pictures(app, "no-push");
        }

        var detached = Make("no-push-detached", PushKind.Clean);
        PushRepositories.DetachHead(detached);
        var detachedCli = _context.Cli(detached, "review", "--push");
        var detachedReason = detachedCli.Output.Split('\n').Select(line => line.Trim()).FirstOrDefault(line => line.StartsWith("No push to review: ", StringComparison.Ordinal))?.Substring("No push to review: ".Length) ?? string.Empty;
        using(var app = _context.Open(detached))
        {
            app.Model.ShowReview();
            app.Pump(600);
            app.PumpUntil(() => app.Model.Review.Verdict.Kind == VerdictKind.NoPush, 20000);
            app.Pump(300);
            _context.Report.Check("review-no-push", "a detached HEAD: the reason the command line prints", app.Model.Review.Verdict.Lead == detachedReason && detachedReason.Length > 0, $"window: {app.Model.Review.Verdict.Lead}", $"command line: {detachedReason}");
        }

        var plain = Path.Combine(_parent, "not-a-repository");
        Directory.CreateDirectory(plain);
        using(var app = _context.Open(plain))
        {
            app.Model.ShowReview();
            app.Pump(600);
            var review = app.Model.Review;
            _context.Report.Check("review-no-push", "a folder that is not a repository: 'No repository open', and the run button cannot be pressed", review.Verdict.Kind == VerdictKind.NoRepository && review.Verdict.Heading == "No repository open" && !review.RunCommand.CanExecute(null) && Texts(app).Contains("No repository open"), review.Verdict.Heading);
            Pictures(app, "no-repository");
        }
    }

    private void Pictures(AppWindow app, string state)
    {
        var folder = _context.Options.PicturesFolder;
        app.Resize(1440, 900);
        ScrollToTop(app);
        app.MovePointerAway();
        app.Pump(300);
        app.Save(folder, $"review-{state}-1440.png");
        app.Resize(760, 1000);
        app.Pump(300);
        app.Save(folder, $"review-{state}-760.png");
        app.Resize(1440, 900);
        app.Pump(300);
    }

    private void KeyboardPictures(AppWindow app)
    {
        app.Find<Button>(button => button.Classes.Contains("fileline")).First().Focus(NavigationMethod.Tab);
        app.Pump(300);
        Pictures(app, "focus-file");
        app.Find<Button>(button => button.Classes.Contains("findinghead")).First().Focus(NavigationMethod.Tab);
        app.Pump(300);
        Pictures(app, "focus-finding");
    }
}
