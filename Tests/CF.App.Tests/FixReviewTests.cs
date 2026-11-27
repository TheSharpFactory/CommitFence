using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;

using CF.App.Review;
using CF.App.Shell;
using CF.App.Tests.Support;
using CF.Cli.Commands;
using CF.Core.Changes;
using CF.Core.Findings;
using CF.Core.Fixes;
using CF.Core.Ports;
using CF.Core.Review;
using Xunit;

namespace CF.Integration.Tests;

public sealed class FixReviewTests
{
    [AvaloniaFact]
    public async Task CommandAndReviewApplyIdenticalBytesAndFocusMovesToTheNextFinding()
    {
        using var scratch = new TestFolder();
        var fixture = new FixReviewFixture(scratch.Path);
        using var shell = new ShellHost(fileRules: FixReviewFixture.FileRules, fixes: fixture, parentFolder: scratch.Path);
        var refs = PushedRefs.FromRefs([
            new PushedRef("refs/heads/main", "123", "refs/heads/main", "100"),
            new PushedRef("refs/heads/other", "456", "refs/heads/other", "100")
        ]);
        shell.Review.PushScope = _ => PushScopeAnswer.Found(ReviewScope.ForPush("origin", "origin", refs), "main", "origin");
        shell.Review.Result = scope => fixture.Result(scope.PushedRefs.Refs.Count > 1);
        File.WriteAllBytes(fixture.PathToSource, FixReviewFixture.Before);
        using var output = new StringWriter();
        using var error = new StringWriter();
        var folder = shell.RepositoryPath;
        var environment = new CommandEnvironment(folder, folder, Path.Combine(folder, "commitfence"), TextReader.Null, output, error, false);
        var services = shell.Services;
        var runner = new CommandRunner(services.ProductInfo, services.Rules, services.Review, services.Setup, services.Updater, environment, fixes: services.Fixes);

        Assert.Equal(ExitCode.Blocked, runner.Run(["fix", "IDE0011", FixReviewFixture.Source]));
        Assert.Contains("Choose a pushed ref with --head, or a range with --base and --head.", error.ToString(), StringComparison.Ordinal);
        Assert.Equal(0, fixture.Previews);

        Assert.Equal(ExitCode.Passed, runner.Run(["fix", "IDE0011", FixReviewFixture.Source, "--head", "other", "--apply"]));
        Assert.Equal("refs/heads/other", Assert.Single(shell.Review.Scopes[^1].PushedRefs.Refs).LocalRef);
        shell.Review.Result = _ => fixture.Result();
        Assert.Contains("Applied the fix to InvoiceTotals.cs. Review again before you push.", output.ToString(), StringComparison.Ordinal);
        var commandBytes = File.ReadAllBytes(fixture.PathToSource);
        File.WriteAllBytes(fixture.PathToSource, FixReviewFixture.Before);

        using var host = new ViewHost(new MainWindow { DataContext = shell.Model, Width = 1440, Height = 1040 });
        shell.Model.ShowReview();
        await shell.Model.Review.RunCommand.ExecuteAsync(null);
        var group = Assert.Single(shell.Model.Review.FileGroups);
        var findings = group.Findings;
        var first = findings[0];
        await first.Preparation!;
        host.Pump();

        var labels = first.Difference.Where(line => line.HasPlaceLabel).ToList();
        Assert.Collection(labels,
            line =>
            {
                Assert.Equal(6, line.OldLine);
                Assert.Equal("This finding", line.PlaceLabel);
            },
            line =>
            {
                Assert.Equal(8, line.OldLine);
                Assert.Equal("Also line 8, Warns", line.PlaceLabel);
            });

        var header = host.Find<Button>(button => ReferenceEquals(button.DataContext, first) && button.Classes.Contains("findinghead"));
        Assert.NotNull(header);
        Assert.True(header.Focus());
        host.Press(PhysicalKey.Tab);
        var open = Assert.IsType<Button>(host.Window.FocusManager?.GetFocusedElement());
        Assert.StartsWith("Open ", AutomationProperties.GetName(open), StringComparison.Ordinal);
        host.Press(PhysicalKey.Tab);
        var difference = Assert.IsType<DiffCode>(host.Window.FocusManager?.GetFocusedElement());
        host.Press(PhysicalKey.A, RawInputModifiers.Control);
        Assert.Contains("return maximum;", difference.SelectedText, StringComparison.Ordinal);
        host.Press(PhysicalKey.Tab);
        var apply = Assert.IsType<Button>(host.Window.FocusManager?.GetFocusedElement());
        Assert.Equal("Apply fix", AutomationProperties.GetName(apply));
        var reviewsBeforeApply = shell.Review.Scopes.Count;
        host.Press(PhysicalKey.Enter);
        await first.ApplyFixCommand.ExecutionTask!;
        host.Pump();

        Assert.Equal(reviewsBeforeApply, shell.Review.Scopes.Count);
        Assert.True(first.IsFixed);
        Assert.False(first.IsOpen);
        Assert.Equal(FixReviewFixture.After, commandBytes);
        Assert.Equal(commandBytes, File.ReadAllBytes(fixture.PathToSource));
        Assert.True(findings[1].IsFixed);
        Assert.False(findings[1].IsOpen);
        Assert.Same(findings[2], Assert.IsType<Button>(host.Window.FocusManager?.GetFocusedElement()).DataContext);

        findings[2].IsOpen = true;
        Assert.True(findings[2].FileChanged);
        Assert.False(findings[2].OffersFix);
        Assert.Null(findings[2].Preparation);
        Assert.Null(findings[2].ApplyFailure);
        Assert.True(findings[2].IsExcerptStale);
        Assert.True(findings[2].HasFixNotice);
        Assert.False(findings[2].ShowsNoFix);
        Assert.Equal("A fix changed this file after the review. Review again to prepare a fix for this finding.", findings[2].FixNoticeText);
        Assert.Equal("1 fix applied to your files, covering 2 findings. It is not committed yet: review again to update this verdict.", shell.Model.Review.AppliedFixesText);
        Assert.Equal(0, group.BlockCount);
        Assert.Equal(2, group.WarnCount);
        Assert.Equal(1, shell.Model.Review.BlockingCount);
        Assert.Equal("2 fixed", group.FixedText);
        var panelFile = Assert.Single(shell.Model.Review.PanelFiles);
        Assert.Same(group, panelFile.Group);
        Assert.Contains("0 blocking, 2 warning, 2 fixed", panelFile.SpokenName, StringComparison.Ordinal);
        Assert.Equal(2, fixture.Previews);
        Assert.Equal(commandBytes, File.ReadAllBytes(fixture.PathToSource));
    }

    [AvaloniaFact]
    public async Task ApplyMarksOnlyItsTreeAndFileCountsFollowTheRuleFilter()
    {
        using var scratch = new TestFolder();
        var fixture = new FixReviewFixture(scratch.Path);
        using var shell = new ShellHost(fileRules: FixReviewFixture.FileRules, fixes: fixture, parentFolder: scratch.Path);
        shell.Review.Result = _ => fixture.Result(multipleTrees: true);
        File.WriteAllBytes(fixture.PathToSource, FixReviewFixture.Before);
        var review = shell.Model.Review;
        review.SelectedChoice = review.Choices[2];
        await review.RunCommand.ExecuteAsync(null);
        review.RuleRows.Single(rule => rule.RuleId == "IDE0011").ChooseCommand.Execute(null);
        var group = Assert.Single(review.FileGroups);
        var fixable = group.Findings.Single(finding => finding.Line == 8);
        fixable.IsOpen = true;
        await fixable.Preparation!;
        await fixable.ApplyFixCommand.ExecuteAsync(null);

        Assert.Same(fixable, Assert.Single(group.Findings, finding => finding.IsFixed));
        Assert.All(group.Findings.Where(finding => finding != fixable), finding => Assert.True(finding.FileChanged));
        Assert.Equal("1 blocking, 1 warning, 1 fixed", group.SpokenCounts);
        Assert.Contains("covering 1 finding", review.AppliedFixesText, StringComparison.Ordinal);

        review.RuleRows.Single(rule => rule.RuleId == "IDE0055").ChooseCommand.Execute(null);
        group = Assert.Single(review.FileGroups);
        Assert.True(Assert.Single(group.Findings).FileChanged);
        Assert.Equal(0, group.FixedCount);
        Assert.Equal("0 blocking, 1 warning", group.SpokenCounts);

        review.ClearRuleCommand.Execute(null);
        Assert.Equal("1 blocking, 3 warning, 1 fixed", Assert.Single(review.FileGroups).SpokenCounts);
    }

    [Fact]
    public void EndOfFileReplacementCountsAsOnePlace()
    {
        const string patch = """
            @@ -1 +1 @@
            -class OldName {}
            \ No newline at end of file
            +class NewName {}
            """;
        var fix = new Fix(new RepositoryLocation("sample", "sample/.git"), FixReviewFixture.Source, "IDE0055",
            "class OldName {}"u8.ToArray(), "class NewName {}\n"u8.ToArray(), patch);
        var finding = Samples.Finding(fix.File, 1, fix.RuleId, Severity.Warn);

        var difference = FixDiff.Read(fix, finding, [finding], isFolderReview: true);

        Assert.Equal(2, difference.Count);
        Assert.Equal("This finding", Assert.Single(difference, line => line.HasPlaceLabel).PlaceLabel);
        Assert.Equal("The fix formats the whitespace of this whole file: 1 place (+1 −1).", fix.DescribeReach(folderReview: true));
    }
}
