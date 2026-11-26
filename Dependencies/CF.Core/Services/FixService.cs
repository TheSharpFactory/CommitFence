using CF.Core.Code;
using CF.Core.Findings;
using CF.Core.Fixes;
using CF.Core.Ports;
using CF.Core.Review;

namespace CF.Core.Services;

/// <summary>Runs a language's fixer on the reviewed tree and applies a preview to a matching working file.</summary>
public sealed class FixService(IReadOnlyList<ILanguage> languages, ISnapshotStore snapshots, IReviewLock reviewLock, IFileSystem files) : IFixService
{
    private const string _changedFile = "The file changed after the review. Review again to fix this one.";

    /// <inheritdoc />
    public FixAnswer SuggestFix(FixRequest request)
    {
        var finding = request.Finding;
        if(finding.Source != FindingSource.EditorConfig || finding.File == null)
            return new FixAnswer();

        var fixer = languages.FirstOrDefault(language => language.Id == finding.Language)?.Fixer;
        if(fixer == null)
            return new FixAnswer();

        try
        {
            using var held = reviewLock.Acquire(request.Repository);
            return Preview(request, fixer, finding.File);
        }
        catch(Exception exception)
        {
            return new FixAnswer(Reason: ReviewBoundary.MessageOf(exception));
        }
    }

    /// <inheritdoc />
    public FixApplied ApplyFix(Fix fix)
    {
        try
        {
            using var held = reviewLock.Acquire(fix.Repository);
            var folder = fix.Repository.WorkFolder;
            if(!TryFileIn(folder, fix.File, out var path) || !Matches(path, fix.Before))
                return new FixApplied(_changedFile) { FileChanged = true };

            return Replace(fix, path);
        }
        catch(Exception exception) when(exception is IOException or UnauthorizedAccessException or ArgumentException or TimeoutException)
        {
            return new FixApplied(exception.Message);
        }
    }

    private FixAnswer Preview(FixRequest request, IFixer fixer, string file)
    {
        var repository = request.Repository;
        var folder = snapshots.MoveTo(repository, request.Tree);
        try
        {
            if(!TryFileIn(folder, file, out var path) || !files.FileExists(path))
                return new FixAnswer();

            var project = request.Finding.ProjectFile;
            if(project != null && !TryFileIn(folder, project, out _))
                return new FixAnswer(Reason: "The build project is outside the reviewed tree.");

            var before = ReadBytes(path);
            var originalUntracked = snapshots.ReadUntrackedFiles(repository).ToHashSet(StringComparer.Ordinal);
            var freshFiles = new List<string>();
            FixAttempt attempt;
            try
            {
                attempt = fixer.Change(folder, request.Finding);
            }
            finally
            {
                freshFiles.AddRange(snapshots.ReadUntrackedFiles(repository).Where(path => !originalUntracked.Contains(path)));
                foreach(var freshFile in freshFiles)
                {
                    if(TryFileIn(folder, freshFile, out var freshPath))
                        files.DeleteFile(freshPath);
                }
            }

            if(!attempt.Ran || attempt.Reason != null)
                return new FixAnswer(Reason: attempt.Reason);

            if(freshFiles.Count > 0)
                return new FixAnswer(Reason: "The formatter created another file.");

            if(snapshots.ReadChangedFiles(repository).Any(changed => !string.Equals(changed, file, StringComparison.Ordinal)))
                return new FixAnswer(Reason: "The formatter changed another file.");

            if(!TryFileIn(folder, file, out path) || !files.FileExists(path))
                return new FixAnswer(Reason: "The formatter did not leave a regular file to apply.");

            var after = FixSource.KeepEndings(before, ReadBytes(path));
            if(before.AsSpan().SequenceEqual(after))
                return new FixAnswer();

            var original = FixSource.Read(before);
            var formatted = FixSource.Read(after);
            var patch = FixSource.SameLines(original, formatted) ? string.Empty
                : snapshots.MakePatch(repository, file, original.Text, formatted.Text);
            return new FixAnswer(new Fix(repository, file, request.Finding.RuleId, before, after, patch,
                FixSource.DescribeChanges(original, formatted, patch.Length == 0)));
        }
        finally
        {
            snapshots.MoveTo(repository, request.Tree);
        }
    }

    private FixApplied Replace(Fix fix, string path)
    {
        var temporaryPath = path + $".{Guid.NewGuid():N}.new";
        files.WriteNewFile(temporaryPath, fix.After.Span);
        try
        {
            if(!TryFileIn(fix.Repository.WorkFolder, fix.File, out _) || !Matches(path, fix.Before))
                return new FixApplied(_changedFile) { FileChanged = true };

            files.ReplaceFile(temporaryPath, path);
            return new FixApplied();
        }
        finally
        {
            files.DeleteFile(temporaryPath);
        }
    }

    private bool Matches(string path, ReadOnlyMemory<byte> expected)
    {
        return files.FileExists(path) && ReadBytes(path).AsSpan().SequenceEqual(expected.Span);
    }

    private byte[] ReadBytes(string path)
    {
        using var input = files.OpenRead(path);
        using var bytes = new MemoryStream();
        input.CopyTo(bytes);
        return bytes.ToArray();
    }

    private bool TryFileIn(string folder, string file, out string path)
    {
        var root = files.ResolveRealPath(folder);
        path = Path.GetFullPath(file, root);
        var relative = Path.GetRelativePath(root, path).Replace(Path.DirectorySeparatorChar, '/');
        return !Path.IsPathRooted(file) && file.Length > 0
            && string.Equals(relative, file, StringComparison.Ordinal)
            && !relative.Split('/').Any(part => part is "." or ".." || part.Contains(':'))
            && string.Equals(files.ResolveRealPath(path), path, StringComparison.Ordinal);
    }
}
