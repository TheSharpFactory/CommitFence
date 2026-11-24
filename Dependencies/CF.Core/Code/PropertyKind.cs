namespace CF.Core.Code;

/// <summary>
/// Where the value of a property comes from.
/// </summary>
public enum PropertyKind
{
    /// <summary>
    /// Known from reading the file, no build needed.
    /// </summary>
    Syntax,

    /// <summary>
    /// Known only from the compiler, so it needs the build.
    /// </summary>
    Meaning
}