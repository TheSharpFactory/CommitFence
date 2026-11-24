namespace CF.Core.Structure;

/// <summary>
/// The structure rules of the settings file: the modules and the directions between them. Layout and reference rules
/// are not read yet.
/// </summary>
public sealed class StructureRules
{
    /// <summary>
    /// Creates the rules.
    /// </summary>
    public StructureRules(IReadOnlyList<ModuleDefinition> modules, LayerDirections directions)
    {
        Modules = modules;
        Directions = directions;
    }

    /// <summary>
    /// The modules, in the order of the file.
    /// </summary>
    public IReadOnlyList<ModuleDefinition> Modules { get; }

    /// <summary>
    /// Which module may use which.
    /// </summary>
    public LayerDirections Directions { get; }

    /// <summary>
    /// Rules that say nothing: a settings file without a structure part.
    /// </summary>
    public static StructureRules None { get; } = new StructureRules(new List<ModuleDefinition>(), new LayerDirections(new List<ModulePair>(), new List<ModulePair>()));

    /// <summary>
    /// Whether there is nothing to judge.
    /// </summary>
    public bool IsEmpty
    {
        get { return Modules.Count == 0 || Directions.IsEmpty; }
    }

    /// <summary>
    /// The name of the first module that holds code in this namespace, assembly and file, or nothing when no module
    /// does.
    /// </summary>
    public string? FindModule(string namespaceName, string assembly, string file)
    {
        return Modules.FirstOrDefault(module => module.Contains(namespaceName, assembly, file))?.Name;
    }

    /// <summary>
    /// Whether the module of a used type whose file is not known cannot be told and could matter: a module named by
    /// folder, listed before the module the namespace and assembly alone give (or before all of them, when they give
    /// none), could hold the type, and code of the using module may not use that module.
    /// </summary>
    public bool IsPlacementOpen(string fromModule, string usedNamespace, string usedAssembly)
    {
        var decided = Modules.ToList().FindIndex(module => module.Contains(usedNamespace, usedAssembly, string.Empty));
        var candidates = decided < 0 ? Modules : Modules.Take(decided);

        return candidates.Any(module => module.IsNamedByFolder && !Directions.Permits(fromModule, module.Name));
    }

    /// <summary>
    /// The rules written out in one text, the same for the same rules: two settings files with the same structure rules
    /// have the same text.
    /// </summary>
    public string Describe()
    {
        var modules = Modules.Select(module => module.Name + "=" + string.Join(",", module.Selectors));
        var allow = Directions.Allow.Select(pair => "allow " + pair);
        var forbid = Directions.Forbid.Select(pair => "forbid " + pair);

        return string.Join(";", modules.Concat(allow).Concat(forbid));
    }
}