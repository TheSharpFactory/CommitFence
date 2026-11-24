using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using CF.Tests.Review.Support;
using Xunit;

namespace CF.Tests.Review;

/// <summary>
/// The whole-folder review on real git, and the push whose answer the warm private copy and a cold rebuild must agree on.
/// These start git and real builds.
/// </summary>
public sealed partial class WholeRealGitTests
{
    private const string _library = """
        namespace Library;

        public static class Greeter
        {
            public static string Hello(string name)
            {
                return "Hello " + name;
            }

            public static int Old()
            {
                return 1;
            }
        }
        """;

    private const string _libraryWithoutOld = """
        namespace Library;

        public static class Greeter
        {
            public static string Hello(string name)
            {
                return "Hello " + name;
            }
        }
        """;

    private const string _consumer = """
        namespace Consumer;

        public static class Use
        {
            public static string Run()
            {
                return Library.Greeter.Hello("x");
            }

            public static int Legacy()
            {
        #if NET9_0
                return Library.Greeter.Old();
        #else
                return 0;
        #endif
            }
        }
        """;

    [Fact]
    public void WholeReviewUsesHeadAndPreservesLocalWork()
    {
        var breach = ReviewSources.Class("Core", ReviewSources.Add, ReviewSources.SignBad);
        using(var sandbox = new GitSandbox())
        {
            var repository = sandbox.CreateRepository("whole");
            repository.WriteProject(ReviewSources.Settings(ReviewSources.Team010), ("src/Core.cs", breach));
            repository.Commit("a committed breach");
            repository.LeaveWorkInProgress();
            repository.Write("src/Core.cs", ReviewSources.Class("Core", ReviewSources.Add, ReviewSources.SignOk));
            repository.Write("docs/staged.txt", "a staged edit\n");
            repository.Git("add", "docs/staged.txt");
            repository.Write("src/Extra.cs", ReviewSources.Class("Extra", ReviewSources.SignBad));
            var stateBefore = repository.CaptureDeveloperState();

            var review = sandbox.RunProgram(repository.Folder, "review", "--all");

            Assert.Equal(1, review.ExitCode);
            Assert.Contains($"src/Core.cs({ReviewSources.LineOf(breach, "if(x > 0)")},", review.Output, StringComparison.Ordinal);
            Assert.DoesNotContain("Extra.cs", review.Output, StringComparison.Ordinal);
            Assert.Equal(stateBefore, repository.CaptureDeveloperState());
        }
    }

    [Fact]
    public void WarmPushAndColdReviewFindTheSameBrokenConsumer()
    {
        Assert.SkipUnless(HasReferencePackOf("9."), "the targeting pack of net9.0 is not installed, and a review never fetches packages, so the fixture cannot target net9.0");
        using(var sandbox = new GitSandbox())
        {
            sandbox.InstallProgram();
            var repository = sandbox.CreateRepository("acceptance");
            repository.Write("Library/Library.csproj", ProjectOf("net9.0;net11.0", null));
            repository.Write("Consumer/Consumer.csproj", ProjectOf("net9.0;net11.0", "Library"));
            repository.Write("Library/Greeter.cs", _library + "\n");
            repository.Write("Consumer/Use.cs", _consumer + "\n");
            repository.Write("commitfence.json", ReviewSources.Settings(ReviewSources.Team010));
            repository.Write(".gitignore", "bin/\nobj/\n");
            repository.InstallTeamHook();
            repository.Commit("initial");
            repository.Git("push", "-q", "--no-verify", "origin", "main");

            // the warm-up: a clean push through the hook, so that the private copy and the kept answers exist
            repository.Write("Library/Extra.cs", "namespace Library;\n\npublic static class Extra\n{\n    public static int Two()\n    {\n        return 2;\n    }\n}\n");
            var warmTip = repository.Commit("a clean change that warms the private copy");
            var warm = sandbox.RunGit(repository.Folder, "push", "origin", "main");
            Assert.Equal(0, warm.ExitCode);
            Assert.Equal("refs/heads/main " + warmTip, repository.RemoteRefs());

            // the push under test renames a source file that the project picks up by wildcard and removes a member that an
            // untouched project uses on one of its two frameworks
            repository.Git("mv", "Library/Greeter.cs", "Library/Salutation.cs");
            repository.Write("Library/Salutation.cs", _libraryWithoutOld + "\n");
            var brokenTip = repository.Commit("Greeter.cs is renamed and Old() is removed");

            var push = sandbox.RunGit(repository.Folder, "push", "origin", "main");
            var cold = sandbox.RunProgramWith(repository.Folder, new Dictionary<string, string?> { ["COMMITFENCE_DATA_FOLDER"] = Path.Combine(sandbox.Root, "data-cold") }, "review", "--base", warmTip, "--head", brokenTip);

            var hookText = push.Output + push.Error;
            Assert.NotEqual(0, push.ExitCode);
            Assert.Equal("refs/heads/main " + warmTip, repository.RemoteRefs());
            var useLine = ReviewSources.LineOf(_consumer, "Greeter.Old()");
            // the whole text of the hook goes with a failure: on another system that is the only evidence there is
            Assert.True(System.Text.RegularExpressions.Regex.IsMatch(hookText, $@"(?m)^Consumer/Use\.cs\({useLine},\d+\): error CS0117: .*\[Consumer, net9\.0\]\s*$"), "the hook said: " + hookText);
            Assert.DoesNotContain("[Consumer, net11.0", hookText, StringComparison.Ordinal);
            Assert.Equal(1, cold.ExitCode);
            var hookFindings = FindingsIn(hookText);
            Assert.NotEmpty(hookFindings);
            Assert.Equal(FindingsIn(cold.Output), hookFindings);
        }
    }

    /// <summary>
    /// A finding as the program prints it: the file with its line and column, the severity, the rule and the message.
    /// </summary>
    [GeneratedRegex(@"^\S.*\(\d+,\d+\): (error|warning) \S+: .*$", RegexOptions.Multiline | RegexOptions.CultureInvariant)]
    private static partial Regex FindingPattern();

    private static List<string> FindingsIn(string text)
    {
        return FindingPattern().Matches(text).Select(match => match.Value.TrimEnd('\r')).Order(StringComparer.Ordinal).ToList();
    }

    private static string ProjectOf(string frameworks, string? reference)
    {
        var references = reference == null ? string.Empty : $"  <ItemGroup>\n    <ProjectReference Include=\"..\\{reference}\\{reference}.csproj\" />\n  </ItemGroup>\n";
        return $"<Project Sdk=\"Microsoft.NET.Sdk\">\n  <PropertyGroup>\n    <TargetFrameworks>{frameworks}</TargetFrameworks>\n    <Nullable>enable</Nullable>\n    <ImplicitUsings>enable</ImplicitUsings>\n  </PropertyGroup>\n{references}</Project>\n";
    }

    /// <summary>
    /// Whether the .NET installation this test runs on holds the reference assemblies for the framework: a review builds
    /// with no network, so a project can only target a framework whose pack is already there.
    /// </summary>
    private static bool HasReferencePackOf(string versionPrefix)
    {
        var dotnetRoot = Path.GetFullPath(Path.Combine(RuntimeEnvironment.GetRuntimeDirectory(), "..", "..", ".."));
        var packs = Path.Combine(dotnetRoot, "packs", "Microsoft.NETCore.App.Ref");

        return Directory.Exists(packs) && Directory.EnumerateDirectories(packs).Any(folder => Path.GetFileName(folder).StartsWith(versionPrefix, StringComparison.Ordinal));
    }
}
