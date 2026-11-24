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
}