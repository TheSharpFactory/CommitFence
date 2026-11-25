using CF.Core.Findings;

namespace CF.Core.Rules;

/// <summary>The effect of the three configurable severity words. A null effect means off.</summary>
public sealed record Strictness(StrictnessPreset Preset, Severity? Error, Severity? Warning, Severity? Suggestion)
{
    /// <summary>The mapping used when the settings file leaves strictness out.</summary>
    public static Strictness Standard { get; } = new Strictness(StrictnessPreset.Standard, Severity.Block, Severity.Warn, Severity.Warn);

    /// <summary>Builds a named mapping.</summary>
    public static Strictness For(StrictnessPreset preset)
    {
        return preset switch
        {
            StrictnessPreset.Quiet => new Strictness(preset, Severity.Block, Severity.Warn, null),
            StrictnessPreset.Standard => Standard,
            StrictnessPreset.Strict => new Strictness(preset, Severity.Block, Severity.Block, Severity.Warn),
            StrictnessPreset.Custom or _ => throw new ArgumentOutOfRangeException(nameof(preset))
        };
    }

    /// <summary>Reads the name of a selectable preset as written in settings and command arguments.</summary>
    public static StrictnessPreset? ReadPreset(string? name)
    {
        return name switch
        {
            "quiet" => StrictnessPreset.Quiet,
            "standard" => StrictnessPreset.Standard,
            "strict" => StrictnessPreset.Strict,
            _ => null
        };
    }

    /// <summary>Gets the push effect of a word the file uses.</summary>
    public Severity? EffectOf(string word)
    {
        return word.ToLowerInvariant() switch
        {
            "error" => Error,
            "warning" => Warning,
            "suggestion" => Suggestion,
            _ => null
        };
    }
}