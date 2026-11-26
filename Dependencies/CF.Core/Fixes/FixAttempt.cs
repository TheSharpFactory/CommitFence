namespace CF.Core.Fixes;

/// <summary>Whether a fixer ran, and the reason if the run failed.</summary>
public sealed record FixAttempt(bool Ran, string? Reason = null);
