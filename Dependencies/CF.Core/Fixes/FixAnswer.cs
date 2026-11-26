namespace CF.Core.Fixes;

/// <summary>The fix, or the reason it could not be prepared. An empty answer means the rule has no fix.</summary>
public sealed record FixAnswer(Fix? Fix = null, string? Reason = null)
{
    /// <summary>The message to show when there is no fix.</summary>
    public string NoFixText
    {
        get { return Reason == null ? "No automatic fix." : "No automatic fix: " + Reason; }
    }
}
