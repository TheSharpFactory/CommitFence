namespace CF.Core.Structure;

/// <summary>
/// A module: a name and the selectors that say which code belongs to it.
/// </summary>
public sealed class ModuleDefinition
{
    /// <summary>
    /// Creates a module.
    /// </summary>
    public ModuleDefinition(string name, IReadOnlyList<ModuleSelector> selectors)
    {
        Name = name;
        Selectors = selectors;
    }

    /// <summary>
    /// The name layer directions use.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// What belongs to the module: code that any selector matches.
    /// </summary>
    public IReadOnlyList<ModuleSelector> Selectors { get; }

    /// <summary>
    /// Whether any selector names code by folder, which only code whose file is known can match.
    /// </summary>
    public bool IsNamedByFolder
    {
        get { return Selectors.Any(selector => selector.Kind == ModuleSelectorKind.Folder); }
    }

    /// <summary>
    /// Whether code in this namespace, assembly and file belongs to the module.
    /// </summary>
    public bool Contains(string namespaceName, string assembly, string file)
    {
        return Selectors.Any(selector => selector.Matches(namespaceName, assembly, file));
    }
}