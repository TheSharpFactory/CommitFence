namespace CF.App.Parts;

/// <summary>
/// What a rule does to a push, as the app draws it.
/// </summary>
internal enum EffectKind
{
    /// <summary>
    /// The stop sign: the push is refused.
    /// </summary>
    Block,

    /// <summary>
    /// The triangle: the push goes through with a warning.
    /// </summary>
    Warn,

    /// <summary>
    /// The off sign: the rule is not in force.
    /// </summary>
    Off,

    /// <summary>The file has no main setting.</summary>
    Unset
}