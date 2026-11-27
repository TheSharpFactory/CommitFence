using CF.Core.Code;
using CF.Core.Ports;
using CF.Core.Rules;
using CF.Core.Rules.Settings;
using CF.Core.Structure;

namespace CF.Core.Services;

/// <inheritdoc cref="IStructureService" />
public sealed class StructureService : IStructureService
{
    private readonly ISettingsLock _settingsLock;
    private readonly SettingsFile _file;
    private readonly SettingsSaver _saver;
    private readonly IGit _git;
    private readonly IStructureJudgments _judgments;

    /// <summary>Creates the service over the settings and judgment stores.</summary>
    public StructureService(IFileSystem files, ISettingsLock settingsLock, IReadOnlyList<ILanguage> languages, IGit git, IStructureJudgments judgments)
    {
        var reader = new SettingsReader(languages);
        _settingsLock = settingsLock;
        _file = new SettingsFile(files, reader);
        _saver = new SettingsSaver(files, reader);
        _git = git;
        _judgments = judgments;
    }

    /// <inheritdoc />
    public StructureReport Show(string folder)
    {
        var structure = _file.Read(SettingsFileName.PathIn(folder)).Structure;
        var names = structure.Modules.Select(module => module.Name).ToList();
        var pairs = (from fromName in names
                     from toName in names
                     where fromName != toName && structure.Directions.Permits(fromName, toName)
                     select new StructureDirection(fromName, toName)).ToList();
        var bySource = pairs.ToLookup(pair => pair.From, StringComparer.Ordinal);
        var byTarget = pairs.ToLookup(pair => pair.To, StringComparer.Ordinal);
        var modules = structure.Modules.Select(module => new StructureModuleReport(module.Name,
            module.Selectors.Select(selector => new StructurePart(selector.Kind.ToString().ToLowerInvariant(), selector.Pattern)).ToList(),
            bySource[module.Name].Select(pair => pair.To).ToList(), byTarget[module.Name].Select(pair => pair.From).ToList())).ToList();
        var directions = structure.Directions;
        var lists = new StructureDirectionLists(
            directions.HasAllowList ? directions.Allow.Select(pair => new StructureDirection(pair.From, pair.To)).ToList() : null,
            directions.HasForbidList ? directions.Forbid.Select(pair => new StructureDirection(pair.From, pair.To)).ToList() : null);
        var repository = _git.FindRepository(folder);
        return new StructureReport(modules, lists, pairs, repository == null ? null : _judgments.Read(repository));
    }

    /// <inheritdoc />
    public StructureChange AddModule(string folder, string name)
    {
        return Edit(folder, $"Added module '{name}'.", (modules, directions) =>
        {
            CheckNewName(modules, name);
            modules.Add(new ModuleDefinition(name, []));
            return directions;
        });
    }

    /// <inheritdoc />
    public StructureChange RenameModule(string folder, string name, string newName)
    {
        return Edit(folder, $"Renamed module '{name}' to '{newName}'.", (modules, directions) =>
        {
            var index = FindModule(modules, name);
            CheckNewName(modules, newName);
            modules[index] = new ModuleDefinition(newName, modules[index].Selectors);
            return WithPairs(directions, RenamePairs(directions.Allow, name, newName), RenamePairs(directions.Forbid, name, newName));
        });
    }

    /// <inheritdoc />
    public StructureChange RemoveModule(string folder, string name)
    {
        return Edit(folder, $"Removed module '{name}'.", (modules, directions) =>
        {
            modules.RemoveAt(FindModule(modules, name));
            var allow = WithoutModule(directions.Allow, name);
            if(directions.HasForbidList && directions.Allow.Count > 0 && allow.Count == 0 && modules.Count > 1)
                throw new RuleChangeException("Removing this module would remove the last allowed direction while both lists are present. Choose one direction list first.");

            return WithPairs(directions, allow, WithoutModule(directions.Forbid, name));
        });
    }

    /// <inheritdoc />
    public StructureChange AddPart(string folder, string name, string part)
    {
        return EditPart(folder, name, part, true);
    }

    /// <inheritdoc />
    public StructureChange RemovePart(string folder, string name, string part)
    {
        return EditPart(folder, name, part, false);
    }

    /// <inheritdoc />
    public StructureChange SetDirection(string folder, string from, string to, bool allowed)
    {
        var message = allowed ? $"Allowed {from} to use {to}." : $"Stopped allowing {from} to use {to}.";
        var initialized = false;
        var change = Edit(folder, message, (modules, directions) =>
        {
            FindModule(modules, from);
            FindModule(modules, to);
            if(from == to)
                throw new RuleChangeException("A module may always use itself.");

            if(allowed && !directions.HasAllowList && !directions.HasForbidList)
            {
                initialized = true;
                return new LayerDirections([new ModulePair(from, to)], [], true, false, directions.NoCycles);
            }

            if(directions.HasAllowList == directions.HasForbidList)
                throw new RuleChangeException("Set exactly one direction list, 'allow' or 'forbid', in the settings file before editing directions.");

            if(directions.Permits(from, to) == allowed)
                throw new RuleChangeException(allowed ? $"{from} may already use {to}." : $"{from} may not use {to}.");

            return directions.HasAllowList
                ? WithPairs(directions, ChangePair(directions.Allow, from, to, allowed), directions.Forbid)
                : WithPairs(directions, directions.Allow, ChangePair(directions.Forbid, from, to, !allowed));
        });
        return initialized ? new StructureChange(change.Path, change.Before, change.After, message + " Every other use between modules is now a finding.") : change;
    }

    /// <inheritdoc />
    public void Undo(string folder, StructureChange change)
    {
        var path = SettingsFileName.PathIn(folder);
        using var held = SettingsSaver.TakeLock(_settingsLock, path);
        var text = _file.ReadText(path);
        if(path != change.Path || text != change.After)
            throw new RuleChangeException("The settings file changed after this edit. Nothing was undone.");

        _saver.Restore(path, text, change.Before);
    }

    private StructureChange EditPart(string folder, string name, string part, bool add)
    {
        var selector = StructureReader.ParseSelector(part);
        var message = add ? $"Added '{part}' to '{name}'." : $"Removed '{part}' from '{name}'.";
        return Edit(folder, message, (modules, directions) =>
        {
            var index = FindModule(modules, name);
            var parts = modules[index].Selectors.ToList();
            var matching = parts.FindIndex(candidate => candidate.Kind == selector.Kind && candidate.Pattern == selector.Pattern);
            if(add == (matching >= 0))
                throw new RuleChangeException(add ? $"'{name}' already has the part '{part}'." : $"'{name}' does not have the part '{part}'.");

            if(add)
                parts.Add(selector);
            else
                parts.RemoveAt(matching);

            modules[index] = new ModuleDefinition(name, parts);
            return directions;
        });
    }

    private StructureChange Edit(string folder, string message, Func<List<ModuleDefinition>, LayerDirections, LayerDirections> edit)
    {
        var path = SettingsFileName.PathIn(folder);
        using var held = SettingsSaver.TakeLock(_settingsLock, path);
        var text = _file.ReadText(path);
        var settings = _file.Parse(text);
        var structure = settings.Structure;
        var modules = structure.Modules.ToList();
        var directions = edit(modules, structure.Directions);
        var updated = new StructureRules(modules, directions, structure.References, structure.Layout);
        var after = _saver.SaveStructure(path, text, new RuleSettings(settings.Requires, settings.Rules, updated, settings.ConfiguredStrictness, settings.Schema));
        return new StructureChange(path, text, after, message);
    }

    private static int FindModule(List<ModuleDefinition> modules, string name)
    {
        var index = modules.FindIndex(module => module.Name == name);
        return index >= 0 ? index : throw new RuleChangeException($"'{name}' is not a module.");
    }

    private static void CheckNewName(List<ModuleDefinition> modules, string name)
    {
        if(string.IsNullOrWhiteSpace(name) || name != name.Trim() || name.Contains("->", StringComparison.Ordinal))
            throw new RuleChangeException("Give the module a name without leading or trailing space or '->'.");

        if(modules.Exists(module => module.Name == name))
            throw new RuleChangeException($"A module named '{name}' already exists.");
    }

    private static LayerDirections WithPairs(LayerDirections current, IReadOnlyList<ModulePair> allow, IReadOnlyList<ModulePair> forbid)
    {
        return new LayerDirections(allow, forbid, current.HasAllowList, current.HasForbidList, current.NoCycles);
    }

    private static List<ModulePair> RenamePairs(IReadOnlyList<ModulePair> pairs, string name, string newName)
    {
        return pairs.Select(pair => new ModulePair(pair.From == name ? newName : pair.From, pair.To == name ? newName : pair.To)).ToList();
    }

    private static List<ModulePair> WithoutModule(IReadOnlyList<ModulePair> pairs, string name)
    {
        return pairs.Where(pair => pair.From != name && pair.To != name).ToList();
    }

    private static List<ModulePair> ChangePair(IReadOnlyList<ModulePair> pairs, string from, string to, bool add)
    {
        var updated = pairs.Where(pair => !pair.Is(from, to)).ToList();
        if(add)
            updated.Add(new ModulePair(from, to));

        return updated;
    }
}
