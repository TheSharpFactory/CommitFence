namespace CF.Languages.CSharp.Toolchain;

/// <summary>
/// A project file found in the tree, with the name the report uses for it.
/// </summary>
internal sealed class ProjectFile
{
    public ProjectFile(string path, string displayName)
    {
        Path = path;
        DisplayName = displayName;
    }

    public string Path { get; }

    public string DisplayName { get; }
}