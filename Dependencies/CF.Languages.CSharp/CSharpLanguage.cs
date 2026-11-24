using CF.Core.Code;
using CF.Core.Ports;
using CF.Languages.CSharp.Catalog;
using CF.Languages.CSharp.EditorConfig;
using CF.Languages.CSharp.Meaning;
using CF.Languages.CSharp.Reader;
using CF.Languages.CSharp.Toolchain;

namespace CF.Languages.CSharp;

/// <summary>
/// The C# language plug-in: its catalog, its reader and its toolchain, its meaning (which the build answers) and the rules of its .editorconfig.
/// </summary>
public sealed class CSharpLanguage : ILanguage
{
    /// <summary>
    /// Creates the plug-in. The toolchain runs the machine's dotnet through the process port and keeps its own files in
    /// the data folder.
    /// </summary>
    public CSharpLanguage(IProcessRunner processRunner, IFileSystem fileSystem, IDataFolder dataFolder)
    {
        var toolchain = new CSharpToolchain(processRunner, fileSystem, dataFolder);
        Toolchain = toolchain;
        Meaning = new CSharpMeaningSource(toolchain);
        RepositoryRules = new EditorConfigRules(fileSystem, new DotnetTool(processRunner));
    }

    /// <inheritdoc />
    public string Id
    {
        get { return "csharp"; }
    }

    /// <inheritdoc />
    public string Name
    {
        get { return "C#"; }
    }

    /// <inheritdoc />
    public IArtifactCatalog Catalog { get; } = new CSharpArtifactCatalog();

    /// <inheritdoc />
    public ICodeReader Reader { get; } = new CSharpCodeReader();

    /// <inheritdoc />
    public IToolchain? Toolchain { get; }

    /// <inheritdoc />
    public IMeaningSource? Meaning { get; }

    /// <inheritdoc />
    public IRepositoryRules? RepositoryRules { get; }

    /// <inheritdoc />
    public bool OwnsFile(string path)
    {
        return path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase);
    }
}