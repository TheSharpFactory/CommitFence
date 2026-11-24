namespace CF.App.Rules;

/// <summary>
/// The parts of a row that take the keyboard, in the order Right moves through them.
/// </summary>
internal enum RowPart
{
    /// <summary>
    /// The row itself.
    /// </summary>
    Row,

    /// <summary>
    /// The control that sets what the rule does to a push.
    /// </summary>
    Effect,

    /// <summary>
    /// The switch of a team rule.
    /// </summary>
    Switch,

    /// <summary>
    /// The line of the file in the row of a rule that .editorconfig sets.
    /// </summary>
    Line
}
