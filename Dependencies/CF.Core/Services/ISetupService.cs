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
    /// Writes a starter commitfence.json at the root of the repository the folder is in, unless one exists, and gives
    /// the hook line to add.
    /// </summary>
    SetupReport Init(string folder);
}