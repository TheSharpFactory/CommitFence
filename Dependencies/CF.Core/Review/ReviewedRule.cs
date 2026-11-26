using CF.Core.Code;
using CF.Core.Rules;

namespace CF.Core.Review;

/// <summary>The definition a review used for one finding.</summary>
public sealed record ReviewedRule(RuleDefinition? Team, FileRule? File);
