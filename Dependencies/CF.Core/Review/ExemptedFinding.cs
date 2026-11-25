using CF.Core.Findings;

namespace CF.Core.Review;

/// <summary>A finding the review set aside, with the allow comment that covered it.</summary>
public sealed record ExemptedFinding(Finding Finding, int CommentLine, string Reason);