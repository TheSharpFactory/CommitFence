using CF.Core.Setup;

namespace CF.Core.Services;

/// <summary>
/// Gets the program and a repository ready for the hook.
/// </summary>
public interface ISetupService
{
    /// <summary>
    /// Writes the full path of the program into the user's git settings and adds its folder to the user's PATH, or only
    /// says so when the options ask for a dry run.
    /// </summary>
    SetupReport Install(string programPath, InstallOptions options);

    /// <summary>
    /// Writes starter settings and sets up the selected hook or reports the changes in print mode.
    /// </summary>
    SetupReport Init(string folder, HookKind hook = HookKind.PrePush, bool print = false);

    /// <summary>Whether the repository's root settings file exists.</summary>
    bool HasSettings(string path);

    /// <summary>Writes only the starter settings, leaving an existing file as it is.</summary>
    SetupReport StartSettings(string folder);

    /// <summary>Adds the line Doctor previewed, provided the same insertion is still safe.</summary>
    SetupReport AddHookLine(string folder, HookAddition addition);
}