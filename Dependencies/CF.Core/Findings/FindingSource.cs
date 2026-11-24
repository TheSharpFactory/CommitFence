namespace CF.Core.Findings;

/// <summary>
/// What produced a finding.
/// </summary>
public enum FindingSource
{
    /// <summary>
    /// The language's compiler.
    /// </summary>
    Compiler,

    /// <summary>
    /// A rule of the .editorconfig, run by the compiler.
    /// </summary>
    EditorConfig,

    /// <summary>
    /// A code rule of the settings file, run by the rule engine.
    /// </summary>
    CodeRule,

    /// <summary>
    /// A structure rule of the settings file.
    /// </summary>
    StructureRule,

    /// <summary>
    /// A rule that comes from a pack.
    /// </summary>
    Pack
}