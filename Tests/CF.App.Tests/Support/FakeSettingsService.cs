using System.Globalization;
using CF.Core.Findings;
using CF.Core.Rules;
using CF.Core.Services;

namespace CF.App.Tests.Support;

/// <summary>
/// The settings service of the tests: it keeps the strictness in memory, counts the rules by effect the way the test sets them, and
/// refuses to save when the test gives it a reason.
/// </summary>
internal sealed class FakeSettingsService : ISettingsService
{
    private static readonly (string Word, int Count)[] _words =
        [("error", 41), ("warning", 6), ("suggestion", 20), ("silent, none", 11)];

    private Strictness _current = Strictness.Standard;
    private int _revision;

    /// <summary>
    /// The mapping the file holds when it is not a preset: the three words and what each does.
    /// </summary>
    public Strictness? Custom { get; set; }

    public Exception? SaveFailure { get; set; }

    /// <summary>
    /// When set, a save waits for it to be opened before it writes: the test chooses again while a save is in flight.
    /// </summary>
    public ManualResetEventSlim? SaveGate { get; set; }

    public List<StrictnessPreset> Saved { get; } = new List<StrictnessPreset>();

    public void OutsideEdit()
    {
        _revision++;
    }

    public StrictnessReport Show(string folder)
    {
        var current = Custom ?? _current;
        var mappings = new List<Strictness> { Strictness.For(StrictnessPreset.Quiet), Strictness.Standard, Strictness.For(StrictnessPreset.Strict) };
        if(Custom != null)
            mappings.Add(Custom);

        var rows = _words.Select(word => new StrictnessRow(word.Word, mappings.Select(mapping => mapping.EffectOf(word.Word)).ToList())).ToList();
        var counts = _words.ToLookup(word => current.EffectOf(word.Word), word => word.Count);
        return new StrictnessReport(current, mappings.Select(mapping => mapping.Preset).ToList(), rows,
            counts[Severity.Block].Sum(), counts[Severity.Warn].Sum(), counts[null].Sum());
    }

    public Strictness SetStrictness(string folder, StrictnessPreset preset)
    {
        return ChangeStrictness(folder, _ => Strictness.For(preset)).After;
    }

    public StrictnessChange ChangeStrictness(string folder, Func<Strictness, Strictness> change, string? expectedRevision = null)
    {
        SaveGate?.Wait();
        if(SaveFailure != null)
            throw SaveFailure;

        var previousRevision = _revision.ToString(CultureInfo.InvariantCulture);
        if(expectedRevision != null && previousRevision != expectedRevision)
            throw new RuleChangeException("Could not undo: commitfence.json changed on disk.");

        var before = Custom ?? _current;
        var after = change(before);
        Saved.Add(after.Preset);
        _current = after;
        Custom = after.Preset == StrictnessPreset.Custom ? after : null;
        return new StrictnessChange(before, after, previousRevision, (++_revision).ToString(CultureInfo.InvariantCulture));
    }
}

/// <summary>
/// The languages of a build, as the test sets them.
/// </summary>
internal sealed class FakeLanguageService : ILanguageService
{
    public IReadOnlyList<LanguageReport> Reports { get; set; } = [new LanguageReport("C#", [".cs"], 502, "A full review builds them with the repository's .NET SDK.")];

    public IReadOnlyList<LanguageReport> Show(string folder)
    {
        return Reports;
    }

    public IReadOnlyList<string> Tracked { get; set; } = [];

    public PathMatches? FindFiles(string folder, string languageId, string pattern)
    {
        var start = pattern.Replace("**", "");
        return new PathMatches(Tracked.Where(path => path.StartsWith(start.TrimEnd('*'), StringComparison.Ordinal)).ToList());
    }
}

/// <summary>
/// What the app keeps about itself on this machine, in memory.
/// </summary>
internal sealed class MemoryMachineSettings : CF.Core.Ports.IMachineSettings
{
    public string DataFolder { get; set; } = Samples.DataFolder;

    public bool ReduceMotion { get; set; }

    public void SaveReduceMotion(bool reduce)
    {
        ReduceMotion = reduce;
    }
}
