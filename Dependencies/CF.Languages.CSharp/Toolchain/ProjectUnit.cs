namespace CF.Languages.CSharp.Toolchain;

/// <summary>
/// One project built for one target framework: the unit the review compiles, and the unit a log and a coverage entry
/// belong to.
/// </summary>
internal sealed class ProjectUnit
{
    public ProjectUnit(ProjectFile project, string framework, IReadOnlySet<string> compileFiles, IReadOnlyList<string> referencedProjects, string intermediateAssembly, IReadOnlyList<string> defineConstants, string repositoryAfterTargets)
    {
        Project = project;
        Framework = framework;
        CompileFiles = compileFiles;
        ReferencedProjects = referencedProjects;
        IntermediateAssembly = intermediateAssembly;
        DefineConstants = defineConstants;
        RepositoryAfterTargets = repositoryAfterTargets;
    }

    public ProjectFile Project { get; }

    public string Framework { get; }

    /// <summary>
    /// The full paths of the source files the build says this project compiles for this framework.
    /// </summary>
    public IReadOnlySet<string> CompileFiles { get; }

    /// <summary>
    /// The full paths of the project files this project references, for this framework.
    /// </summary>
    public IReadOnlyList<string> ReferencedProjects { get; }

    /// <summary>
    /// The compiler's output assembly in obj: removing it makes the next build compile again.
    /// </summary>
    public string IntermediateAssembly { get; }

    /// <summary>
    /// The conditional compilation symbols the build defines for this project and framework: the project's own, the
    /// ones the SDK adds for the framework, and DEBUG or TRACE as its configuration says.
    /// </summary>
    public IReadOnlyList<string> DefineConstants { get; }

    /// <summary>
    /// What the repository itself sets CustomAfterDirectoryBuildTargets to, or an empty text: the build of the review
    /// passes it on, so that the slot we use is not taken from the repository.
    /// </summary>
    public string RepositoryAfterTargets { get; }

    public string Key
    {
        get { return Project.Path + "|" + Framework; }
    }

    public string Label
    {
        get { return $"{Project.DisplayName} ({Framework})"; }
    }

    /// <summary>
    /// The project file relative to the tree, a bar and the framework: the same in every tree that holds the project.
    /// </summary>
    public string PartKeyIn(string treeFolder)
    {
        return ToolchainPaths.RelativeTo(treeFolder, Project.Path) + "|" + Framework;
    }
}