namespace CF.Languages.CSharp.Toolchain;

/// <summary>
/// What this push must compile: the units to review, the units they need built first, in the order to build them, and
/// the changed files no project compiles.
/// </summary>
internal sealed class BuildPlan
{
    public BuildPlan(IReadOnlySet<string> reviewedKeys, IReadOnlyList<ProjectUnit> order, IReadOnlyList<string> notCovered)
    {
        ReviewedKeys = reviewedKeys;
        Order = order;
        NotCovered = notCovered;
    }

    /// <summary>
    /// The keys of the units that own changed files or depend on units that do: their logs are read and each has a
    /// coverage entry.
    /// </summary>
    public IReadOnlySet<string> ReviewedKeys { get; }

    /// <summary>
    /// Every unit to build, prerequisites before the units that need them.
    /// </summary>
    public IReadOnlyList<ProjectUnit> Order { get; }

    /// <summary>
    /// Changed source files, relative to the tree, that no project compiles.
    /// </summary>
    public IReadOnlyList<string> NotCovered { get; }
}