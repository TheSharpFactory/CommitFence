using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.RegularExpressions;
using Avalonia.Threading;
using CF.App.Rules.Page;
using CF.Core.Code;
using CF.Core.Findings;
using CF.Core.Rules;
using CF.Core.Rules.Settings;
using CF.Core.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CF.App.Rules;

/// <summary>
/// The Rules screen: the list of the team's rules with its search and its filter, and the banner for a settings file that
/// cannot be read or written. It holds no logic of the product: it asks the rule service and shows the answer.
/// </summary>
internal sealed partial class RulesViewModel : ObservableObject, IRuleChanges, IDisposable
{
    private static readonly TimeSpan _undoTime = TimeSpan.FromSeconds(10);

    private const string _customName = "Custom";
    private const string _customNote = "Written here, kept in commitfence.json";
    private const string _fileGroupName = ".editorconfig";
    private const string _fileNote = "On and off are set in the file";
    private const string _readingNote = "Reading .editorconfig";

    private readonly IRuleService _rules;
    private readonly OpenedFolder _folder;
    private readonly RuleCatalog _catalog;
    private readonly DispatcherTimer _undoTimer;
    private readonly Dictionary<string, RulePageViewModel> _pages = new Dictionary<string, RulePageViewModel>();
    private readonly ISettingsChangeSource? _watcher;
    private readonly Dictionary<string, TeamRuleRowViewModel> _rows = new Dictionary<string, TeamRuleRowViewModel>();
    private readonly GroupHeadingViewModel _customHeading;
    private readonly GroupHeadingViewModel _fileHeading;
    private readonly ISettingsChangeSource? _fileWatcher;
    private readonly Dictionary<string, FileRuleRowViewModel> _fileRows = new Dictionary<string, FileRuleRowViewModel>();
    private IReadOnlyList<RuleDefinition> _definitions = new List<RuleDefinition>();
    private RulePageViewModel? _draft;

    // the page of the rule that was sent here from another screen to be narrowed, while its strip stands
    private RulePageViewModel? _narrowed;
    private RuleDefinition? _removed;
    private string? _removedBefore;
    private bool _hasLoaded;
    private RuleListItemViewModel? HiddenOpen { get; set; }
    private List<FileRule> _fileRules = new List<FileRule>();
    private IReadOnlyList<string> _fileNotes = new List<string>();
    private string? _fileName;
    private string? _fileProblem;
    private int _fileReading;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSearchText), nameof(ShowsSearchHint))]
    public partial string SearchText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial FilterSegmentViewModel SelectedFilter { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedRule), nameof(SelectedFileRule), nameof(CurrentPage), nameof(ShowsPage), nameof(ShowsFilePage), nameof(ShowsList), nameof(ShowsNoRuleBlock))]
    public partial RuleListItemViewModel? SelectedItem { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CurrentPage), nameof(ShowsPage), nameof(ShowsFilePage), nameof(ShowsList), nameof(ShowsNoRuleBlock))]
    public partial bool IsDraftOpen { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsPage), nameof(ShowsFilePage), nameof(ShowsList))]
    public partial bool IsPageOpen { get; set; }

    [ObservableProperty]
    public partial string? UndoText { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasBanner))]
    public partial string? BannerText { get; set; }

    [ObservableProperty]
    public partial bool CanOpenSettingsFile { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsPage), nameof(ShowsFilePage), nameof(ShowsList), nameof(ShowsNoRuleBlock))]
    public partial bool IsOnePane { get; set; }

    [ObservableProperty]
    public partial bool HasNoMatch { get; set; }

    [ObservableProperty]
    public partial string NoMatchText { get; set; } = string.Empty;

    public RulesViewModel(IRuleService rules, OpenedFolder folder, RuleCatalog catalog, ISettingsChangeSource? watcher, ISettingsChangeSource? fileWatcher = null)
    {
        _rules = rules;
        _folder = folder;
        _catalog = catalog;
        _undoTimer = new DispatcherTimer { Interval = _undoTime };
        _undoTimer.Tick += OnUndoTimeUp;
        _customHeading = new GroupHeadingViewModel(GroupSource.Custom, _customName, _customNote, "written here", Rebuild);
        _fileHeading = new GroupHeadingViewModel(GroupSource.EditorConfig, _fileGroupName, _readingNote, "on and off set in the file", Rebuild, OpenEditorConfig) { ShowsCount = false };
        FilePage = new FileRulePageViewModel(OpenEditorConfigLine);
        Filters = new List<FilterSegmentViewModel>
        {
            new FilterSegmentViewModel(RuleFilter.All, "All"),
            new FilterSegmentViewModel(RuleFilter.Blocks, "Blocks"),
            new FilterSegmentViewModel(RuleFilter.Warns, "Warns"),
            new FilterSegmentViewModel(RuleFilter.Off, "Off")
        };
        SelectedFilter = Filters[0];
        _watcher = watcher;
        if(_watcher != null)
            _watcher.Changed += ReloadSettings;

        _fileWatcher = fileWatcher;
        if(_fileWatcher != null)
            _fileWatcher.Changed += ReloadFileRules;

        Reload();
        ReloadFileRules();
    }

    /// <summary>
    /// Raised when a person asks to open the settings file in the program the system uses for it.
    /// </summary>
    public event Action<string>? OpenFileRequested;

    /// <summary>
    /// Everything in the list, in the order it is drawn: headings, rows and the lines of words between them.
    /// </summary>
    public ObservableCollection<RuleListItemViewModel> Items { get; } = new ObservableCollection<RuleListItemViewModel>();

    /// <summary>
    /// The four segments of the filter.
    /// </summary>
    public IReadOnlyList<FilterSegmentViewModel> Filters { get; }

    /// <summary>
    /// Whether both groups have been read, so that the counts of the filter are not a guess.
    /// </summary>
    public bool RuleCountsAreKnown
    {
        get
        {
            return _hasLoaded && FileRulesAreRead;
        }
    }

    /// <summary>
    /// The number the filter shows for one of its choices: the rules of both groups that pass it.
    /// </summary>
    public int CountOf(RuleFilter filter)
    {
        return Filters.First(segment => segment.Filter == filter).Count;
    }

    public bool HasSearchText
    {
        get
        {
            return SearchText.Length > 0;
        }
    }

    public bool ShowsSearchHint
    {
        get
        {
            return SearchText.Length == 0;
        }
    }

    public bool HasBanner
    {
        get
        {
            return BannerText != null;
        }
    }

    /// <summary>
    /// The rule that is selected, or nothing when a heading is or nothing is.
    /// </summary>
    public TeamRuleRowViewModel? SelectedRule
    {
        get
        {
            return OpenItem as TeamRuleRowViewModel;
        }
    }

    /// <summary>
    /// The rule of .editorconfig that is selected, or nothing.
    /// </summary>
    public FileRuleRowViewModel? SelectedFileRule
    {
        get
        {
            return OpenItem as FileRuleRowViewModel;
        }
    }

    /// <summary>
    /// The one page of the rules .editorconfig sets, bound to the rule that is chosen.
    /// </summary>
    public FileRulePageViewModel FilePage { get; }

    /// <summary>
    /// Whether the page of a rule of .editorconfig is the one drawn.
    /// </summary>
    public bool ShowsFilePage
    {
        get
        {
            return !IsDraftOpen && SelectedFileRule != null && (!IsOnePane || IsPageOpen);
        }
    }

    public bool HasUndo
    {
        get
        {
            return UndoText != null;
        }
    }

    /// <summary>
    /// The page of the rule that is open: the new rule being written, or the page of the selected rule.
    /// </summary>
    public RulePageViewModel? CurrentPage
    {
        get
        {
            if(IsDraftOpen)
                return _draft;

            return SelectedRule != null && _pages.TryGetValue(SelectedRule.Id, out var page) ? page : null;
        }
    }

    /// <summary>
    /// Whether the page is drawn: beside the list in two panes, and in one pane only after a rule was opened.
    /// </summary>
    public bool ShowsPage
    {
        get
        {
            return (CurrentPage != null || (!IsDraftOpen && SelectedFileRule != null)) && (!IsOnePane || IsPageOpen);
        }
    }

    /// <summary>
    /// Whether the list is drawn: always in two panes, and in one pane except while the page is over it.
    /// </summary>
    public bool ShowsList
    {
        get
        {
            return !IsOnePane || !ShowsPage;
        }
    }

    /// <summary>
    /// The rule whose page is open: the row selected in the list, or a rule that a search or the filter now hides, whose page stays open.
    /// </summary>
    private RuleListItemViewModel? OpenItem
    {
        get
        {
            return SelectedItem ?? HiddenOpen;
        }
    }

    /// <summary>
    /// Whether the right pane holds the block that says there is no rule to open: two panes, the rules read, the list with no row to
    /// open. It is what is left when a rule is always opened by itself.
    /// </summary>
    public bool ShowsNoRuleBlock
    {
        get
        {
            return !IsOnePane && RuleCountsAreKnown && CurrentPage == null && SelectedFileRule == null;
        }
    }

    /// <summary>
    /// The repository has no rule at all, as against rules that the search or the filter hides.
    /// </summary>
    public bool HasNoRules
    {
        get
        {
            return _definitions.Count == 0 && _fileRules.Count == 0;
        }
    }

    public string NoRuleHeading
    {
        get
        {
            return HasNoRules ? "No rules yet" : "No rule open";
        }
    }

    public string NoRuleLead
    {
        get
        {
            return HasNoRules ? $"A rule is a sentence. Write the first one for {_folder.Name}." : "Nothing in the list matches. Clear the search or the filter to choose one.";
        }
    }

    /// <summary>
    /// The picture of a rule that the block shows: the sample first rule, written the way a finding writes a rule.
    /// </summary>
    public IReadOnlyList<CF.App.Review.SentencePieceViewModel> Specimen { get; } = CF.App.Review.SentencePieceViewModel.Specimen();

    /// <summary>
    /// What a screen reader says for the specimen.
    /// </summary>
    public string SpecimenSpokenName { get; } = "For example: for if statement, where statements in body is 1 and body fits on one line is true, require braces is false";

    /// <summary>
    /// Opens the first row the list shows when no rule is open (arriving at Rules, widening from one pane to two), and opens the next
    /// row, or the one before when it was the last, when the open rule is gone. A rule that a search or the filter hides stays open.
    /// The keyboard stays where it was.
    /// </summary>
    private void KeepOrOpenRule(RuleListItemViewModel? open, int position)
    {
        if(IsOnePane || IsDraftOpen || !_hasLoaded)
            return;

        var rows = Items.Where(item => item is TeamRuleRowViewModel or FileRuleRowViewModel).ToList();
        if(open != null && StillExists(open))
        {
            if(SelectedItem == null && !rows.Contains(open))
            {
                HiddenOpen = open;
                NotifyOpenChanged();
            }

            return;
        }

        HiddenOpen = null;
        if(rows.Count > 0)
            SelectedItem = rows[Math.Clamp(Math.Max(position, 0), 0, rows.Count - 1)];

        NotifyOpenChanged();
    }

    private bool StillExists(RuleListItemViewModel item)
    {
        return item switch
        {
            TeamRuleRowViewModel team => _rows.ContainsKey(team.Id),
            FileRuleRowViewModel file => _fileRows.ContainsKey(file.Rule.Id),
            _ => true
        };
    }

    private void EnsureRuleOpen()
    {
        var open = OpenItem;
        var position = open == null ? -1 : Items.Where(item => item is TeamRuleRowViewModel or FileRuleRowViewModel).ToList().IndexOf(open);
        KeepOrOpenRule(open, position);
    }

    private void NotifyOpenChanged()
    {
        OnPropertyChanged(nameof(SelectedRule));
        OnPropertyChanged(nameof(SelectedFileRule));
        OnPropertyChanged(nameof(CurrentPage));
        OnPropertyChanged(nameof(ShowsPage));
        OnPropertyChanged(nameof(ShowsFilePage));
        OnPropertyChanged(nameof(ShowsList));
        OnPropertyChanged(nameof(ShowsNoRuleBlock));
        OnPropertyChanged(nameof(HasNoRules));
        OnPropertyChanged(nameof(NoRuleHeading));
        OnPropertyChanged(nameof(NoRuleLead));
    }

    /// <summary>
    /// Whether a page is one this list still holds: a page of a rule that is gone is let go of by the view.
    /// </summary>
    public bool Holds(RulePageViewModel page)
    {
        return ReferenceEquals(page, _draft) || _pages.ContainsValue(page);
    }

    public void Dispose()
    {
        _undoTimer.Stop();
        if(_watcher != null)
        {
            _watcher.Changed -= ReloadSettings;
            _watcher.Dispose();
        }

        if(_fileWatcher != null)
        {
            _fileWatcher.Changed -= ReloadFileRules;
            _fileWatcher.Dispose();
        }
    }

    /// <inheritdoc />
    public void SetEnabled(string ruleId, bool enabled)
    {
        Apply(ruleId, () => _rules.SetEnabled(_folder.SettingsPath, ruleId, enabled));
    }

    /// <inheritdoc />
    public void SetSeverity(string ruleId, Severity severity)
    {
        Apply(ruleId, () => _rules.SetSeverity(_folder.SettingsPath, ruleId, severity));
    }

    /// <inheritdoc />
    public string? ChangeRule(string ruleId, RuleChange change)
    {
        return Attempt(() => _rules.ChangeRule(_folder.SettingsPath, ruleId, change));
    }

    /// <inheritdoc />
    public string? AddRule(RuleDraft draft, string? afterRuleId)
    {
        var code = _draft?.TryIt.Code ?? string.Empty;
        var message = Attempt(() => AddAt(draft, afterRuleId));
        if(message == null)
        {
            if(_pages.TryGetValue(draft.Id, out var saved))
                saved.TryIt.Code = code;

            OpenSaved(draft.Id);
        }

        return message;
    }

    /// <inheritdoc />
    public TryAnswer TryChangedRule(string ruleId, RuleChange change, string code)
    {
        return Answer(() => _rules.TryChangedRule(_folder.SettingsPath, ruleId, change, code, null));
    }

    /// <inheritdoc />
    public TryAnswer TryNewRule(RuleDraft draft, string code)
    {
        return Answer(() => _rules.TryNewRule(_folder.SettingsPath, draft, code, null));
    }

    /// <inheritdoc />
    public void RemoveRule(string ruleId)
    {
        var removed = _definitions.FirstOrDefault(rule => rule.Id == ruleId);
        var next = _definitions.SkipWhile(rule => rule.Id != ruleId).Skip(1).FirstOrDefault();
        var rows = Items.OfType<TeamRuleRowViewModel>().ToList();
        var position = rows.FindIndex(row => row.Id == ruleId);
        if(removed == null || !Apply(ruleId, () => _rules.RemoveRule(_folder.SettingsPath, ruleId)))
            return;

        _removed = removed;
        _removedBefore = next?.Id;
        UndoText = $"{ruleId} removed.";
        OnPropertyChanged(nameof(HasUndo));
        _undoTimer.Stop();
        _undoTimer.Start();
        IsPageOpen = false;
        var remaining = Items.OfType<TeamRuleRowViewModel>().ToList();
        SelectedItem = remaining.Count == 0 ? null : remaining[Math.Clamp(position, 0, remaining.Count - 1)];
    }

    /// <inheritdoc />
    public void DuplicateRule(string ruleId)
    {
        var source = _definitions.FirstOrDefault(rule => rule.Id == ruleId);
        if(source == null)
            return;

        _draft = new RulePageViewModel(_catalog, this, NextId(), null);
        _draft.FillFrom(source);
        SelectedItem = null;
        IsDraftOpen = true;
        IsPageOpen = true;
    }

    /// <inheritdoc />
    public void DiscardDraft()
    {
        _draft = null;
        IsDraftOpen = false;
        IsPageOpen = false;
        EnsureRuleOpen();
    }

    /// <summary>
    /// Opens a page for a new rule, with an identifier no rule has. The new rule that was begun and not saved is the one
    /// that opens again.
    /// </summary>
    public void NewRule()
    {
        _draft ??= new RulePageViewModel(_catalog, this, NextId(), null);
        SelectedItem = null;
        IsDraftOpen = true;
        IsPageOpen = true;
    }

    /// <summary>
    /// One pane: the page of the selected rule comes over the list.
    /// </summary>
    public void OpenSelectedRule()
    {
        if(SelectedRule != null || SelectedFileRule != null)
            IsPageOpen = true;
    }

    /// <summary>
    /// Writes the open rule (Ctrl+S), when there is something to write.
    /// </summary>
    public void SaveOpenRule()
    {
        var page = CurrentPage;
        if(page != null && page.SaveCommand.CanExecute(null))
            page.SaveCommand.Execute(null);
    }

    /// <summary>
    /// Reads the settings file again and shows what it says. A file that cannot be read is shown as the banner, and the
    /// rules last read stay in the list.
    /// </summary>
    public void Reload()
    {
        try
        {
            _definitions = _rules.ListRules(_folder.SettingsPath);
            _hasLoaded = true;
            BannerText = null;
            CanOpenSettingsFile = false;
        }
        catch(SettingsException exception)
        {
            ShowBanner(SettingsFileName.FormatError(_folder.SettingsPath, exception));
        }

        Rebuild();
    }

    private void ReloadSettings()
    {
        Reload();
        ReloadFileRules();
    }

    partial void OnSearchTextChanged(string value)
    {
        Rebuild();
    }

    partial void OnSelectedFilterChanged(FilterSegmentViewModel value)
    {
        Rebuild();
    }

    partial void OnSelectedItemChanged(RuleListItemViewModel? value)
    {
        if(value != null)
            IsDraftOpen = false;

        // another rule is opened: the strip of the rule that was sent here goes
        if(_narrowed != null && !ReferenceEquals(CurrentPage, _narrowed))
            EndNarrowing();

        if(value is FileRuleRowViewModel fileRow)
            FilePage.Show(fileRow.Rule);

        RefreshBeside();
    }

    partial void OnSelectedItemChanged(RuleListItemViewModel? oldValue, RuleListItemViewModel? newValue)
    {
        if(newValue is TeamRuleRowViewModel or FileRuleRowViewModel)
            HiddenOpen = null;
        else if(newValue != null && oldValue is TeamRuleRowViewModel or FileRuleRowViewModel)
            HiddenOpen = oldValue;
    }

    partial void OnIsOnePaneChanged(bool value)
    {
        RefreshBeside();
        if(!value)
            EnsureRuleOpen();
    }

    [RelayCommand]
    private void ClearSearch()
    {
        SearchText = string.Empty;
    }

    [RelayCommand]
    private void NewRuleFromHead()
    {
        NewRule();
    }

    [RelayCommand]
    private void Back()
    {
        IsPageOpen = false;
    }

    [RelayCommand]
    private void UndoRemove()
    {
        var removed = _removed;
        if(removed == null)
            return;

        var draft = new RuleDraft(removed.Id, removed.Language, removed.ArtifactType, removed.Severity, removed.Title, DraftsOf(removed.Where), DraftsOf(removed.Require), removed.Forbid, removed.Paths, removed.Message, removed.Why);
        var restored = Apply(removed.Id, () => BringBack(draft, removed.Enabled));
        ClearUndo();
        if(restored)
            OpenSaved(removed.Id);
    }

    [RelayCommand]
    private void OpenSettingsFile()
    {
        OpenFileRequested?.Invoke(_folder.SettingsPath);
    }

    private static List<ConditionDraft> DraftsOf(IReadOnlyList<RuleCondition> conditions)
    {
        return conditions.Select(condition => new ConditionDraft(condition.PropertyName, condition.ExpectedValues.Select(value => value.ToString()).ToList(), condition.Comparison == Comparison.IsOneOf)).ToList();
    }

    private void AddAt(RuleDraft draft, string? afterRuleId)
    {
        if(afterRuleId == null)
            _rules.AddRule(_folder.SettingsPath, draft);
        else
            _rules.AddRuleAfter(_folder.SettingsPath, draft, afterRuleId);
    }

    private void BringBack(RuleDraft draft, bool enabled)
    {
        if(_removedBefore == null)
            _rules.AddRule(_folder.SettingsPath, draft);
        else
            _rules.AddRuleBefore(_folder.SettingsPath, draft, _removedBefore);

        if(!enabled)
            _rules.SetEnabled(_folder.SettingsPath, draft.Id, false);
    }

    private bool Apply(string ruleId, Action change)
    {
        try
        {
            change();
            Reload();
            return true;
        }
        catch(SettingsException exception)
        {
            ShowBanner(SettingsFileName.FormatError(_folder.SettingsPath, exception));
            RefreshRow(ruleId);
        }
        catch(RuleChangeException exception)
        {
            ShowBanner(exception.Message);
            RefreshRow(ruleId);
        }

        return false;
    }

    /// <summary>
    /// Makes a change and shows what the file says after it. The message of a refusal is given back, to stand where the
    /// person made the change; nothing is given back when it worked.
    /// </summary>
    private string? Attempt(Action change)
    {
        try
        {
            change();
            Reload();
        }
        catch(SettingsException exception)
        {
            return SettingsFileName.FormatError(_folder.SettingsPath, exception);
        }
        catch(RuleChangeException exception)
        {
            return exception.Message;
        }

        return null;
    }

    private TryAnswer Answer(Func<RuleTryResult> attempt)
    {
        try
        {
            return TryAnswer.Of(attempt());
        }
        catch(SettingsException exception)
        {
            return TryAnswer.Refused(SettingsFileName.FormatError(_folder.SettingsPath, exception));
        }
        catch(RuleChangeException exception)
        {
            return TryAnswer.Refused(exception.Message);
        }
    }

    private void OpenSaved(string ruleId)
    {
        if(_draft != null && _draft.Id == ruleId)
            _draft = null;

        IsDraftOpen = false;
        if(_rows.TryGetValue(ruleId, out var row))
        {
            SelectedItem = row;
            IsPageOpen = IsOnePane;
        }
    }

    private void ClearUndo()
    {
        _undoTimer.Stop();
        _removed = null;
        UndoText = null;
        OnPropertyChanged(nameof(HasUndo));
    }

    private void OnUndoTimeUp(object? sender, EventArgs e)
    {
        ClearUndo();
    }

    private void RefreshBeside()
    {
        foreach(var row in _rows.Values)
            row.PageIsBeside = !IsOnePane && ReferenceEquals(row, SelectedItem);
    }

    /// <summary>
    /// An identifier for a new rule: the family of the team's own rules, "TEAM", and the number after the highest there is.
    /// The command line asks for it, so the page proposes one.
    /// </summary>
    [GeneratedRegex("^TEAM(?<number>[0-9]+)$")]
    private static partial Regex TeamId();

    private string NextId()
    {
        var numbers = _definitions.Select(rule => TeamId().Match(rule.Id)).Where(match => match.Success).ToList();
        var highest = numbers.Count == 0 ? 0 : numbers.Max(match => int.Parse(match.Groups["number"].Value, CultureInfo.InvariantCulture));
        var width = Math.Max(3, numbers.Count == 0 ? 3 : numbers.Max(match => match.Groups["number"].Value.Length));

        return "TEAM" + (highest + 1).ToString(CultureInfo.InvariantCulture).PadLeft(width, '0');
    }

    private void ShowBanner(string text)
    {
        BannerText = text;
        CanOpenSettingsFile = _folder.SettingsExist || File.Exists(_folder.SettingsPath);
    }

    private void RefreshRow(string ruleId)
    {
        if(_rows.TryGetValue(ruleId, out var row))
            row.RefreshBindings();

        if(_pages.TryGetValue(ruleId, out var page))
            page.RefreshBindings();
    }

    private void Rebuild()
    {
        UpdateRows();
        UpdateCounts();
        var desired = new List<RuleListItemViewModel>();
        if(_hasLoaded)
        {
            desired.AddRange(CreateGroupItems());
            desired.AddRange(CreateFileGroupItems());
        }

        var open = OpenItem;
        var position = open == null ? -1 : Items.Where(item => item is TeamRuleRowViewModel or FileRuleRowViewModel).ToList().IndexOf(open);
        SyncItems(desired);
        UpdateNoMatch(desired);
        KeepOrOpenRule(open, position);
    }

    private void UpdateRows()
    {
        var ids = new HashSet<string>(_definitions.Select(rule => rule.Id));
        foreach(var stale in _rows.Keys.Where(id => !ids.Contains(id)).ToList())
        {
            _rows.Remove(stale);
            _pages.Remove(stale);
        }

        foreach(var rule in _definitions)
        {
            if(_rows.TryGetValue(rule.Id, out var row))
            {
                row.Update(rule);
                _pages[rule.Id].Update(rule);
            }
            else
            {
                AddRow(rule);
            }
        }

        RefreshBeside();
    }

    private void AddRow(RuleDefinition rule)
    {
        var row = new TeamRuleRowViewModel(rule, _catalog.LanguageName(rule.Language), this);
        var page = new RulePageViewModel(_catalog, this, rule.Id, rule);
        page.DirtyChanged += () => row.HasUnsavedEdits = page.IsDirty;
        _rows[rule.Id] = row;
        _pages[rule.Id] = page;
    }

    private void UpdateCounts()
    {
        foreach(var segment in Filters)
            segment.Count = _definitions.Count(rule => Passes(segment.Filter, rule)) + _fileRules.Count(rule => PassesFile(segment.Filter, rule));

        _customHeading.Count = _definitions.Count;
        UpdateFileHeading();
    }

    private static bool Passes(RuleFilter filter, RuleDefinition rule)
    {
        return filter switch
        {
            RuleFilter.All => true,
            RuleFilter.Blocks => rule.Enabled && rule.Severity == Severity.Block,
            RuleFilter.Warns => rule.Enabled && rule.Severity == Severity.Warn,
            RuleFilter.Off => !rule.Enabled,
            _ => throw new ArgumentOutOfRangeException(nameof(filter), filter, null)
        };
    }

    private bool MatchesSearch(RuleDefinition rule)
    {
        var text = SearchText.Trim();
        if(text.Length == 0)
            return true;

        return rule.Id.Contains(text, StringComparison.OrdinalIgnoreCase) || rule.Title.Contains(text, StringComparison.OrdinalIgnoreCase);
    }

    private IEnumerable<RuleListItemViewModel> CreateGroupItems()
    {
        yield return _customHeading;
        if(!_customHeading.IsExpanded)
            yield break;

        var shown = _definitions.Where(rule => Passes(SelectedFilter.Filter, rule) && MatchesSearch(rule)).ToList();
        if(shown.Count == 0 && SearchText.Trim().Length == 0)
            yield return new GroupNoteViewModel(EmptyGroupText(), SelectedFilter.Filter != RuleFilter.All);

        foreach(var rule in shown)
            yield return _rows[rule.Id];
    }

    private string EmptyGroupText()
    {
        return SelectedFilter.Filter switch
        {
            RuleFilter.All => "No custom rules yet.",
            RuleFilter.Blocks => "No custom rule blocks.",
            RuleFilter.Warns => "No custom rule warns.",
            RuleFilter.Off => "No custom rule is off.",
            _ => throw new ArgumentOutOfRangeException(nameof(SelectedFilter))
        };
    }

    private void SyncItems(List<RuleListItemViewModel> desired)
    {
        for(var index = 0; index < desired.Count; index++)
        {
            if(index < Items.Count && ReferenceEquals(Items[index], desired[index]))
                continue;

            var existing = IndexOfFrom(desired[index], index + 1);
            if(existing > index)
                Items.Move(existing, index);
            else
                Items.Insert(index, desired[index]);
        }

        while(Items.Count > desired.Count)
            Items.RemoveAt(Items.Count - 1);
    }

    private int IndexOfFrom(RuleListItemViewModel item, int start)
    {
        for(var index = start; index < Items.Count; index++)
        {
            if(ReferenceEquals(Items[index], item))
                return index;
        }

        return -1;
    }

    private void UpdateNoMatch(IReadOnlyList<RuleListItemViewModel> desired)
    {
        var searching = SearchText.Trim().Length > 0;
        var anyRow = desired.Any(item => item is TeamRuleRowViewModel);
        var anyFileRow = desired.Any(item => item is FileRuleRowViewModel);
        HasNoMatch = _hasLoaded && searching && !anyRow && !anyFileRow && _customHeading.IsExpanded && _fileHeading.IsExpanded && FileRulesAreRead;
        NoMatchText = HasNoMatch ? $"No rule matches “{SearchText.Trim()}”" : string.Empty;
    }
}