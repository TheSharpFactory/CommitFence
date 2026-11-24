using System.Reflection;

namespace CF.Core.Services;

/// <summary>
/// Reads the name and the version of the product; the version comes from the build, which takes it from the VERSION
/// file.
/// </summary>
public sealed class ProductInfoService : IProductInfoService
{
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

    private static string ReadVersion()
    {
        var assembly = typeof(ProductInfoService).Assembly;
        var attribute = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
        if(attribute == null)
            return "0";

        return attribute.InformationalVersion;
    }
}