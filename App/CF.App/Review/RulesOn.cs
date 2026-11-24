namespace CF.App.Review;

/// <summary>
/// How many of the rules that are on block a push and how many only warn: the numbers of "Blocks" and "Warns" in the filter of Rules.
/// </summary>
internal sealed record RulesOn(int Blocks, int Warns);
