namespace CF.Core.Findings;

/// <summary>
/// What kind of part a coverage entry is about.
/// </summary>
public enum CoverageKind
{
    /// <summary>
    /// A source file read by the rule engine.
    /// </summary>
    File,

    /// <summary>
    /// A part of the language's own build: a project and framework, or a file the build does not cover.
    /// </summary>
    Build,

    /// <summary>
    /// A rule of the settings file that could not be run.
    /// </summary>
    Rule
}