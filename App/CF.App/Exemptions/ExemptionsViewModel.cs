using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.RegularExpressions;
using CF.App.Shell;
using CF.Core.Rules;
using CF.Core.Rules.Settings;
using CF.Core.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CF.App.Exemptions;

/// <summary>
/// The Exemptions screen: every line of the last commit that sets a rule aside, by file with the most first, the count by rule beside
/// it, and the rule chosen there. The commit is scanned when the screen opens and on F5, off the interface's thread, and the scan is
/// cancelled when the screen moves on. What the screen counts and says comes from <see cref="ExemptionCounts"/>, as the command's does.
/// </summary>
internal sealed partial class ExemptionsViewModel : ObservableObject, IScreenModel
{
    private const int _fewest = 20;
    private const int _mostFiles = 50;
    private static readonly TimeSpan _ringAfter = TimeSpan.FromMilliseconds(400);

    private readonly IExemptionService? _exemptions;
    private readonly IRuleService? _rules;
    private readonly OpenedFolder _folder;
    private readonly Action<string> _openRule;
    private readonly Action<string, string> _narrow;

    private CancellationTokenSource? _scanning;
    private ExemptionScan? _scan;
    private ExemptionCounts _all = new ExemptionCounts([]);
    private ExemptionCounts _narrowed = new ExemptionCounts([]);
    private ExemptionCounts _visible = new ExemptionCounts([]);
    private Dictionary<string, ExemptionRuleInfo> _ruleInfos = new Dictionary<string, ExemptionRuleInfo>(StringComparer.Ordinal);
    private Dictionary<string, int> _inFile = new Dictionary<string, int>(StringComparer.Ordinal);
    private ExemptionFilter _filter = ExemptionFilter.None;
    private string? _filterRuleId;
    private bool _showsAllFiles;
    private bool _rebuilding;

    public ExemptionsViewModel(IExemptionService? exemptions, IRuleService? rules, OpenedFolder folder, Action<string> openRule, Action<string, string> narrow)
    {
        Items.Add(new ExemptionHeadViewModel(this));
        _exemptions = exemptions;
        _rules = rules;
        _folder = folder;
        _openRule = openRule;
        _narrow = narrow;
    }

    /// <summary>
    /// The window opens the file at a line in the program the system uses for it: the working folder's file, not the commit's.
    /// </summary>
    public event Action<string, int>? OpenLineRequested;

    /// <summary>
    /// Ctrl+F: the keyboard goes to the search.
    /// </summary>
    public event Action? FocusSearchRequested;

    /// <summary>
    /// What a person can be told about the last scan.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsList), nameof(ShowsEmptyBlock), nameof(ShowsNothingToScan), nameof(ShowsFailure), nameof(ShowsNoExemption), nameof(EmptyHeading), nameof(EmptyLead))]
    public partial ExemptionsState State { get; private set; } = ExemptionsState.Scanning;

    /// <summary>
    /// The scan has run for 400 ms: the busy ring and "Reading the last commit" stand in the summary's place.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsSummary))]
    public partial bool ShowsScanning { get; private set; }

    [ObservableProperty]
    public partial string? Meta { get; private set; }

    [ObservableProperty]
    public partial string FailureText { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial string SummaryMarked { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial string ButtonText { get; private set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNothingFound), nameof(HasFiles), nameof(NothingFoundText))]
    public partial string SearchText { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasFiles))]
    public partial string FilesLine { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial string OpenAllText { get; private set; } = "Open all";

    [ObservableProperty]
    public partial ChosenRuleViewModel? Chosen { get; private set; }

    [ObservableProperty]
    public partial IReadOnlyList<ExemptionRuleRowViewModel> RuleRows { get; private set; } = [];

    [ObservableProperty]
    public partial IReadOnlyList<object> RulePanelItems { get; private set; } = [];

    [ObservableProperty]
    public partial string RulesHead { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial string FilterText { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial string ClearName { get; private set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PanelIsListed), nameof(PanelToggleText))]
    public partial bool IsOnePane { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PanelIsListed), nameof(PanelToggleText))]
    public partial bool PanelShown { get; set; }

    /// <summary>
    /// The flat list of the page: the head, each file's line, its exemptions while it is open, and the tail.
    /// </summary>
    public ObservableCollection<ExemptionListItem> Items { get; } = new ObservableCollection<ExemptionListItem>();

    public bool ShowsList
    {
        get { return State == ExemptionsState.Listed; }
    }

    public bool ShowsNoExemption
    {
        get { return State == ExemptionsState.NoExemption; }
    }

    public bool ShowsNothingToScan
    {
        get { return State == ExemptionsState.NoCommit; }
    }

    public bool ShowsFailure
    {
        get { return State == ExemptionsState.Failed; }
    }

    /// <summary>
    /// The block that takes the sheet's room: no exemption, no commit yet, or a commit that could not be read.
    /// </summary>
    public bool ShowsEmptyBlock
    {
        get { return State is ExemptionsState.NoExemption or ExemptionsState.NoCommit or ExemptionsState.Failed; }
    }

    public string EmptyHeading
    {
        get
        {
            return State switch
            {
                ExemptionsState.NoCommit => ExemptionWords.NoCommitTitle,
                ExemptionsState.Failed => "The last commit could not be read",
                ExemptionsState.Scanning or ExemptionsState.Listed or ExemptionsState.NoExemption => ExemptionWords.EmptyTitle,
                _ => throw new ArgumentOutOfRangeException(nameof(State))
            };
        }
    }

    public string EmptyLead
    {
        get
        {
            return State switch
            {
                ExemptionsState.NoCommit => _scan?.NoCommitSentence ?? string.Empty,
                ExemptionsState.Failed => FailureText,
                ExemptionsState.Scanning or ExemptionsState.Listed or ExemptionsState.NoExemption => ExemptionWords.EmptySentence,
                _ => throw new ArgumentOutOfRangeException(nameof(State))
            };
        }
    }

    public string EmptyExample
    {
        get { return ShowsNoExemption ? ExemptionWords.EmptyExample : string.Empty; }
    }

    /// <summary>
    /// The summary stands unless the scan has run long enough to show its ring in its place.
    /// </summary>
    public bool ShowsSummary
    {
        get { return !ShowsScanning; }
    }

    public bool HasButton
    {
        get { return ButtonText.Length > 0; }
    }

    public bool HasFilter
    {
        get { return _filter != ExemptionFilter.None; }
    }

    public bool FilterIsRule
    {
        get { return _filter == ExemptionFilter.Rule; }
    }

    public bool FilterIsWithoutReason
    {
        get { return _filter == ExemptionFilter.WithoutReason; }
    }

    public bool HasChosen
    {
        get { return Chosen != null; }
    }

    public bool HasSearch
    {
        get { return SearchText.Length > 0; }
    }

    /// <summary>
    /// The search finds nothing in what the filter shows.
    /// </summary>
    public bool HasNothingFound
    {
        get { return HasSearch && _visible.Entries == 0 && _narrowed.Entries > 0; }
    }

    public string NothingFoundText
    {
        get { return $"“{SearchText}”"; }
    }

    public bool HasFiles
    {
        get { return FilesLine.Length > 0 && !HasNothingFound; }
    }

    public bool PanelIsListed
    {
        get { return !IsOnePane || PanelShown; }
    }

    public string PanelToggleText
    {
        get { return PanelShown ? "Hide" : "Show"; }
    }

    /// <summary>
    /// The screen is shown: the last commit is scanned.
    /// </summary>
    public async Task ShownAsync()
    {
        await ScanAsync();
    }

    /// <summary>
    /// The screen moves on: a scan that is still reading is cancelled.
    /// </summary>
    public void Left()
    {
        _scanning?.Cancel();
    }

    /// <summary>
    /// Reads the last commit again off the interface's thread. While it reads, the screen keeps what it showed last; after 400 ms the
    /// summary gives way to the ring. A scan that is overtaken by a later one, or cancelled, changes nothing.
    /// </summary>
    public async Task ScanAsync()
    {
        _scanning?.Cancel();
        var cancellation = new CancellationTokenSource();
        _scanning = cancellation;
        if(_scan == null)
            State = ExemptionsState.Scanning;

        var work = Task.Run(() => Read(cancellation.Token), cancellation.Token);
        try
        {
            if(await Task.WhenAny(work, Task.Delay(_ringAfter, cancellation.Token)) != work && !work.IsCompleted)
                ShowsScanning = true;
        }
        catch(OperationCanceledException)
        {
            // the screen moved on while the ring was still waiting: the read below ends the same way
        }

        ReadResult? result;
        try
        {
            result = await work;
        }
        catch(OperationCanceledException)
        {
            result = null;
        }

        if(!ReferenceEquals(_scanning, cancellation))
            return;

        ShowsScanning = false;
        _scanning = null;
        cancellation.Dispose();
        if(result != null)
            Take(result);
    }

    /// <summary>
    /// F5 scans the last commit again.
    /// </summary>
    [RelayCommand]
    private void Again()
    {
        _ = ScanAsync();
    }

    /// <summary>
    /// Esc ends a chosen rule or the "without a reason" view. True when it did.
    /// </summary>
    public bool EscapePressed()
    {
        if(!HasFilter)
            return false;

        ClearFilter();
        return true;
    }

    public void FocusSearch()
    {
        FocusSearchRequested?.Invoke();
    }

    [RelayCommand]
    private void ShowWithoutReason()
    {
        SetFilter(ExemptionFilter.WithoutReason, null);
    }

    [RelayCommand]
    private void ClearFilter()
    {
        SetFilter(ExemptionFilter.None, null);
    }

    [RelayCommand]
    private void ClearSearch()
    {
        SearchText = string.Empty;
    }

    [RelayCommand]
    private void TogglePanel()
    {
        PanelShown = !PanelShown;
    }

    [RelayCommand]
    private void ToggleAll()
    {
        var files = Items.OfType<ExemptionFileViewModel>().ToList();
        var open = files.Any(file => !file.IsOpen);
        foreach(var file in files)
        {
            file.IsOpen = open;
        }

        OpenAllText = open ? "Fold all" : "Open all";
    }

    partial void OnSearchTextChanged(string value)
    {
        Rebuild();
    }

    private void SetFilter(ExemptionFilter filter, string? ruleId)
    {
        _filter = filter;
        _filterRuleId = ruleId;
        _showsAllFiles = false;
        Rebuild();
    }

    private void ChooseRule(ExemptionRuleRowViewModel row)
    {
        if(row.IsChosen)
            ClearFilter();
        else
            SetFilter(ExemptionFilter.Rule, row.RuleId);
    }

    private ReadResult? Read(CancellationToken cancellationToken)
    {
        if(_exemptions == null)
            return null;

        try
        {
            var scan = _exemptions.Scan(_folder.FolderPath, true, cancellationToken);
            return new ReadResult(scan, null, scan.HasNoCommit ? new Dictionary<string, ExemptionRuleInfo>() : ReadRules());
        }
        catch(ExemptionScanException exception)
        {
            return new ReadResult(null, exception.Message, new Dictionary<string, ExemptionRuleInfo>());
        }
    }

    /// <summary>
    /// The rules the repository has, team rules and rules of .editorconfig, by id. A file that cannot be read leaves them out: an
    /// exemption that names a rule that is not found is listed with its id alone.
    /// </summary>
    private Dictionary<string, ExemptionRuleInfo> ReadRules()
    {
        var infos = new Dictionary<string, ExemptionRuleInfo>(StringComparer.Ordinal);
        if(_rules == null)
            return infos;

        try
        {
            foreach(var listing in _rules.ListFileRules(_folder.SettingsPath))
            {
                foreach(var rule in listing.Rules)
                {
                    infos.TryAdd(rule.Id, ExemptionRuleInfo.File(rule));
                }
            }
        }
        catch(RuleChangeException)
        {
            // what .editorconfig says cannot be read: its rules are listed with their ids alone
        }

        try
        {
            foreach(var rule in _rules.ListRules(_folder.SettingsPath))
            {
                infos[rule.Id] = ExemptionRuleInfo.Team(rule);
            }
        }
        catch(SettingsException)
        {
            // the settings file cannot be read: the team's rules are listed with their ids alone
        }

        return infos;
    }

    private void Take(ReadResult result)
    {
        _scan = result.Scan;
        _ruleInfos = result.Rules;
        FailureText = result.Failure ?? string.Empty;
        Meta = result.Scan is { HasNoCommit: false } scan ? scan.HeadLine : null;
        if(result.Scan == null)
            State = result.Failure == null ? ExemptionsState.Scanning : ExemptionsState.Failed;
        else if(result.Scan.HasNoCommit)
            State = ExemptionsState.NoCommit;
        else
            State = result.Scan.Exemptions.Count == 0 ? ExemptionsState.NoExemption : ExemptionsState.Listed;

        _all = new ExemptionCounts(result.Scan?.Exemptions ?? []);
        _inFile = _all.Files.ToDictionary(file => file.Path, file => file.Exemptions.Count, StringComparer.Ordinal);
        if(_filter == ExemptionFilter.Rule && !_all.Rules.Any(rule => rule.RuleId == _filterRuleId))
        {
            _filter = ExemptionFilter.None;
            _filterRuleId = null;
        }

        Rebuild();
    }

    private ExemptionRuleInfo InfoOf(string ruleId)
    {
        return _ruleInfos.TryGetValue(ruleId, out var info) ? info : ExemptionRuleInfo.Unknown(ruleId);
    }

    /// <summary>
    /// Works out what the screen shows from the scan, the filter and the search, and lays the list out again.
    /// </summary>
    private void Rebuild()
    {
        if(_rebuilding)
            return;

        _rebuilding = true;
        try
        {
            var all = _scan?.Exemptions ?? [];
            var narrowed = _filter switch
            {
                ExemptionFilter.WithoutReason => all.Where(entry => entry.IsFinding).ToList(),
                ExemptionFilter.Rule => all.Where(entry => entry.RuleId == _filterRuleId).ToList(),
                ExemptionFilter.None => all.ToList(),
                _ => throw new ArgumentOutOfRangeException(nameof(_filter))
            };
            _narrowed = new ExemptionCounts(narrowed);
            _visible = HasSearch ? new ExemptionCounts(_narrowed.Files.SelectMany(MatchingEntries).ToList()) : _narrowed;
            ShowSummary();
            ShowPanel();
            ShowChosen();
            ShowFilesLine();
            LayOutItems();
        }
        finally
        {
            _rebuilding = false;
        }

        OnPropertyChanged(nameof(HasNothingFound));
        OnPropertyChanged(nameof(NothingFoundText));
        OnPropertyChanged(nameof(HasFiles));
        OnPropertyChanged(nameof(HasFilter));
        OnPropertyChanged(nameof(FilterIsRule));
        OnPropertyChanged(nameof(FilterIsWithoutReason));
        OnPropertyChanged(nameof(HasButton));
        OnPropertyChanged(nameof(HasChosen));
    }

    private IEnumerable<ExemptionEntry> MatchingEntries(ExemptionFileGroup file)
    {
        return GroupRows(file).Where(row => row.Any(Matches)).SelectMany(row => row);
    }

    private static IEnumerable<IGrouping<(string RuleId, string? Reason, bool IsFinding), ExemptionEntry>> GroupRows(ExemptionFileGroup file)
    {
        return file.Exemptions.GroupBy(entry => (entry.RuleId, Reason: string.IsNullOrWhiteSpace(entry.Reason) ? null : entry.Reason.Trim(), entry.IsFinding));
    }

    private bool Matches(ExemptionEntry entry)
    {
        return entry.RuleId.Contains(SearchText, StringComparison.OrdinalIgnoreCase)
            || entry.File.Contains(SearchText, StringComparison.OrdinalIgnoreCase)
            || (entry.Reason?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ?? false);
    }

    /// <summary>
    /// "28 lines set 8 rules aside, in 17 files" with "2 have no reason" after it; while a view is chosen, the sentence of that view and
    /// its filter piece in the button's place.
    /// </summary>
    private void ShowSummary()
    {
        var total = _all.Entries.ToString(CultureInfo.InvariantCulture);
        var every = $"Show all {total} {(_all.Entries == 1 ? "exemption" : "exemptions")}";
        switch(_filter)
        {
            case ExemptionFilter.WithoutReason:
                SummaryMarked = Bold(_narrowed.WithoutReasonSummary);
                ButtonText = string.Empty;
                FilterText = "without a reason";
                ClearName = every;
                break;
            case ExemptionFilter.Rule:
                SummaryMarked = Bold(_narrowed.SummaryOfRule(_filterRuleId!));
                ButtonText = string.Empty;
                FilterText = _filterRuleId!;
                ClearName = every;
                break;
            default:
                SummaryMarked = Bold(_all.Summary);
                ButtonText = _all.WithoutReasonButton;
                FilterText = string.Empty;
                ClearName = string.Empty;
                break;
        }
    }

    [GeneratedRegex(@"\b\d+\b")]
    private static partial Regex Numbers();

    private static string Bold(string sentence)
    {
        return Numbers().Replace(sentence, "**$0**");
    }

    private void ShowPanel()
    {
        var most = _all.Rules.Count == 0 ? 1 : _all.Rules.Max(rule => rule.Count);
        RuleRows = _all.Rules.Select(rule => new ExemptionRuleRowViewModel(rule, InfoOf(rule.RuleId), most, ChooseRule)
        {
            IsChosen = _filter == ExemptionFilter.Rule && rule.RuleId == _filterRuleId,
            IsSteppedBack = _filter == ExemptionFilter.Rule && rule.RuleId != _filterRuleId
        }).ToList();
        var items = new List<object>();
        AddRuleGroup(items, "Custom rules", ExemptionRuleSource.Custom);
        AddRuleGroup(items, "Rules of .editorconfig", ExemptionRuleSource.File);
        AddRuleGroup(items, "Rules the repository does not have", ExemptionRuleSource.Unknown);
        RulePanelItems = items;
        var count = _all.Rules.Count;
        RulesHead = _filter == ExemptionFilter.Rule ? $"1 of {count.ToString(CultureInfo.InvariantCulture)} rules shown" : $"{count.ToString(CultureInfo.InvariantCulture)} {(count == 1 ? "rule" : "rules")}";
    }

    private void AddRuleGroup(List<object> items, string title, ExemptionRuleSource source)
    {
        var rows = RuleRows.Where(row => row.Rule.Source == source).ToList();
        if(rows.Count == 0)
            return;

        items.Add(new ExemptionRuleGroupHeading(title, rows.Count.ToString(CultureInfo.InvariantCulture), source, items.Count > 0));
        items.AddRange(rows);
    }

    private void ShowChosen()
    {
        if(_filter != ExemptionFilter.Rule || _filterRuleId == null)
        {
            Chosen = null;
            return;
        }

        var info = InfoOf(_filterRuleId);
        var chosen = new ChosenRuleViewModel(info, _narrowed.Exemptions, () => ActOn(info));
        Chosen = chosen;
    }

    private void ActOn(ExemptionRuleInfo rule)
    {
        if(rule.IsCustom && Chosen != null)
            _narrow(rule.Id, Chosen.StripText);
        else if(rule.IsKnown)
            _openRule(rule.Id);
    }

    private void ShowFilesLine()
    {
        var files = _visible.Files.Count;
        if(files == 0)
        {
            FilesLine = string.Empty;
            return;
        }

        var count = files.ToString(CultureInfo.InvariantCulture);
        var word = files == 1 ? "file" : "files";
        FilesLine = _filter == ExemptionFilter.Rule ? $"{count} {word} with exemptions of {_filterRuleId}, most first" : $"{count} {word}, most exemptions first";
    }

    /// <summary>
    /// The list under the head. Up to 20 exemptions: every file starts open. More: every file starts folded but the first.
    /// Rows start closed, except in the view of those with no reason. Past 50 files the rest wait for "Show".
    /// </summary>
    private void LayOutItems()
    {
        while(Items.Count > 1)
        {
            Items.RemoveAt(Items.Count - 1);
        }

        var allOpen = _visible.Entries <= _fewest;
        var shown = _showsAllFiles ? _visible.Files : _visible.Files.Take(_mostFiles).ToList();
        for(var index = 0; index < shown.Count; index++)
        {
            var file = shown[index];
            var rows = GroupRows(file).Select(row => new ExemptionRowViewModel(row.ToList(), InfoOf(row.Key.RuleId),
                _filter == ExemptionFilter.WithoutReason, OpenLine, _openRule)).ToList();
            var item = new ExemptionFileViewModel(file, rows, _inFile.GetValueOrDefault(file.Path, file.Exemptions.Count), allOpen || index == 0, FileToggled);
            Items.Add(item);
            if(item.IsOpen)
                AddRows(Items.Count, rows);
        }

        if(shown.Count > 0)
            Items.Add(TailOf(_visible.Files.Skip(shown.Count).ToList()));

        OpenAllText = Items.OfType<ExemptionFileViewModel>().Any(file => !file.IsOpen) ? "Open all" : "Fold all";
    }

    private ExemptionTailViewModel TailOf(List<ExemptionFileGroup> rest)
    {
        if(rest.Count == 0)
            return new ExemptionTailViewModel(string.Empty, string.Empty, () => { });

        var count = rest.Count.ToString(CultureInfo.InvariantCulture);
        var each = rest.Select(file => file.Exemptions.Count).Distinct().ToList();
        var words = each.Count == 1 ? $" with {(each[0] == 1 ? "one exemption" : each[0].ToString(CultureInfo.InvariantCulture) + " exemptions")} each" : string.Empty;
        var text = $"And {count} more files{words}.";
        return new ExemptionTailViewModel(text, $"Show {count} more files", () =>
        {
            _showsAllFiles = true;
            Rebuild();
        });
    }

    private void AddRows(int at, IReadOnlyList<ExemptionRowViewModel> rows)
    {
        for(var offset = 0; offset < rows.Count; offset++)
        {
            Items.Insert(at + offset, rows[offset]);
        }
    }

    /// <summary>
    /// A file was opened or folded: its exemptions come into the list under it or go out of it.
    /// </summary>
    private void FileToggled(ExemptionFileViewModel file)
    {
        if(_rebuilding)
            return;

        var at = Items.IndexOf(file);
        if(at < 0)
            return;

        if(file.IsOpen)
        {
            if(file.Rows.Count > 0 && !ReferenceEquals(Items.ElementAtOrDefault(at + 1), file.Rows[0]))
                AddRows(at + 1, file.Rows);
        }
        else
        {
            foreach(var row in file.Rows)
            {
                Items.Remove(row);
            }
        }

        OpenAllText = Items.OfType<ExemptionFileViewModel>().Any(item => !item.IsOpen) ? "Open all" : "Fold all";
    }

    private void OpenLine(ExemptionEntry entry)
    {
        OpenLineRequested?.Invoke(Path.Combine(_scan?.WorkFolder ?? _folder.FolderPath, entry.File.Replace('/', Path.DirectorySeparatorChar)), entry.Line);
    }

    private sealed record ReadResult(ExemptionScan? Scan, string? Failure, Dictionary<string, ExemptionRuleInfo> Rules);
}

/// <summary>
/// What the screen is saying about the last commit.
/// </summary>
internal enum ExemptionsState
{
    /// <summary>The first scan is reading.</summary>
    Scanning,

    /// <summary>Exemptions were found.</summary>
    Listed,

    /// <summary>The commit holds no exemption.</summary>
    NoExemption,

    /// <summary>The repository has no commit yet.</summary>
    NoCommit,

    /// <summary>The commit could not be read.</summary>
    Failed
}

/// <summary>
/// The view of the exemptions that is chosen, which narrows the list.
/// </summary>
internal enum ExemptionFilter
{
    None,

    WithoutReason,

    Rule
}
