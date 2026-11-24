namespace CF.App.Rules;

/// <summary>
/// Where the rules under a heading come from.
/// </summary>
internal enum GroupSource
{
    /// <summary>
    /// The rules the team wrote, kept in commitfence.json.
    /// </summary>
    Custom,

    /// <summary>
    /// The rules the repository's own .editorconfig sets for the compiler.
    /// </summary>
    EditorConfig
}
