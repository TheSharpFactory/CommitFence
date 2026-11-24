namespace CF.App.Rules.Page;

/// <summary>
/// The four labels of Try it: the three things a rule says about an artifact, and the warning that code does not parse.
/// </summary>
internal enum TryKind
{
    Breaks,
    Follows,
    NotCovered,
    Warn
}
