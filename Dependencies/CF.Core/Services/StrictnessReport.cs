using CF.Core.Findings;
using CF.Core.Rules;

namespace CF.Core.Services;

/// <summary>The strictness in effect, choices to display, and the Rules list's counts by effect.</summary>
public sealed record StrictnessReport(Strictness Current, IReadOnlyList<StrictnessPreset> Columns, IReadOnlyList<StrictnessRow> Rows,
    int Blocking, int Warning, int Off)
{
    /// <summary>The rule counts in the words shared by the settings views.</summary>
    public string CountSentence
    {
        get { return $"With {Current.Preset}, {Blocking} rules of the file block a push, {Warning} only warn and {Off} are off."; }
    }
}

/// <summary>A file severity and its push effect in each column. A null effect means off.</summary>
public sealed record StrictnessRow(string Word, IReadOnlyList<Severity?> Effects);