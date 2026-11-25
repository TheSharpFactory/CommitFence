using System.Reflection;
using System.Text;

namespace CF.Core.Services;

/// <summary>
/// Reads the name and the version of the product; the version comes from the build, which takes it from the VERSION
/// file. The release notes come from CHANGELOG.md, which the build carries inside the program.
/// </summary>
public sealed class ProductInfoService : IProductInfoService
{
    private const string _changelogResource = "CHANGELOG.md";

    /// <inheritdoc />
    public string Name
    {
        get { return "CommitFence"; }
    }

    /// <inheritdoc />
    public string Version
    {
        get { return ReadVersion(); }
    }

    /// <inheritdoc />
    public VersionNotes? Notes
    {
        get { return ReadNotes(); }
    }

    private static string ReadVersion()
    {
        var assembly = typeof(ProductInfoService).Assembly;
        var attribute = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
        if(attribute == null)
            return "0";

        return attribute.InformationalVersion;
    }

    private VersionNotes? ReadNotes()
    {
        using var stream = typeof(ProductInfoService).Assembly.GetManifestResourceStream(_changelogResource);
        if(stream == null)
            return null;

        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return VersionNotes.Find(Encoding.UTF8.GetString(memory.GetBuffer(), 0, (int)memory.Length), Version);
    }
}
