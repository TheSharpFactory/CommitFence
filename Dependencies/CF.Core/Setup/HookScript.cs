using System.Text;
using System.Text.RegularExpressions;
using CF.Core.Ports;

namespace CF.Core.Setup;

/// <summary>Reads hooks and plans edits without executing their contents.</summary>
internal sealed partial class HookScript(IDoctorGit git, IFileSystem files)
{
    private const string _gitLfsReason = "This hook also runs Git LFS, which reads the same input as CommitFence, so CommitFence does not change it.";
    private const string _outsideReason = "This hooks folder is outside the repository, so CommitFence leaves it to you.";
    private const string _wrapperReason = "The active hook is not a recognised Husky dispatcher, so CommitFence leaves it to you.";
    private const string _linkedReason = "The hook path goes through a link, so CommitFence leaves it to you.";
    private const string _huskyDispatcher = ". \"$(dirname \"$0\")/h\"";
    private static readonly UTF8Encoding _utf8 = new UTF8Encoding(false, true);
    private static readonly string[] _shells = ["#!/bin/sh", "#!/usr/bin/env sh", "#!/bin/bash", "#!/usr/bin/env bash"];
    private static readonly string[] _huskySources = [""". "$(dirname "$0")/_/husky.sh" """.TrimEnd(), """. "$(dirname "$0")/husky.sh" """.TrimEnd()];

    [GeneratedRegex("""^"\$\(\s*git[ \t]+config[ \t]+--get[ \t]+commitfence\.path\s*\)"[ \t]+review[ \t]+--hook[ \t]+pre-push[ \t]+"\$@"(?=$|[ \t;&|<>])""")]
    private static partial Regex EarlierPushLine();

    [GeneratedRegex("""^[ \t]*\|\|[ \t]*exit[ \t]+\$\?(?:[ \t]+#.*)?$""")]
    private static partial Regex Stop();

    [GeneratedRegex("""(?<![\w.-])git(?:-lfs(?:\.exe)?["']?|(?:\.exe)?["']?[ \t]+(?:-[^;&|<>\r\n]*?[ \t]+)?["']?lfs["']?)(?=$|[\s;&|()<>])""")]
    private static partial Regex GitLfs();

    public HookEdit Inspect(RepositoryLocation repository, HookKind kind)
    {
        var hook = kind == HookKind.PrePush ? "pre-push" : "pre-commit";
        var active = git.FindHook(repository, hook);
        var huskyFolder = Path.GetFullPath(Path.Combine(repository.WorkFolder, ".husky"));
        var isWrapperPath = SamePath(active, Path.Combine(huskyFolder, "_", hook));
        var reason = ManualReason(repository, active);
        var followsWrapper = isWrapperPath && reason == null && IsHuskyWrapper(active);
        var path = followsWrapper ? Path.Combine(huskyFolder, hook) : active;
        var husky = followsWrapper || SamePath(path, Path.Combine(huskyFolder, hook));
        var shown = Path.GetRelativePath(repository.WorkFolder, path).Replace('\\', '/');
        if(followsWrapper)
            reason = ManualReason(repository, path);
        else if(isWrapperPath)
            reason ??= _wrapperReason;

        var edit = new HookEdit(active, path, shown, kind, husky) { ManualReason = reason };
        if(files.IsLink(path))
            return edit;

        if(!files.FileExists(path))
        {
            if(reason == null && (husky || git.ReadHookSetting(repository, "core.hooksPath") == null))
            {
                var prefix = husky ? string.Empty : "#!/bin/sh\n";
                return edit with { Updated = prefix + HookLine.For(kind) + "\n", InsertionLine = husky ? 1 : 2 };
            }

            return edit;
        }

        var original = ReadBytes(path);
        edit = edit with { Original = original };
        try
        {
            return PlanExisting(edit, _utf8.GetString(original));
        }
        catch(DecoderFallbackException)
        {
            return edit;
        }
    }

    private string? ManualReason(RepositoryLocation repository, string path)
    {
        var root = IsInside(repository.WorkFolder, path) ? repository.WorkFolder : repository.GitDirectory;
        if(!IsInside(root, path))
            return _outsideReason;

        for(var entry = path; !SamePath(entry, root); entry = Path.GetDirectoryName(entry)!)
        {
            if(files.IsLink(entry))
                return _linkedReason;
        }

        return null;
    }

    private static bool IsInside(string folder, string path)
    {
        var relative = Path.GetRelativePath(folder, path);
        return relative != "." && relative != ".." && !Path.IsPathRooted(relative)
            && !relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal);
    }

    private bool IsHuskyWrapper(string path)
    {
        if(!files.FileExists(path))
            return false;

        try
        {
            var lines = DoctorExcerpt.ReadLines(_utf8.GetString(ReadBytes(path)));
            if(lines.Count == 0 || !_shells.Contains(lines[0], StringComparer.Ordinal))
                return false;

            var command = FindLine(lines, IsCommand);
            if(command < 0 || FindLine(lines, IsCommand, command + 1) >= 0)
                return false;

            var source = lines[command].Trim();
            return source == _huskyDispatcher || source == _huskySources[1];
        }
        catch(DecoderFallbackException)
        {
            return false;
        }
    }

    private byte[] ReadBytes(string path)
    {
        using var file = files.OpenRead(path);
        using var buffer = new MemoryStream();
        file.CopyTo(buffer);
        return buffer.ToArray();
    }

    private static HookEdit PlanExisting(HookEdit edit, string original)
    {
        var mark = original.StartsWith('\uFEFF') ? 1 : 0;
        var lines = DoctorExcerpt.ReadLines(original[mark..]);
        var review = FindLine(lines, line => ArgumentsEnd(line, edit.Kind) > 0);
        var continued = original[mark..].Replace("\\\r\n", "", StringComparison.Ordinal).Replace("\\\n", "", StringComparison.Ordinal);
        var runsLfs = FindLine(DoctorExcerpt.ReadLines(continued), line => IsCommand(line) && GitLfs().IsMatch(line)) >= 0;
        edit = edit with { Lines = lines, ReviewIndex = review, Manager = ManagerOf(lines), RunsGitLfs = runsLfs };
        if(runsLfs)
            edit = edit with { ManualReason = _gitLfsReason };

        if(review >= 0 || edit.ManualReason != null || edit.Manager != null || original.AsSpan().ContainsAny("\0\u0085\u2028\u2029\f") || mark != 0)
            return edit;

        var hasShell = lines.Count > 0 && _shells.Contains(lines[0], StringComparer.Ordinal);
        var first = FindLine(lines, IsCommand);
        var hasHuskySource = first >= 0 && hasShell && _huskySources.Contains(lines[first].Trim(), StringComparer.Ordinal);
        if(!hasHuskySource && first >= 0 && lines[first].Contains("husky.sh", StringComparison.Ordinal))
            return edit;

        var hasOtherShebang = lines.Count > 0 && lines[0].StartsWith("#!", StringComparison.Ordinal) && !hasShell;
        if(hasOtherShebang || (!hasShell && !edit.IsHusky))
            return edit;

        if(hasHuskySource)
        {
            edit = edit with { IsHusky = true };
            first = FindLine(lines, IsCommand, first + 1);
        }

        if(lines.Any(line => line.Trim() == _huskyDispatcher))
            return edit;

        return Insert(edit, original, first < 0 ? lines.Count : first);
    }

    private static HookEdit Insert(HookEdit edit, string original, int index)
    {
        var offset = 0;
        for(var line = 0; line < index; line++)
        {
            offset += edit.Lines[line].Length;
            if(offset == original.Length)
                break;

            offset += original.AsSpan(offset).StartsWith("\r\n", StringComparison.Ordinal) ? 2 : 1;
        }

        var separator = offset > 0 && original[offset - 1] is not ('\n' or '\r') ? "\n" : string.Empty;
        var updated = original.Insert(offset, separator + HookLine.For(edit.Kind) + "\n");
        return edit with { Updated = updated, InsertionLine = index + 1 };
    }

    public void Write(HookEdit edit)
    {
        if(edit.Updated == null)
            throw new InvalidOperationException("There is no hook edit to write.");

        if(edit.Original == null)
            files.CreateFolder(Path.GetDirectoryName(edit.Path)!);

        files.WriteHook(edit.Path, edit.Original, _utf8.GetBytes(edit.Updated));
    }

    public static bool StopsHook(string line, int argumentsEnd)
    {
        return argumentsEnd > 0 && Stop().IsMatch(line.AsSpan(argumentsEnd));
    }

    public static int ArgumentsEnd(string line, HookKind kind)
    {
        var command = kind == HookKind.PrePush ? HookLine.PrePushCommand : HookLine.PreCommitCommand;
        if(line.StartsWith(command, StringComparison.Ordinal))
        {
            var endsArguments = line.Length == command.Length || " \t;&|<>".Contains(line[command.Length], StringComparison.Ordinal);
            if(endsArguments)
                return command.Length;
        }

        return kind == HookKind.PrePush ? EarlierPushLine().Match(line).Length : 0;
    }

    public static int FirstCommand(IReadOnlyList<string> lines)
    {
        var first = FindLine(lines, IsCommand);
        return first >= 0 && _huskySources.Contains(lines[first].Trim(), StringComparer.Ordinal)
            ? FindLine(lines, IsCommand, first + 1) : first;
    }

    private static bool IsCommand(string line)
    {
        return line.Length != 0 && !line.StartsWith('#');
    }

    private static int FindLine(IReadOnlyList<string> lines, Func<string, bool> matches, int start = 0)
    {
        for(var index = start; index < lines.Count; index++)
        {
            if(matches(lines[index].Trim()))
                return index;
        }

        return -1;
    }

    private static string? ManagerOf(IReadOnlyList<string> lines)
    {
        foreach(var line in lines)
        {
            var text = line.Trim();
            if(text.Contains("lefthook", StringComparison.OrdinalIgnoreCase))
                return "lefthook";

            if(text.Contains("Generated by pre-commit", StringComparison.OrdinalIgnoreCase) || text.Contains("PRE_COMMIT_HOME", StringComparison.Ordinal))
                return "pre-commit";

            if(text.Contains("husky-run", StringComparison.OrdinalIgnoreCase) || text.Contains("husky > pre-", StringComparison.OrdinalIgnoreCase))
                return "husky";

            if(text.Contains("overcommit", StringComparison.OrdinalIgnoreCase))
                return "overcommit";
        }

        return null;
    }

    private static bool SamePath(string left, string right)
    {
        return string.Equals(Path.GetFullPath(left), Path.GetFullPath(right),
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
    }
}

internal sealed record HookEdit(string GitPath, string Path, string ShownPath, HookKind Kind, bool IsHusky)
{
    public byte[]? Original { get; init; }

    public string? Updated { get; init; }

    public int InsertionLine { get; init; }

    public IReadOnlyList<string> Lines { get; init; } = [];

    public int ReviewIndex { get; init; } = -1;

    public string? Manager { get; init; }

    public string? ManualReason { get; init; }

    public bool RunsGitLfs { get; init; }

    public bool HasReviewLine
    {
        get
        {
            if(ReviewIndex < 0)
                return false;

            var command = Lines[ReviewIndex].Trim();
            return HookScript.StopsHook(command, HookScript.ArgumentsEnd(command, Kind));
        }
    }
}
