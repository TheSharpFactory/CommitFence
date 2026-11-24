namespace CF.Core.Review;

/// <summary>
/// The number of findings one rule has in a review.
/// </summary>
/// <param name="RuleId">The identifier of the rule.</param>
/// <param name="Count">How many findings it has.</param>
public sealed record RuleCount(string RuleId, int Count);