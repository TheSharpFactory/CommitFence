namespace CF.Core.Code;

/// <summary>
/// What the build does with a rule a repository's own file sets.
/// </summary>
public enum FileRuleState
{
    /// <summary>
    /// The build applies the rule.
    /// </summary>
    On,

    /// <summary>
    /// The build does not apply the rule.
    /// </summary>
    Off,

    /// <summary>
    /// The file leaves the choice to the rule's own default, which the product does not know.
    /// </summary>
    OwnDefault,

    /// <summary>
    /// The language's tools do not understand the setting, so the file's line has no effect the product can state.
    /// </summary>
    NotUnderstood
}
