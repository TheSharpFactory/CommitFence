using CF.Adapters.Machine;
using CF.Core.Changes;
using CF.Core.Code;
using CF.Core.Ports;
using CF.Core.Review;
using CF.Languages.CSharp;

namespace CF.Tests.Toolchain.Support;

/// <summary>
/// A tree on disk whose build is played by a <see cref="FakeDotnet"/>: the projects, the sources and the settings file are
/// real files in a temporary folder, everything the review asks the build is answered from the script.
/// </summary>
internal sealed class FakeBuildFixture : IDisposable
{
    private readonly TestFolder _root = new TestFolder();
    private readonly EmptyTreeGit _git = new EmptyTreeGit();
    private int _runs;

    public FakeBuildFixture(string settings, params FakeProject[] projects)
        : this(settings, _ => projects)
    {
    }

    /// <summary>
    /// A fixture whose projects are described once the temporary folder is known.
    /// </summary>
    public FakeBuildFixture(string settings, Func<string, FakeProject[]> describeProjects)
    {
        var projects = describeProjects(_root.Path);
        Tree = Path.Combine(_root.Path, "tree");
        Data = Path.Combine(_root.Path, "data");
        Directory.CreateDirectory(Data);
        Dotnet = new FakeDotnet(Tree, projects);
        TreeFiles.WriteTree(Tree, projects, settings);
    }

    public string Root
    {
        get { return _root.Path; }
    }

    public string Tree { get; }

    public string Data { get; }

    public FakeDotnet Dotnet { get; }

    /// <summary>
    /// Runs the C# toolchain alone on the tree, as the review would after finding the changed files.
    /// </summary>
    public ToolchainResult RunToolchain(params ChangedFile[] files)
    {
        return RunToolchain(new SystemFileSystem(), files);
    }

    /// <summary>
    /// The same, with the plug-in reading and writing through the given file system.
    /// </summary>
    public ToolchainResult RunToolchain(IFileSystem fileSystem, params ChangedFile[] files)
    {
        var language = new CSharpLanguage(Dotnet, fileSystem, new TestDataFolder(Data));
        _runs++;
        var logFolder = Path.Combine(Data, "toolchain-logs", _runs.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Directory.CreateDirectory(logFolder);

        return language.Toolchain!.Run(new ToolchainRequest("tree-after", Tree, logFolder, files, false));
    }

    /// <summary>
    /// Reviews a change to the tree, as the product does once git has said which files changed.
    /// </summary>
    public ReviewResult Review(params ChangedFile[] files)
    {
        var harness = ReviewHarness.ForCSharp(Dotnet, Data, new FolderSnapshots(Tree), _git);
        return harness.Review(ChangeSet.ForChanges("the change", "tree-before", "tree-after", files, new List<string>(), string.Empty));
    }

    public string FullPath(string relative)
    {
        return Path.Combine(Tree, relative);
    }

    public void Dispose()
    {
        _root.Dispose();
    }
}
