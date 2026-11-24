using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using CF.App.Review;
using CF.App.Tests.Support;
using CF.Cli.Commands;
using CF.Core.Changes;
using CF.Core.Code;
using CF.Core.Findings;
using CF.Core.Ports;
using CF.Core.Review;
using CF.Core.Rules.Settings;
using CF.Core.Services;
using Xunit;

namespace CF.App.Tests;

/// <summary>
/// The Review screen: what it shows for a known answer of the review, the push it asks for, the one run at a time, the folder
/// review and its filter, the way to a rule, the keys, and the narrow window. The review itself is a double that records what it
/// was asked and answers what the test says.
/// </summary>
public sealed class ReviewScreenTests
{
    private const string _unrelatedWorkingText = "working tree line";

    /// <summary>
    /// What a review was asked to look at, as the parts that carry meaning: the remote and the refs of the push.
    /// </summary>
    private sealed record ScopeFacts(ScopeKind Kind, string RemoteName, string RemoteUrl, string Refs);

    [Theory]
    [InlineData("published branch")]
    [InlineData("unpublished branch")]
    [InlineData("no remote")]
    [InlineData("detached head")]
    public async Task PushPreviewUsesHookScope(string situation)
    {
        var git = new FakeGit { HeadCommit = Samples.Commit('a') };
        git.Remotes.Add("origin");
        git.RemoteAddresses["origin"] = "https://example.com/r.git";
        var remoteObject = new string('0', 40);
        var branch = "main";
        if(situation == "published branch")
        {
            git.FollowedBranch = new BranchUpstream("origin", "refs/heads/main");
            git.TrackingCommits["refs/remotes/origin/main"] = Samples.Commit('b');
            remoteObject = Samples.Commit('b');
        }
        else if(situation == "unpublished branch")
        {
            git.Branch = "feature";
            branch = "feature";
        }
        else if(situation == "no remote")
            git.Remotes.Clear();
        else
            git.Branch = null;

        var finder = new PushScopeFinder(git);
        var review = new FakeReviewService
        {
            PushScope = finder.Find,
            Result = _ => Samples.Result(ReviewOutcome.Blocked, new[] { Samples.Finding("a.cs", 3, "TEAM001", Severity.Block) }, new[] { Samples.Reviewed("a.cs") })
        };
        var hookInput = $"refs/heads/{branch} {Samples.Commit('a')} refs/heads/{branch} {remoteObject}\n";

        var preview = Run(review, string.Empty, "review", "--push");
        using var host = new ReviewHost();
        host.Review.PushScope = finder.Find;
        host.Review.Result = review.Result;
        await host.RunAsync();

        if(situation is "no remote" or "detached head")
        {
            Assert.Equal(ExitCode.BadSettingsOrUsage, preview.ExitCode);
            Assert.Empty(review.Scopes);
            Assert.Contains(situation == "no remote" ? "no remote" : "detached", preview.Output, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(VerdictKind.NoPush, host.Model.Verdict.Kind);
            Assert.Contains(situation == "no remote" ? "no remote" : "detached", host.Model.Verdict.Lead, StringComparison.OrdinalIgnoreCase);
            Assert.Empty(host.Review.Scopes);
            return;
        }

        var hook = Run(review, hookInput, "review", "--hook", "pre-push", "origin", "https://example.com/r.git");

        Assert.Equal(2, review.Scopes.Count);
        Assert.Equal(FactsOf(review.Scopes[1]), FactsOf(review.Scopes[0]));
        Assert.Equal(FactsOf(review.Scopes[1]), FactsOf(Assert.Single(host.Review.Scopes)));
        Assert.Equal(ExitCode.Blocked, hook.ExitCode);
        Assert.Equal(hook.ExitCode, preview.ExitCode);
        Assert.Equal(VerdictKind.Refused, host.Model.Verdict.Kind);
        var pushed = Assert.Single(review.Scopes[0].PushedRefs.Refs);
        Assert.Equal($"refs/heads/{branch}", pushed.RemoteRef);
        Assert.Equal(situation == "unpublished branch", pushed.IsNewOnRemote);
    }

    [Theory]
    [InlineData("refused")]
    [InlineData("warnings only")]
    [InlineData("clean")]
    [InlineData("incomplete")]
    public async Task PushResultShowsWhatWasReviewed(string outcome)
    {
        using var host = new ReviewHost();
        var findings = new List<Finding>();
        var coverage = new List<Coverage> { Samples.Reviewed("src/a.cs"), Samples.Reviewed("src/b.cs"), Samples.Reviewed("src/c.cs") };
        var reviewOutcome = ReviewOutcome.Clean;
        if(outcome is "refused" or "warnings only")
        {
            findings.Add(Samples.Finding("src/a.cs", 9, "TEAM002", Severity.Warn));
            findings.Add(Samples.Finding("src/a.cs", 3, "TEAM001", outcome == "refused" ? Severity.Block : Severity.Warn));
            findings.Add(Samples.Finding("src/b.cs", 4, "TEAM002", Severity.Warn));
            reviewOutcome = outcome == "refused" ? ReviewOutcome.Blocked : ReviewOutcome.Warned;
        }
        else if(outcome == "incomplete")
        {
            coverage[2] = Samples.NotReviewed("src/c.cs", "the file does not parse");
            reviewOutcome = ReviewOutcome.Incomplete;
        }

        host.Review.Result = _ => Samples.Result(reviewOutcome, findings, coverage);

        await host.RunAsync();

        var model = host.Model;
        Assert.True(model.HasResult);
        var shown = model.FileGroups.SelectMany(group => group.Findings.Select(finding => $"{group.Path}:{finding.Line} {finding.RuleId} {(finding.IsBlock ? "block" : "warn")}")).ToList();
        if(outcome is "refused" or "warnings only")
        {
            Assert.Equal(outcome == "refused" ? VerdictKind.Refused : VerdictKind.AllowedWithWarnings, model.Verdict.Kind);
            Assert.Equal(outcome == "refused" ? 1 : 0, model.BlockingCount);
            Assert.Equal(new[] { $"src/a.cs:3 TEAM001 {(outcome == "refused" ? "block" : "warn")}", "src/a.cs:9 TEAM002 warn", "src/b.cs:4 TEAM002 warn" }, shown.Order(StringComparer.Ordinal));
            Assert.Equal(new[] { "src/a.cs", "src/b.cs" }, model.FileGroups.Select(group => group.Path));
            var panel = model.PanelFiles.ToDictionary(file => file.Path, StringComparer.Ordinal);
            Assert.True(panel["src/a.cs"].HasFindings);
            Assert.True(panel["src/c.cs"].IsClean);
            Assert.False(model.HasCleanPush);
        }
        else if(outcome == "clean")
        {
            Assert.Equal(VerdictKind.AllowedClean, model.Verdict.Kind);
            Assert.Empty(shown);
            Assert.Equal(new[] { "src/a.cs", "src/b.cs", "src/c.cs" }, model.CleanPushFiles);
            Assert.False(model.HasPanel);
        }
        else
        {
            // zero findings, and still not a pass: the file that could not be read is named with its reason
            Assert.Equal(VerdictKind.Incomplete, model.Verdict.Kind);
            Assert.Empty(shown);
            Assert.False(model.HasCleanPush);
            var notReviewed = Assert.Single(model.PanelFiles, file => file.IsNotReviewed);
            Assert.Equal("src/c.cs", notReviewed.Path);
            Assert.Contains("does not parse", notReviewed.NotReviewedReason, StringComparison.Ordinal);
            Assert.True(model.HasReasons);
            Assert.True(model.RunIsMain);
        }
    }

    [AvaloniaFact]
    public async Task FindingNamesItsScopedSourceAndOpensThatLine()
    {
        using var host = new ShellHost(fileRules: [RulesHost.ScopedRule()]);
        host.Review.Result = _ => Samples.Result(ReviewOutcome.Blocked,
            [Samples.Finding("tests/Unit/C.cs", 9, "IDE0055", Severity.Warn, source: FindingSource.EditorConfig)], [Samples.Reviewed("tests/Unit/C.cs")]);
        host.Model.ShowReview();
        await host.Model.Review.RunCommand.ExecuteAsync(null);
        var finding = Assert.Single(Assert.Single(host.Model.Review.FileGroups).Findings);
        Assert.Equal("Warns", finding.EffectWord);
        Assert.Equal("From .editorconfig, set for tests/**/*.cs on line 18. It only warns, so it does not stop this push.", finding.SourceLine);
        Assert.Equal("Open .editorconfig at line 18", finding.OpenLineText);
        var opened = 0;
        host.Model.Review.OpenLineRequested += (_, line) => opened = line;
        finding.OpenLineCommand.Execute(null);
        Assert.Equal(18, opened);
    }

    [AvaloniaFact]
    public async Task RunningReviewAcceptsOnlyOneRun()
    {
        using var host = new ReviewHost();
        host.Review.Gate = new ManualResetEventSlim();
        host.Review.Result = _ => Samples.Result(ReviewOutcome.Blocked, new[] { Samples.Finding("a.cs", 3, "TEAM001", Severity.Block) }, new[] { Samples.Reviewed("a.cs") });
        var model = host.Model;

        var first = model.RunCommand.ExecuteAsync(null);
        await Task.Run(() => host.Review.Entered.Wait(TestContext.Current.CancellationToken), TestContext.Current.CancellationToken);
        var second = model.RunCommand.ExecuteAsync(null);
        model.SelectedChoice = model.Choices[1];
        model.SelectedChoice = model.Choices[0];

        Assert.True(model.IsBusy);
        Assert.False(model.RunCommand.CanExecute(null));
        Assert.Single(host.Review.Scopes);

        host.Review.Gate.Set();
        await first;
        await second;

        Assert.Single(host.Review.Scopes);
        Assert.False(model.IsBusy);
        Assert.True(model.RunCommand.CanExecute(null));
        Assert.Equal(VerdictKind.Refused, model.Verdict.Kind);
        Assert.True(model.HasResult);
    }

    [Theory]
    [InlineData("no push")]
    [InlineData("no repository")]
    [InlineData("bad settings")]
    [InlineData("target outside the folder")]
    public async Task UnavailableReviewExplainsWhatHappened(string situation)
    {
        using var host = new ReviewHost(insideRepository: situation != "no repository");
        var model = host.Model;
        switch(situation)
        {
            case "no push":
                host.Review.PushScope = _ => PushScopeAnswer.None("This repository has no remote, so the branch main has nowhere to be pushed.", "main");
                await host.RunAsync();
                Assert.Equal(VerdictKind.NoPush, model.Verdict.Kind);
                Assert.Contains("no remote", model.Verdict.Lead, StringComparison.Ordinal);
                Assert.False(model.HasResult);
                Assert.Empty(host.Review.Scopes);
                break;
            case "no repository":
                Assert.Equal(VerdictKind.NoRepository, model.Verdict.Kind);
                Assert.False(model.CanRun);
                Assert.False(model.RunCommand.CanExecute(null));
                break;
            case "bad settings":
                host.Review.Failure = new SettingsException("The rules cannot be read.", 3, 7);
                await host.RunAsync();
                Assert.NotNull(model.BannerText);
                Assert.Contains("(3,7)", model.BannerText, StringComparison.Ordinal);
                Assert.Contains("cannot be read", model.BannerText, StringComparison.Ordinal);
                Assert.False(model.HasResult);
                Assert.Equal(VerdictKind.NoReview, model.Verdict.Kind);
                break;
            default:
                host.Review.Result = _ => Samples.Result(ReviewOutcome.Warned, new[] { Samples.Finding("a.cs", 3, "TEAM002", Severity.Warn) }, new[] { Samples.Reviewed("a.cs") });
                model.SelectedChoice = model.Choices[1];
                await host.RunAsync();
                var before = model.Verdict;
                host.Review.Failure = new ReviewTargetException("The folder is outside the repository.");
                model.UseTarget(Path.Combine(Path.GetTempPath(), "elsewhere"));
                await host.RunAsync();
                Assert.Contains("outside the repository", model.BannerText, StringComparison.Ordinal);
                Assert.Same(before, model.Verdict);
                Assert.True(model.HasResult);
                Assert.Single(model.FileGroups);
                break;
        }
    }

    [Fact]
    public async Task FindingShowsReviewedSource()
    {
        using var host = new ReviewHost();
        Directory.CreateDirectory(Path.Combine(host.RepositoryPath, "src"));
        var workingText = string.Join('\n', Enumerable.Range(1, 6).Select(number => $"// {_unrelatedWorkingText} {number}"));
        File.WriteAllText(Path.Combine(host.RepositoryPath, "src", "a.cs"), workingText);
        var snapshot = string.Join('\n', Enumerable.Range(1, 6).Select(number => $"// reviewed line {number}"));
        host.Review.Result = _ => Samples.Result(ReviewOutcome.Warned, new[] { Samples.Finding("src/a.cs", 3, "TEAM002", Severity.Warn) }, new[] { Samples.Reviewed("src/a.cs") }, new Dictionary<string, string> { ["src/a.cs"] = snapshot });

        await host.RunAsync();
        var finding = Assert.Single(Assert.Single(host.Model.FileGroups).Findings);
        finding.ToggleCommand.Execute(null);

        Assert.True(finding.IsOpen);
        var lines = finding.Lines.Select(line => (line.Number, Text: string.Concat(line.Segments.Select(segment => segment.Text)), line.IsBreaking)).ToList();
        Assert.Equal((3, "// reviewed line 3", true), lines.Single(line => line.Number == 3));
        Assert.Single(lines, line => line.IsBreaking);
        Assert.All(lines, line => Assert.StartsWith("// reviewed line", line.Text, StringComparison.Ordinal));
        Assert.DoesNotContain(lines, line => line.Text.Contains(_unrelatedWorkingText, StringComparison.Ordinal));
    }

    [AvaloniaTheory]
    [InlineData("TEAM001", true)]
    [InlineData("CA1000", false)]
    public async Task OpenFindingNavigatesToItsRulePage(string ruleId, bool isTeamRule)
    {

        // the rule of .editorconfig is in the service before the window model is made, so that every listing has it, the one the
        // model's constructor starts included; its arrival is posted to this thread and cannot come before the wait below
        using var host = new ShellHost(fileRules: new[] { new FileRule("CA1000", "Do not declare static members on generic types", "error", FileRuleState.On, Severity.Block, ".editorconfig", 4) });
        host.Review.Result = _ => Samples.Result(
            ReviewOutcome.Blocked,
            new[] { Samples.Finding("a.cs", 3, ruleId, Severity.Block, source: isTeamRule ? FindingSource.CodeRule : FindingSource.EditorConfig) },
            new[] { Samples.Reviewed("a.cs") });
        host.Model.ShowReview();
        await Signals.FileRulesArrived(host.Model.Rules);
        await host.Model.Review.RunCommand.ExecuteAsync(null);
        var finding = Assert.Single(Assert.Single(host.Model.Review.FileGroups).Findings);

        Assert.True(finding.CanOpenRule);
        finding.OpenRuleCommand.Execute(null);

        Assert.True(host.Model.ShowsRules);
        if(isTeamRule)
        {
            Assert.Equal(ruleId, host.Model.Rules.SelectedRule?.Id);
            Assert.Equal(ruleId, host.Model.Rules.CurrentPage?.Id);
        }
        else
        {
            Assert.Equal(ruleId, host.Model.Rules.SelectedFileRule?.Id);
            Assert.Equal(ruleId, host.Model.Rules.FilePage.Id);
            Assert.True(host.Model.Rules.ShowsFilePage);
        }
    }

    [Theory]
    [InlineData("repository", "", false, false)]
    [InlineData("subfolder", "src/Billing", true, false)]
    [InlineData("project", "src/Billing/Billing.csproj", false, true)]
    public async Task FolderRunUsesTheSelectedTarget(string target, string relative, bool leavesOutUncommitted, bool clean)
    {
        using var host = new ReviewHost();
        var path = relative.Length == 0 ? string.Empty : Path.Combine(host.RepositoryPath, relative.Replace('/', Path.DirectorySeparatorChar));
        var findings = clean ? new List<Finding>() : new List<Finding> { Samples.Finding("src/Billing/a.cs", 3, "TEAM001", Severity.Block), Samples.Finding("src/Billing/a.cs", 8, "TEAM002", Severity.Warn) };
        host.Review.Result = _ => new ReviewResult(clean ? ReviewOutcome.Clean : ReviewOutcome.Blocked, findings, new[] { Samples.Reviewed("src/Billing/a.cs") }, new List<string>(), string.Empty, TimeSpan.FromSeconds(1))
        {
            LeavesOutUncommittedWork = leavesOutUncommitted
        };
        var model = host.Model;
        model.SelectedChoice = model.Choices[1];
        model.UseTarget(path);
        Assert.Empty(host.Review.Scopes);

        await host.RunAsync();

        var scope = Assert.Single(host.Review.Scopes);
        Assert.Equal(ScopeKind.WholeFolder, scope.Kind);
        Assert.Equal(path, scope.TargetPath);
        var expectedLabel = relative.Length == 0 ? "invoicing" : relative;
        Assert.Equal($"in {expectedLabel}", model.Verdict.HeadingTail);
        Assert.Equal(leavesOutUncommitted, model.Verdict.LeavesOutUncommitted);
        if(clean)
        {
            Assert.Equal(VerdictKind.FolderClean, model.Verdict.Kind);
            Assert.Empty(model.FileGroups);
        }
        else
        {
            Assert.Equal(VerdictKind.FolderFindings, model.Verdict.Kind);
            var shown = Assert.Single(model.FileGroups).Findings;
            Assert.Equal(new[] { "Would block", "Would warn" }, shown.Select(finding => finding.EffectWord));
        }

        Assert.Equal(target == "repository" ? string.Empty : path, model.FolderPath);
    }

    [Fact]
    public async Task RuleFilterRestrictsAndRestoresFindings()
    {
        using var host = new ReviewHost();
        host.Review.Result = _ => Samples.Result(
            ReviewOutcome.Blocked,
            new[]
            {
                Samples.Finding("a.cs", 3, "TEAM001", Severity.Block),
                Samples.Finding("a.cs", 5, "TEAM001", Severity.Block),
                Samples.Finding("a.cs", 9, "TEAM002", Severity.Warn),
                Samples.Finding("b.cs", 4, "TEAM002", Severity.Warn)
            },
            new[] { Samples.Reviewed("a.cs"), Samples.Reviewed("b.cs") });
        var model = host.Model;
        model.SelectedChoice = model.Choices[1];
        await host.RunAsync();
        Assert.Equal(new[] { "TEAM001", "TEAM002" }, model.RuleRows.Select(row => row.RuleId));
        Assert.Equal(2, model.FileGroups.Count);

        model.RuleRows[0].ChooseCommand.Execute(null);

        Assert.Equal("TEAM001", model.ChosenRule);
        var group = Assert.Single(model.FileGroups);
        Assert.Equal("a.cs", group.Path);
        Assert.Equal(new[] { 3, 5 }, group.Findings.Select(finding => finding.Line!.Value));
        Assert.Contains(model.Verdict.Counts, piece => piece.IsFilter);

        model.ClearRuleCommand.Execute(null);

        Assert.Null(model.ChosenRule);
        Assert.Equal(new[] { "a.cs", "b.cs" }, model.FileGroups.Select(file => file.Path));
        Assert.Equal(4, model.FileGroups.Sum(file => file.Total));
        Assert.DoesNotContain(model.Verdict.Counts, piece => piece.IsFilter);
    }

    [Fact]
    public async Task SwitchingScopePreservesEachLastReview()
    {
        using var host = new ReviewHost();
        host.Review.Result = scope => scope.Kind == ScopeKind.WholeFolder
            ? Samples.Result(ReviewOutcome.Warned, new[] { Samples.Finding("a.cs", 3, "TEAM001", Severity.Warn), Samples.Finding("b.cs", 4, "TEAM002", Severity.Warn) }, new[] { Samples.Reviewed("a.cs"), Samples.Reviewed("b.cs") })
            : Samples.Result(ReviewOutcome.Clean, null, new[] { Samples.Reviewed("c.cs") });
        var model = host.Model;
        await host.RunAsync();
        Assert.Equal(VerdictKind.AllowedClean, model.Verdict.Kind);

        model.SelectedChoice = model.Choices[1];
        var target = Path.Combine(host.RepositoryPath, "src");
        model.UseTarget(target);
        Assert.False(model.HasResult);
        Assert.Single(host.Review.Scopes);

        await host.RunAsync();
        model.RuleRows[1].ChooseCommand.Execute(null);
        Assert.Equal(2, host.Review.Scopes.Count);

        model.SelectedChoice = model.Choices[0];
        Assert.Equal(VerdictKind.AllowedClean, model.Verdict.Kind);
        Assert.Equal(new[] { "c.cs" }, model.CleanPushFiles);
        model.SelectedChoice = model.Choices[1];

        Assert.Equal(VerdictKind.FolderFindings, model.Verdict.Kind);
        Assert.Equal("TEAM002", model.ChosenRule);
        Assert.Equal(target, model.FolderPath);
        Assert.Equal("b.cs", Assert.Single(model.FileGroups).Path);
        Assert.Equal(2, host.Review.Scopes.Count);
    }

    [AvaloniaFact]
    public async Task KeyboardUserCanInspectReview()
    {
        using var host = new ReviewHost();
        host.Review.Result = scope => Samples.Result(
            ReviewOutcome.Blocked,
            new[]
            {
                Samples.Finding("a.cs", 3, "TEAM001", Severity.Block),
                Samples.Finding("a.cs", 9, "TEAM002", Severity.Warn),
                Samples.Finding("b.cs", 4, "TEAM002", Severity.Warn)
            },
            new[] { Samples.Reviewed("a.cs"), Samples.Reviewed("b.cs") });
        var model = host.Model;
        var view = new ReviewView { DataContext = model };
        using var window = new ViewHost(view, 1500, 900);

        view.FocusRunButton();
        window.Press(PhysicalKey.Enter);
        await Signals.Until(model, () => model.HasResult && !model.IsBusy);
        window.Pump();

        Assert.Single(host.Review.Scopes);
        var panelFiles = window.FindAll<Button>(button => button.Classes.Contains("panelfile") && button.IsEffectivelyVisible);
        Assert.Equal(2, panelFiles.Count);
        panelFiles[0].Focus(NavigationMethod.Tab);
        window.Press(PhysicalKey.ArrowDown);
        Assert.True(ReferenceEquals(window.Window.FocusManager?.GetFocusedElement(), panelFiles[1]));
        window.Press(PhysicalKey.ArrowUp);
        Assert.True(ReferenceEquals(window.Window.FocusManager?.GetFocusedElement(), panelFiles[0]));

        var foldAll = window.Find<Button>(button => button.Name == "OpenAllButton")!;
        Assert.Equal("Fold all", AutomationProperties.GetName(foldAll));
        foldAll.Focus(NavigationMethod.Tab);
        window.Press(PhysicalKey.Enter);
        Assert.All(model.FileGroups, group => Assert.False(group.IsOpen));
        Assert.Equal("Open all", model.OpenAllText);

        var fileLine = view.FileLineButtons()[0];
        fileLine.Focus(NavigationMethod.Tab);
        window.Press(PhysicalKey.ArrowRight);
        Assert.True(model.FileGroups[0].IsOpen);
        window.Pump();
        var action = window.Find<Button>(button => AutomationProperties.GetName(button) == "Open the rule" && button.IsEffectivelyVisible);
        Assert.NotNull(action);
        action.Focus(NavigationMethod.Tab);
        window.Press(PhysicalKey.Enter);
        Assert.NotEmpty(host.ShownRules);

        model.SelectedChoice = model.Choices[1];
        view.FocusRunButton();
        window.Press(PhysicalKey.Enter);
        await Signals.Until(model, () => model.HasResult && !model.IsBusy);
        window.Pump();
        var rows = window.FindAll<Button>(button => button.Classes.Contains("rulerow") && button.IsEffectivelyVisible);
        Assert.Equal(model.RuleRows.Count, rows.Count);
        rows[0].Focus(NavigationMethod.Tab);
        window.Press(PhysicalKey.Enter);
        Assert.Equal(model.RuleRows[0].RuleId, model.ChosenRule);
        window.Press(PhysicalKey.Escape);
        Assert.Null(model.ChosenRule);
    }

    [AvaloniaFact]
    public async Task NarrowReviewCanRevealItsFileList()
    {
        using var host = new ReviewHost();
        host.Review.Result = _ => Samples.Result(
            ReviewOutcome.Blocked,
            new[] { Samples.Finding("src/a.cs", 3, "TEAM001", Severity.Block) },
            new[] { Samples.Reviewed("src/a.cs"), Samples.Reviewed("src/b.cs") });
        var model = host.Model;
        var view = new ReviewView { DataContext = model };
        using var window = new ViewHost(view, 700, 900);
        await host.RunAsync();
        window.Pump();
        Assert.True(model.IsOnePane);
        Assert.False(model.PanelIsListed);
        Assert.Empty(window.FindAll<Button>(button => button.Classes.Contains("panelfile") && button.IsEffectivelyVisible));

        var toggle = window.Find<Button>(button => AutomationProperties.GetName(button) == "Show the files of this push" && button.IsEffectivelyVisible);
        Assert.NotNull(toggle);
        toggle.Focus(NavigationMethod.Tab);
        window.Press(PhysicalKey.Enter);

        Assert.True(model.PanelIsListed);
        var file = Assert.Single(window.FindAll<Button>(button => button.Classes.Contains("panelfile") && button.IsEffectivelyVisible));
        Assert.StartsWith("src/a.cs", AutomationProperties.GetName(file), StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public async Task BigFolderReviewMakesOnlyTheRowsInView()
    {
        using var host = BigFolderReview(out var model);
        var view = new ReviewView { DataContext = model };
        using var window = new ViewHost(view, 1440, 900);
        await host.RunAsync();
        model.ToggleAllCommand.Execute(null);
        window.Pump();

        var rows = view.FindControl<ItemsControl>("Rows")!;
        var panel = rows.GetVisualDescendants().OfType<VirtualizingStackPanel>().First();
        Assert.Equal(_bigFindings + _bigFiles + 2, model.Items.Count);
        Assert.InRange(panel.Children.Count, 1, 119);

        var last = model.FileGroups[^1].Findings[^1];
        rows.ScrollIntoView(model.Items.Count - 1);
        window.Pump();

        var lastRow = window.Find<Button>(button => ReferenceEquals(button.DataContext, last));
        Assert.NotNull(lastRow);
        Assert.Contains(last.Message, AutomationProperties.GetName(lastRow), StringComparison.Ordinal);
        Assert.InRange(panel.Children.Count, 1, 119);
    }

    [AvaloniaFact]
    public async Task BigFolderReviewShowsTheRightRowsAfterEveryChange()
    {
        using var host = BigFolderReview(out var model);
        var view = new ReviewView { DataContext = model };
        using var window = new ViewHost(view, 1440, 3000);
        await host.RunAsync();
        window.Pump();
        var first = model.FileGroups[0];
        var second = model.FileGroups[1];
        Assert.Equal(new object[] { first, second }, RowsInOrder(window).Take(2));

        first.ToggleCommand.Execute(null);
        window.Pump();
        Assert.Equal(first.Findings.Count, first.Findings.Distinct().Count());
        Assert.Equal(new object[] { first }.Concat(first.Findings).Append(second), RowsInOrder(window).Take(first.Findings.Count + 2));

        var finding = first.Findings[0];
        finding.ToggleCommand.Execute(null);
        window.Pump();
        var code = window.FindAll<TextBlock>(block => block.Inlines != null && string.Concat(block.Inlines.OfType<Run>().Select(run => run.Text)) == "// reviewed line 1");
        Assert.NotEmpty(code);
        Assert.Contains(window.FindAll<Button>(button => AutomationProperties.GetName(button) == "Copy as a prompt" && button.IsEffectivelyVisible), button => ReferenceEquals(button.DataContext, finding.Copy));

        first.ToggleCommand.Execute(null);
        window.Pump();
        Assert.Equal(new object[] { first, second }, RowsInOrder(window).Take(2));

        model.RuleRows.First(row => row.RuleId == "TEAM001").ChooseCommand.Execute(null);
        window.Pump();
        Assert.Equal(_bigFiles / 2, model.FileGroups.Count);
        Assert.All(model.FileGroups, group => Assert.Equal(0, int.Parse(group.Path[^7..^3], System.Globalization.CultureInfo.InvariantCulture) % 2));
        Assert.Equal(new object[] { model.FileGroups[0], model.FileGroups[1], model.FileGroups[2] }, RowsInOrder(window).Take(3));
        model.ToggleAllCommand.Execute(null);
        window.Pump();
        Assert.DoesNotContain(RowsInOrder(window), row => row is FindingViewModel { RuleId: "TEAM002" });
    }

    private const int _bigFindings = 5000;
    private const int _bigFiles = 400;

    /// <summary>
    /// A folder review of 5,000 findings in 400 files (even files have the rule TEAM001, odd files TEAM002); the first file's text is known.
    /// </summary>
    private static ReviewHost BigFolderReview(out ReviewViewModel model)
    {
        var host = new ReviewHost();
        var findings = new List<Finding>();
        for(var index = 0; index < _bigFindings; index++)
            findings.Add(Samples.Finding($"src/f{index % _bigFiles:D4}.cs", (index / _bigFiles) + 1, index % 2 == 0 ? "TEAM001" : "TEAM002", index % 2 == 0 ? Severity.Block : Severity.Warn, $"Message {index}"));

        var coverage = Enumerable.Range(0, _bigFiles).Select(file => Samples.Reviewed($"src/f{file:D4}.cs")).ToList();
        var text = string.Join('\n', Enumerable.Range(1, 20).Select(number => $"// reviewed line {number}"));
        host.Review.Result = _ => Samples.Result(ReviewOutcome.Blocked, findings, coverage, new Dictionary<string, string> { ["src/f0000.cs"] = text });
        model = host.Model;
        model.SelectedChoice = model.Choices[1];
        return host;
    }

    /// <summary>
    /// The files' lines and findings that exist as controls, from the top of the window down.
    /// </summary>
    private static List<object?> RowsInOrder(ViewHost window)
    {
        return window.FindAll<Button>(button => (button.Classes.Contains("fileline") || button.Classes.Contains("findinghead")) && button.IsEffectivelyVisible)
            .OrderBy(button => button.TranslatePoint(default, window.Window)!.Value.Y)
            .Select(button => button.DataContext)
            .ToList();
    }

    private static ScopeFacts FactsOf(ReviewScope scope)
    {
        var refs = scope.PushedRefs.Refs.Select(pushed => $"{pushed.LocalRef} {pushed.LocalObject} {pushed.RemoteRef} {pushed.RemoteObject}");
        return new ScopeFacts(scope.Kind, scope.RemoteName, scope.RemoteUrl, string.Join(" | ", refs));
    }

    private static (ExitCode ExitCode, string Output) Run(IReviewService review, string input, params string[] arguments)
    {
        var output = new StringWriter();
        var workFolder = Path.Combine(Path.GetTempPath(), "cf-app-tests-work");
        var programFolder = Path.Combine(Path.GetTempPath(), "cf-app-tests-program");
        var environment = new CommandEnvironment(workFolder, programFolder, Path.Combine(programFolder, "commitfence"), new StringReader(input), output, new StringWriter(), false);
        var exitCode = new CommandRunner(new ProductInfoService(), null!, review, null!, null!, environment).Run(arguments);
        return (exitCode, output.ToString());
    }
}