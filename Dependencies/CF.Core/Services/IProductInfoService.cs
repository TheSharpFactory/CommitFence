namespace CF.Core.Services;

/// <summary>
/// Tells which product is running and in which version.
/// </summary>
public interface IProductInfoService
{
    /// <summary>
    /// The name of the product.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// The version as written in the VERSION file.
    /// </summary>
    string Version { get; }

    /// <summary>
    /// The release notes of the version that runs, carried inside the program, or nothing when its changelog has no section for it.
    /// </summary>
    VersionNotes? Notes
    {
        get { return null; }
    }
}
