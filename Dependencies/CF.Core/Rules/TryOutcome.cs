namespace CF.Core.Rules;

/// <summary>
/// What a rule says about one artifact of its type in code that is tried.
/// </summary>
public enum TryOutcome
{
    /// <summary>
    /// The rule looks at the artifact and the artifact breaks it.
    /// </summary>
    BreaksIt,

    /// <summary>
    /// The rule looks at the artifact and the artifact follows it.
    /// </summary>
    FollowsIt,

    /// <summary>
    /// The rule does not look at the artifact: a where condition or the paths leave it out.
    /// </summary>
    NotCovered
}