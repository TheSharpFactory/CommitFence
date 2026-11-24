using CF.Core.Findings;
using CF.Core.Ports;
using CF.Core.Rules;
using CF.Core.Rules.Settings;
using CF.Core.Setup;

namespace CF.Core.Review;

/// <summary>
/// Finds and loads the settings file for a review, and refuses the review when the program is older than the repository
/// asks for.
/// </summary>
public sealed class SettingsInTree
{
    private readonly IFileSystem _fileSystem;
    private readonly SettingsFile _settingsFile;
    private readonly string _programVersion;

    /// <summary>
    /// Creates the helper.
    /// </summary>
    public SettingsInTree(IFileSystem fileSystem, SettingsFile settingsFile, string programVersion)
    {
        _fileSystem = fileSystem;
        _settingsFile = settingsFile;
        _programVersion = programVersion;
    }

    /// <summary>
    /// A program older than the repository's requires key judges nothing: the review is incomplete, with the
    /// instruction to update. Returns true when it refused.
    /// </summary>
    internal bool RefusesOlderProgram(string settingsPath, ReviewRun run)
    {
        var refusal = VersionRequirement.Refusal(_settingsFile.ReadRequires(settingsPath), _programVersion);
        if(refusal == null)
            return false;

        run.AddCoverage(TreeUnderReview.DefaultSettingsFile, CoverageStatus.Failed, refusal);
        return true;
    }

    /// <summary>
    /// The settings in the file, which must exist.
    /// </summary>
    internal RuleSettings Read(string settingsPath)
    {
        return _settingsFile.Read(settingsPath);
    }

    /// <summary>
    /// The settings in the file. When the file is not required and does not exist there are none.
    /// </summary>
    internal RuleSettings? Load(string settingsPath, bool required)
    {
        if(!required && !_fileSystem.FileExists(settingsPath))
            return null;

        return Read(settingsPath);
    }
}