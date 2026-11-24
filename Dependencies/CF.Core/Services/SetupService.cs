using System.Security;
using CF.Core.Ports;
using CF.Core.Setup;

namespace CF.Core.Services;

/// <summary>
/// Installs the program for the user and sets a repository up. Both outside effects of install, the git settings and
/// the PATH, go through ports.
/// </summary>
public sealed class SetupService : ISetupService
{
    private const string _settingsFile = "commitfence.json";
    private static readonly string _starterSettings = string.Join("\n", "{", "    \"rules\": []", "}") + "\n";

    private readonly IUserGitSettings _gitSettings;
    private readonly IUserPath _userPath;
    private readonly IGit _git;
    private readonly IFileSystem _fileSystem;

    /// <summary>
    /// Creates the service.
    /// </summary>
    public SetupService(IUserGitSettings gitSettings, IUserPath userPath, IGit git, IFileSystem fileSystem)
    {
        _gitSettings = gitSettings;
        _userPath = userPath;
        _git = git;
        _fileSystem = fileSystem;
    }

    /// <inheritdoc />
    public SetupReport Install(string programPath, InstallOptions options)
    {
        var lines = new List<string>();
        try
        {
            InstallGitSetting(programPath, options, lines);
            InstallPathEntry(Path.GetDirectoryName(programPath) ?? string.Empty, options, lines);
        }
        catch(Exception exception) when(IsSetupFailure(exception))
        {
            lines.Add("Failed: " + exception.Message);
            return new SetupReport(lines, true);
        }

        return new SetupReport(lines, false);
    }

    /// <inheritdoc />
    public SetupReport Init(string folder)
    {
        try
        {
            return InitIn(folder);
        }
        catch(Exception exception) when(IsSetupFailure(exception))
        {
            return new SetupReport(new List<string> { "Failed: " + exception.Message }, true);
        }
    }

    private SetupReport InitIn(string folder)
    {
        var repository = _git.FindRepository(folder);
        if(repository == null)
            return new SetupReport(new List<string> { "init must be run inside a git repository: " + folder + " is not in one." }, true);

        var lines = new List<string>
        {
            WriteStarterSettings(repository.WorkFolder),
            "Add this line as the first command of the pre-push hook (with husky, the file .husky/pre-push):",
            HookLine.PrePush
        };

        return new SetupReport(lines, false);
    }

    private string WriteStarterSettings(string repositoryFolder)
    {
        var settingsPath = Path.Combine(repositoryFolder, _settingsFile);
        if(_fileSystem.FileExists(settingsPath))
            return $"{settingsPath} already exists and was left as it is.";

        try
        {
            _fileSystem.WriteNewFile(settingsPath, _starterSettings);
        }
        catch(IOException) when(_fileSystem.FileExists(settingsPath))
        {
            return $"{settingsPath} already exists and was left as it is.";
        }

        return $"Wrote {settingsPath}, with no rules yet. Add rules with commitfence rules add.";
    }

    /// <summary>
    /// The text as one literal argument of a POSIX shell: in single quotes, where every character stands for itself
    /// except the quote, which is written as a closed quote, an escaped quote and an opened quote.
    /// </summary>
    private static string QuoteForShell(string text)
    {
        return "'" + text.Replace("'", "'\\''") + "'";
    }

    private static bool IsSetupFailure(Exception exception)
    {
        return exception is GitException || exception is IOException || exception is UnauthorizedAccessException || exception is SecurityException;
    }

    private void InstallGitSetting(string programPath, InstallOptions options, List<string> lines)
    {
        if(_gitSettings.Read(HookLine.PathSetting) == programPath)
        {
            lines.Add($"The git setting {HookLine.PathSetting} already holds {programPath}.");
            return;
        }

        if(options.DryRun)
        {
            lines.Add($"Would set the git setting {HookLine.PathSetting} to {programPath}.");
            return;
        }

        _gitSettings.Write(HookLine.PathSetting, programPath);
        lines.Add($"Set the git setting {HookLine.PathSetting} to {programPath}.");
    }

    private void InstallPathEntry(string folder, InstallOptions options, List<string> lines)
    {
        if(!options.ChangePath)
        {
            lines.Add("Left the PATH as it is.");
            return;
        }

        if(!_userPath.CanChange)
        {
            lines.Add("This system sets the PATH in a shell profile. To type commitfence anywhere, add this line to it:");
            lines.Add("export PATH=\"$PATH:\"" + QuoteForShell(folder));
            return;
        }

        if(_userPath.Contains(folder))
        {
            lines.Add($"{folder} is already in your PATH.");
            return;
        }

        if(options.DryRun)
        {
            lines.Add($"Would add {folder} to your PATH, for programs started after that.");
            return;
        }

        _userPath.Add(folder);
        lines.Add($"Added {folder} to your PATH, for programs started after that.");
    }
}