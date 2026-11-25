using System.Text.Json;
using CF.Core.Structure;

namespace CF.Core.Rules.Settings;

/// <summary>
/// Reads the modules and their directions and preserves rules that the review cannot yet judge.
/// </summary>
internal sealed class StructureReader
{
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
        var modules = ReadModules(dto.Modules ?? [], JsonPositions.Join(path, "modules"));
        var directions = ReadDirections(dto.Directions ?? new DirectionsDto(), modules, JsonPositions.Join(path, "directions"));
        CheckRuleObjects(dto.References, JsonPositions.Join(path, "references"));
        CheckRuleObjects(dto.Layout, JsonPositions.Join(path, "layout"));

        return new StructureRules(modules, directions, dto.References, dto.Layout);
    }

    private void RejectUnknownKeys(Dictionary<string, JsonElement>? unknownKeys, string path, string where)
    {
        if(unknownKeys == null || unknownKeys.Count == 0)
            return;

        var key = unknownKeys.Keys.First();
        throw _positions.ErrorAt(JsonPositions.Join(path, key), $"The key '{key}' is not known in {where}.");
    }

    private void CheckRuleObjects(List<JsonElement>? rules, string path)
    {
        if(rules == null)
            return;

        for(var index = 0; index < rules.Count; index++)
        {
            if(rules[index].ValueKind != JsonValueKind.Object)
                throw _positions.ErrorAt(path + "[" + index + "]", "A structure rule must be a JSON object.");
        }
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

        var selectors = new List<ModuleSelector>();
        if(texts != null)
        {
            for(var index = 0; index < texts.Count; index++)
                selectors.Add(ReadSelector(texts[index], path + "[" + index + "]"));
        }

        return new ModuleDefinition(name, selectors);
    }

    private ModuleSelector ReadSelector(string? text, string path)
    {
        try
        {
            return ParseSelector(text ?? string.Empty);
        }
        catch(RuleChangeException exception)
        {
            throw _positions.ErrorAt(path, exception.Message);
        }
    }

    public static ModuleSelector ParseSelector(string text)
    {
        var colon = text.IndexOf(':', StringComparison.Ordinal);
        if(colon <= 0 || colon == text.Length - 1)
            throw new RuleChangeException("A selector is written kind:pattern, with the kind namespace, project or folder, such as namespace:*.Domain.");

        var kind = text[..colon];
        var pattern = text[(colon + 1)..];
        return kind switch
        {
            "namespace" => new ModuleSelector(ModuleSelectorKind.Namespace, pattern),
            "project" => new ModuleSelector(ModuleSelectorKind.Project, pattern),
            "folder" => new ModuleSelector(ModuleSelectorKind.Folder, pattern),
            _ => throw new RuleChangeException($"'{kind}' is not a kind of selector. Use namespace, project or folder.")
        };
    }

    private LayerDirections ReadDirections(DirectionsDto dto, IReadOnlyList<ModuleDefinition> modules, string path)
    {
        RejectUnknownKeys(dto.UnknownKeys, path, "the directions");
        var allow = ReadPairs(dto.Allow, modules, JsonPositions.Join(path, "allow"));
        var forbid = ReadPairs(dto.Forbid, modules, JsonPositions.Join(path, "forbid"));
        return new LayerDirections(allow, forbid, dto.Allow != null, dto.Forbid != null, dto.NoCycles);
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
