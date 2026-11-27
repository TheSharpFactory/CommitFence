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
    /// <summary>The exact settings content written by setup.</summary>
    public const string StarterSettings = "{\n    \"rules\": []\n}\n";

    private readonly IUserGitSettings _gitSettings;
    private readonly IUserPath _userPath;
    private readonly IGit _git;
    private readonly IFileSystem _fileSystem;
    private readonly HookScript _hooks;

    /// <summary>
    /// Creates the service.
    /// </summary>
    public SetupService(IUserGitSettings gitSettings, IUserPath userPath, IGit git, IFileSystem fileSystem, IDoctorGit hookGit)
    {
        _gitSettings = gitSettings;
        _userPath = userPath;
        _git = git;
        _fileSystem = fileSystem;
        _hooks = new HookScript(hookGit, fileSystem);
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
    public SetupReport Init(string folder, HookKind hook = HookKind.PrePush, bool print = false)
    {
        var lines = new List<string>();
        try
        {
            var repository = _git.FindRepository(folder);
            if(repository == null)
                return new SetupReport([$"init must be run inside a git repository: {folder} is not in one."], true);

            var edit = _hooks.Inspect(repository, hook);
            lines.Add(WriteStarterSettings(repository.WorkFolder, print).Message);
            DescribeHook(edit, print, lines);
            return new SetupReport(lines, false);
        }
        catch(Exception exception) when(IsSetupFailure(exception))
        {
            lines.Add("Failed: " + DoctorCheck.Message(exception));
            return new SetupReport(lines, true);
        }
    }

    private void DescribeHook(HookEdit edit, bool print, List<string> lines)
    {
        var path = DisplayPath(edit.Path);
        if(edit.HasReviewLine)
        {
            lines.Add($"{path} already contains the CommitFence line and was left as it is.");
            if(edit.ManualReason != null)
                lines.Add(edit.ManualReason);

            DescribeInactiveHook(edit.GitPath, lines);
            return;
        }

        if(edit.Updated == null)
        {
            lines.AddRange(ManualHookInstructions(edit));
            DescribeInactiveHook(edit.GitPath, lines);
            return;
        }

        if(print)
        {
            if(edit.Original == null && !edit.IsHusky)
                lines.Add($"Would create {path} with #!/bin/sh and add this line at line {edit.InsertionLine}:");
            else
                lines.Add($"Would add this line to {path} at line {edit.InsertionLine}:");

            lines.Add(HookLine.For(edit.Kind));
            return;
        }

        try
        {
            _hooks.Write(edit);
        }
        catch(Exception exception) when(IsSetupFailure(exception))
        {
            throw new IOException(Failure(path, exception), exception);
        }

        lines.Add($"Added the CommitFence line to {path} at line {edit.InsertionLine}.");
        if(!OperatingSystem.IsWindows())
            lines.Add($"Made {path} executable.");

        if(edit.GitPath != edit.Path)
            DescribeInactiveHook(edit.GitPath, lines);
    }

    private void DescribeInactiveHook(string path, List<string> lines)
    {
        if(_fileSystem.FileExists(path) && !_fileSystem.IsExecutable(path))
            lines.Add($"{DisplayPath(path)} is inactive because it is not executable.");
    }

    private static List<string> ManualHookInstructions(HookEdit edit)
    {
        var path = DisplayPath(edit.Path);
        if(edit.ManualReason == null && edit.Manager == "lefthook")
        {
            var advice = new List<string> { $"lefthook generated {path}.", LefthookAdvice.For(edit.ShownPath).ToString(), LefthookAdvice.Entry(edit.Kind) };
            if(edit.Kind == HookKind.PrePush)
                advice.Add(LefthookAdvice.MergeHint.ToString());

            return advice;
        }

        var lines = new List<string>();
        if(edit.ManualReason != null)
            lines.Add(edit.ManualReason);

        lines.Add(edit.RunsGitLfs
            ? $"To use this line in {path}, arrange for both commands to get the hook input:"
            : $"Add this line as the first command of {path}:");
        lines.Add(HookLine.For(edit.Kind));
        return lines;
    }

    /// <inheritdoc />
    public bool HasSettings(string path)
    {
        return _fileSystem.FileExists(path);
    }

    /// <inheritdoc />
    public SetupReport StartSettings(string folder)
    {
        string? repositoryFolder = null;
        try
        {
            var repository = _git.FindRepository(folder);
            if(repository == null)
                return SettingsWriteFailure("the folder is not in a Git repository", "Open a Git repository, then try again.");

            repositoryFolder = repository.WorkFolder;
            var (message, created) = WriteStarterSettings(repositoryFolder);
            return new SetupReport([message], false) { SettingsCreated = created, WorktreeRoot = created ? repository.WorkFolder : null };
        }
        catch(Exception exception) when(IsSetupFailure(exception))
        {
            if(repositoryFolder != null && _fileSystem.FolderExists(Path.Combine(repositoryFolder, _settingsFile)))
                return SettingsWriteFailure($"a folder named {_settingsFile} already exists there", $"Move or rename the {_settingsFile} folder, then try again.");

            var reason = DoctorCheck.Message(exception).TrimEnd('.');
            var remedy = repositoryFolder != null && IsFolderAccessFailure(exception) ? "Make the folder writable and try again" : "Try again";
            return SettingsWriteFailure(reason, $"{remedy}, or copy the three lines and create the file yourself.");
        }
    }

    private static bool IsFolderAccessFailure(Exception exception)
    {
        if(exception is UnauthorizedAccessException or SecurityException)
            return true;

        // Windows ERROR_WRITE_PROTECT and Unix EROFS surface as IOExceptions.
        return exception is IOException && exception.HResult == (OperatingSystem.IsWindows() ? unchecked((int)0x80070013) : 30);
    }

    private static SetupReport SettingsWriteFailure(string reason, string remedy)
    {
        return new SetupReport([$"Could not write {_settingsFile}: {reason}.", $"Nothing was written. {remedy}"], true);
    }

    /// <inheritdoc />
    public SetupReport AddHookLine(string folder, HookAddition addition)
    {
        var shown = DisplayPath(addition.Before.Path);
        try
        {
            var repository = _git.FindRepository(folder);
            if(repository == null)
                return new SetupReport([$"Could not write {shown}: the folder is not in a Git repository.", "Nothing was changed. Open a Git repository, then check again."], true);

            shown = Path.GetRelativePath(repository.WorkFolder, addition.Before.Path).Replace('\\', '/');
            var edit = _hooks.Inspect(repository, addition.Kind);
            if(edit.ManualReason != null)
                return new SetupReport([$"Could not write {shown}: the hook can no longer be edited here.", $"Nothing was changed. {edit.ManualReason} Check again."], true);

            var matches = edit.Path == addition.Before.Path && edit.Updated != null && edit.Original != null
                && edit.InsertionLine == addition.After.Line && DoctorExcerpt.ReadLines(edit.Updated).SequenceEqual(addition.After.Lines.Select(line => line.Text));
            if(!matches)
                return new SetupReport([$"Could not write {shown}: the hook changed since it was checked.", "Nothing was changed. Check again for the current script."], true);

            _hooks.Write(edit);
            return new SetupReport([$"Added the CommitFence line to {shown} at line {edit.InsertionLine}."], false) { WorktreeRoot = repository.WorkFolder };
        }
        catch(Exception exception) when(IsSetupFailure(exception))
        {
            var reason = DoctorCheck.Message(exception).TrimEnd('.');
            var next = exception is UnauthorizedAccessException
                ? $"Make it writable and try again, or copy the line and add it by hand as line {addition.After.Line}."
                : $"Check again to confirm the insertion point, then retry or copy the line and add it by hand as line {addition.After.Line}.";
            return new SetupReport([$"Could not write {shown}: {reason}.", $"Nothing was changed. {next}"], true);
        }
    }

    private static string Failure(string name, Exception exception)
    {
        return $"Could not write {name}: {DoctorCheck.Message(exception).TrimEnd('.')}. Nothing was changed.";
    }

    private static string DisplayPath(string path)
    {
        return path.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
    }

    private (string Message, bool Created) WriteStarterSettings(string repositoryFolder, bool print = false)
    {
        var settingsPath = Path.GetFullPath(Path.Combine(repositoryFolder, _settingsFile));
        if(_fileSystem.FileExists(settingsPath))
            return ($"{settingsPath} already exists and was left as it is.", false);

        if(print)
            return ($"Would write {settingsPath}, with no rules yet.", false);

        try
        {
            _fileSystem.WriteNewFile(settingsPath, StarterSettings);
        }
        catch(IOException) when(_fileSystem.FileExists(settingsPath))
        {
            return ($"{settingsPath} already exists and was left as it is.", false);
        }

        return ($"Wrote {settingsPath}, with no rules yet. Add rules with commitfence rules add.", true);
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
