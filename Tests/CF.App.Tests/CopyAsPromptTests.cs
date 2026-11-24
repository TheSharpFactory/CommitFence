using Avalonia.Headless.XUnit;
using CF.App.Review;
using CF.App.Tests.Support;
using CF.Core.Findings;
using CF.Core.Prompts;
using CF.Core.Review;
using Xunit;

namespace CF.App.Tests;

/// <summary>
/// The two buttons of the Review screen that put a prompt for an AI coding agent on the clipboard: what they copy is what the
/// body's writer gives, and they say "Copied" until the time is up.
/// </summary>
public sealed class CopyAsPromptTests
{
    private const string _file = "src/Billing/InvoiceTotals.cs";
    private const string _source = "namespace Billing;\n\npublic sealed class InvoiceTotals\n{\n    public int Total(int[] lines)\n    {\n        var sum = 0;\n        foreach(var line in lines)\n            if(line > 0) { sum += line; }\n\n        return sum;\n    }\n}\n";

    [AvaloniaFact]
    public async Task CopyAsAPromptPutsTheWritersTextForThatFindingOnTheClipboard()
    {
        using var host = new ReviewHost();
        var clipboard = new FakeClipboard();
        host.Model.Clipboard = clipboard;
        var result = Result(Samples.Finding(_file, 9, "TEAM001", Severity.Block, "No braces around a single-line if"));
        host.Review.Result = _ => result;
        await host.RunAsync();

        var finding = host.Model.FileGroups.Single().Findings.Single();
        await finding.Copy.CopyCommand.ExecuteAsync(null);

        Assert.Equal(AgentPrompt.ForFinding(result.Findings[0], RuleOf(host, "TEAM001"), _source), clipboard.Text);
        Assert.Equal("Copied", finding.Copy.SpokenName);
        finding.Copy.EndCopied();
        Assert.Equal("Copy as a prompt", finding.Copy.SpokenName);
    }

    [AvaloniaFact]
    public async Task CopyAllAsAPromptPutsThePromptForTheWholeReviewOnTheClipboardAndIsAbsentOnACleanReview()
    {
        using var host = new ReviewHost();
        var clipboard = new FakeClipboard();
        host.Model.Clipboard = clipboard;
        await host.RunAsync();
        Assert.False(host.Model.CanCopyAll);

        var result = Result(Samples.Finding(_file, 9, "TEAM001", Severity.Block, "No braces around a single-line if"), Samples.Finding(_file, 5, "TEAM002", Severity.Warn, "A method needs a documentation comment."));
        host.Review.Result = _ => result;
        await host.RunAsync();
        await host.Model.CopyAll.CopyCommand.ExecuteAsync(null);

        var rules = new Dictionary<string, PromptRule> { ["TEAM001"] = RuleOf(host, "TEAM001"), ["TEAM002"] = RuleOf(host, "TEAM002") };
        Assert.True(host.Model.CanCopyAll);
        Assert.Equal(AgentPrompt.ForReview(result.Findings, result.Sources, rules, false), clipboard.Text);
        Assert.Equal("Copied", host.Model.CopyAll.SpokenName);
    }

    private static ReviewResult Result(params Finding[] findings)
    {
        return Samples.Result(ReviewOutcome.Blocked, findings, new[] { Samples.Reviewed(_file) }, new Dictionary<string, string> { [_file] = _source });
    }

    private static PromptRule RuleOf(ReviewHost host, string id)
    {
        return PromptRule.Of(host.Rules.ShowRule(Path.Combine(host.RepositoryPath, "commitfence.json"), id)!);
    }
}