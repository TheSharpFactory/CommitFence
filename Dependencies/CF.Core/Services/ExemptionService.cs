using CF.Core.Code;
using CF.Core.Ports;

namespace CF.Core.Services;

/// <inheritdoc cref="IExemptionService" />
public sealed class ExemptionService : IExemptionService
{
    private const int _linesAround = 3;

    private readonly IGit _git;
    private readonly IReadOnlyList<ILanguage> _languages;

    /// <summary>Creates the service for the installed languages.</summary>
    public ExemptionService(IGit git, IReadOnlyList<ILanguage> languages)
    {
        _git = git;
        _languages = languages;
    }

    /// <inheritdoc />
    public ExemptionScan Scan(string folder, bool withLines, CancellationToken cancellationToken)
    {
        try
        {
            var repository = _git.FindRepository(folder) ?? throw new ExemptionScanException($"{folder} is not in a Git repository.");
            var branch = _git.CheckedOutBranch(repository);
            var commit = _git.ResolveCommit(repository, "HEAD");
            var tree = commit == null ? null : _git.ResolveTree(repository, commit);
            if(commit == null || tree == null)
                return ExemptionScan.NoCommit(branch);

            return ExemptionScan.Of(branch, commit, ReadFiles(repository, tree, withLines, cancellationToken), repository.WorkFolder);
        }
        catch(GitException exception)
        {
            throw new ExemptionScanException(exception.Message, exception);
        }
        catch(AggregateException exception) when(exception.InnerExceptions.OfType<GitException>().FirstOrDefault() is { } git)
        {
            throw new ExemptionScanException(git.Message, git);
        }
    }

    private List<ExemptionEntry> ReadFiles(RepositoryLocation repository, string tree, bool withLines, CancellationToken cancellationToken)
    {
        var owned = _git.ListFiles(repository, tree, string.Empty)
            .Select(path => (Path: path, Language: _languages.FirstOrDefault(language => language.OwnsFile(path))))
            .Where(file => file.Language != null)
            .ToList();
        return owned.AsParallel()
            .AsOrdered()
            .WithCancellation(cancellationToken)
            .SelectMany(file => ReadFile(repository, tree, file.Path, file.Language!, withLines))
            .OrderBy(entry => entry.File, StringComparer.Ordinal)
            .ThenBy(entry => entry.Line)
            .ToList();
    }

    private List<ExemptionEntry> ReadFile(RepositoryLocation repository, string tree, string path, ILanguage language, bool withLines)
    {
        var text = _git.ReadFile(repository, tree, path);
        if(text == null)
            return [];

        IReadOnlyList<SourceExemption> exemptions;
        try
        {
            exemptions = language.ReadExemptions(new CodeFile(path, language.Id, text));
        }
        catch(CodeReadException)
        {
            return [];
        }

        // the file is split into lines once, whatever the number of its exemptions
        var starts = withLines && exemptions.Count > 0 ? Excerpt.LineStarts(text) : null;
        return exemptions.Select(exemption => Entry(path, text, starts, exemption)).ToList();
    }

    private static ExemptionEntry Entry(string path, string text, IReadOnlyList<int>? starts, SourceExemption exemption)
    {
        var lines = starts == null
            ? new List<FileLine>()
            : Excerpt.Around(text, starts, exemption.Line, _linesAround).Select(line => new FileLine(line.Number, text[line.Start..line.End])).ToList();
        var isFinding = exemption is { Form: ExemptionForm.AllowComment, Reason: null };
        return new ExemptionEntry(path, exemption.Line, exemption.Target, exemption.Form, exemption.Reason, isFinding, lines);
    }
}
