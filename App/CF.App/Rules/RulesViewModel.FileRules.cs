using CF.Core.Code;
using CF.Core.Findings;
using CF.Core.Rules;
using Avalonia.Threading;

namespace CF.App.Rules;

/// <summary>
/// The second group of the list: the rules the repository's own .editorconfig sets. They are asked of the rule service, off the
/// interface's thread (the listing starts the SDK's tool to find the titles and takes about half a second), and the group
/// arrives when it is ready. They are listed with their effect as a fact: nothing here changes them.
/// </summary>
internal sealed partial class RulesViewModel
{
    private sealed record FileAnswer(IReadOnlyList<FileRuleListing>? Listings, string? Problem);

    /// <summary>
    /// Whether the group of .editorconfig has been read (or has said why it could not be): it is still being read before that.
    /// </summary>
    public bool FileRulesAreRead { get; private set; }

    /// <summary>
    /// Raised on the interface's thread each time the rules of .editorconfig have been listed and are in the list.
    /// </summary>
    public event Action? FileRulesArrived;

    /// <summary>
    /// Asks for the rules of .editorconfig again, off the interface's thread, and shows them when they arrive. The rows
    /// shown stay until then.
    /// </summary>
    public void ReloadFileRules()
    {
        var generation = ++_fileReading;
        Task.Run(ReadFileRules).ContinueWith(task => Dispatcher.UIThread.Post(() => ShowFileRules(generation, task.Result)), TaskScheduler.Default);
    }

    private FileAnswer ReadFileRules()
    {
        try
        {
            return new FileAnswer(_rules.ListFileRules(_folder.SettingsPath), null);
        }
        catch(RuleChangeException exception)
        {
            return new FileAnswer(null, exception.Message);
        }
    }

    private void ShowFileRules(int generation, FileAnswer answer)
    {
        if(generation != _fileReading)
            return;

        _fileProblem = answer.Problem;
        _fileName = answer.Listings?.Select(listing => listing.File).FirstOrDefault(file => file != null);
        _fileRules = answer.Listings == null ? new List<FileRule>() : answer.Listings.SelectMany(listing => listing.Rules).ToList();
        _fileNotes = answer.Listings == null ? new List<string>() : answer.Listings.SelectMany(listing => listing.Notes).ToList();
        FileRulesAreRead = true;
        UpdateFileRows();
        Rebuild();
        FileRulesArrived?.Invoke();
    }

    private void UpdateFileRows()
    {
        var ids = new HashSet<string>(_fileRules.Select(rule => rule.Id));
        foreach(var stale in _fileRows.Keys.Where(id => !ids.Contains(id)).ToList())
            _fileRows.Remove(stale);

        foreach(var rule in _fileRules)
        {
            if(_fileRows.TryGetValue(rule.Id, out var row))
                row.Update(rule);
            else
                _fileRows[rule.Id] = new FileRuleRowViewModel(rule, OpenEditorConfig);
        }

        if(SelectedItem is FileRuleRowViewModel selected && _fileRows.TryGetValue(selected.Id, out var current))
            FilePage.Show(current.Rule);
    }

    private void OpenEditorConfig()
    {
        var folder = Path.GetDirectoryName(_folder.SettingsPath) ?? string.Empty;
        OpenFileRequested?.Invoke(Path.Combine(folder, _fileName ?? _fileGroupName));
    }

    private void UpdateFileHeading()
    {
        var hasFile = FileRulesAreRead && _fileProblem == null && _fileName != null;
        _fileHeading.ShowsCount = hasFile;
        _fileHeading.Count = _fileRules.Count;
        _fileHeading.HasOpenFile = hasFile;
        _fileHeading.NoteHasLock = FileRulesAreRead;
        _fileHeading.Note = FileRulesAreRead ? _fileNote : _readingNote;
    }

    private IEnumerable<RuleListItemViewModel> CreateFileGroupItems()
    {
        yield return _fileHeading;
        if(!_fileHeading.IsExpanded || !FileRulesAreRead)
            yield break;

        if(_fileProblem != null)
        {
            yield return new GroupNoteViewModel(_fileProblem, false);
            yield break;
        }

        if(_fileName == null)
        {
            yield return new GroupNoteViewModel($"No {_fileGroupName} in {_folder.Name}.", false);
            yield break;
        }

        foreach(var note in _fileNotes)
            yield return new GroupNoteViewModel(note, true);

        var shown = _fileRules.Where(rule => PassesFile(SelectedFilter.Filter, rule) && MatchesFileSearch(rule)).ToList();
        if(shown.Count == 0 && SearchText.Trim().Length == 0)
            yield return new GroupNoteViewModel(EmptyFileGroupText(), _fileRules.Count > 0);

        foreach(var rule in shown)
            yield return _fileRows[rule.Id];
    }

    private static bool PassesFile(RuleFilter filter, FileRule rule)
    {
        return filter switch
        {
            RuleFilter.All => true,
            RuleFilter.Blocks => rule.State == FileRuleState.On && rule.Severity == Severity.Block,
            RuleFilter.Warns => rule.State == FileRuleState.On && rule.Severity == Severity.Warn,
            RuleFilter.Off => rule.State == FileRuleState.Off,
            _ => throw new ArgumentOutOfRangeException(nameof(filter), filter, null)
        };
    }

    private bool MatchesFileSearch(FileRule rule)
    {
        var text = SearchText.Trim();
        if(text.Length == 0)
            return true;

        return rule.Id.Contains(text, StringComparison.OrdinalIgnoreCase) || (rule.Title?.Contains(text, StringComparison.OrdinalIgnoreCase) ?? false);
    }

    private string EmptyFileGroupText()
    {
        if(_fileRules.Count == 0)
            return $"No rules set in {_fileGroupName}.";

        return SelectedFilter.Filter switch
        {
            RuleFilter.All => $"No rules set in {_fileGroupName}.",
            RuleFilter.Blocks => $"No {_fileGroupName} rule blocks.",
            RuleFilter.Warns => $"No {_fileGroupName} rule warns.",
            RuleFilter.Off => $"No {_fileGroupName} rule is off.",
            _ => throw new ArgumentOutOfRangeException(nameof(SelectedFilter))
        };
    }
}
