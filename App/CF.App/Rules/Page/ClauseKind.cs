namespace CF.App.Rules.Page;

/// <summary>
/// Which clause of the sentence a condition stands in: after "where", or after "require".
/// </summary>
internal enum ClauseKind
{
    /// <summary>
    /// A condition of the where clause: which artifacts the rule looks at.
    /// </summary>
    Where,

    /// <summary>
    /// A condition of the require clause: what must hold of them.
    /// </summary>
    Require
}
