using CF.Adapters.Machine;
using CF.Adapters.Storage;
using CF.Core.Changes;
using CF.Core.Code;
using CF.Core.Ports;
using CF.Core.Review;
using CF.Core.Services;
using CF.Tests.Review.Support;
using CF.Tests.Toolchain.Support;
using Xunit;
using ReviewHarness = CF.Tests.Review.Support.ReviewHarness;

namespace CF.Tests.Review;

/// <summary>The last review that judged structure, through the same review path the programs use.</summary>
public sealed class StructureJudgmentTests
{
    private const string _file = "src/App/Caller.cs";
    private const string _settings = """
        {
          "structure": {
            "modules": {"app":["folder:src/App/**"],"web":["folder:src/Web/**"],"store":["namespace:Demo.Store"]},
            "directions": {"allow":["store -> app"]}
          }
        }
        """;

    [Theory]
    [InlineData(false, false, null)]
    [InlineData(false, true, null)]
    [InlineData(true, false, null)]
    [InlineData(false, false, "src/App/Original.cs")]
    [InlineData(false, false, "src/Web/Original.cs")]
    public void CompletedJudgmentSurvivesOtherReviews(bool wholeFolder, bool failWrite, string? originalPath)
    {
        using var harness = new ReviewHarness();
        var files = new SystemFileSystem();
        var data = new TestDataFolder(Path.Combine(harness.Scratch, "judgments"));
        if(failWrite)
            File.WriteAllText(data.RootFolder, "A directory cannot be made here.");

        var judgments = new FileStructureJudgments(data, files);
        harness.StructureJudgments = judgments;
        var before = originalPath == null ? FakeToolchain.CompleteAnswers() : Answers(originalPath);
        var after = Answers(_file);
        var toolchain = new FakeToolchain { Answer = _ => new ToolchainResult([], [], [], after, []) };
        harness.Languages = new List<ILanguage> { new TestLanguage(toolchain, new FakeMeaningSource(before)) };
        var source = "namespace Demo.App;\n\nclass Caller { }\n";
        harness.Fake.AddCommit("before", new Dictionary<string, string> { ["commitfence.json"] = _settings, [originalPath ?? _file] = source });
        harness.Fake.AddCommit("after", new Dictionary<string, string> { ["commitfence.json"] = _settings, [_file] = source + "\n" }, "before");
        harness.Fake.Name("HEAD", "after");
        if(originalPath != null)
            harness.Fake.DiffAnswer = (_, _) => $"diff --git a/{originalPath} b/{_file}\nsimilarity index 100%\nrename from {originalPath}\nrename to {_file}\n";

        TreeFiles.Write(harness.RepositoryFolder, "commitfence.json", _settings);
        TreeFiles.Write(harness.RepositoryFolder, _file, source);
        var service = new StructureService(files, new FileSettingsLock(data), harness.Languages, harness.Git, judgments);
        Assert.Null(service.Show(harness.RepositoryFolder).LastJudgment);

        var scope = wholeFolder ? ReviewScope.ForWholeFolder(Path.Combine(harness.RepositoryFolder, "src", "App")) : ReviewScope.ForRange("before", "after");
        var review = harness.Service().ReviewChanges(harness.RepositoryFolder, scope, null);
        if(originalPath == "src/App/Original.cs")
        {
            Assert.Equal(ExitCode.Passed, review.ExitCode);
            Assert.Empty(review.Findings);
            Assert.Empty(Assert.IsType<StructureJudgment>(service.Show(harness.RepositoryFolder).LastJudgment).Breaches);
            return;
        }

        Assert.Equal(ExitCode.Blocked, review.ExitCode);
        Assert.Equal("ARCH001", Assert.Single(review.Findings).RuleId);

        var report = service.Show(harness.RepositoryFolder);
        if(failWrite)
        {
            Assert.Null(report.LastJudgment);
            return;
        }

        var recorded = Assert.IsType<StructureJudgment>(report.LastJudgment);
        var breach = Assert.Single(recorded.Breaches);
        Assert.Equal(new DateTimeOffset(2026, 11, 24, 12, 0, 0, TimeSpan.Zero), recorded.EndedAt);
        Assert.Equal(wholeFolder ? "src/App" : null, recorded.Folder);
        Assert.Equal(new StructureBreach("app", "store", "Caller.Run", "Secret", _file, 4), breach);
        Assert.Equal("app may not use store. Caller.Run uses Secret, in src/App/Caller.cs, line 4.", breach.Sentence);
        Assert.Equal("1 use breaks a direction", report.JudgmentSentence(recorded.EndedAt));
        var originalFile = Assert.Single(Directory.GetFiles(data.RootFolder, "*.json", SearchOption.AllDirectories));
        var original = File.ReadAllText(originalFile);

        harness.Service().ReviewFiles(Path.Combine(harness.RepositoryFolder, "commitfence.json"),
            new List<FileToReview> { new FileToReview(_file, Path.Combine(harness.RepositoryFolder, _file)) });
        Assert.Equal(original, File.ReadAllText(originalFile));
        toolchain.Answer = _ => new ToolchainResult([], [], [], MeaningAnswers.Missing("csharp", "Incomplete build"), []);
        var incomplete = harness.Service().ReviewChanges(harness.RepositoryFolder, scope, null);
        Assert.Equal(ExitCode.Incomplete, incomplete.ExitCode);
        Assert.Equal(original, File.ReadAllText(originalFile));

        toolchain.Answer = _ => FakeToolchain.Nothing();
        var clean = harness.Service().ReviewChanges(harness.RepositoryFolder, scope, null);
        Assert.Equal(ExitCode.Passed, clean.ExitCode);
        var cleanReport = service.Show(harness.RepositoryFolder);
        Assert.Empty(Assert.IsType<StructureJudgment>(cleanReport.LastJudgment).Breaches);
        Assert.Equal($"Nothing breaks them in the last review of {(wholeFolder ? "src/App" : "the changes")}, today 12:00",
            cleanReport.JudgmentSentence(recorded.EndedAt));
    }

    private static MeaningAnswers Answers(string path)
    {
        var use = new MeaningUse(path, "Caller.Run", "Demo.App", "App", "Secret.Open", "Secret", "Demo.Store", "Store", "src/Store/Secret.cs",
            new List<SourcePoint> { new SourcePoint(4, 5) });
        return new MeaningAnswers("csharp", [], new List<MeaningUse> { use }, [], [], []);
    }
}
