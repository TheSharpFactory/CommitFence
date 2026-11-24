namespace CF.App.Rules;

/// <summary>
/// The one question the filter of the list answers: what a rule does to a push.
/// </summary>
internal enum RuleFilter
{
    /// <summary>
    /// Every rule.
    /// </summary>
    All,

    /// <summary>
    /// The rules that are on and refuse the push.
    /// </summary>
    Blocks,

    /// <summary>
    /// The rules that are on and only warn.
    /// </summary>
    Warns,

    /// <summary>
    /// The rules that are off.
    /// </summary>
    Off
}
