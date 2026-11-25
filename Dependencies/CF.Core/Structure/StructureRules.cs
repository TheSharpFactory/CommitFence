using System.Globalization;
using System.Text;
using System.Text.Json;

namespace CF.Core.Structure;

/// <summary>
/// The structure rules of the settings file, including the declarations this version does not judge.
/// </summary>
public sealed class StructureRules
{
    /// <summary>
    /// Creates the rules.
    /// </summary>
    public StructureRules(IReadOnlyList<ModuleDefinition> modules, LayerDirections directions, IReadOnlyList<JsonElement>? references = null, IReadOnlyList<JsonElement>? layout = null)
    {
        Modules = modules;
        Directions = directions;
        References = references;
        Layout = layout;
    }

    /// <summary>
    /// The modules, in the order of the file.
    /// </summary>
    public IReadOnlyList<ModuleDefinition> Modules { get; }

    /// <summary>
    /// Which module may use which.
    /// </summary>
    public LayerDirections Directions { get; }

    /// <summary>The reference rules as written, if present.</summary>
    public IReadOnlyList<JsonElement>? References { get; }

    /// <summary>The layout rules as written, if present.</summary>
    public IReadOnlyList<JsonElement>? Layout { get; }

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
        if(this == None)
            return string.Empty;

        var description = new StringBuilder();
        foreach(var module in Modules)
        {
            AddField(description, "module");
            AddField(description, module.Name);
            AddList(description, "parts", module.Selectors.Select(selector => selector.ToString()));
        }

        AddList(description, "allow", Directions.HasAllowList ? Directions.Allow.Select(pair => pair.ToString()) : null);
        AddList(description, "forbid", Directions.HasForbidList ? Directions.Forbid.Select(pair => pair.ToString()) : null);
        AddField(description, Directions.NoCycles?.ToString() ?? string.Empty);
        AddList(description, "references", References?.Select(rule => rule.GetRawText()));
        AddList(description, "layout", Layout?.Select(rule => rule.GetRawText()));
        return description.ToString();
    }

    private static void AddList(StringBuilder description, string name, IEnumerable<string>? entries)
    {
        if(entries == null)
            return;

        AddField(description, name);
        var list = entries.ToList();
        AddField(description, list.Count.ToString(CultureInfo.InvariantCulture));
        foreach(var entry in list)
            AddField(description, entry);
    }

    private static void AddField(StringBuilder description, string value)
    {
        description.Append(value.Length.ToString(CultureInfo.InvariantCulture));
        description.Append(':');
        description.Append(value);
    }
}
