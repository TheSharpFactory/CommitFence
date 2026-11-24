namespace CF.Core.Structure;

/// <summary>
/// One way of naming the code of a module, written kind:pattern.
/// </summary>
/// <remarks>
/// A pattern may use * for any run of characters and ? for one character; a folder pattern also takes **. A namespace
/// pattern matches a namespace and every namespace below it.
/// </remarks>
public sealed class ModuleSelector
{
    /// <summary>
    /// Creates a selector.
    /// </summary>
    public ModuleSelector(ModuleSelectorKind kind, string pattern)
    {
        Kind = kind;
        Pattern = pattern;
    }

    /// <summary>
    /// What the pattern names.
    /// </summary>
    public ModuleSelectorKind Kind { get; }

    /// <summary>
    /// The pattern.
    /// </summary>
    public string Pattern { get; }

    /// <summary>
    /// Whether code in this namespace, assembly and file belongs to what the selector names. A file that is not known
    /// is the empty text and matches no folder.
    /// </summary>
    public bool Matches(string namespaceName, string assembly, string file)
    {
        switch(Kind)
        {
            case ModuleSelectorKind.Namespace:
                return MatchesNamespace(namespaceName);
            case ModuleSelectorKind.Project:
                return PathFilter.Matches(new List<string> { Pattern }, assembly);
            default:
                return file.Length > 0 && PathFilter.Matches(new List<string> { Pattern }, file);
        }
    }

    /// <summary>
    /// The selector as it is written in the settings file.
    /// </summary>
    public override string ToString()
    {
        return Kind.ToString().ToLowerInvariant() + ":" + Pattern;
    }

    private bool MatchesNamespace(string namespaceName)
    {
        var patterns = new List<string> { Pattern };
        var candidate = namespaceName;
        while(candidate.Length > 0)
        {
            if(PathFilter.Matches(patterns, candidate))
                return true;

            var dot = candidate.LastIndexOf('.');
            candidate = dot < 0 ? string.Empty : candidate.Substring(0, dot);
        }

        return false;
    }
}