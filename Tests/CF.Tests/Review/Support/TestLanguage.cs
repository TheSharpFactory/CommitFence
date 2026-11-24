using CF.Core.Changes;
using CF.Core.Code;
using CF.Languages.CSharp.Catalog;
using CF.Languages.CSharp.Meaning;
using CF.Languages.CSharp.Reader;

namespace CF.Tests.Review.Support;

/// <summary>
/// The C# language with its real catalog and reader, and a toolchain and a source of meaning that answer what the test
/// says: no build is started.
/// </summary>
internal sealed class TestLanguage : ILanguage
{
    public TestLanguage(FakeToolchain? toolchain = null, IMeaningSource? meaning = null)
    {
        Toolchain = toolchain;
        Meaning = meaning;
    }

    public string Id
    {
        get { return "csharp"; }
    }

    public string Name
    {
        get { return "C#"; }
    }

    public IArtifactCatalog Catalog { get; } = new CSharpArtifactCatalog();

    public ICodeReader Reader { get; } = new CSharpCodeReader();

    public IToolchain? Toolchain { get; }

    public IMeaningSource? Meaning { get; }

    public IRepositoryRules? RepositoryRules
    {
        get { return null; }
    }

    public bool OwnsFile(string path)
    {
        return path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>
/// A toolchain that answers what the test gives it and counts the builds it was asked for.
/// </summary>
internal sealed class FakeToolchain : IToolchain
{
    private readonly Lock _syncRoot = new Lock();
    private readonly List<ToolchainRequest> _requests = new List<ToolchainRequest>();

    /// <summary>
    /// The answer of a build; with nothing set, a build that found nothing and covered nothing.
    /// </summary>
    public Func<ToolchainRequest, ToolchainResult>? Answer { get; set; }

    public IReadOnlyList<ToolchainRequest> Requests
    {
        get
        {
            lock(_syncRoot)
            {
                return _requests.ToList();
            }
        }
    }

    public IReadOnlyList<ChangedFile> FilesOfConcern(string treeFolder, IReadOnlyList<ChangedFile> files)
    {
        return files.Where(file => file.Path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)).ToList();
    }

    public bool IsProjectFile(string path)
    {
        return path.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase);
    }

    public ToolchainResult Run(ToolchainRequest request)
    {
        lock(_syncRoot)
        {
            _requests.Add(request);
        }

        return Answer != null ? Answer(request) : Nothing();
    }

    public static ToolchainResult Nothing()
    {
        return new ToolchainResult(new List<CF.Core.Findings.Finding>(), new List<CF.Core.Findings.Coverage>(), new List<string>(), CompleteAnswers(), new List<FileReading>());
    }

    public static MeaningAnswers CompleteAnswers()
    {
        return new MeaningAnswers("csharp", new List<MeaningDeclaration>(), new List<MeaningUse>(), new List<string>(), new List<string>(), new List<string>());
    }
}

/// <summary>
/// The meaning the build would have found, as the test states it, applied to the artifacts by the product's own code.
/// </summary>
internal sealed class FakeMeaningSource : IMeaningSource
{
    private readonly MeaningAnswers _answers;

    public FakeMeaningSource(MeaningAnswers answers)
    {
        _answers = answers;
    }

    public MeaningAnswers Collect(MeaningRequest request)
    {
        return _answers;
    }

    public IReadOnlyList<Artifact> Apply(CodeFile file, IReadOnlyList<Artifact> artifacts, MeaningAnswers answers)
    {
        return new MeaningApplier(new MeaningIndex(answers)).Apply(file, artifacts);
    }
}
