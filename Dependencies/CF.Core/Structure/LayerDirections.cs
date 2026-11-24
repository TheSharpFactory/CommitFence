namespace CF.Core.Structure;

/// <summary>
/// Which module may use which. With an allow list, a direction that is not listed is a breach;
/// a forbid list names the breaches instead. A module may always use itself.
/// </summary>
public sealed class LayerDirections
{
    /// <summary>
    /// Creates the directions.
    /// </summary>
    public LayerDirections(IReadOnlyList<ModulePair> allow, IReadOnlyList<ModulePair> forbid)
    {
        Allow = allow;
        Forbid = forbid;
    }

    /// <summary>
    /// The directions that are allowed; when there are some, every other direction between two modules is a breach.
    /// </summary>
    public IReadOnlyList<ModulePair> Allow { get; }

    /// <summary>
    /// The directions that are never allowed.
    /// </summary>
    public IReadOnlyList<ModulePair> Forbid { get; }

    /// <summary>
    /// Whether the directions say anything at all.
    /// </summary>
    public bool IsEmpty
    {
        get { return Allow.Count == 0 && Forbid.Count == 0; }
    }

    /// <summary>
    /// Whether code of the first module may use code of the second.
    /// </summary>
    public bool Permits(string from, string to)
    {
        if(from == to)
            return true;

        if(Forbid.Any(pair => pair.Is(from, to)))
            return false;

        return Allow.Count == 0 || Allow.Any(pair => pair.Is(from, to));
    }
}