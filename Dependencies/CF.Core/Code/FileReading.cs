namespace CF.Core.Code;

/// <summary>
/// One way a changed file is compiled: by one project for one target framework, with the conditional symbols that build
/// defines.
/// </summary>
/// <remarks>
/// A file compiled in several ways is read once for each way, because code switched by a condition on a symbol is
/// different code in each.
/// </remarks>
public sealed class FileReading
{
    /// <summary>
    /// Creates a reading.
    /// </summary>
    public FileReading(string path, string project, string targetFramework, IReadOnlyList<string> symbols)
    {
        Path = path;
        Project = project;
        TargetFramework = targetFramework;
        Symbols = symbols;
    }

    /// <summary>
    /// The file, as git names it: relative to the repository, with forward slashes.
    /// </summary>
    public string Path { get; }

    /// <summary>
    /// The project that compiles the file.
    /// </summary>
    public string Project { get; }

    /// <summary>
    /// The target framework the project is built for.
    /// </summary>
    public string TargetFramework { get; }

    /// <summary>
    /// The conditional symbols the build defines for this project and framework.
    /// </summary>
    public IReadOnlyList<string> Symbols { get; }
}