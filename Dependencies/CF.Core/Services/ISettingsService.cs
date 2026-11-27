using CF.Core.Rules;

namespace CF.Core.Services;

/// <summary>Reads and changes the review policy for a folder.</summary>
public interface ISettingsService
{
    /// <summary>Reads the strictness, its table, and the effect counts from the folder's files.</summary>
    StrictnessReport Show(string folder);

    /// <summary>Saves a named preset in the folder's settings file.</summary>
    Strictness SetStrictness(string folder, StrictnessPreset preset);

    /// <summary>Changes strictness against the mapping on disk, optionally requiring a matching file revision.</summary>
    StrictnessChange ChangeStrictness(string folder, Func<Strictness, Strictness> change, string? expectedRevision = null);
}