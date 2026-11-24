namespace CF.Core.Services;

/// <summary>
/// The folder a program works on: where its settings file is, and which repository and branch it is in.
/// </summary>
public sealed class OpenedFolder
{
    /// <summary>
    /// Creates the description of a folder.
    /// </summary>
    public OpenedFolder(string folderPath, string settingsPath, bool settingsExist, string name, string? branch, bool insideRepository)
    {
        FolderPath = folderPath;
        SettingsPath = settingsPath;
        SettingsExist = settingsExist;
        Name = name;
        Branch = branch;
        InsideRepository = insideRepository;
    }

    /// <summary>
    /// The full path of the folder.
    /// </summary>
    public string FolderPath { get; }

    /// <summary>
    /// The full path of the settings file of the folder, whether it exists or not.
    /// </summary>
    public string SettingsPath { get; }

    /// <summary>
    /// Whether the settings file exists.
    /// </summary>
    public bool SettingsExist { get; }

    /// <summary>
    /// The name of the repository (the name of its top folder), or of the folder when it is not in a repository.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// The branch the repository is on, or the short id of a commit when HEAD is detached; nothing outside a
    /// repository or when git cannot say.
    /// </summary>
    public string? Branch { get; }

    /// <summary>
    /// Whether the folder is inside a git repository.
    /// </summary>
    public bool InsideRepository { get; }
}
