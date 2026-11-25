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
        SaveGate?.Wait();
        if(SaveFailure != null)
            throw SaveFailure;

        Saved.Add(preset);
        _current = Strictness.For(preset);
        Custom = null;
        return _current;
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
