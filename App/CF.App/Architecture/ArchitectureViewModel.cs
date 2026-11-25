using Avalonia.Threading;
using CF.App.Rules;
using CF.App.Shell;
using CF.Core.Ports;
using CF.Core.Rules;
using CF.Core.Rules.Settings;
using CF.Core.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CF.App.Architecture;

/// <summary>The structure service's report and the edits available for the chosen module.</summary>
internal sealed partial class ArchitectureViewModel : ObservableObject, IScreenModel, IDisposable
{
    private readonly IStructureService _service;
    private readonly IClock _clock;
    private readonly OpenedFolder _folder;
    private readonly ISettingsChangeSource _changes;
    private readonly Func<StructureJudgment, bool> _holdsReview;
    private readonly Action<StructureJudgment> _openReview;
    private readonly DispatcherTimer _undoTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
    private StructureChange? _created;
    private StructureChange? _removed;
    private string? _removedName;

    public ArchitectureViewModel(IStructureService service, IClock clock, OpenedFolder folder, ISettingsChangeSource changes,
        Func<StructureJudgment, bool> holdsReview, Action<StructureJudgment> openReview)
    {
        _service = service;
        _clock = clock;
        _folder = folder;
        _changes = changes;
        _holdsReview = holdsReview;
        _openReview = openReview;
        _changes.Changed += OnFileChanged;
        _undoTimer.Tick += (_, _) => ClearUndo();
    }

    public event Action? Changed;
    public event Action<string>? OpenFileRequested;

    public StructureReport Report { get; private set; } = new StructureReport([], new StructureDirectionLists(null, null), [], null);
    public ModuleLayers Layers { get; private set; } = new ModuleLayers(new StructureReport([], new StructureDirectionLists(null, null), [], null));
    public StructureModuleReport? Selected { get; private set; }
    public string SelectedName
    {
        get { return Selected?.Name ?? string.Empty; }
    }
    public string MoreName
    {
        get { return "More for " + SelectedName; }
    }
    public IReadOnlyList<string> MayUse
    {
        get { return ListsAreAbsent ? [] : Selected?.MayUse ?? []; }
    }
    public IReadOnlyList<string> UsedBy
    {
        get { return ListsAreAbsent ? [] : Selected?.UsedBy ?? []; }
    }
    public bool HasModules
    {
        get { return Selected != null; }
    }
    public string? Banner { get; private set; }
    public bool HasBanner
    {
        get { return Banner != null; }
    }
    public string? Notice { get; private set; }
    public bool HasNotice
    {
        get { return Notice != null; }
    }
    public bool CanOpenFile
    {
        get { return File.Exists(_folder.SettingsPath); }
    }
    public bool IsWritable { get; private set; }
    public bool CanEdit
    {
        get { return IsWritable && !EditingName; }
    }
    public bool EditingName { get; private set; }
    public bool HasUndo
    {
        get { return _removed != null; }
    }
    public string? UndoText
    {
        get { return _removedName == null ? null : $"'{_removedName}' removed."; }
    }
    public string Count
    {
        get { return Report.CountSentence; }
    }
    public string Judgment
    {
        get { return Report.JudgmentSentence(_clock.Now); }
    }
    public bool IsUnjudged
    {
        get { return Report.LastJudgment == null; }
    }
    public bool IsClean
    {
        get { return !IsUnjudged && ActiveBreaches.Count == 0; }
    }
    public bool HasJudgedBreaches
    {
        get { return ActiveBreaches.Count > 0; }
    }
    public IReadOnlyList<StructureDirection> Directions
    {
        get { return ListsAreAbsent ? [] : Report.AllowedDirections; }
    }
    public IReadOnlyList<StructureBreach> ActiveBreaches { get; private set; } = [];
    public StructureBreach? DrawnBreach
    {
        get { return ActiveBreaches.Count == 0 ? null : ActiveBreaches[0]; }
    }
    public StructureBreach? SelectedBreach
    {
        get { return ActiveBreaches.FirstOrDefault(breach => breach.FromModule == SelectedName); }
    }
    public bool HasSelectedBreach
    {
        get { return SelectedBreach != null; }
    }
    public bool HasMoreBreaches
    {
        get { return MoreBreaches != null; }
    }
    public bool CanOpenReview
    {
        get { return HasSelectedBreach && Report.LastJudgment is { } judgment && _holdsReview(judgment); }
    }
    public IReadOnlyList<StructureBreach> ShownBreaches
    {
        get
        {
            return ActiveBreaches.Where(breach => breach.FromModule == SelectedName).Take(3).ToList();
        }
    }

    public string? MoreBreaches
    {
        get
        {
            var more = ActiveBreaches.Count(breach => breach.FromModule == SelectedName) - 3;
            return more > 0 ? $"and {more} more" : null;
        }
    }

    public string BreachContext
    {
        get
        {
            return Report.LastJudgment is { } judgment
                ? $"Found by the {Report.JudgmentScope(_clock.Now)}. " +
                    (judgment.Folder == null ? "It blocks the push." : "It would block a push.")
                : string.Empty;
        }
    }

    private bool ListsAreAbsent
    {
        get { return Report.Directions is { Allow: null, Forbid: null }; }
    }

    public void Choose(string name)
    {
        if(name == SelectedName || EditingName || FindModule(name) is not { } module)
            return;

        Selected = module;
        Publish();
    }

    public string? DirectionRefusal(string from, string to)
    {
        if(from == to)
            return "A module may always use itself.";

        if(IsAllowed(from, to) || ListsAreAbsent)
            return null;

        var remaining = new Stack<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        remaining.Push(to);
        while(remaining.TryPop(out var name))
        {
            if(name == from)
                return $"{from} may not use a module above it";

            if(seen.Add(name) && FindModule(name) is { } module)
            {
                foreach(var target in module.MayUse)
                    remaining.Push(target);
            }
        }

        return null;
    }

    public bool IsAllowed(string from, string to)
    {
        return !ListsAreAbsent && FindModule(from)?.MayUse.Contains(to) == true;
    }

    public void ToggleDirection(string from, string to)
    {
        if(!CanEdit)
            return;

        if(DirectionRefusal(from, to) is { } refusal)
        {
            Notice = refusal;
            Publish();
            return;
        }

        SetDirection(from, to, !IsAllowed(from, to));
    }

    public void SetDirection(string from, string to, bool allow)
    {
        if(allow && DirectionRefusal(from, to) is { } refusal)
        {
            Notice = refusal;
            Publish();
            return;
        }

        Apply(folder => _service.SetDirection(folder, from, to, allow));
    }

    public bool SavePart(string kind, string pattern)
    {
        return Apply(folder => _service.AddPart(folder, SelectedName, kind + ":" + pattern)) != null;
    }

    public void RemovePart(StructurePart part)
    {
        Apply(folder => _service.RemovePart(folder, SelectedName, part.ToString()));
    }

    public void StartRename()
    {
        if(!CanEdit || !HasModules)
            return;

        EditingName = true;
        Publish();
    }

    public void FinishName(string name)
    {
        if(!EditingName || !IsWritable)
            return;

        if(name != SelectedName && Apply(folder => _service.RenameModule(folder, SelectedName, name), name, true) == null)
            return;

        _created = null;
        EditingName = false;
        Publish();
    }

    public void CancelName()
    {
        var created = _created;
        _created = null;
        EditingName = false;
        if(created != null)
            Restore(created, null);
        else
            Publish();
    }

    [RelayCommand]
    private void Again()
    {
        Notice = null;
        Reload();
    }

    [RelayCommand(CanExecute = nameof(CanEdit))]
    private void NewModule()
    {
        var number = Report.Modules.Count + 1;
        while(FindModule($"module-{number}") != null)
            number++;

        var name = $"module-{number}";
        _created = Apply(folder => _service.AddModule(folder, name), name);
        if(_created != null)
        {
            EditingName = true;
            Publish();
        }
    }

    [RelayCommand]
    private void AllowBreach()
    {
        if(SelectedBreach is { } breach)
            SetDirection(breach.FromModule, breach.ToModule, true);
    }

    [RelayCommand]
    private void OpenReview()
    {
        if(CanOpenReview && Report.LastJudgment is { } judgment)
            _openReview(judgment);
    }

    [RelayCommand]
    private void OpenFile()
    {
        if(CanOpenFile)
            OpenFileRequested?.Invoke(_folder.SettingsPath);
    }

    [RelayCommand]
    private void RemoveModule()
    {
        if(!CanEdit || !HasModules)
            return;

        var name = SelectedName;
        var row = Layers.Rows.Single(items => items.Contains(name));
        var neighbour = row.SkipWhile(item => item != name).Skip(1).FirstOrDefault() ?? row.TakeWhile(item => item != name).LastOrDefault();
        var removed = Apply(folder => _service.RemoveModule(folder, name), neighbour);
        if(removed == null)
            return;

        _removed = removed;
        _removedName = name;
        _undoTimer.Start();
        Publish();
    }

    [RelayCommand]
    private void Undo()
    {
        if(!CanEdit || _removed == null)
            return;

        Restore(_removed, _removedName);
        ClearUndo();
    }

    private void Restore(StructureChange change, string? select)
    {
        try
        {
            _service.Undo(_folder.FolderPath, change);
            Reload(select);
        }
        catch(Exception exception) when(exception is RuleChangeException or SettingsException)
        {
            ShowFailure(exception);
        }
    }

    private StructureChange? Apply(Func<string, StructureChange> edit, string? select = null, bool whileRenaming = false)
    {
        if(!IsWritable || (EditingName && !whileRenaming))
            return null;

        try
        {
            var change = edit(_folder.FolderPath);
            ClearUndo();
            Notice = change.Message.Contains("Every other use", StringComparison.Ordinal) ? change.Message : null;
            Reload(select);
            return change;
        }
        catch(Exception exception) when(exception is RuleChangeException or SettingsException)
        {
            ShowFailure(exception);
            return null;
        }
    }

    private void ShowFailure(Exception exception)
    {
        var readOnly = exception.Message.Contains("could not be written", StringComparison.OrdinalIgnoreCase) ||
            exception.Message.Contains("read-only", StringComparison.OrdinalIgnoreCase);
        Notice = readOnly ? null : exception.Message;
        if(readOnly)
        {
            IsWritable = false;
            Banner = "commitfence.json cannot be written: it is read-only.";
        }

        Publish();
    }

    private void OnFileChanged()
    {
        Reload();
    }

    private void Reload(string? select = null)
    {
        var current = select ?? Selected?.Name;
        try
        {
            Report = _service.Show(_folder.FolderPath);
            Layers = new ModuleLayers(Report with { AllowedDirections = Directions });
            ActiveBreaches = Report.ActiveBreaches;
            if(EditingName && FindModule(current) == null)
            {
                EditingName = false;
                _created = null;
            }

            Selected = FindModule(current) ?? FindModule(DrawnBreach?.FromModule) ??
                Report.Modules.OrderByDescending(module => module.MayUse.Count + module.UsedBy.Count).FirstOrDefault();
            IsWritable = CanOpenFile && !new FileInfo(_folder.SettingsPath).IsReadOnly;
            Banner = IsWritable ? null : "commitfence.json cannot be written: it is read-only.";
        }
        catch(SettingsException exception)
        {
            Banner = SettingsFileName.FormatError(_folder.SettingsPath, exception);
            IsWritable = false;
        }
        catch(Exception exception) when(exception is IOException or UnauthorizedAccessException)
        {
            Banner = exception.Message;
            IsWritable = false;
        }

        Publish();
    }

    private StructureModuleReport? FindModule(string? name)
    {
        return Report.Modules.FirstOrDefault(module => module.Name == name);
    }

    private void ClearUndo()
    {
        _undoTimer.Stop();
        if(_removed == null)
            return;

        _removed = null;
        _removedName = null;
        Publish();
    }

    private void Publish()
    {
        OnPropertyChanged(string.Empty);
        NewModuleCommand.NotifyCanExecuteChanged();
        Changed?.Invoke();
    }

    public void Dispose()
    {
        _undoTimer.Stop();
        _changes.Changed -= OnFileChanged;
        _changes.Dispose();
    }
}
