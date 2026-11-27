using System.Security.Cryptography;
using System.Text;
using CF.Core.Code;
using CF.Core.Findings;
using CF.Core.Ports;
using CF.Core.Rules;
using CF.Core.Rules.Settings;

namespace CF.Core.Services;

/// <inheritdoc cref="ISettingsService" />
public sealed class SettingsService : ISettingsService
{
    private static readonly IReadOnlyList<string> _words = ["error", "warning", "suggestion", "silent, none"];

    private readonly ISettingsLock _settingsLock;
    private readonly SettingsFile _file;
    private readonly SettingsSaver _saver;
    private readonly FileRules _fileRules;

    /// <summary>Creates a settings service for the installed languages.</summary>
    public SettingsService(IFileSystem fileSystem, ISettingsLock settingsLock, IReadOnlyList<ILanguage> languages)
    {
        _settingsLock = settingsLock;
        var reader = new SettingsReader(languages);
        _file = new SettingsFile(fileSystem, reader);
        _saver = new SettingsSaver(fileSystem, reader);
        _fileRules = new FileRules(languages);
    }

    /// <inheritdoc />
    public StrictnessReport Show(string folder)
    {
        var path = SettingsFileName.PathIn(folder);
        var strictness = _file.Read(path).Strictness;
        var mappings = new List<Strictness>
        {
            Strictness.For(StrictnessPreset.Quiet), Strictness.Standard, Strictness.For(StrictnessPreset.Strict)
        };
        if(strictness.Preset == StrictnessPreset.Custom)
            mappings.Add(strictness);

        var places = _fileRules.List(path, strictness).SelectMany(listing => listing.Rules).Select(rule => rule.ListedPlace).OfType<FileRulePlace>().ToList();
        var rows = _words.Select(word => new StrictnessRow(word, mappings.Select(mapping => mapping.EffectOf(word)).ToList())).ToList();
        return new StrictnessReport(strictness, mappings.Select(mapping => mapping.Preset).ToList(), rows,
            places.Count(place => place.Severity == Severity.Block), places.Count(place => place.Severity == Severity.Warn),
            places.Count(place => place.State == FileRuleState.Off));
    }

    /// <inheritdoc />
    public Strictness SetStrictness(string folder, StrictnessPreset preset)
    {
        return ChangeStrictness(folder, _ => Strictness.For(preset)).After;
    }

    /// <inheritdoc />
    public StrictnessChange ChangeStrictness(string folder, Func<Strictness, Strictness> change, string? expectedRevision = null)
    {
        var path = SettingsFileName.PathIn(folder);
        using var held = SettingsSaver.TakeLock(_settingsLock, path);
        var (text, previousRevision) = _file.ReadVersion(path);
        if(expectedRevision != null && expectedRevision != previousRevision)
            throw new RuleChangeException("Could not undo: commitfence.json changed on disk.");

        var settings = _file.Parse(text);
        var before = settings.Strictness;
        var after = change(before);
        if(after == settings.ConfiguredStrictness)
            return new StrictnessChange(before, after, previousRevision, previousRevision);

        var saved = _saver.SaveStrictness(path, text, new RuleSettings(settings.Requires, settings.Rules, settings.Structure, after, settings.Schema), previousRevision);
        // The file system port writes UTF-8 without a byte order mark.
        var revision = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(saved)));
        return new StrictnessChange(before, after, previousRevision, revision);
    }
}
