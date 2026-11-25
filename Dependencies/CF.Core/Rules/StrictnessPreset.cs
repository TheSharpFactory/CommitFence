namespace CF.Core.Rules;

/// <summary>The named strictness choices and a mapping supplied by the repository.</summary>
public enum StrictnessPreset
{
    /// <summary>Suggestions are off.</summary>
    Quiet,

    /// <summary>Warnings and suggestions warn.</summary>
    Standard,

    /// <summary>Warnings block.</summary>
    Strict,

    /// <summary>The settings file defines the effects.</summary>
    Custom
}