using CF.Adapters.Machine;
using CF.Core.Changes;
using CF.Core.Code;
using CF.Core.Findings;
using CF.Core.Ports;
using CF.Core.Review;
using CF.Core.Rules.Settings;
using CF.Languages.CSharp;
using CF.Tests.Toolchain.Support;

namespace CF.Tests.Meaning.Support;

/// <summary>
/// The rules of the team that need to know what the code refers to, as the tests write them into a settings file.
/// </summary>
internal static class MeaningSettings
{
    public const string MethodAccessRule = """
        {
            "id": "TEAM011",
            "language": "csharp",
            "for": "method",
            "severity": "block",
            "title": "Only public and private methods inside an internal class",
            "where": { "containingType": "class", "containingTypeDeclaredAccess": "internal", "isOverride": false },
            "require": { "declaredAccess": ["public", "private"] }
        }
        """;

    public const string ClockRule = """
        {
            "id": "TEAM012",
            "language": "csharp",
            "for": "usage",
            "severity": "block",
            "title": "Use IClock, not DateTime.Now",
            "where": { "target": "System.DateTime.Now" },
            "forbid": true
        }
        """;

    public const string FileApiRule = """
        {
            "id": "TEAM013",
            "language": "csharp",
            "for": "usage",
            "severity": "block",
            "title": "Core uses no file API",
            "where": { "targetType": ["System.IO.File"] },
            "forbid": true,
            "paths": [ "Core/**" ]
        }
        """;

    public static string Rules(params string[] rules)
    {
        return "{ \"rules\": [ " + string.Join(", ", rules) + " ] }";
    }

    /// <summary>
    /// A settings file with layer directions only: the modules as written ("kind:pattern" per module), and the allowed
    /// directions.
    /// </summary>
    public static string Layers(IReadOnlyDictionary<string, string[]> modules, params string[] allowed)
    {
        var listed = modules.Select(module => $"\"{module.Key}\": [ {string.Join(", ", module.Value.Select(selector => $"\"{selector}\""))} ]");
        var allow = allowed.Select(direction => $"\"{direction}\"");
        return "{ \"structure\": { \"modules\": { " + string.Join(", ", listed) + " }, \"directions\": { \"allow\": [ " + string.Join(", ", allow) + " ] } } }";
    }
}

/// <summary>
/// The source of meaning for a tree before a push: it answers with what the test hands it, and counts how often it was asked.
/// </summary>
internal sealed class FixedMeaningSource : IMeaningSource
{
    public MeaningAnswers? Before { get; set; }

    public List<MeaningRequest> Requests { get; } = new List<MeaningRequest>();

    public MeaningAnswers Collect(MeaningRequest request)
    {
        Requests.Add(request);
        return Before ?? MeaningAnswers.Missing("csharp", "the test gave no answers for the tree before");
    }

    public IReadOnlyList<Artifact> Apply(CodeFile file, IReadOnlyList<Artifact> artifacts, MeaningAnswers answers)
    {
        throw new NotSupportedException();
    }
}

/// <summary>
/// The C# plug-in with a source of meaning that the test controls.
/// </summary>
internal sealed class FixedMeaningLanguage : ILanguage
{
    private readonly ILanguage _inner;

    public FixedMeaningLanguage(ILanguage inner, IMeaningSource meaning)
    {
        _inner = inner;
        Meaning = meaning;
    }

    public string Id
    {
        get { return _inner.Id; }
    }

    public string Name
    {
        get { return _inner.Name; }
    }

    public IArtifactCatalog Catalog
    {
        get { return _inner.Catalog; }
    }

    public ICodeReader Reader
    {
        get { return _inner.Reader; }
    }

    public IToolchain? Toolchain
    {
        get { return null; }
    }

    public IMeaningSource? Meaning { get; }

    public IRepositoryRules? RepositoryRules
    {
        get { return null; }
    }

    public bool OwnsFile(string path)
    {
        return _inner.OwnsFile(path);
    }
}

/// <summary>
/// The decisions of the review that follow the compiler's answers: the rules about what code refers to, and the layer
/// directions compared between the tree before a push and the tree after it. The answers are handed in by the test.
/// </summary>
internal sealed class MeaningHarness
{
    private readonly ILanguage _csharp;
    private readonly SettingsFile _settingsFile;

    public MeaningHarness()
    {
        var fileSystem = new SystemFileSystem();
        _csharp = new CSharpLanguage(new FakeDotnet(TestPaths.Temp), fileSystem, new TestDataFolder(TestPaths.Temp));
        _settingsFile = new SettingsFile(fileSystem, new SettingsReader(new List<ILanguage> { _csharp }));
    }

    public FixedMeaningSource Before { get; } = new FixedMeaningSource();

    public FolderSnapshots Snapshots { get; } = new FolderSnapshots("/private-copy");

    /// <summary>
    /// What the rules about meaning find in the files of the analysis, with every line counted as changed.
    /// </summary>
    public IReadOnlyList<Finding> RuleFindings(string settingsJson, MeaningAnalysis analysis)
    {
        var languages = new List<ILanguage> { _csharp };
        var sources = analysis.Sources.Select(source => ReadSource(source.Key, source.Value)).ToList();
        var run = new ReviewRun();
        MeaningRulePass.Run(_settingsFile.Parse(settingsJson).ActiveRules, sources, new[] { new LanguageAnswers(_csharp, MeaningCompiler.Answers(analysis)) }, languages, run);

        return run.ToResult(TimeSpan.Zero).Findings;
    }

    /// <summary>
    /// The layer directions for a push whose tree after has these answers; the tree before has the answers set in
    /// <see cref="Before"/>, and is git's empty tree for a first push.
    /// </summary>
    public ReviewResult Layers(string settingsJson, MeaningAnswers after, bool isFirstPush = false)
    {
        var language = new FixedMeaningLanguage(_csharp, Before);
        var logs = new FolderLogs(TestPaths.Temp);
        var review = new StructureReview(new EmptyTreeGit(), Snapshots, logs, _settingsFile);
        var changeSet = ChangeSet.ForChanges("the push", isFirstPush ? EmptyTreeGit.EmptyTreeName : "tree-before", "tree-after", new List<ChangedFile>(), new List<string>(), string.Empty);
        var tree = new TreeUnderReview(new RepositoryLocation("/work", "/work/.git"), changeSet, "/private-copy", null);
        var run = new ReviewRun();

        review.Run(tree, _settingsFile.Parse(settingsJson), new[] { new LanguageAnswers(language, after) }, run);

        return run.ToResult(TimeSpan.Zero);
    }

    private ReadSource ReadSource(string path, string text)
    {
        var file = new CodeFile(path, _csharp.Id, text);
        var ways = new List<ReadWay> { new ReadWay(new List<FileReading>(), _csharp.Reader.Read(file)) };

        var change = new ChangedFile(file.Path, file.Language, ChangeStatus.Added, [LineRange.ForWholeFile()]);
        return new ReadSource(_csharp, file, ways, change);
    }
}
