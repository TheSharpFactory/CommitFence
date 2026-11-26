using Avalonia.Headless.XUnit;
using CF.App.Review;
using CF.App.Tests.Support;
using CF.Core.Changes;
using CF.Core.Findings;
using CF.Core.Review;
using Xunit;

namespace CF.App.Tests;

/// <summary>
/// Review of the staged changes, and the way from a finding's code to the file on disk. The review is the test's; the file on disk is a
/// real one in a temporary folder.
/// </summary>
public sealed class ReviewStagedTests
{
    private const string _commit = "4f2a91c";

    [AvaloniaFact]
    public async Task StagedChangesAreReviewedAndNothingStagedHasNothingToRun()
    {
        using var host = new ReviewHost();
        host.Review.StagedNow = _ => StagedAnswer.Of(["src/a.cs", "src/b.cs"], 1, true, _commit, "tree-1", "base-1");
        host.Review.Result = _ => Samples.Result(ReviewOutcome.Blocked, [Samples.Finding("src/a.cs", 3, "TEAM001", Severity.Block), Samples.Finding("src/b.cs", 4, "TEAM002", Severity.Warn)], [Samples.Reviewed("src/a.cs"), Samples.Reviewed("src/b.cs")], null, "tree-1", "base-1", _commit + new string('0', 33));
        var model = host.Model;

        // the first segment is the push, the second the staged changes; choosing one starts nothing
        Assert.Equal(["The push", "Staged changes", "A folder"], model.Choices.Select(choice => choice.Label));
        model.SelectedChoice = model.Choices[1];
        await model.RefreshAsync();
        Assert.Equal(VerdictKind.StagedReady, model.Verdict.Kind);
        Assert.Equal("2 files staged", model.Verdict.Heading);
        Assert.Equal("Not reviewed yet. Review them to see what the next commit would meet.", model.Verdict.Lead);
        Assert.Equal(["On top of commit 4f2a91c", "2 files"], model.Verdict.Facts.Select(fact => fact.Spoken));
        Assert.Equal(["a.cs", "b.cs"], model.Verdict.StagedFiles.Select(file => file.Name));
        Assert.Empty(host.Review.Scopes);

        await host.RunAsync();
        Assert.Equal(ScopeKind.Staged, Assert.Single(host.Review.Scopes).Kind);
        Assert.Equal(VerdictKind.StagedFindings, model.Verdict.Kind);
        Assert.Equal("1 blocking finding in the staged changes", model.Verdict.Heading);
        Assert.Equal(string.Empty, model.Verdict.HeadingTail);
        Assert.Equal(["1 finding would block it", "1 would only warn"], model.Verdict.Counts.Select(count => count.Text));
        Assert.Equal(["On top of commit 4f2a91c", "2 files"], model.Verdict.Facts.Take(2).Select(fact => fact.Spoken));
        Assert.True(model.Verdict.LeavesOutUncommitted);
        Assert.Equal("Changes in 1 file are not staged, so they are not part of this review.", model.Verdict.LeavesOutText);
        Assert.Equal("2 staged, 2 with findings", model.PanelHead);
        var blocking = model.FileGroups.SelectMany(group => group.Findings).First(finding => finding.IsBlock);
        Assert.Equal("Would block", blocking.EffectWord);
        blocking.IsOpen = true;
        Assert.EndsWith(", where it blocks a push. Once committed, it would stop the push.", blocking.SourceLine, StringComparison.Ordinal);

        // the index moves on after the review: the run button asks for a new one, also when the same files are staged again
        Assert.True(model.ResultMatches);
        host.Review.StagedNow = _ => StagedAnswer.Of(["src/a.cs", "src/b.cs"], 1, true, _commit, "tree-1", "base-1");
        await model.RefreshAsync();
        Assert.False(model.StagedChanged);
        host.Review.StagedNow = _ => StagedAnswer.Of(["src/a.cs", "src/b.cs"], 1, true, _commit, "tree-2", "base-1");
        await model.RefreshAsync();
        Assert.True(model.StagedChanged);
        Assert.Equal("Review now", model.RunText);

        // nothing is staged: no button to run, and a fact about what is not staged
        using var empty = new ReviewHost();
        empty.Review.StagedNow = _ => StagedAnswer.Of([], 5, false, _commit);
        model = empty.Model;
        model.SelectedChoice = model.Choices[1];
        await model.RefreshAsync();

        Assert.Equal(VerdictKind.NothingStaged, model.Verdict.Kind);
        Assert.Equal("Nothing is staged", model.Verdict.Heading);
        Assert.Equal("git add", model.Verdict.LeadCode);
        Assert.Equal("5 files changed, none staged", model.Verdict.ContentLine);
        Assert.Equal("Stage files with git add and they show here, to review before you commit.", model.Verdict.Lead);
        Assert.True(model.Verdict.ShowsCheckAgain);
        Assert.False(model.ShowsBarRun);
        Assert.False(model.RunCommand.CanExecute(null));

        empty.Review.StagedNow = _ => StagedAnswer.Of([], 0, false, _commit);
        model.SelectedChoice = model.Choices[0];
        model.SelectedChoice = model.Choices[1];
        await model.RefreshAsync();
        Assert.Equal("No file has changed since the last commit.", model.Verdict.ContentLine);

        // with no commit yet, the block does not speak of a last commit
        empty.Review.StagedNow = _ => StagedAnswer.Of([], 0, false, "");
        await model.CheckAgainCommand.ExecuteAsync(null);
        Assert.Equal("No changes are staged for the first commit.", model.Verdict.ContentLine);

        // "Check again" says what it found: still nothing staged (said once), or why the index could not be read, with "Try again"
        Assert.Matches(@"^Still nothing staged, \d\d:\d\d\.$", model.CheckNote);

        // the note is only what its own lookup found: a lookup another one replaced says nothing, and any refresh or change of scope clears it
        using var gate = new ManualResetEventSlim();
        empty.Review.StagedNow = _ =>
        {
            gate.Wait();
            return StagedAnswer.Of([], 0, false, "");
        };
        var superseded = model.CheckAgainCommand.ExecuteAsync(null);
        var newer = model.RefreshAsync();
        gate.Set();
        await Task.WhenAll(superseded, newer);
        Assert.False(model.HasCheckNote);
        await model.CheckAgainCommand.ExecuteAsync(null);
        Assert.Matches(@"^Still nothing staged, \d\d:\d\d\.$", model.CheckNote);
        await model.RefreshAsync();
        Assert.False(model.HasCheckNote);
        await model.CheckAgainCommand.ExecuteAsync(null);
        model.SelectedChoice = model.Choices[0];
        Assert.False(model.HasCheckNote);
        model.SelectedChoice = model.Choices[1];
        await model.RefreshAsync();
        var checked_ = new List<VerdictKind>();
        model.CheckedAgain += checked_.Add;
        empty.Review.StagedNow = _ => StagedAnswer.Unknown("git did not answer.");
        await model.CheckAgainCommand.ExecuteAsync(null);
        Assert.Equal(VerdictKind.StagedUnreadable, model.Verdict.Kind);
        Assert.True(model.Verdict.ShowsTryAgain);
        Assert.Equal("git did not answer.", model.Verdict.ProblemFirst);
        Assert.Equal("Staged changes cannot be read", model.Verdict.Heading);
        Assert.False(model.HasCheckNote);

        // "Check again" reads the index again: ten files are staged now, and the block lists eight of them
        empty.Review.StagedNow = _ => StagedAnswer.Of(Enumerable.Range(1, 10).Select(number => $"src/f{number}.cs").ToList(), 0, false, "");
        await model.CheckAgainCommand.ExecuteAsync(null);
        Assert.Equal(VerdictKind.StagedReady, model.Verdict.Kind);
        Assert.Equal(8, model.Verdict.StagedFiles.Count);
        Assert.Equal("and 2 more", model.Verdict.MoreFiles);
        Assert.Equal("Not reviewed yet. Review them to see what the next commit would meet.", model.Verdict.Lead);
        Assert.Equal("For the first commit", model.Verdict.Facts[0].Spoken);
        Assert.Equal("Review now", model.RunText);
        Assert.True(model.RunCommand.CanExecute(null));
        Assert.Equal([VerdictKind.StagedUnreadable, VerdictKind.StagedReady], checked_);

        // a staged review that could not finish says so, also when it found something
        using var unfinished = new ReviewHost();
        unfinished.Review.Result = _ => Samples.Result(ReviewOutcome.Incomplete, [Samples.Finding("src/a.cs", 3, "TEAM001", Severity.Warn)], [Samples.Reviewed("src/a.cs")]);
        unfinished.Model.SelectedChoice = unfinished.Model.Choices[1];
        await unfinished.RunAsync();
        Assert.Equal(VerdictKind.Incomplete, unfinished.Model.Verdict.Kind);
    }

    [AvaloniaFact]
    public async Task FindingOpensItsFileInTheEditorInThreeStates()
    {
        using var host = new ReviewHost();
        var folder = Path.Combine(host.RepositoryPath, "src", "Billing");
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, "PaymentGateway.cs");
        var reviewed = "using System;\n\nclass PaymentGateway\n{\n    void Charge()\n    {\n        Send();\n    }\n}\n";
        File.WriteAllText(path, reviewed);
        var refs = PushedRefs.FromRefs([new PushedRef("refs/heads/main", _commit + new string('0', 33), "refs/heads/main", new string('0', 40))]);
        host.Review.PushScope = _ => PushScopeAnswer.Found(ReviewScope.ForPush("origin", "https://example.com/r.git", refs), "main", "origin");
        const string file = "src/Billing/PaymentGateway.cs";
        host.Review.Result = _ => Samples.Result(ReviewOutcome.Blocked, [Samples.Finding(file, 7, "TEAM001", Severity.Block)], [Samples.Reviewed(file)], new Dictionary<string, string> { [file] = reviewed });
        var model = host.Model;
        model.EditorName = "Visual Studio Code";
        var opened = new List<(string Path, int Line)>();
        model.OpenLineRequested += (target, line) => opened.Add((target, line));
        await host.RunAsync();
        var finding = Assert.Single(Assert.Single(model.FileGroups).Findings);

        // the same file: the head says which version the lines are, and the button opens the finding's own line
        finding.IsOpen = true;
        Assert.Equal("Commit `4f2a91c` in this push.", finding.HeadText);
        Assert.Equal("Same as on disk", finding.CopyTag);
        Assert.Equal("Open at line 7", finding.OpenSourceText);
        Assert.Equal("Open src/Billing/PaymentGateway.cs at line 7 in Visual Studio Code (Ctrl+O)", finding.OpenSourceTip);
        finding.OpenSourceCommand.Execute(null);
        Assert.Equal((path, 7), Assert.Single(opened));

        // the copy on disk has changed and the code is found again, further down
        File.WriteAllText(path, "// a new first line\n// and a second\n" + reviewed);
        finding.IsOpen = false;
        finding.IsOpen = true;
        Assert.Equal("Moved to line 9. Commit 4f2a91c in this push. Your copy on disk has changed since.", finding.CopyNote);
        Assert.Equal("Open at line 9", finding.OpenSourceText);

        // the code is not found: the button opens the file at its top, and the head says so
        File.WriteAllText(path, "class Other\n{\n}\n");
        finding.IsOpen = false;
        finding.IsOpen = true;
        Assert.Equal("Not found on disk. Commit 4f2a91c in this push. Your copy on disk has changed since, and this code was not found in it, so the file opens at the top.", finding.CopyNote);
        Assert.Equal("Open the file", finding.OpenSourceText);
        Assert.True(finding.CanOpenSource);
        Assert.False(finding.HasTwoNumbers);
        finding.OpenSourceCommand.Execute(null);
        Assert.Equal((path, 1), opened[^1]);

        // the file is gone: the action is to review again, and nothing can be opened
        File.Delete(path);
        finding.IsOpen = false;
        finding.IsOpen = true;
        Assert.Equal("Not on disk. Commit 4f2a91c in this push. PaymentGateway.cs is not on disk now. If you renamed or deleted it, commit that, then review again.", finding.CopyNote);
        Assert.Equal("Commit `4f2a91c` in this push.", finding.HeadText);
        Assert.True(finding.ShowsReviewAgain);
        Assert.False(finding.OpenSourceCommand.CanExecute(null));

        // no editor that takes a line: the button says so, and opens the file
        model.EditorName = null;
        Assert.Equal("Open the file", finding.OpenSourceText);
        Assert.Equal("No editor that opens at a line was found.", finding.OpenSourceTip);
    }

    [AvaloniaTheory]
    [InlineData(2, 1, "2 blocking findings in the staged changes", "Block")]
    [InlineData(0, 3, "No blocking findings in the staged changes", "Warn")]
    [InlineData(0, 0, "No blocking findings in the staged changes", "Clean")]
    public async Task TheStagedHeadlineSaysWhatCommitFenceFoundNotWhatGitWillDo(int blocking, int warnings, string headline, string tile)
    {
        using var host = new ReviewHost();
        host.Review.StagedNow = _ => StagedAnswer.Of(["src/a.cs"], 0, true, _commit, "tree-1", "base-1");
        var findings = Enumerable.Range(0, blocking).Select(index => Samples.Finding("src/a.cs", 3 + index, "TEAM001", Severity.Block))
            .Concat(Enumerable.Range(0, warnings).Select(index => Samples.Finding("src/a.cs", 20 + index, "TEAM002", Severity.Warn))).ToList();
        var outcome = blocking > 0 ? ReviewOutcome.Blocked : warnings > 0 ? ReviewOutcome.Warned : ReviewOutcome.Clean;
        host.Review.Result = _ => Samples.Result(outcome, findings, [Samples.Reviewed("src/a.cs")], null, "tree-1", "base-1", _commit + new string('0', 33));
        host.Model.SelectedChoice = host.Model.Choices[1];
        await host.Model.RefreshAsync();
        await host.RunAsync();

        // the headline counts what CommitFence found; the counts of both kinds stay on the line of chips under it
        var verdict = host.Model.Verdict;
        Assert.Equal(headline, verdict.Heading);
        Assert.Equal(string.Empty, verdict.HeadingTail);
        Assert.Equal(tile == "Block", verdict.ShowsBlockTile);
        Assert.Equal(tile == "Warn", verdict.ShowsWarnTile);
        Assert.Equal(tile == "Clean", verdict.Kind == VerdictKind.StagedClean);
        if(warnings > 0)
            Assert.Contains(verdict.Counts, count => count.Text.EndsWith("would only warn", StringComparison.Ordinal));
    }

    [AvaloniaFact]
    public async Task AStagedWarningNamesItsNumberBoxAndAStagedFileGoneFromDiskSaysWhatTheCommitWouldRecord()
    {
        using var host = new ReviewHost();
        var folder = Path.Combine(host.RepositoryPath, "src");
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, "Gateway.cs");
        var reviewed = "class Gateway\n{\n    void Charge()\n    {\n        Send();\n    }\n}\n";
        File.WriteAllText(path, reviewed);
        const string file = "src/Gateway.cs";
        host.Review.StagedNow = _ => StagedAnswer.Of([file], 0, false, _commit, "tree-1", "base-1");
        host.Review.Result = _ => Samples.Result(ReviewOutcome.Warned, [Samples.Finding(file, 5, "TEAM001", Severity.Warn)], [Samples.Reviewed(file)], new Dictionary<string, string> { [file] = reviewed }, "tree-1", "base-1", _commit + new string('0', 33));
        host.Model.SelectedChoice = host.Model.Choices[1];
        await host.Model.RefreshAsync();
        await host.RunAsync();
        var finding = Assert.Single(Assert.Single(host.Model.FileGroups).Findings);

        // a warning's number box takes the warning's colour and is named for what it is, with the copy it counts in
        File.WriteAllText(path, "// one\n// two\n" + reviewed);
        finding.IsOpen = true;
        var line = Assert.Single(finding.Lines, candidate => candidate.IsBreaking);
        Assert.True(line.IsWarning);
        Assert.False(line.IsBlocking);
        Assert.Equal("Warning at staged line 5, on disk line 7", line.SpokenNumbers);

        // a file deleted on disk can still be staged: a new review would find the same, so the head says what to do instead
        File.Delete(path);
        finding.IsOpen = false;
        finding.IsOpen = true;
        Assert.Equal("Not on disk. The staged copy, on top of commit 4f2a91c. Gateway.cs is not on disk now, but its staged copy is what the next commit would record. Stage the deletion or restore the file, then review again.", finding.CopyNote);
    }

    [AvaloniaFact]
    public async Task ACopyThatDiffersOnlyElsewhereSaysTheFileChangedWhileItsLinesDidNot()
    {
        using var host = new ReviewHost();
        var folder = Path.Combine(host.RepositoryPath, "src");
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, "Gateway.cs");
        var reviewed = "class Gateway\n{\n    void Charge()\n    {\n        Send();\n    }\n}\n";
        File.WriteAllText(path, reviewed);
        const string file = "src/Gateway.cs";
        var refs = PushedRefs.FromRefs([new PushedRef("refs/heads/main", _commit + new string('0', 33), "refs/heads/main", new string('0', 40))]);
        host.Review.PushScope = _ => PushScopeAnswer.Found(ReviewScope.ForPush("origin", "https://example.com/r.git", refs), "main", "origin");
        host.Review.StagedNow = _ => StagedAnswer.Of([file], 0, false, _commit, "tree-1", "base-1");
        host.Review.Result = _ => Samples.Result(ReviewOutcome.Warned, [Samples.Finding(file, 5, "TEAM001", Severity.Warn)], [Samples.Reviewed(file)], new Dictionary<string, string> { [file] = reviewed }, "tree-1", "base-1", _commit + new string('0', 33));

        // staged: a line added at the end of the file leaves the lines of the finding where they were
        host.Model.SelectedChoice = host.Model.Choices[1];
        await host.Model.RefreshAsync();
        await host.RunAsync();
        var staged = Assert.Single(Assert.Single(host.Model.FileGroups).Findings);
        staged.IsOpen = true;
        Assert.Equal("Same as on disk. The staged copy, on top of commit 4f2a91c.", staged.CopyNote);
        File.WriteAllText(path, reviewed + "// a note\n");
        staged.IsOpen = false;
        staged.IsOpen = true;
        Assert.Equal("Same as on disk. The staged copy, on top of commit 4f2a91c. The file changed elsewhere since; these lines did not.", staged.CopyNote);

        // the push says the same with its own copy
        host.Model.SelectedChoice = host.Model.Choices[0];
        await host.RunAsync();
        var pushed = Assert.Single(Assert.Single(host.Model.FileGroups).Findings);
        pushed.IsOpen = true;
        Assert.Equal("Same as on disk. Commit 4f2a91c in this push. The file changed elsewhere since; these lines did not.", pushed.CopyNote);
    }

    [AvaloniaFact]
    public async Task MovedCodeShowsTheReviewedNumberAndTheNumberOnDisk()
    {
        using var host = new ReviewHost();
        var folder = Path.Combine(host.RepositoryPath, "src");
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, "Gateway.cs");
        var reviewed = "class Gateway\n{\n    void Charge()\n    {\n        Send();\n    }\n}\n";
        File.WriteAllText(path, reviewed);
        var refs = PushedRefs.FromRefs([new PushedRef("refs/heads/main", _commit + new string('0', 33), "refs/heads/main", new string('0', 40))]);
        host.Review.PushScope = _ => PushScopeAnswer.Found(ReviewScope.ForPush("origin", "https://example.com/r.git", refs), "main", "origin");
        const string file = "src/Gateway.cs";
        host.Review.Result = _ => Samples.Result(ReviewOutcome.Blocked, [Samples.Finding(file, 5, "TEAM001", Severity.Block)], [Samples.Reviewed(file)], new Dictionary<string, string> { [file] = reviewed });
        await host.RunAsync();
        var finding = Assert.Single(Assert.Single(host.Model.FileGroups).Findings);

        // the same copy: one number column
        finding.IsOpen = true;
        Assert.False(finding.HasTwoNumbers);
        Assert.All(finding.Lines, line => Assert.False(line.TwoNumbers));

        // two lines are added above: two columns, the number on disk is two more, and the finding's line is the one that is marked
        File.WriteAllText(path, "// one\n// two\n" + reviewed);
        finding.IsOpen = false;
        finding.IsOpen = true;
        Assert.True(finding.HasTwoNumbers);
        Assert.Equal("Pushed", finding.FirstNumberHead);
        Assert.All(finding.Lines, line => Assert.Equal(line.Number + 2, line.DiskNumber));
        Assert.Equal(7, Assert.Single(finding.Lines, line => line.IsBreaking).DiskNumber);
        Assert.Equal("Blocking finding at pushed line 5, on disk line 7", Assert.Single(finding.Lines, line => line.IsBreaking).SpokenNumbers);

        // a line not matched on disk has no second number, and is not said to be gone
        File.WriteAllText(path, "// one\n// two\nclass Gateway\n{\n    void Charge2()\n    {\n        Send();\n    }\n}\n");
        finding.IsOpen = false;
        finding.IsOpen = true;
        var unmatched = Assert.Single(finding.Lines, line => line.Number == 3);
        Assert.Null(unmatched.DiskNumber);
        Assert.Equal("Pushed line 3, not matched on disk", unmatched.SpokenNumbers);
    }

    [AvaloniaFact]
    public async Task FindingReadsTheFileWhenItIsOpenedAndMovesOnlyToCodeThatIsThereOnce()
    {
        using var host = new ReviewHost();
        var folder = Path.Combine(host.RepositoryPath, "src");
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, "Gateway.cs");
        var reviewed = "class Gateway\n{\n    void Charge()\n    {\n        Send();\n    }\n}\n";
        File.WriteAllText(path, reviewed);
        var refs = PushedRefs.FromRefs([new PushedRef("refs/heads/main", _commit + new string('0', 33), "refs/heads/main", new string('0', 40))]);
        host.Review.PushScope = _ => PushScopeAnswer.Found(ReviewScope.ForPush("origin", "https://example.com/r.git", refs), "main", "origin");
        const string file = "src/Gateway.cs";
        host.Review.Result = _ => Samples.Result(ReviewOutcome.Blocked, [Samples.Finding(file, 5, "TEAM001", Severity.Block)], [Samples.Reviewed(file)], new Dictionary<string, string> { [file] = reviewed });
        var model = host.Model;
        model.EditorName = "Visual Studio Code";
        var opened = new List<(string Path, int Line)>();
        model.OpenLineRequested += (target, line) => opened.Add((target, line));
        await host.RunAsync();
        var finding = Assert.Single(Assert.Single(model.FileGroups).Findings);
        finding.IsOpen = true;

        // two lines are added above while the finding is open: the file is read again when the button is used
        File.WriteAllText(path, "// one\n// two\n" + reviewed);
        finding.OpenSourceCommand.Execute(null);
        Assert.Equal((path, 7), Assert.Single(opened));

        // the same three lines are in two methods: nothing says which one the finding was in, so the file opens at the top
        File.WriteAllText(path, "class Gateway\n{\n    void Charge()\n    {\n        Send();\n    }\n\n    void Refund()\n    {\n        Send();\n    }\n}\n");
        finding.OpenSourceCommand.Execute(null);
        Assert.Equal((path, 1), opened[^1]);
        Assert.Equal("Found more than once on disk. Commit 4f2a91c in this push. Your copy on disk has changed since, and this code is found in it more than once, so the file opens at the top.", finding.CopyNote);
        Assert.Equal("Open the file", finding.OpenSourceText);
    }

    [Fact]
    public void TheCopyOnDiskIsTrulyTaggedSameMovedNotFoundSeveralGoneOrNotCompared()
    {
        using var folder = new TestFolder();
        var path = Path.Combine(folder.Path, "Gateway.cs");
        const string reviewed = "class Gateway\n{\n    void Charge()\n    {\n        Send();\n    }\n}\n";

        // an edit elsewhere in the file leaves the code on its line: it is the same code
        File.WriteAllText(path, reviewed + "// a note at the end\n");
        Assert.Equal(SourceCopyState.Same, SourceCopy.Compare(reviewed, path, 5).State);

        // lines above push it down: it moved
        File.WriteAllText(path, "// one\n" + reviewed);
        var moved = SourceCopy.Compare(reviewed, path, 5);
        Assert.Equal((SourceCopyState.Found, 6), (moved.State, moved.Line));

        // it is not there, or it is there twice
        File.WriteAllText(path, "class Other\n{\n}\n");
        Assert.Equal(SourceCopyState.Lost, SourceCopy.Compare(reviewed, path, 5).State);
        File.WriteAllText(path, reviewed + reviewed);
        Assert.Equal(SourceCopyState.Ambiguous, SourceCopy.Compare(reviewed, path, 5).State);

        // no reviewed text: not compared, and it says why, while a file that is not there is gone
        var unread = SourceCopy.Compare(null, path, 5);
        Assert.Equal((SourceCopyState.NotCompared, SourceCopy.ReviewedCopyMissing), (unread.State, unread.Reason));
        Assert.Equal(SourceCopyState.Gone, SourceCopy.Compare(reviewed, Path.Combine(folder.Path, "None.cs"), 5).State);

        // a folder where the file should be cannot be read, which is not the same as gone
        var blocked = SourceCopy.Compare(reviewed, folder.Path, 5);
        Assert.Equal(SourceCopyState.NotCompared, blocked.State);
        Assert.False(string.IsNullOrWhiteSpace(blocked.Reason));
    }
}
