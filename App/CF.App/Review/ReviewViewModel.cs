using System.Collections.ObjectModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using CF.Core.Changes;
using CF.Core.Findings;
using CF.Core.Fixes;
using CF.Core.Ports;
using CF.Core.Prompts;
using CF.Core.Review;
using CF.Core.Rules.Settings;
using CF.Core.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CF.App.Review;

/// <summary>
/// The Review screen: the bar with its choice and run button, the verdict, the panel (the files of a push, or the count by rule
/// of a folder) and the files with their findings. It holds no logic of the product: the body finds the scope of the push or of
/// the folder and reviews it, this shows the answer. The review runs off the window's thread; the window stays usable and a second
/// press starts nothing. Each kind of review keeps its last result while the program runs; moving the choice starts nothing.
/// </summary>
internal sealed partial class ReviewViewModel : ObservableObject
{
    private const int _startOpenUpTo = 20;
    private const int _cleanFilesNamed = 8;
    private const int _recentFolders = 5;
    private const int _reasonsFirst = 5;
    private const int _cleanPushFilesListed = 20;
    private const double _secondsWithDecimal = 10;
    private const string _noFolderYet = "No folder reviewed yet";
    private const string _noFolderLead = "It is read as the last commit holds it, old findings included.";

    private readonly ReviewHeadViewModel _head;
    private readonly ReviewTailViewModel _tail;
    private bool _itemsPaused;

    private readonly IReviewService _review;
    private readonly ReviewFixQueue? _fixes;
    private readonly ConditionalWeakTable<ReviewResult, ReviewFixSession> _fixSessions = new();
    private readonly IRuleService _rules;
    private readonly IClock _clock;
    private readonly OpenedFolder _folder;
    private readonly Action<string> _showRule;
    private readonly List<string> _recent = new List<string>();
    private ReviewState? _changesState;
    private ReviewState? _folderState;
    private ReviewResult? _result;
    private Dictionary<string, RuleInfo> _ruleInfos = new Dictionary<string, RuleInfo>(StringComparer.Ordinal);
    private string _resultTarget = string.Empty;
    private string _resultLabel = string.Empty;
    private bool _startsOpen;
    private bool _running;
    private int _generation;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsEmptyBlock), nameof(ShowsRulesLine), nameof(ShowsBarRun), nameof(RunIsMain), nameof(RunIsSecond), nameof(CanCopyAll))]
    public partial VerdictViewModel Verdict { get; set; }

    private int _fixedFindings;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasAppliedFixes), nameof(AppliedFixesText))]
    public partial int AppliedFixes { get; private set; }

    public bool HasAppliedFixes
    {
        get { return AppliedFixes > 0; }
    }

    public string AppliedFixesText
    {
        get
        {
            var findings = _fixedFindings == 1 ? "finding" : "findings";
            return AppliedFixes == 1
                ? string.Create(CultureInfo.InvariantCulture, $"1 fix applied to your files, covering {_fixedFindings} {findings}. It is not committed yet: review again to update this verdict.")
                : string.Create(CultureInfo.InvariantCulture, $"{AppliedFixes} fixes applied to your files, covering {_fixedFindings} {findings}. They are not committed yet: review again to update this verdict.");
        }
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RunText), nameof(CanRun), nameof(RunShowsIcon))]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanCopyAll))]
    public partial bool HasResult { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasBanner), nameof(ShowsEmptyBlock), nameof(ShowsRulesLine), nameof(ShowsBarRun))]
    public partial string? BannerText { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<ReasonViewModel> Reasons { get; set; } = new List<ReasonViewModel>();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasReasons), nameof(CanShowAllReasons), nameof(ReasonsShown))]
    public partial bool AllReasonsShown { get; set; }

    [ObservableProperty]
    public partial string ReasonsLine { get; set; } = string.Empty;

    /// <summary>
    /// The files of a clean push, by path, as the main column lists them (there is no panel for a push with nothing in it).
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCleanPush))]
    public partial IReadOnlyList<string> CleanPushFiles { get; set; } = new List<string>();

    [ObservableProperty]
    public partial string CleanPushLine { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCleanPushMore))]
    public partial string CleanPushMore { get; set; } = string.Empty;

    /// <summary>
    /// What a push would meet in numbers: the rules that are on, as the filter of Rules counts them. Not set while the rules are not read.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasRulesLine), nameof(ShowsRulesLine), nameof(RulesBlockText), nameof(RulesWarnText), nameof(HasRulesBlock), nameof(HasRulesWarn), nameof(HasNoRuleOn))]
    public partial RulesOn? RulesCounts { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasFiles))]
    public partial IReadOnlyList<FileGroupViewModel> FileGroups { get; set; } = new List<FileGroupViewModel>();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPanel))]
    public partial IReadOnlyList<PanelFileViewModel> PanelFiles { get; set; } = new List<PanelFileViewModel>();

    [ObservableProperty]
    public partial string PanelHead { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCleanSummary), nameof(HasPanel))]
    public partial string CleanFilesText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string FilesLine { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string OpenAllText { get; set; } = "Open all";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BlockingText), nameof(HasBlocking))]
    public partial int BlockingCount { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PanelIsListed), nameof(PanelToggleText))]
    public partial bool IsOnePane { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PanelIsListed), nameof(PanelToggleText))]
    public partial bool PanelShown { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsFolderChosen), nameof(HasPanel), nameof(HasRulePanel), nameof(RunText), nameof(RunIsMain), nameof(RunIsSecond), nameof(RunShowsIcon))]
    public partial ReviewChoiceViewModel SelectedChoice { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasRulePanel))]
    public partial IReadOnlyList<RuleRowViewModel> RuleRows { get; set; } = new List<RuleRowViewModel>();

    [ObservableProperty]
    public partial string RulesHead { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasChosenRule), nameof(CanCopyAll))]
    public partial string? ChosenRule { get; set; }

    [ObservableProperty]
    public partial string ShowAllText { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasFolderClean))]
    public partial string FolderCleanLead { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string FolderCleanNames { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string FolderLabel { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool PickerOpen { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<FolderPickerEntryViewModel> PickerEntries { get; set; } = new List<FolderPickerEntryViewModel>();

    public ReviewViewModel(IReviewService review, IRuleService rules, IClock clock, OpenedFolder folder, Action<string> showRule, IFixService? fixes = null)
    {
        _review = review;
        _fixes = fixes == null ? null : new ReviewFixQueue(fixes);
        _rules = rules;
        _clock = clock;
        _folder = folder;
        _showRule = showRule;
        CopyAll = new CopyButtonViewModel("Copy all as a prompt", BuildAllPrompt, () => Clipboard);
        _head = new ReviewHeadViewModel(this);
        _tail = new ReviewTailViewModel(this);
        Items.Add(_head);
        Items.Add(_tail);
        Choices = new List<ReviewChoiceViewModel>
        {
            new ReviewChoiceViewModel("The changes", "branch", true),
            new ReviewChoiceViewModel("A folder", "folder", true)
        };
        SelectedChoice = Choices[0];
        FolderLabel = folder.Name;
        Verdict = folder.InsideRepository ? Starting() : VerdictViewModel.Blank(VerdictKind.NoRepository, "No repository open", "Open a folder inside a git repository to review what a push would send.");
    }

    /// <summary>
    /// The page as one flat list: the head, then each file's line followed by its findings while it is open, then the tail.
    /// </summary>
    public ObservableCollection<ReviewListItemViewModel> Items { get; } = new ObservableCollection<ReviewListItemViewModel>();

    /// <summary>
    /// Raised when a file of the panel asks to be brought to the top of the sheet.
    /// </summary>
    public event Action<FileGroupViewModel>? FileRevealRequested;

    public event Action<FindingViewModel>? FindingFocusRequested;

    /// <summary>
    /// Raised when the banner asks to open the settings file in the program the system uses for it.
    /// </summary>
    public event Action<string>? OpenFileRequested;

    /// <summary>
    /// "Open Doctor" on a reason that says the machine is not ready: the window shows the Doctor screen.
    /// </summary>
    public event Action? OpenDoctorRequested;

    public event Action<string, int>? OpenLineRequested;

    private void OpenConfigLine(int line)
    {
        var folder = Path.GetDirectoryName(_folder.SettingsPath) ?? string.Empty;
        OpenLineRequested?.Invoke(Path.Combine(folder, ".editorconfig"), line);
    }

    /// <summary>
    /// Raised when the picker asks for the system's own dialog: true for a folder, false for a project file. The view answers with
    /// <see cref="UseTarget" />.
    /// </summary>
    public event Action<bool>? ChooseRequested;

    /// <summary>
    /// Where the copy buttons put their text: the window gives the system's clipboard.
    /// </summary>
    public ITextClipboard? Clipboard { get; set; }

    /// <summary>
    /// The button of the verdict's first line: the prompt for the review as shown.
    /// </summary>
    public CopyButtonViewModel CopyAll { get; }

    /// <summary>
    /// Whether the review as shown has findings to hand over.
    /// </summary>
    public bool CanCopyAll
    {
        get
        {
            return HasResult && FindingsShown().Count > 0;
        }
    }

    public IReadOnlyList<ReviewChoiceViewModel> Choices { get; }

    public bool IsFolderChosen
    {
        get
        {
            return ReferenceEquals(SelectedChoice, Choices[1]);
        }
    }

    public bool CanRun
    {
        get
        {
            return !IsBusy && _folder.InsideRepository;
        }
    }

    /// <summary>
    /// Whether the result on the page is of the choice shown: for a folder, of the folder now named on the button.
    /// </summary>
    public bool ResultMatches
    {
        get
        {
            return HasResult && (!IsFolderChosen || string.Equals(_resultTarget, FolderPath, StringComparison.Ordinal));
        }
    }

    public string RunText
    {
        get
        {
            if(IsBusy)
                return "Reviewing";

            return ResultMatches ? "Review again" : "Review now";
        }
    }

    /// <summary>
    /// "Review now" keeps its shield; "Review again", the second button, has its words alone.
    /// </summary>
    public bool RunShowsIcon
    {
        get
        {
            return !IsBusy && !ResultMatches;
        }
    }

    /// <summary>
    /// The main look: no result for the choice, and an incomplete review (which is no result either).
    /// </summary>
    public bool RunIsMain
    {
        get
        {
            return !ResultMatches || Verdict.IsIncomplete;
        }
    }

    public bool RunIsSecond
    {
        get
        {
            return !RunIsMain;
        }
    }

    /// <summary>
    /// States before a review take the sheet: one block in the middle of the pane, in place of the verdict.
    /// </summary>
    public bool ShowsEmptyBlock
    {
        get
        {
            return Verdict.Kind is VerdictKind.NoReview or VerdictKind.NoPush or VerdictKind.NoRepository && !HasBanner;
        }
    }

    /// <summary>
    /// The block before a review holds the run button itself, so the review bar does not draw its own then.
    /// </summary>
    public bool ShowsBarRun
    {
        get
        {
            return !(ShowsEmptyBlock && Verdict.Kind == VerdictKind.NoReview);
        }
    }

    public bool HasReasons
    {
        get
        {
            return Reasons.Count > 0;
        }
    }

    public IReadOnlyList<ReasonViewModel> ReasonsShown
    {
        get
        {
            return AllReasonsShown ? Reasons : Reasons.Take(_reasonsFirst).ToList();
        }
    }

    public bool CanShowAllReasons
    {
        get
        {
            return !AllReasonsShown && Reasons.Count > _reasonsFirst;
        }
    }

    public string ShowAllReasonsText
    {
        get
        {
            return $"Show all {Reasons.Count}";
        }
    }

    public bool HasCleanPush
    {
        get
        {
            return CleanPushFiles.Count > 0;
        }
    }

    public bool HasCleanPushMore
    {
        get
        {
            return CleanPushMore.Length > 0;
        }
    }

    /// <summary>
    /// The rules line stands in the block before a review, once the rules are read.
    /// </summary>
    public bool ShowsRulesLine
    {
        get
        {
            return ShowsEmptyBlock && Verdict.Kind == VerdictKind.NoReview && RulesCounts != null;
        }
    }

    public bool HasRulesLine
    {
        get
        {
            return RulesCounts != null;
        }
    }

    public bool HasRulesBlock
    {
        get
        {
            return RulesCounts is { Blocks: > 0 };
        }
    }

    public bool HasRulesWarn
    {
        get
        {
            return RulesCounts is { Warns: > 0 };
        }
    }

    public bool HasNoRuleOn
    {
        get
        {
            return RulesCounts is { Blocks: 0, Warns: 0 };
        }
    }

    public string RulesBlockText
    {
        get
        {
            var count = RulesCounts?.Blocks ?? 0;
            return count == 1 ? "1 rule blocks a push" : $"{count} rules block a push";
        }
    }

    public string RulesWarnText
    {
        get
        {
            var count = RulesCounts?.Warns ?? 0;
            return count == 1 ? "1 only warns" : $"{count} only warn";
        }
    }

    public bool HasBanner
    {
        get
        {
            return BannerText != null;
        }
    }

    public bool HasFiles
    {
        get
        {
            return FileGroups.Count > 0;
        }
    }

    /// <summary>
    /// The panel of a push: its files.
    /// </summary>
    public bool HasPanel
    {
        get
        {
            return !IsFolderChosen && (PanelFiles.Count > 0 || CleanFilesText.Length > 0);
        }
    }

    /// <summary>
    /// The panel of a folder: the count by rule.
    /// </summary>
    public bool HasRulePanel
    {
        get
        {
            return IsFolderChosen && RuleRows.Count > 0;
        }
    }

    public bool HasChosenRule
    {
        get
        {
            return ChosenRule != null;
        }
    }

    public bool HasCleanSummary
    {
        get
        {
            return CleanFilesText.Length > 0;
        }
    }

    public bool HasFolderClean
    {
        get
        {
            return FolderCleanLead.Length > 0;
        }
    }

    public bool HasBlocking
    {
        get
        {
            return BlockingCount > 0;
        }
    }

    /// <summary>
    /// The tooltip of the count on the rail while it shows.
    /// </summary>
    public string BlockingText
    {
        get
        {
            return BlockingCount == 1 ? "1 finding blocks the last push" : $"{BlockingCount} findings block the last push";
        }
    }

    /// <summary>
    /// Whether the panel's rows are listed: always in two panes, and in one pane once "Show" is pressed.
    /// </summary>
    public bool PanelIsListed
    {
        get
        {
            return !IsOnePane || PanelShown;
        }
    }

    public string PanelToggleText
    {
        get
        {
            return PanelShown ? "Hide" : "Show";
        }
    }

    /// <summary>
    /// The folder that is open: where the system's dialogs open.
    /// </summary>
    public string FolderRoot
    {
        get
        {
            return _folder.FolderPath;
        }
    }

    /// <summary>
    /// Takes the keyboard's Escape: it closes the picker, or else ends a rule chosen. Returns whether it did anything.
    /// </summary>
    public bool EscapePressed()
    {
        if(PickerOpen)
        {
            PickerOpen = false;
            return true;
        }

        if(ChosenRule == null)
            return false;

        ClearRule();
        return true;
    }

    /// <summary>
    /// The folder or project file the folder review is of, as a whole path; empty for the whole repository.
    /// </summary>
    public string FolderPath { get; private set; } = string.Empty;

    /// <summary>
    /// Looks for the push again while nothing is reviewed: a branch without a remote says so, and a branch with one says that
    /// no review has been made yet.
    /// </summary>
    public async Task RefreshAsync()
    {
        if(_running || IsFolderChosen || HasResult || !_folder.InsideRepository)
            return;

        var generation = ++_generation;
        var answer = await Task.Run(() => _review.FindPushScope(_folder.FolderPath));
        if(generation == _generation && !_running && !IsFolderChosen && !HasResult)
            Verdict = answer.HasPush ? Starting() : NoPush(answer);
    }

    /// <summary>Whether one of the two saved review results contains this judgment.</summary>
    public bool HoldsStructureJudgment(StructureJudgment judgment)
    {
        return !IsBusy && (MatchesJudgment(_result, judgment) || MatchesJudgment(_changesState?.Result, judgment) || MatchesJudgment(_folderState?.Result, judgment));
    }

    public bool ShowStructureJudgment(StructureJudgment judgment)
    {
        if(!HoldsStructureJudgment(judgment))
            return false;

        if(!MatchesJudgment(_result, judgment))
            SelectedChoice = MatchesJudgment(_changesState?.Result, judgment) ? Choices[0] : Choices[1];

        return true;
    }

    private static bool MatchesJudgment(ReviewResult? result, StructureJudgment judgment)
    {
        return result?.StructureJudgment is { } held && held.EndedAt == judgment.EndedAt &&
            held.Folder == judgment.Folder && held.Breaches.SequenceEqual(judgment.Breaches);
    }

    /// <summary>
    /// Names the folder or project file the folder review is of (an empty path is the whole repository). Nothing is started: the run
    /// button says "Review now".
    /// </summary>
    public void UseTarget(string path)
    {
        FolderPath = path;
        FolderLabel = LabelOf(path);
        PickerOpen = false;
        NotifyRunButton();
    }

    partial void OnSelectedChoiceChanged(ReviewChoiceViewModel oldValue, ReviewChoiceViewModel newValue)
    {
        if((object?)oldValue == null)
            return;

        var saved = Capture();
        if(ReferenceEquals(oldValue, Choices[1]))
            _folderState = saved;
        else
            _changesState = saved;

        PickerOpen = false;
        Restore(ReferenceEquals(newValue, Choices[1]) ? _folderState : _changesState, ReferenceEquals(newValue, Choices[1]));
        NotifyRunButton();
        if(!ReferenceEquals(newValue, Choices[1]))
            _ = RefreshAsync();
    }

    partial void OnHasResultChanged(bool value)
    {
        NotifyRunButton();
    }

    partial void OnPickerOpenChanged(bool value)
    {
        if(value)
            PickerEntries = BuildPicker();
    }

    private void NotifyRunButton()
    {
        OnPropertyChanged(nameof(ResultMatches));
        OnPropertyChanged(nameof(RunText));
        OnPropertyChanged(nameof(RunIsMain));
        OnPropertyChanged(nameof(RunIsSecond));
        OnPropertyChanged(nameof(RunShowsIcon));
    }

    [RelayCommand(CanExecute = nameof(CanRun))]
    private async Task RunAsync()
    {
        if(_running)
            return;

        _running = true;
        _fixedFindings = 0;
        AppliedFixes = 0;
        _generation++;
        IsBusy = true;
        RunCommand.NotifyCanExecuteChanged();
        var before = Verdict;
        var isFolder = IsFolderChosen;
        var target = FolderPath;
        BannerText = null;
        PickerOpen = false;
        Verdict = VerdictViewModel.Running();
        try
        {
            var ran = await Task.Run(() => Execute(isFolder, target));
            Show(ran, before, target);
        }
        finally
        {
            _running = false;
            IsBusy = false;
            RunCommand.NotifyCanExecuteChanged();
        }
    }

    [RelayCommand]
    private void ToggleAll()
    {
        var open = !FileGroups.Any(group => group.IsOpen);
        _itemsPaused = true;
        foreach(var group in FileGroups)
            group.IsOpen = open;

        _itemsPaused = false;
        SyncItems();
    }

    [RelayCommand]
    private void TogglePanel()
    {
        PanelShown = !PanelShown;
    }

    [RelayCommand]
    private void OpenSettingsFile()
    {
        OpenFileRequested?.Invoke(_folder.SettingsPath);
    }

    [RelayCommand]
    private void TogglePicker()
    {
        if(!IsBusy)
            PickerOpen = !PickerOpen;
    }

    [RelayCommand]
    private void PickEntry(FolderPickerEntryViewModel entry)
    {
        switch(entry.Kind)
        {
            case FolderPickerEntryKind.Whole:
                UseTarget(string.Empty);
                break;
            case FolderPickerEntryKind.Recent:
                UseTarget(entry.Path);
                break;
            default:
                PickerOpen = false;
                ChooseRequested?.Invoke(entry.Kind == FolderPickerEntryKind.ChooseFolder);
                break;
        }
    }

    [RelayCommand]
    private void ClearRule()
    {
        if(ChosenRule == null)
            return;

        ChosenRule = null;
        ApplyRule();
    }

    private void ChooseRule(RuleRowViewModel row)
    {
        ChosenRule = ChosenRule == row.RuleId ? null : row.RuleId;
        ApplyRule();
    }

    private static VerdictViewModel Starting()
    {
        return VerdictViewModel.Blank(VerdictKind.NoReview, "No review yet", "Run one to see what a push would meet.");
    }

    private static VerdictViewModel NoFolderYet()
    {
        return VerdictViewModel.Blank(VerdictKind.NoReview, _noFolderYet, _noFolderLead);
    }

    private static VerdictViewModel NoPush(PushScopeAnswer answer)
    {
        return VerdictViewModel.Blank(VerdictKind.NoPush, "No push to review", answer.Reason);
    }

    private string LabelOf(string path)
    {
        if(path.Length == 0)
            return _folder.Name;

        var relative = Path.GetRelativePath(_folder.FolderPath, path);
        var outside = relative == ".." || relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal) || Path.IsPathRooted(relative);
        return outside ? path : relative.Replace(Path.DirectorySeparatorChar, '/');
    }

    private void Remember(string path)
    {
        _recent.RemoveAll(candidate => string.Equals(candidate, path, StringComparison.Ordinal));
        _recent.Insert(0, path);
        if(_recent.Count > _recentFolders)
            _recent.RemoveRange(_recentFolders, _recent.Count - _recentFolders);
    }

    private List<FolderPickerEntryViewModel> BuildPicker()
    {
        var entries = new List<FolderPickerEntryViewModel> { new FolderPickerEntryViewModel(FolderPickerEntryKind.Whole, _folder.Name, string.Empty, string.Empty, FolderPath.Length == 0) };
        foreach(var path in _recent)
            entries.Add(new FolderPickerEntryViewModel(FolderPickerEntryKind.Recent, LabelOf(path), File.Exists(path) ? "project" : "folder", path, string.Equals(path, FolderPath, StringComparison.Ordinal)));

        entries.Add(new FolderPickerEntryViewModel(FolderPickerEntryKind.ChooseFolder, "Choose a folder...", string.Empty, string.Empty, false));
        entries.Add(new FolderPickerEntryViewModel(FolderPickerEntryKind.ChooseProject, "Choose a project file...", string.Empty, string.Empty, false));
        return entries;
    }

    private ReviewState Capture()
    {
        return new ReviewState
        {
            Verdict = Verdict,
            HasResult = HasResult,
            FileGroups = FileGroups,
            PanelFiles = PanelFiles,
            PanelHead = PanelHead,
            CleanFilesText = CleanFilesText,
            FilesLine = FilesLine,
            OpenAllText = OpenAllText,
            RuleRows = RuleRows,
            RulesHead = RulesHead,
            ChosenRule = ChosenRule,
            ShowAllText = ShowAllText,
            FolderCleanLead = FolderCleanLead,
            FolderCleanNames = FolderCleanNames,
            Result = _result,
            RuleInfos = _ruleInfos,
            ResultTarget = _resultTarget,
            ResultLabel = _resultLabel,
            StartsOpen = _startsOpen,
            Reasons = Reasons,
            ReasonsLine = ReasonsLine,
            AllReasonsShown = AllReasonsShown,
            CleanPushFiles = CleanPushFiles,
            CleanPushLine = CleanPushLine,
            CleanPushMore = CleanPushMore
        };
    }

    private void Restore(ReviewState? state, bool isFolder)
    {
        BannerText = null;
        if(state == null)
        {
            Blank(isFolder);
            return;
        }

        Verdict = state.Verdict;
        HasResult = state.HasResult;
        FileGroups = state.FileGroups;
        PanelFiles = state.PanelFiles;
        PanelHead = state.PanelHead;
        CleanFilesText = state.CleanFilesText;
        FilesLine = state.FilesLine;
        OpenAllText = state.OpenAllText;
        RuleRows = state.RuleRows;
        RulesHead = state.RulesHead;
        ChosenRule = state.ChosenRule;
        ShowAllText = state.ShowAllText;
        FolderCleanLead = state.FolderCleanLead;
        FolderCleanNames = state.FolderCleanNames;
        _result = state.Result;
        _ruleInfos = state.RuleInfos;
        _resultTarget = state.ResultTarget;
        _resultLabel = state.ResultLabel;
        _startsOpen = state.StartsOpen;
        SetReasons(state.Reasons, state.ReasonsLine, state.AllReasonsShown);
        SetCleanPush(state.CleanPushFiles, state.CleanPushLine, state.CleanPushMore);
        OnPropertyChanged(nameof(CanCopyAll));
    }

    private void Blank(bool isFolder)
    {
        Verdict = isFolder ? NoFolderYet() : Starting();
        HasResult = false;
        FileGroups = new List<FileGroupViewModel>();
        PanelFiles = new List<PanelFileViewModel>();
        PanelHead = string.Empty;
        CleanFilesText = string.Empty;
        FilesLine = string.Empty;
        OpenAllText = "Open all";
        RuleRows = new List<RuleRowViewModel>();
        RulesHead = string.Empty;
        ChosenRule = null;
        ShowAllText = string.Empty;
        FolderCleanLead = string.Empty;
        FolderCleanNames = string.Empty;
        _result = null;
        _ruleInfos = new Dictionary<string, RuleInfo>(StringComparer.Ordinal);
        _resultTarget = string.Empty;
        _resultLabel = string.Empty;
        SetReasons(new List<ReasonViewModel>(), string.Empty, false);
        SetCleanPush(new List<string>(), string.Empty, string.Empty);
        OnPropertyChanged(nameof(CanCopyAll));
    }

    private void SetReasons(IReadOnlyList<ReasonViewModel> reasons, string line, bool all)
    {
        Reasons = reasons;
        ReasonsLine = line;
        AllReasonsShown = all;
        OnPropertyChanged(nameof(HasReasons));
        OnPropertyChanged(nameof(ReasonsShown));
        OnPropertyChanged(nameof(CanShowAllReasons));
        OnPropertyChanged(nameof(ShowAllReasonsText));
    }

    private void SetCleanPush(IReadOnlyList<string> files, string line, string more)
    {
        CleanPushFiles = files;
        CleanPushLine = line;
        CleanPushMore = more;
    }

    /// <summary>
    /// Sets what the block before a review says about the rules: nothing while they are not read.
    /// </summary>
    public void ShowRulesLine(RulesOn? counts)
    {
        RulesCounts = counts;
    }

    [RelayCommand]
    private void ShowAllReasons()
    {
        AllReasonsShown = true;
        OnPropertyChanged(nameof(ReasonsShown));
    }

    private Ran Execute(bool isFolder, string target)
    {
        var answer = isFolder ? null : _review.FindPushScope(_folder.FolderPath);
        if(answer != null && answer.Scope == null)
            return new Ran(answer, null, null, new Dictionary<string, RuleInfo>(StringComparer.Ordinal));

        var scope = answer?.Scope?.WithSource(ReviewSource.App) ?? ReviewScope.ForWholeFolder(target);

        try
        {
            var result = _review.ReviewChanges(_folder.FolderPath, scope, null);
            return new Ran(answer, result, null, RulesBehind(result));
        }
        catch(SettingsException exception)
        {
            var shown = SettingsFileName.Default + " (in the tree under review)";
            return new Ran(answer, null, SettingsFileName.FormatError(shown, exception), new Dictionary<string, RuleInfo>(StringComparer.Ordinal));
        }
        catch(ReviewTargetException exception)
        {
            return new Ran(answer, null, exception.Message, new Dictionary<string, RuleInfo>(StringComparer.Ordinal));
        }
    }

    private Dictionary<string, RuleInfo> RulesBehind(ReviewResult result)
    {
        var infos = new Dictionary<string, RuleInfo>(StringComparer.Ordinal);
        var team = ReadTeamRules();
        var file = ReadFileRules();
        foreach(var finding in result.Findings.Where(finding => !infos.ContainsKey(finding.RuleId)))
        {
            var isTeam = finding.Source is FindingSource.CodeRule or FindingSource.StructureRule;
            if(isTeam && team.TryGetValue(finding.RuleId, out var definition))
                infos[finding.RuleId] = new RuleInfo(finding.Source, definition.Title, true, SentencePieceViewModel.Of(definition), PromptRule.Of(definition));
            else if(!isTeam && file.TryGetValue(finding.RuleId, out var fileRule))
                infos[finding.RuleId] = new RuleInfo(finding.Source, fileRule.Title ?? string.Empty, true, null, PromptRule.Of(fileRule), fileRule);
            else
                infos[finding.RuleId] = new RuleInfo(finding.Source, string.Empty, false, null);
        }

        return infos;
    }

    private Dictionary<string, CF.Core.Rules.RuleDefinition> ReadTeamRules()
    {
        try
        {
            return _rules.ListRules(_folder.SettingsPath).ToDictionary(rule => rule.Id, StringComparer.Ordinal);
        }
        catch(SettingsException)
        {
            return new Dictionary<string, CF.Core.Rules.RuleDefinition>(StringComparer.Ordinal);
        }
    }

    private Dictionary<string, CF.Core.Code.FileRule> ReadFileRules()
    {
        try
        {
            return _rules.ListFileRules(_folder.SettingsPath).SelectMany(listing => listing.Rules).GroupBy(rule => rule.Id, StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        }
        catch(Exception exception) when(exception is SettingsException || exception is CF.Core.Rules.RuleChangeException)
        {
            return new Dictionary<string, CF.Core.Code.FileRule>(StringComparer.Ordinal);
        }
    }

    private void Show(Ran ran, VerdictViewModel before, string target)
    {
        if(ran.Problem != null)
        {
            BannerText = ran.Problem;
            Verdict = before;
            return;
        }

        if(ran.Result == null)
        {
            Verdict = ran.Scope == null ? before : NoPush(ran.Scope);
            return;
        }

        if(ran.Scope == null)
            ShowFolderResult(ran.Result, ran.Rules, target);
        else
            ShowResult(ran.Result, ran.Rules);
    }

    private void ShowResult(ReviewResult result, Dictionary<string, RuleInfo> rules)
    {
        _result = result;
        _ruleInfos = rules;
        _startsOpen = result.Findings.Count <= _startOpenUpTo;
        var groups = BuildGroups(result, rules, null, false);
        SetFiles(groups);
        BuildPanel(result, groups);
        BuildReasons(result);
        BuildCleanPush(result);
        FilesLine = groups.Count == 1 ? "1 file with findings" : $"{groups.Count} files with findings, most findings first";
        Verdict = Describe(result, groups.Count);
        BlockingCount = result.Findings.Count(finding => finding.Severity == Severity.Block);
        HasResult = true;
        UpdateOpenAllText();
    }

    private void ShowFolderResult(ReviewResult result, Dictionary<string, RuleInfo> rules, string target)
    {
        _result = result;
        _ruleInfos = rules;
        _resultTarget = target;
        _resultLabel = LabelOf(target);
        if(target.Length > 0)
            Remember(target);

        _startsOpen = result.Findings.Count <= _startOpenUpTo;
        ChosenRule = null;
        BuildRuleRows(result, rules);
        BuildFolderClean(result);
        BuildReasons(result);
        SetCleanPush(new List<string>(), string.Empty, string.Empty);
        ApplyRule();
        HasResult = true;
        NotifyRunButton();
    }

    private void SetFiles(List<FileGroupViewModel> groups)
    {
        foreach(var group in groups)
        {
            group.PropertyChanged += (_, e) =>
            {
                if(e.PropertyName != nameof(FileGroupViewModel.IsOpen))
                    return;

                UpdateOpenAllText();
                if(!_itemsPaused)
                    SyncItems();
            };
        }

        FileGroups = groups;
        UpdateOpenAllText();
    }

    private List<FileGroupViewModel> BuildGroups(ReviewResult result, Dictionary<string, RuleInfo> rules, string? onlyRule, bool isFolder)
    {
        var groups = new List<FileGroupViewModel>();
        foreach(var byFile in result.Findings.GroupBy(finding => finding.File ?? "(no file)", StringComparer.Ordinal))
        {
            var models = byFile.Where(finding => onlyRule == null || finding.RuleId == onlyRule)
                .Select(finding => FindingModel(result, finding, byFile, rules[finding.RuleId], isFolder)).ToList();
            if(models.Count > 0)
                groups.Add(new FileGroupViewModel(byFile.Key, models, _startsOpen));
        }

        return groups.OrderByDescending(group => group.Total).ThenBy(group => group.Path, StringComparer.Ordinal).ToList();
    }

    private FindingViewModel FindingModel(ReviewResult result, Finding finding, IEnumerable<Finding> fileFindings, RuleInfo rule, bool isFolder)
    {
        var session = _fixSessions.GetOrCreateValue(result);
        if(session.Models.TryGetValue(finding, out var model))
            return model;

        Func<Task<FixAnswer>>? prepare = null;
        var tree = result.TreeOf(finding);
        if(_fixes != null && result.Repository != null && tree != null && finding.Source == FindingSource.EditorConfig)
        {
            var request = new FixRequest(result.Repository, tree, finding);
            prepare = () => session.PrepareAsync(_fixes, request);
        }

        model = new FindingViewModel(finding, fileFindings, rule, ContentOf(result, finding), false,
            _showRule, isFolder, () => Clipboard, OpenConfigLine, _fixes, prepare);
        session.Models.Add(finding, model);
        session.UpdateFinding(finding, model);
        model.FixFinished += (completed, fix) =>
        {
            var covered = session.RecordFix(fix, tree, result);
            if(!ReferenceEquals(_result, result) || _running)
                return;

            _fixedFindings += covered;
            AppliedFixes++;
            foreach(var group in FileGroups)
                group.RefreshFixedCount();

            FocusNextFinding(completed);
        };
        model.IsOpen = _startsOpen && finding.Severity == Severity.Block && !model.IsFixed;
        return model;
    }

    private void FocusNextFinding(FindingViewModel completed)
    {
        var rows = FileGroups.SelectMany(group => group.Findings.Select(finding => (Group: group, Finding: finding))).ToList();
        var index = rows.FindIndex(row => ReferenceEquals(row.Finding, completed));
        var next = rows.Skip(index + 1).FirstOrDefault(row => !row.Finding.IsFixed);
        if(next.Finding == null)
            next = rows.Take(index).FirstOrDefault(row => !row.Finding.IsFixed);

        if(next.Finding != null)
        {
            next.Group.IsOpen = true;
            FindingFocusRequested?.Invoke(next.Finding);
            return;
        }

        if(index >= 0)
            FindingFocusRequested?.Invoke(completed);
    }

    /// <summary>
    /// The findings the page shows: all of them, or the rule's own while a rule is chosen in a folder review.
    /// </summary>
    private IReadOnlyList<Finding> FindingsShown()
    {
        if(_result == null)
            return new List<Finding>();

        return ChosenRule == null ? _result.Findings : _result.Findings.Where(finding => finding.RuleId == ChosenRule).ToList();
    }

    private string BuildAllPrompt()
    {
        var rules = _ruleInfos.Where(entry => entry.Value.Prompt != null).ToDictionary(entry => entry.Key, entry => entry.Value.Prompt!, StringComparer.Ordinal);
        return AgentPrompt.ForReview(FindingsShown(), _result?.Sources ?? new Dictionary<string, string>(), rules, _result?.Outcome == ReviewOutcome.Incomplete);
    }

    private static string? ContentOf(ReviewResult result, Finding finding)
    {
        return finding.File != null && result.Sources.TryGetValue(finding.File, out var content) ? content : null;
    }

    private void BuildPanel(ReviewResult result, List<FileGroupViewModel> groups)
    {
        var files = result.Coverage.Where(entry => entry.Kind == CoverageKind.File).GroupBy(entry => entry.What, StringComparer.Ordinal).ToList();
        var byPath = groups.ToDictionary(group => group.Path, StringComparer.Ordinal);
        var rows = new List<PanelFileViewModel>();
        rows.AddRange(groups.Select(group => new PanelFileViewModel(group.Path, group, ShowFile, string.Empty)));
        var others = files.Where(file => !byPath.ContainsKey(file.Key)).OrderBy(file => file.Key, StringComparer.Ordinal).ToList();
        var failures = result.Coverage.Where(entry => entry.Status != CoverageStatus.Reviewed && entry.Kind != CoverageKind.Rule).GroupBy(entry => entry.What, StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.First().Reason, StringComparer.Ordinal);
        var clean = others.Where(file => !failures.ContainsKey(file.Key)).ToList();
        var collapse = clean.Count > _cleanFilesNamed;
        foreach(var file in others)
        {
            if(failures.TryGetValue(file.Key, out var reason))
                rows.Add(new PanelFileViewModel(file.Key, null, null, reason));
            else if(!collapse)
                rows.Add(new PanelFileViewModel(file.Key, null, null, string.Empty));
        }

        PanelFiles = rows;
        CleanFilesText = collapse ? $"{clean.Count} files have no findings." : string.Empty;
        PanelHead = $"{files.Count} in this push, {groups.Count} with findings";
    }

    /// <summary>
    /// What could not be reviewed, as a list under the verdict: the build first, then the others as the body gives them.
    /// </summary>
    private void BuildReasons(ReviewResult result)
    {
        var entries = result.Outcome == ReviewOutcome.Incomplete ? result.Coverage.Where(entry => entry.Status != CoverageStatus.Reviewed).OrderBy(entry => entry.Kind == CoverageKind.Build ? 0 : 1).ToList() : new List<Coverage>();
        var reasons = entries.Select((entry, index) => ReasonViewModel.Parse(entry.What, entry.Reason, index == 0, () => OpenDoctorRequested?.Invoke())).ToList();
        var hasBuild = entries.Any(entry => entry.Kind == CoverageKind.Build);
        var line = reasons.Count == 0 ? string.Empty : (reasons.Count == 1 ? "1 reason" : $"{reasons.Count} reasons") + (hasBuild ? ", the build first" : string.Empty);
        SetReasons(reasons, line, false);
    }

    /// <summary>
    /// A clean push has no panel: it names the same files, so the main column lists them, each with its tick.
    /// </summary>
    private void BuildCleanPush(ReviewResult result)
    {
        if(result.Outcome != ReviewOutcome.Clean)
        {
            SetCleanPush(new List<string>(), string.Empty, string.Empty);
            return;
        }

        var files = result.Coverage.Where(entry => entry.Kind == CoverageKind.File).Select(entry => entry.What).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
        var line = files.Count == 1 ? "1 file reviewed, without findings" : $"{files.Count} files reviewed, none with findings";
        var more = files.Count > _cleanPushFilesListed ? $"and {files.Count - _cleanPushFilesListed} more, none with findings." : string.Empty;
        SetCleanPush(files.Take(_cleanPushFilesListed).ToList(), files.Count == 0 ? string.Empty : line, more);
        PanelFiles = new List<PanelFileViewModel>();
        CleanFilesText = string.Empty;
        PanelHead = string.Empty;
    }

    private void BuildRuleRows(ReviewResult result, Dictionary<string, RuleInfo> rules)
    {
        var counts = result.CountByRule();
        var most = counts.Count == 0 ? 1 : counts.Max(count => count.Count);
        var hasBars = counts.Select(count => count.Count).Distinct().Count() > 1;
        var rows = new List<RuleRowViewModel>();
        foreach(var count in counts)
        {
            var isBlock = result.Findings.Any(finding => finding.RuleId == count.RuleId && finding.Severity == Severity.Block);
            var title = rules.TryGetValue(count.RuleId, out var info) && info.Title.Length > 0 ? info.Title : count.RuleId;
            rows.Add(new RuleRowViewModel(count.RuleId, title, count.Count, isBlock, (double)count.Count / most, hasBars, ChooseRule));
        }

        RuleRows = rows;
    }

    private void BuildFolderClean(ReviewResult result)
    {
        var withFindings = result.Findings.Select(finding => finding.File).Where(file => file != null).ToHashSet(StringComparer.Ordinal);
        var failed = result.Coverage.Where(entry => entry.Status != CoverageStatus.Reviewed && entry.Kind != CoverageKind.Rule).Select(entry => entry.What).ToHashSet(StringComparer.Ordinal);
        var clean = result.Coverage.Where(entry => entry.Kind == CoverageKind.File && entry.Status == CoverageStatus.Reviewed).Select(entry => entry.What).Distinct(StringComparer.Ordinal).Where(file => !withFindings.Contains(file) && !failed.Contains(file)).Order(StringComparer.Ordinal).ToList();
        if(clean.Count == 0)
        {
            FolderCleanLead = string.Empty;
            FolderCleanNames = string.Empty;
        }
        else if(clean.Count <= _cleanFilesNamed)
        {
            FolderCleanLead = clean.Count == 1 ? "1 file has no findings:" : $"{clean.Count} files have no findings:";
            FolderCleanNames = string.Join(", ", clean);
        }
        else
        {
            FolderCleanLead = $"{clean.Count} files have no findings.";
            FolderCleanNames = string.Empty;
        }
    }

    /// <summary>
    /// Shows the folder result again for the rule chosen, or for all: the files and their tallies, the line above them, the
    /// heading and its counts, the head of the count by rule and the rows that step back.
    /// </summary>
    private void ApplyRule()
    {
        if(_result == null)
            return;

        foreach(var row in RuleRows)
        {
            row.IsChosen = row.RuleId == ChosenRule;
            row.IsSteppedBack = ChosenRule != null && !row.IsChosen;
        }

        var groups = BuildGroups(_result, _ruleInfos, ChosenRule, true);
        SetFiles(groups);
        var total = _result.Findings.Count;
        ShowAllText = $"Show all {total}";
        RulesHead = ChosenRule == null ? (RuleRows.Count == 1 ? "1 rule has findings" : $"{RuleRows.Count} rules have findings") : $"1 of {RuleRows.Count} rules shown";
        if(ChosenRule == null)
            FilesLine = groups.Count == 1 ? "1 file with findings" : $"{groups.Count} files with findings, most findings first";
        else
            FilesLine = $"{groups.Count} {(groups.Count == 1 ? "file" : "files")} with findings of {ChosenRule}, most findings first";

        Verdict = DescribeFolder(_result, groups.Count);
    }

    private void ShowFile(FileGroupViewModel group)
    {
        group.IsOpen = true;
        FileRevealRequested?.Invoke(group);
    }

    partial void OnFileGroupsChanged(IReadOnlyList<FileGroupViewModel> value)
    {
        SyncItems();
    }

    /// <summary>
    /// Makes the list the page shows match the files: the rows that stay are kept (and so are their controls), the others come and go.
    /// </summary>
    private void SyncItems()
    {
        var desired = new List<ReviewListItemViewModel>(FileGroups.Count + 2) { _head };
        foreach(var group in FileGroups)
        {
            desired.Add(group);
            if(group.IsOpen)
                desired.AddRange(group.Findings);
        }

        desired.Add(_tail);
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

    private int IndexOfFrom(ReviewListItemViewModel item, int start)
    {
        for(var index = start; index < Items.Count; index++)
        {
            if(ReferenceEquals(Items[index], item))
                return index;
        }

        return -1;
    }

    private void UpdateOpenAllText()
    {
        OpenAllText = FileGroups.Any(group => group.IsOpen) ? "Fold all" : "Open all";
    }

    private VerdictViewModel Describe(ReviewResult result, int filesWithFindings)
    {
        var block = result.Findings.Count(finding => finding.Severity == Severity.Block);
        var warn = result.Findings.Count - block;
        var counts = new List<CountPieceViewModel>();
        var facts = Facts(result, filesWithFindings, null, _clock.Now);
        switch(result.Outcome)
        {
            case ReviewOutcome.Blocked:
                counts.Add(new CountPieceViewModel(block == 1 ? "1 finding blocks it" : $"{block} findings block it", CountPieceKind.Block));
                AddWarnings(counts, warn, "only warns", "only warn");
                return new VerdictViewModel(VerdictKind.Refused, "Push refused", string.Empty, counts, facts);
            case ReviewOutcome.Warned:
                counts.Add(new CountPieceViewModel("Nothing blocks it", CountPieceKind.Allow));
                AddWarnings(counts, warn, "only warns", "only warn");
                return new VerdictViewModel(VerdictKind.AllowedWithWarnings, "Push allowed", string.Empty, counts, facts);
            case ReviewOutcome.Clean:
                counts.Add(new CountPieceViewModel("No findings", CountPieceKind.Allow));
                return new VerdictViewModel(VerdictKind.AllowedClean, "Push allowed", string.Empty, counts, facts);
            default:
                counts.Add(new CountPieceViewModel("The review could not finish, so the push was not allowed. Fix what is listed below, then review again.", CountPieceKind.Plain));
                return new VerdictViewModel(VerdictKind.Incomplete, "Review incomplete", string.Empty, counts, facts);
        }
    }

    /// <summary>
    /// The verdict of a folder review: it counts what was already in the code and refuses nothing. With a rule chosen it counts that
    /// rule's findings alone and carries the filter piece.
    /// </summary>
    private VerdictViewModel DescribeFolder(ReviewResult result, int filesShown)
    {
        var findings = ChosenRule == null ? result.Findings : result.Findings.Where(finding => finding.RuleId == ChosenRule).ToList();
        var block = findings.Count(finding => finding.Severity == Severity.Block);
        var warn = findings.Count - block;
        var facts = Facts(result, filesShown, ChosenRule == null ? null : filesShown, _clock.Now, true);
        var tail = "in " + _resultLabel;
        var counts = new List<CountPieceViewModel>();
        if(result.Outcome == ReviewOutcome.Incomplete && findings.Count == 0)
        {
            counts.Add(new CountPieceViewModel("The review could not finish. Fix what is listed below, then review again.", CountPieceKind.Plain));
            return new VerdictViewModel(VerdictKind.Incomplete, "Review incomplete", string.Empty, counts, facts) { LeavesOutUncommitted = result.LeavesOutUncommittedWork };
        }

        if(findings.Count == 0)
            return new VerdictViewModel(VerdictKind.FolderClean, "No findings", string.Empty, counts, facts) { HeadingTail = tail, LeavesOutUncommitted = result.LeavesOutUncommittedWork };

        if(block > 0)
            counts.Add(new CountPieceViewModel(block == 1 ? "1 would block a push" : $"{block} would block a push", CountPieceKind.Block));

        AddWarnings(counts, warn, "would only warn", "would only warn");
        if(ChosenRule != null)
            counts.Add(new CountPieceViewModel(ChosenRule, CountPieceKind.Filter) { Clear = ClearRuleCommand, ClearName = $"Show all {result.Findings.Count} findings" });

        var heading = findings.Count == 1 ? "1 finding" : $"{findings.Count} findings";
        return new VerdictViewModel(VerdictKind.FolderFindings, heading, string.Empty, counts, facts) { HeadingTail = tail, LeavesOutUncommitted = result.LeavesOutUncommittedWork };
    }

    private static void AddWarnings(List<CountPieceViewModel> counts, int warn, string one, string many)
    {
        if(warn > 0)
            counts.Add(new CountPieceViewModel($"{warn} {(warn == 1 ? one : many)}", CountPieceKind.Warn));
    }

    /// <summary>
    /// The facts the body can give: how many files the review covered (and, for a folder, how many have findings), what it was of,
    /// and how long it took and when it ended. The number of commits, the number of lines and whether it was a full or a quick
    /// review are not in what the body returns.
    /// </summary>
    private static List<FactViewModel> Facts(ReviewResult result, int filesWithFindings, int? filesOfRule, DateTimeOffset now, bool isFolder = false)
    {
        var facts = new List<FactViewModel>();
        var files = result.Coverage.Where(entry => entry.Kind == CoverageKind.File).Select(entry => entry.What).Distinct(StringComparer.Ordinal).Count();
        if(isFolder)
            facts.Add(new FactViewModel("branch", "As of the last commit"));

        if(files > 0)
        {
            var noun = files == 1 ? "file" : "files";
            if(filesOfRule != null)
                facts.Add(new FactViewModel("file", $"{filesOfRule} of {files} files"));
            else
                facts.Add(new FactViewModel("file", isFolder ? $"{files} {noun}, {filesWithFindings} with findings" : $"{files} {noun}"));
        }

        facts.Add(new FactViewModel("time", DurationText(result.Duration) + ", " + now.ToLocalTime().ToString("HH:mm", CultureInfo.InvariantCulture)));
        return facts;
    }

    private static string DurationText(TimeSpan duration)
    {
        var seconds = duration.TotalSeconds;
        return seconds < _secondsWithDecimal ? seconds.ToString("0.0", CultureInfo.InvariantCulture) + " s" : Math.Round(seconds).ToString("0", CultureInfo.InvariantCulture) + " s";
    }

    private sealed record Ran(PushScopeAnswer? Scope, ReviewResult? Result, string? Problem, Dictionary<string, RuleInfo> Rules);
}
