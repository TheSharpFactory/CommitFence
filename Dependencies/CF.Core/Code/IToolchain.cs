using CF.Core.Changes;

namespace CF.Core.Code;

/// <summary>
/// The language's own tools, run on the private copy of the tree under review: for C# the team's compiler through the
/// build.
/// </summary>
/// <remarks>
/// It turns what those tools report into findings and says what it covered. It never
/// changes the reviewed repository and never installs anything.
/// </remarks>
public interface IToolchain
{
    /// <summary>
    /// The changed files that make the toolchain necessary, judged on the tree they were changed in: a source file, a
    /// file that decides how the sources are built, and any other file that lies inside a project, since the build
    /// compiles or packs it.
    /// </summary>
    IReadOnlyList<ChangedFile> FilesOfConcern(string treeFolder, IReadOnlyList<ChangedFile> files);

    /// <summary>
    /// Whether the path names a project file of this toolchain: the file that says what one project is built from.
    /// </summary>
    bool IsProjectFile(string path);

    /// <summary>
    /// Runs the tools on the tree and reports.
    /// </summary>
    /// <remarks>
    /// A machine that is not ready, a project that cannot be built and a part that could not be checked are coverage
    /// entries that are not reviewed, never an exception and never a pass.
    /// </remarks>
    ToolchainResult Run(ToolchainRequest request);
}