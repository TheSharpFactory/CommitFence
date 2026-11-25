using CF.Core.Findings;

namespace CF.Core.Code;

/// <summary>A setting at one source location, including the language's path matcher.</summary>
public sealed record FileRulePlace(string Pattern, string Setting, FileRuleState State, Severity? Severity, int Line, IReadOnlyList<FileLine> Lines, Func<string, bool> Matches)
{
    /// <summary>Whether strictness switches off a rule that the file leaves on.</summary>
    public bool IsOffByStrictness
    {
        get { return State == FileRuleState.Off && StateOf(Setting) == FileRuleState.On; }
    }

    /// <summary>Classifies the word the file uses before assigning its effect on a push.</summary>
    public static FileRuleState StateOf(string setting)
    {
        return setting.ToLowerInvariant() switch
        {
            "error" or "warning" or "suggestion" => FileRuleState.On,
            "none" or "silent" => FileRuleState.Off,
            "default" => FileRuleState.OwnDefault,
            _ => FileRuleState.NotUnderstood
        };
    }

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