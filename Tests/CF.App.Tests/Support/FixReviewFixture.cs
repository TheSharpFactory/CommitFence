using System.Text;
using CF.Adapters.Git;
using CF.Adapters.Machine;
using CF.Adapters.Storage;
using CF.App.Tests.Support;
using CF.Core.Findings;
using CF.Core.Fixes;
using CF.Core.Ports;
using CF.Core.Review;
using CF.Core.Rules;
using CF.Core.Services;

namespace CF.Integration.Tests;

/// <summary>A shared preview and real file writer for command-line and headless desktop tests.</summary>
internal sealed class FixReviewFixture : IFixService
{
    public const string Source = "InvoiceTotals.cs";
    private const string _before = """
        namespace Billing;
        public static class InvoiceTotals
        {
            public static decimal ApplyCredit(decimal amount)
            {
                if (amount < 0) return 0;
                var maximum = 250m;
                if (amount > maximum) return maximum;
                return amount;
            }
        }
        """;
    private const string _after = """
        namespace Billing;
        public static class InvoiceTotals
        {
            public static decimal ApplyCredit(decimal amount)
            {
                if (amount < 0)
                {
                    return 0;
                }
                var maximum = 250m;
                if (amount > maximum)
                {
                    return maximum;
                }
                return amount;
            }
        }
        """;
    private const string _patch = """
        diff --git a/InvoiceTotals.cs b/InvoiceTotals.cs
        --- a/InvoiceTotals.cs
        +++ b/InvoiceTotals.cs
        @@ -3,9 +3,15 @@
         {
             public static decimal ApplyCredit(decimal amount)
             {
        -        if (amount < 0) return 0;
        +        if (amount < 0)
        +        {
        +            return 0;
        +        }
                 var maximum = 250m;
        -        if (amount > maximum) return maximum;
        +        if (amount > maximum)
        +        {
        +            return maximum;
        +        }
                 return amount;
             }
         }
        """;

    private readonly FixService _apply;
    private readonly RepositoryLocation _repository;
    private int _previews;

    public FixReviewFixture(string scratch)
    {
        var data = new FixedDataFolder(Path.Combine(scratch, "data"));
        var folder = Path.Combine(scratch, "invoicing");
        _repository = new RepositoryLocation(folder, Path.Combine(folder, ".git"));
        _apply = new FixService([], new GitSnapshotStore(new NoProcessRunner(), data), new FileReviewLock(data), new SystemFileSystem());
    }

    public ManualResetEventSlim? Gate { get; init; }
    public int Previews
    {
        get { return Volatile.Read(ref _previews); }
    }

    public string PathToSource
    {
        get { return Path.Combine(_repository.WorkFolder, Source); }
    }

    public static byte[] Before
    {
        get { return WithMark(_before); }
    }

    public static byte[] After
    {
        get { return WithMark(_after); }
    }

    public static IReadOnlyList<CF.Core.Code.FileRule> FileRules { get; } =
        [
            RulesHost.FileRule("IDE0011", "Add braces", CF.Core.Code.FileRuleState.On, Severity.Block, 4),
            RulesHost.FileRule("IDE0055", "Fix formatting", CF.Core.Code.FileRuleState.On, Severity.Warn, 5)
        ];

    public ReviewResult Result(bool multipleTrees = false)
    {
        var first = Samples.Finding(Source, 6, "IDE0011", Severity.Block, "Add braces to the if statement.", FindingSource.EditorConfig);
        var second = Samples.Finding(Source, 8, "IDE0011", Severity.Warn, "Add braces to this if statement.", FindingSource.EditorConfig);
        var formatting = Samples.Finding(Source, 4, "IDE0055", Severity.Warn, "Fix the spacing around this declaration.", FindingSource.EditorConfig);
        var team = Samples.Finding(Source, 4, "TEAM002", Severity.Warn, "A method needs a documentation comment.");
        var findings = new List<Finding> { first, second, formatting, team };
        var run = new ReviewRun { Tree = "tree" };
        run.AddFindings(findings);
        run.ApplyPolicies(Strictness.Standard);
        if(multipleTrees)
        {
            var other = Samples.Finding(Source, 10, "IDE0011", Severity.Warn, "Add braces on the other ref.", FindingSource.EditorConfig);
            findings.Add(other);
            run.Tree = "other tree";
            run.AddFindings([first, other]);
            run.ApplyPolicies(Strictness.Standard);
        }

        return new ReviewResult(ReviewOutcome.Blocked, findings, [Samples.Reviewed(Source)], [], string.Empty, TimeSpan.FromSeconds(1),
            new Dictionary<string, string> { [Source] = _before })
        {
            Repository = _repository,
            FindingTrees = run.ToResult(TimeSpan.Zero).FindingTrees
        };
    }

    public FixAnswer SuggestFix(FixRequest request)
    {
        Interlocked.Increment(ref _previews);
        Gate?.Wait();
        return request.Finding.RuleId == "IDE0011"
            ? new FixAnswer(new Fix(request.Repository, Source, "IDE0011", Before, After, _patch))
            : new FixAnswer();
    }

    public FixApplied ApplyFix(Fix fix)
    {
        return _apply.ApplyFix(fix);
    }

    private static byte[] WithMark(string text)
    {
        return [.. Encoding.UTF8.Preamble, .. Encoding.UTF8.GetBytes(text.ReplaceLineEndings("\r\n"))];
    }
}
