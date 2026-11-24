namespace CF.Core.Rules.Settings;

/// <summary>
/// The name of the settings file, where each program looks for it, and how a problem in it is shown. Both programs
/// say the same thing.
/// </summary>
public static class SettingsFileName
{
    /// <summary>
    /// The name of the settings file at the root of a team's repository.
    /// </summary>
    public const string Default = "commitfence.json";

    /// <summary>
    /// The full path of the settings file of a folder: the file of that name in the folder itself.
    /// </summary>
    public static string PathIn(string folder)
    {
        return Path.GetFullPath(Default, folder);
    }

    /// <summary>
    /// A problem in the settings file as one line: the path, then the line and column when the file says where.
    /// </summary>
    public static string FormatError(string settingsPath, SettingsException exception)
    {
        if(exception.Line == null)
            return $"{settingsPath}: {exception.Message}";

        return $"{settingsPath}({exception.Line},{exception.Column}): {exception.Message}";
    }
}
