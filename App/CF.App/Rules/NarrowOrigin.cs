namespace CF.App.Rules;

/// <summary>
/// Where a rule was opened from to narrow it: the strip above its sentence says so, in the words of the screen that sent it, and
/// "Back to Exemptions" returns there. The strip goes when the rule is saved or discarded, or another rule is opened.
/// </summary>
internal sealed class NarrowOrigin
{
    public NarrowOrigin(string text, Action back)
    {
        Text = text;
        Back = back;
    }

    /// <summary>
    /// The strip's words, with the lead between two ** marks.
    /// </summary>
    public string Text { get; }

    /// <summary>
    /// Returns to the screen the rule was sent from.
    /// </summary>
    public Action Back { get; }
}
