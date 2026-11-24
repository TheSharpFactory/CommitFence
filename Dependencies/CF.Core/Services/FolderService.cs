using CF.Core.Ports;
using CF.Core.Rules.Settings;

namespace CF.Core.Services;

/// <summary>
/// Describes a folder through the file system and git ports.
/// </summary>
public sealed class FolderService : IFolderService
{
    private readonly IFileSystem _fileSystem;
    private readonly IGit _git;

    /// <summary>
    /// Creates the service.
    /// </summary>
    public FolderService(IFileSystem fileSystem, IGit git)
    {
        _fileSystem = fileSystem;
        _git = git;
    }

    /// <inheritdoc />
    public OpenedFolder Open(string folder)
    {
        var folderPath = Path.GetFullPath(folder);
        var settingsPath = SettingsFileName.PathIn(folderPath);
        var settingsExist = _fileSystem.FileExists(settingsPath);
        var repository = FindRepository(folderPath);
        if(repository == null)
            return new OpenedFolder(folderPath, settingsPath, settingsExist, NameOf(folderPath), null, false);

        return new OpenedFolder(folderPath, settingsPath, settingsExist, NameOf(repository.WorkFolder), _git.CurrentBranch(repository), true);
    }

    private RepositoryLocation? FindRepository(string folderPath)
    {
        try
        {
            return _git.FindRepository(folderPath);
        }
        catch(GitException)
        {
            // git cannot be run here (or the folder is gone): the folder is described as it is, with no branch
            return null;
        }
    }

    private static string NameOf(string folderPath)
    {
        var trimmed = folderPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var name = Path.GetFileName(trimmed);

        return name.Length == 0 ? trimmed : name;
    }
}
