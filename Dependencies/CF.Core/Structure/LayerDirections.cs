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
    public LayerDirections(IReadOnlyList<ModulePair> allow, IReadOnlyList<ModulePair> forbid, bool? hasAllowList = null, bool? hasForbidList = null, bool? noCycles = null)
    {
        Allow = allow;
        Forbid = forbid;
        HasAllowList = hasAllowList ?? allow.Count > 0;
        HasForbidList = hasForbidList ?? forbid.Count > 0;
        NoCycles = noCycles;
    }

    /// <summary>
    /// The directions that are allowed by the allow list.
    /// </summary>
    public IReadOnlyList<ModulePair> Allow { get; }

    /// <summary>
    /// The directions that are never allowed.
    /// </summary>
    public IReadOnlyList<ModulePair> Forbid { get; }

    /// <summary>Whether the file has an allow list, even if it lists no pairs.</summary>
    public bool HasAllowList { get; }

    /// <summary>Whether the file has a forbid list, even if it lists no pairs.</summary>
    public bool HasForbidList { get; }

    /// <summary>The optional cycle rule, preserved in settings until it can be judged.</summary>
    public bool? NoCycles { get; }

    /// <summary>
    /// Whether the directions say anything at all.
    /// </summary>
    public bool IsEmpty
    {
        get { return !HasAllowList && !HasForbidList; }
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

        var needsPermission = HasAllowList && (!HasForbidList || Allow.Count > 0);
        return !needsPermission || Allow.Any(pair => pair.Is(from, to));
    }
}
