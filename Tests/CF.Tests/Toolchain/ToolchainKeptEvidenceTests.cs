using System.Text;
using CF.Adapters.Machine;
using CF.Core.Code;
using CF.Core.Findings;
using CF.Core.Ports;
using CF.Tests.Toolchain.Support;
using Xunit;

namespace CF.Tests.Toolchain;

/// <summary>
/// What a review keeps of a compile between reviews, and when it uses it: a library that always changes and a consumer
/// of it that does not own a changed file, built by a script that says whether the build skipped the consumer.
/// </summary>
public sealed class ToolchainKeptEvidenceTests
{
    public static TheoryData<string> ChangesOfTheKey { get; } = new TheoryData<string>
    {
        "a source",
        "a source a target adds before the compile",
        "a reference",
        "an option",
        "a file given to an analyzer",
        "a property the compiler reads",
        "a rule set",
        "the framework",
        "the compiler version",
        "the analyzer version"
    };

    public static TheoryData<string, int> EvidenceThatCannotBeUsed { get; } = new TheoryData<string, int>
    {
        { "unreadable log", 2 },
        { "log without its closing entry", 2 },
        { "a kept log whose bytes changed", 2 },
        { "unreadable key", 2 },
        { "a compile that wrote its log somewhere else", 2 },
        { "a manifest cut short", 2 },
        { "failed compile", 1 }
    };

    private static List<string> Summary(IEnumerable<Finding> findings)
    {
        return findings.Select(finding => $"{finding.RuleId}@{finding.File}:{finding.Line}").Order(StringComparer.Ordinal).ToList();
    }

    private static List<string> Statuses(IEnumerable<Coverage> coverage)
    {
        return coverage.Select(entry => $"{entry.What}={entry.Status}").Order(StringComparer.Ordinal).ToList();
    }

    [Fact]
    public void SecondReviewOfUnchangedDependentStartsNoCompileOfIt()
    {
        using(var scenario = new Scenario())
        {
            var first = scenario.Run("Library/Greeter.cs");
            scenario.Skipped.Add("Consumer");
            scenario.Calls.Clear();

            var second = scenario.Run("Library/Greeter.cs");

            var consumerCall = Assert.Single(scenario.Calls, call => call.Project == "Consumer");
            Assert.True(consumerCall.OutputExisted, "the build was not made to compile again");
            Assert.NotEmpty(first.Findings);
            Assert.Equal(Summary(first.Findings), Summary(second.Findings));
            Assert.Equal(Statuses(first.Coverage), Statuses(second.Coverage));
            Assert.Equal(string.Empty, first.Coverage.Single(entry => entry.What == "Consumer (net9.0)").Reason);
            Assert.Contains("kept", second.Coverage.Single(entry => entry.What == "Consumer (net9.0)").Reason, StringComparison.Ordinal);
        }
    }

    [Theory]
    [MemberData(nameof(ChangesOfTheKey))]
    public void EachPartOfTheKeyMakesTheProjectCompileAgain(string change)
    {
        using(var scenario = new Scenario())
        {
            scenario.Run("Library/Greeter.cs");
            scenario.Skipped.Add("Consumer");
            scenario.Calls.Clear();
            var fileSystem = scenario.Change(change);

            var second = scenario.Run(fileSystem, "Library/Greeter.cs");

            var consumerCall = scenario.Calls.Last(call => call.Project == "Consumer");
            Assert.False(consumerCall.OutputExisted, "the build was made to compile again");
            var entry = second.Coverage.Single(candidate => candidate.What.StartsWith("Consumer", StringComparison.Ordinal));
            Assert.DoesNotContain("kept", entry.Reason, StringComparison.Ordinal);
            Assert.Equal(CoverageStatus.Failed, entry.Status);
        }
    }

    [Theory]
    [MemberData(nameof(EvidenceThatCannotBeUsed))]
    public void KeptEvidenceThatCannotBeTrustedIsNotUsed(string damage, int consumerBuilds)
    {
        using(var scenario = new Scenario())
        {
            if(damage == "failed compile")
                scenario.Answers["Consumer"] = scenario.CompileError("Consumer/Use.cs");

            scenario.Run("Library/Greeter.cs");
            scenario.Answers.Clear();
            scenario.Skipped.Add("Consumer");
            scenario.Calls.Clear();
            scenario.Damage(damage);
            if(damage == "a compile that wrote its log somewhere else")
                scenario.LogElsewhere.Add("Consumer");

            if(damage == "a manifest cut short")
                scenario.CutShort.Add("Consumer");

            var second = scenario.Run("Library/Greeter.cs");

            Assert.Equal(consumerBuilds, scenario.Calls.Count(call => call.Project == "Consumer"));
            var entry = second.Coverage.Single(candidate => candidate.What == "Consumer (net9.0)");
            Assert.Equal(CoverageStatus.Failed, entry.Status);
            Assert.DoesNotContain("kept", entry.Reason, StringComparison.Ordinal);
            Assert.DoesNotContain(second.Findings, finding => finding.RuleId == "IDE0044");
        }
    }

    [Fact]
    public void ProjectThatOwnsAChangedFileIsCompiledAlthoughEvidenceIsKept()
    {
        using(var scenario = new Scenario())
        {
            scenario.Run("Consumer/Use.cs");
            scenario.Calls.Clear();

            var second = scenario.Run("Consumer/Use.cs");

            var consumerCall = Assert.Single(scenario.Calls, call => call.Project == "Consumer");
            Assert.False(consumerCall.OutputExisted, "the build was made to compile again");
            var entry = second.Coverage.Single(candidate => candidate.What == "Consumer (net9.0)");
            Assert.Equal(CoverageStatus.Reviewed, entry.Status);
            Assert.Equal(string.Empty, entry.Reason);
        }
    }

    private sealed record Call(string Project, bool OutputExisted);

    private sealed class Scenario : IDisposable
    {
        private readonly FakeBuildFixture _fixture;
        private readonly Dictionary<string, string> _assemblies = new Dictionary<string, string> { ["Library"] = "library surface", ["Consumer"] = "consumer surface" };
        private string _nullable = "enable";
        private bool _hasRuleSet;

        public Scenario()
        {
            var library = Project("Library", "net9.0", "Library/Greeter.cs");
            var consumer = Project("Consumer", "net9.0", "Consumer/Use.cs", "Library");
            _fixture = new FakeBuildFixture(TreeFiles.NoRules, library, consumer);
            _fixture.Dotnet.BuildAnswerFor = Answer;
            _fixture.Dotnet.ManifestAdditions = ManifestOf;
            _fixture.Dotnet.LogIsAsked = call => !LogElsewhere.Contains(call.Project);
            _fixture.Dotnet.ManifestIsClosed = call => !CutShort.Contains(call.Project);
            TreeFiles.Write(_fixture.Tree, "Consumer/.editorconfig", "root = true\n");
            TreeFiles.Write(_fixture.Tree, "Consumer/rules.txt", "rules for an analyzer\n");
            TreeFiles.Write(_fixture.Tree, "Consumer/obj/Generated.cs", "namespace N;\n");
            Answers["Consumer"] = new BuildAnswer(0, new SarifLogText().Rule("IDE0044", "suggestion", false).Result("IDE0044", "warning", "Make field readonly.", _fixture.FullPath("Consumer/Use.cs"), 3, 5).Closing("Consumer", 1).ToString());
        }

        public HashSet<string> Skipped { get; } = new HashSet<string>();

        public HashSet<string> LogElsewhere { get; } = new HashSet<string>();

        public HashSet<string> CutShort { get; } = new HashSet<string>();

        public Dictionary<string, BuildAnswer> Answers { get; } = new Dictionary<string, BuildAnswer>();

        public List<Call> Calls { get; } = new List<Call>();

        public ToolchainResult Run(string changedFile)
        {
            return _fixture.RunToolchain(ReviewHarness.Changed(changedFile));
        }

        public ToolchainResult Run(IFileSystem fileSystem, string changedFile)
        {
            return _fixture.RunToolchain(fileSystem, ReviewHarness.Changed(changedFile));
        }

        public BuildAnswer CompileError(string file)
        {
            var log = new SarifLogText().Rule("CS0103", "error", true).Result("CS0103", "error", "The name 'missing' does not exist in the current context.", _fixture.FullPath(file), 7, 5).Closing("Consumer", 1);
            return new BuildAnswer(1, log.ToString());
        }

        /// <summary>
        /// Changes one thing the compile reads and returns the file system the next run reads through.
        /// </summary>
        public IFileSystem Change(string change)
        {
            switch(change)
            {
                case "a source":
                    TreeFiles.Write(_fixture.Tree, "Consumer/Use.cs", "namespace N;\n\npublic class Other\n{\n}\n");
                    break;
                case "a reference":
                    _assemblies["Library"] = "library with a new public type";
                    break;
                case "a source a target adds before the compile":
                    TreeFiles.Write(_fixture.Tree, "Consumer/obj/Generated.cs", "namespace N;\n\npublic class Generated\n{\n}\n");
                    break;
                case "an option":
                    TreeFiles.Write(_fixture.Tree, "Consumer/.editorconfig", "root = true\n\n[*.cs]\ndotnet_diagnostic.IDE0044.severity = warning\n");
                    break;
                case "a file given to an analyzer":
                    TreeFiles.Write(_fixture.Tree, "Consumer/rules.txt", "other rules for an analyzer\n");
                    break;
                case "a rule set":
                    _hasRuleSet = true;
                    break;
                case "a property the compiler reads":
                    _nullable = "disable";
                    break;
                case "the framework":
                    _fixture.Dotnet.Replace(Project("Consumer", "net11.0", "Consumer/Use.cs", "Library"));
                    break;
                case "the compiler version":
                    _fixture.Dotnet.VersionAnswer = new ProcessResult(0, "11.0.200\n", string.Empty);
                    break;
                case "the analyzer version":
                    return new AnalyzerSwappedFileSystem();
            }

            return new SystemFileSystem();
        }

        /// <summary>
        /// Spoils what was kept, the way a crash, a full disk or a hand would.
        /// </summary>
        public void Damage(string damage)
        {
            foreach(var path in Directory.GetFiles(Path.Combine(_fixture.Data, "csharp", "evidence"), "*", SearchOption.AllDirectories))
            {
                if(damage == "unreadable log" && path.EndsWith(".sarif", StringComparison.Ordinal))
                    File.WriteAllText(path, "{ not a log");
                else if(damage == "log without its closing entry" && path.EndsWith(".sarif", StringComparison.Ordinal))
                    File.WriteAllText(path, new SarifLogText().ToString());
                else if(damage == "a kept log whose bytes changed" && path.EndsWith(".sarif", StringComparison.Ordinal))
                    File.AppendAllText(path, "\n");
                else if(damage == "unreadable key" && path.EndsWith(".key.json", StringComparison.Ordinal))
                    File.WriteAllText(path, "not json");
            }
        }

        public void Dispose()
        {
            _fixture.Dispose();
        }

        /// <summary>
        /// What the build's target lists besides the sources and the references: the files the compiler is given for the
        /// consumer, and a property.
        /// </summary>
        private List<string> ManifestOf(BuildCall call)
        {
            var lines = new List<string> { "prop|Nullable=" + _nullable };
            if(_hasRuleSet)
                lines.Add("nokey|ruleset");

            if(call.Project == "Consumer")
            {
                lines.Add("editorconfig|" + _fixture.FullPath("Consumer/.editorconfig"));
                lines.Add("additional|" + _fixture.FullPath("Consumer/rules.txt"));
                lines.Add("source|" + _fixture.FullPath("Consumer/obj/Generated.cs"));
            }

            return lines;
        }

        private static FakeProject Project(string name, string framework, string source, params string[] references)
        {
            return new FakeProject(name + "/" + name + ".csproj", new FakeUnit(framework, new[] { source }, references.Select(reference => reference + "/" + reference + ".csproj").ToList(), null, name + "/obj/Debug/" + framework + "/" + name + ".dll"));
        }

        private BuildAnswer? Answer(BuildCall call)
        {
            var output = _fixture.FullPath(call.Project + "/obj/Debug/" + call.Framework + "/" + call.Project + ".dll");
            Calls.Add(new Call(call.Project, File.Exists(output)));
            if(Skipped.Contains(call.Project))
                return new BuildAnswer(0, null);

            Directory.CreateDirectory(Path.GetDirectoryName(output)!);
            File.WriteAllText(output, _assemblies[call.Project]);

            return Answers.GetValueOrDefault(call.Project);
        }
    }

    /// <summary>
    /// The real file system, except that the analyzer of ours is another build of it.
    /// </summary>
    private sealed class AnalyzerSwappedFileSystem : IFileSystem
    {
        private readonly SystemFileSystem _inner = new SystemFileSystem();

        public bool FileExists(string path)
        {
            return _inner.FileExists(path);
        }

        public bool FolderExists(string path)
        {
            return _inner.FolderExists(path);
        }

        public string ReadAllText(string path)
        {
            return _inner.ReadAllText(path);
        }

        public Stream OpenRead(string path)
        {
            return path.EndsWith("analyzers/CF.Meaning.dll", StringComparison.Ordinal) || path.EndsWith("analyzers\\CF.Meaning.dll", StringComparison.Ordinal) ? new MemoryStream(Encoding.UTF8.GetBytes("another build of the analyzer")) : _inner.OpenRead(path);
        }

        public void WriteAllText(string path, string content)
        {
            _inner.WriteAllText(path, content);
        }

        public void WriteNewFile(string path, string content)
        {
            _inner.WriteNewFile(path, content);
        }

        public void ReplaceFile(string source, string destination)
        {
            _inner.ReplaceFile(source, destination);
        }

        public void CreateFolder(string path)
        {
            _inner.CreateFolder(path);
        }

        public void DeleteFile(string path)
        {
            _inner.DeleteFile(path);
        }

        public IReadOnlyList<string> FindFiles(string folder, string extension, IReadOnlyList<string> skippedFolderNames)
        {
            return _inner.FindFiles(folder, extension, skippedFolderNames);
        }

        public string ResolveRealPath(string path)
        {
            return _inner.ResolveRealPath(path);
        }
    }
}