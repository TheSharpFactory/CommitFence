namespace CF.App.Rules.Page;

/// <summary>
/// What Try it has to say about the rule and the code in its box.
/// </summary>
internal enum TryPhase
{
    /// <summary>
    /// Nothing is typed yet: no labels, no message.
    /// </summary>
    Empty,

    /// <summary>
    /// The sentence is not complete: "Finish the rule to try it."
    /// </summary>
    Unfinished,

    /// <summary>
    /// The edited rule is one a save would refuse: the message of the refusal.
    /// </summary>
    Refused,

    /// <summary>
    /// The rule needs what the compiler knows, so it cannot be tried on code alone.
    /// </summary>
    NeedsBuild,

    /// <summary>
    /// The code does not parse.
    /// </summary>
    DoesNotParse,

    /// <summary>
    /// The code has no artifact of the rule's kind.
    /// </summary>
    NoArtifact,

    /// <summary>
    /// The rule was run: one verdict for each artifact.
    /// </summary>
    Tried
}
