using CF.Core.Findings;

namespace CF.Core.Code;

/// <summary>A setting at one source location, including the language's path matcher.</summary>
public sealed record FileRulePlace(string Pattern, string Setting, FileRuleState State, Severity? Severity, int Line, IReadOnlyList<FileLine> Lines, Func<string, bool> Matches)
{
    /// <summary>The effect in display form.</summary>
    public string Effect
    {
        get
        {
            return State switch
            {
                FileRuleState.On => Severity == Findings.Severity.Block ? "Blocks" : "Warns",
                FileRuleState.Off => "Off",
                FileRuleState.OwnDefault => "default",
                FileRuleState.NotUnderstood => "not understood",
                _ => throw new ArgumentOutOfRangeException(nameof(State))
            };
        }
    }
}