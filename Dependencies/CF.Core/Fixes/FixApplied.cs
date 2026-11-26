namespace CF.Core.Fixes;

/// <summary>The result of applying the preview. A reason is supplied when the write was refused or failed.</summary>
public sealed record FixApplied(string? Reason = null)
{
    /// <summary>Whether the working file no longer matches the preview.</summary>
    public bool FileChanged { get; init; }

    /// <summary>Whether the preview was applied.</summary>
    public bool Succeeded
    {
        get { return Reason == null; }
    }
}
