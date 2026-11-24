using System.Text.Json;
using CF.Core.Structure;

namespace CF.Core.Rules.Settings;

/// <summary>
/// Turns the structure part of the settings file into structure rules, strictly: every module needs selectors, every
/// direction names two known modules, and a part of the shape that this version does not run yet is refused rather than
/// ignored.
/// </summary>
internal sealed class StructureReader
{
    private static readonly string[] _notRunYet = { "references", "layout", "noCycles" };

    private readonly JsonPositions _positions;

    public StructureReader(JsonPositions positions)
    {
        _positions = positions;
    }

    public StructureRules Read(StructureDto? dto, string path)
    {
        if(dto == null)
            return StructureRules.None;

        RejectUnknownKeys(dto.UnknownKeys, path, "the structure part");
        if(dto.Modules == null || dto.Modules.Count == 0)
            throw _positions.ErrorAt(path, "The structure part needs 'modules': at least one module with its selectors.");

        if(dto.Directions == null)
            throw _positions.ErrorAt(path, "The structure part needs 'directions': which module may use which.");

        var modules = ReadModules(dto.Modules, JsonPositions.Join(path, "modules"));
        var directions = ReadDirections(dto.Directions, modules, JsonPositions.Join(path, "directions"));

        return new StructureRules(modules, directions);
    }

    private void RejectUnknownKeys(Dictionary<string, JsonElement>? unknownKeys, string path, string where)
    {
        if(unknownKeys == null || unknownKeys.Count == 0)
            return;

        var key = unknownKeys.Keys.First();
        var message = _notRunYet.Contains(key) ? $"'{key}' is part of the structure rules, but this version does not run it yet. Remove it." : $"The key '{key}' is not known in {where}.";
        throw _positions.ErrorAt(JsonPositions.Join(path, key), message);
    }

    private List<ModuleDefinition> ReadModules(Dictionary<string, List<string>> entries, string path)
    {
        var modules = new List<ModuleDefinition>();
        foreach(var entry in entries)
            modules.Add(ReadModule(entry.Key, entry.Value, JsonPositions.Join(path, entry.Key)));

        return modules;
    }

    private ModuleDefinition ReadModule(string name, List<string>? texts, string path)
    {
        if(string.IsNullOrWhiteSpace(name))
            throw _positions.ErrorAt(path, "A module needs a name.");

        if(texts == null || texts.Count == 0)
            throw _positions.ErrorAt(path, $"The module '{name}' has no selectors; give at least one, such as namespace:*.Domain.");

        var selectors = new List<ModuleSelector>();
        for(var index = 0; index < texts.Count; index++)
            selectors.Add(ReadSelector(texts[index], path + "[" + index + "]"));

        return new ModuleDefinition(name, selectors);
    }

    private ModuleSelector ReadSelector(string? text, string path)
    {
        var colon = text == null ? -1 : text.IndexOf(':', StringComparison.Ordinal);
        if(text == null || colon <= 0 || colon == text.Length - 1)
            throw _positions.ErrorAt(path, "A selector is written kind:pattern, with the kind namespace, project or folder, such as namespace:*.Domain.");

        var pattern = text.Substring(colon + 1);
        switch(text.Substring(0, colon))
        {
            case "namespace":
                return new ModuleSelector(ModuleSelectorKind.Namespace, pattern);
            case "project":
                return new ModuleSelector(ModuleSelectorKind.Project, pattern);
            case "folder":
                return new ModuleSelector(ModuleSelectorKind.Folder, pattern);
            default:
                throw _positions.ErrorAt(path, $"'{text.Substring(0, colon)}' is not a kind of selector. Use namespace, project or folder.");
        }
    }

    private LayerDirections ReadDirections(DirectionsDto dto, IReadOnlyList<ModuleDefinition> modules, string path)
    {
        RejectUnknownKeys(dto.UnknownKeys, path, "the directions");
        var allow = ReadPairs(dto.Allow, modules, JsonPositions.Join(path, "allow"));
        var forbid = ReadPairs(dto.Forbid, modules, JsonPositions.Join(path, "forbid"));
        if(allow.Count == 0 && forbid.Count == 0)
            throw _positions.ErrorAt(path, "The directions need 'allow' or 'forbid' with at least one direction, such as web -> app.");

        return new LayerDirections(allow, forbid);
    }

    private List<ModulePair> ReadPairs(List<string>? texts, IReadOnlyList<ModuleDefinition> modules, string path)
    {
        var pairs = new List<ModulePair>();
        if(texts == null)
            return pairs;

        for(var index = 0; index < texts.Count; index++)
            pairs.Add(ReadPair(texts[index], modules, path + "[" + index + "]"));

        return pairs;
    }

    private ModulePair ReadPair(string? text, IReadOnlyList<ModuleDefinition> modules, string path)
    {
        var parts = (text ?? string.Empty).Split("->", StringSplitOptions.TrimEntries);
        if(parts.Length != 2 || parts[0].Length == 0 || parts[1].Length == 0)
            throw _positions.ErrorAt(path, "A direction is written from -> to, with the names of two modules, such as web -> app.");

        foreach(var name in parts)
        {
            if(!modules.Any(module => module.Name == name))
                throw _positions.ErrorAt(path, $"'{name}' is not a module. Known modules: {string.Join(", ", modules.Select(module => module.Name))}.");
        }

        if(parts[0] == parts[1])
            throw _positions.ErrorAt(path, "A direction joins two different modules; a module may always use itself.");

        return new ModulePair(parts[0], parts[1]);
    }
}