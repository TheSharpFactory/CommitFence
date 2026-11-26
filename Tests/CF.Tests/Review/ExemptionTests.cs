using CF.Adapters.Git;
using CF.Adapters.Machine;
using CF.Cli.Commands;
using CF.Core.Code;
using CF.Core.Review;
using CF.Core.Services;
using CF.Tests.Review.Support;
using CF.Tests.Toolchain.Support;
using Xunit;

namespace CF.Tests.Review;

/// <summary>The scan of the last commit for the lines that set a rule aside, the counting both faces share, and the command that lists them.</summary>
public sealed class ExemptionTests
{
    private const string _first = """
        namespace Shop;

        public class Orders
        {
            // commitfence:allow TEAM010: generated code keeps its braces
            public void A() { if(true) { Run(); } }

            // commitfence:allow TEAM011
            public void B() { }

        #pragma warning disable CA1822 // kept for the plug-in contract
            public void C() { }
        #pragma warning restore CA1822

            [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "The caller reports it")]
            public void D() { }

        #pragma warning disable
            public void E() { }
        }
        """;

    [Fact]
    public void ScanReadsTheThreeFormsFromTheLastCommitWithTheLinesAroundAndSaysWhenThereIsNoCommit()
    {
        using var sandbox = new GitSandbox();
        var data = new TestDataFolder(sandbox.DataFolder);
        var service = new ExemptionService(new GitCommandLine(sandbox.Runner, data), [new TestLanguage()]);
        var repository = sandbox.CreateRepository("shop");

        // no commit yet is an answer of its own
        var empty = service.Scan(repository.Folder, true, CancellationToken.None);
        Assert.True(empty.HasNoCommit);
        Assert.Equal("Exemptions are read from the last commit, and main has none.", empty.NoCommitSentence);

        repository.Write("src/Orders.cs", _first);
        repository.Commit("first");

        // the working file changes after the commit: the scan reads the commit
        repository.Write("src/Orders.cs", "// commitfence:allow TEAM099: only in the working folder\n" + _first.Replace("TEAM010", "TEAM777", StringComparison.Ordinal));
        var scan = service.Scan(Path.Combine(repository.Folder, "src"), true, CancellationToken.None);

        // asked from a subfolder, the scan says where the top of the working tree is (git names the folder behind a link): the paths below are relative to it
        var realFiles = new SystemFileSystem();
        Assert.Equal(realFiles.ResolveRealPath(repository.Folder), realFiles.ResolveRealPath(scan.WorkFolder!));
        Assert.StartsWith("In the last commit of main, ", scan.HeadLine, StringComparison.Ordinal);
        Assert.Equal(7, scan.Commit!.Length);
        Assert.Equal(
            [
                (5, "TEAM010", ExemptionForm.AllowComment, "generated code keeps its braces", false),
                (8, "TEAM011", ExemptionForm.AllowComment, null, true),
                (11, "CA1822", ExemptionForm.Pragma, "kept for the plug-in contract", false),
                (15, "CA1031", ExemptionForm.SuppressMessage, "The caller reports it", false),
                (18, "all warnings", ExemptionForm.Pragma, null, false)
            ],
            scan.Exemptions.Select(entry => (entry.Line, entry.RuleId, entry.Form, entry.Reason, entry.IsFinding)));

        // three lines before and three after, from the commit; a line near the start has fewer before it
        var first = scan.Exemptions[0];
        Assert.Equal([2, 3, 4, 5, 6, 7, 8], first.Lines.Select(line => line.Number));
        Assert.Equal("    // commitfence:allow TEAM010: generated code keeps its braces", first.Lines[3].Text);
        Assert.All(scan.Exemptions, entry => Assert.Equal("src/Orders.cs", entry.File));

        // the command never shows the lines, so a scan made without them does not cut them
        var bare = service.Scan(repository.Folder, false, CancellationToken.None);
        Assert.Equal(5, bare.Exemptions.Count);
        Assert.All(bare.Exemptions, entry => Assert.Empty(entry.Lines));
    }

    [Fact]
    public void SuppressMessageRowsKeepConstantAndUnresolvedIds()
    {
        using var folder = new TestFolder();
        var git = new FakeGit(folder.Path);
        git.AddCommit("one", new Dictionary<string, string>
        {
            ["src/Orders.cs"] = """
                class Orders
                {
                    const string DiagnosticId = "CA" + "1822:Static members";
                    [SuppressMessage("Performance", DiagnosticId, Justification = "unqualified")]
                    [SuppressMessage("Performance", Orders.DiagnosticId, Justification = "qualified")]
                    [SuppressMessage("Performance", Outside.Id, Justification = "external")]
                    [SuppressMessage("Performance", "", Justification = "empty")]
                    [SuppressMessage]
                    [SuppressMessage("Performance", "CA" + "1822", Justification = "joined")]
                    void Order() { }
                }
                """,
            ["src/Outside.cs"] = """class Outside { public const string Id = "CA1000"; }"""
        });
        git.Name("HEAD", "one");
        var service = new ExemptionService(git, [new TestLanguage()]);

        var result = Run(service, folder.Path, "exemptions", "list");

        Assert.Equal(ExitCode.Passed, result.ExitCode);
        Assert.Contains("line 4  CA1822  [SuppressMessage]  unqualified", result.Output, StringComparison.Ordinal);
        Assert.Contains("line 5  CA1822  [SuppressMessage]  qualified", result.Output, StringComparison.Ordinal);
        Assert.Contains("line 6  (not a literal)  [SuppressMessage]  external", result.Output, StringComparison.Ordinal);
        Assert.Contains("line 7  (not a literal)  [SuppressMessage]  empty", result.Output, StringComparison.Ordinal);
        Assert.Contains("line 8  (not a literal)  [SuppressMessage]  No reason given", result.Output, StringComparison.Ordinal);
        Assert.Contains("line 9  CA1822  [SuppressMessage]  joined", result.Output, StringComparison.Ordinal);
    }

    [Fact]
    public void CountingGroupsByFileAndRuleAndNamesTheFolderThatHoldsMostOfARule()
    {
        var entries = new List<ExemptionEntry>
        {
            Entry("src/Updates/A.cs", 1, "TEAM010", false),
            Entry("src/Updates/A.cs", 9, "TEAM010", true),
            Entry("src/Updates/Deep/B.cs", 2, "TEAM010", false),
            Entry("src/Updates/Deep/B.cs", 4, "TEAM010", false),
            Entry("src/Other/C.cs", 3, "TEAM010", false),
            Entry("src/Other/C.cs", 3, "CA1822", false),
            Entry("src/Other/C.cs", 8, "CA1031", false),
            Entry("D.cs", 1, "CA1031", false)
        };
        var counts = new ExemptionCounts(entries);

        // by file, the most first, equal ones by path; by rule, the most first, with the ones that give no reason
        Assert.Equal(["src/Other/C.cs", "src/Updates/A.cs", "src/Updates/Deep/B.cs", "D.cs"], counts.Files.Select(file => file.Path));
        Assert.Equal([("TEAM010", 5, 1), ("CA1031", 2, 0), ("CA1822", 1, 0)], counts.Rules.Select(rule => (rule.RuleId, rule.Count, rule.WithoutReason)));

        // a sentence that says lines counts distinct lines: one line naming two rules is one line; the entries are still eight
        Assert.Equal(8, counts.Entries);
        Assert.Equal("7 lines set 3 rules aside, in 4 files", counts.Summary);
        Assert.Equal("1 has no reason", counts.WithoutReasonButton);
        Assert.Equal("1 line sets 1 rule aside, in 1 file", new ExemptionCounts([entries[0]]).Summary);

        // the deepest folder that holds more than half: four of the five are under src/Updates, and only two under Deep
        var teams = entries.Where(entry => entry.RuleId == "TEAM010").ToList();
        Assert.Equal("5 lines set it aside, 4 of them in src/Updates.", ExemptionCounts.SetAside(teams, "it"));

        // no folder holds more than half of two lines in two folders: the sentence ends after "aside"
        Assert.Equal("2 lines set this rule aside.", ExemptionCounts.SetAside(entries.Where(entry => entry.RuleId == "CA1031").ToList(), "this rule"));
    }

    [Fact]
    public void TheCommandListsByFileNarrowsToOneRuleAndSaysWhenThereAreNone()
    {
        using var folder = new TestFolder();
        var git = new FakeGit(folder.Path);
        git.AddCommit("one", new Dictionary<string, string> { ["src/Orders.cs"] = _first, ["README.md"] = "// commitfence:allow TEAM010: not code" });
        git.Name("HEAD", "one");
        var service = new ExemptionService(git, [new TestLanguage()]);

        var all = Run(service, folder.Path, "exemptions", "list");
        Assert.Equal(ExitCode.Passed, all.ExitCode);
        var lines = all.Output.ReplaceLineEndings("\n").Split('\n');
        Assert.Equal($"In the last commit of main, {FakeGit.IdOf("one")[..7]}", lines[0]);
        Assert.Equal("5 lines set 5 rules aside, in 1 file. 1 has no reason", lines[2]);
        Assert.Equal("src/Orders.cs", lines[4]);
        Assert.Equal(
            [
                "  line 5  TEAM010  generated code keeps its braces",
                "  line 8  TEAM011  No reason given (a finding)",
                "  line 11  CA1822  #pragma warning disable  kept for the plug-in contract",
                "  line 15  CA1031  [SuppressMessage]  The caller reports it",
                "  line 18  all warnings  #pragma warning disable  No reason given"
            ],
            lines.Skip(5).Take(5));

        var narrowed = Run(service, folder.Path, "exemptions", "list", "--rule", "CA1822");
        Assert.Contains("1 line sets 1 rule aside, in 1 file", narrowed.Output, StringComparison.Ordinal);
        Assert.DoesNotContain("TEAM010", narrowed.Output, StringComparison.Ordinal);

        // none: the empty state's sentence with its example, and the exit code stays 0
        git.AddCommit("two", new Dictionary<string, string> { ["src/Orders.cs"] = "namespace Shop;" });
        git.Name("HEAD", "two");
        var none = Run(service, folder.Path, "exemptions", "list");
        Assert.Equal(ExitCode.Passed, none.ExitCode);
        Assert.Contains(ExemptionWords.EmptyTitle, none.Output, StringComparison.Ordinal);
        Assert.Contains("No line in the last commit sets a rule aside. When one has to, name the rule and say why, on the line or the line above:", none.Output, StringComparison.Ordinal);
        Assert.Contains("// commitfence:allow TEAM001: generated code keeps its braces", none.Output, StringComparison.Ordinal);
    }

    private static ExemptionEntry Entry(string file, int line, string rule, bool isFinding)
    {
        return new ExemptionEntry(file, line, rule, ExemptionForm.AllowComment, isFinding ? null : "because", isFinding, []);
    }

    private static CommandOutcome Run(ExemptionService service, string folder, params string[] arguments)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var environment = new CommandEnvironment(folder, folder, Path.Combine(folder, "commitfence"), new StringReader(string.Empty), output, error, false);
        var exitCode = new CommandRunner(null!, null!, null!, null!, null!, environment, exemptions: service).Run(arguments);
        return new CommandOutcome(exitCode, output.ToString(), error.ToString());
    }
}
