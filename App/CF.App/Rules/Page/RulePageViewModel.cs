using System.Collections.ObjectModel;
using Avalonia.Threading;
using CF.Core.Code;
using CF.Core.Findings;
using CF.Core.Rules;
using CF.Core.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CF.App.Rules.Page;

/// <summary>
/// The page of one team rule, or of a new one that is not saved yet: its title and its sentence, which are edited and wait
/// for "Save rule"; its switch and its effect, which are saved at once. It is made once for a rule and kept while the app
/// runs, so choosing another rule shows another page and never builds one again, and a rule's unsaved edits are still there
/// when it is chosen again.
/// </summary>
internal sealed partial class RulePageViewModel : ObservableObject
{
    private const string _requireKey = "require";
    private const string _forbidKey = "forbid";

    private readonly RuleCatalog _catalog;
    private readonly IRuleChanges _changes;
    private readonly AddConditionItem _addItem;
    private readonly AddPathItem _addPathItem;
    private readonly EveryFileItem _everyFile = new EveryFileItem();
    private readonly AddRequirementItem _addRequirementItem;
    private List<ConditionViewModel> _stashedRequire = new List<ConditionViewModel>();
    private bool _loading;
    private bool _justSaved;
    private bool _syncingEffect;
    private RuleSignature? _baselineSignature;
    private RuleDefinition? _baseline;
    private string? _copyOf;

    [ObservableProperty]
    public partial string Title { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Message { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Why { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsOn { get; set; }

    [ObservableProperty]
    public partial Severity Severity { get; set; }

    [ObservableProperty]
    public partial EffectMenuEntry? SelectedEffect { get; set; }

    [ObservableProperty]
    public partial string? ArtifactName { get; set; }

    [ObservableProperty]
    public partial string LanguageId { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsForbid { get; set; }

    [ObservableProperty]
    public partial bool IsDirty { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSave))]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    public partial bool IsSetupWriting { get; set; }

    [ObservableProperty]
    public partial string? ErrorText { get; set; }

    [ObservableProperty]
    public partial bool IsMoreOpen { get; set; }

    /// <summary>
    /// Where the rule was sent from to be narrowed, while it is: the strip above its sentence.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasOrigin), nameof(OriginText))]
    public partial NarrowOrigin? Origin { get; set; }

    public bool HasOrigin
    {
        get { return Origin != null; }
    }

    public string OriginText
    {
        get { return Origin?.Text ?? string.Empty; }
    }

    public RulePageViewModel(RuleCatalog catalog, IRuleChanges changes, string id, RuleDefinition? rule)
    {
        _catalog = catalog;
        _changes = changes;
        Id = id;
        IsDraft = rule == null;
        _addItem = new AddConditionItem(this);
        _addPathItem = new AddPathItem(this);
        _addRequirementItem = new AddRequirementItem(this);
        Effects = new List<EffectMenuEntry> { new EffectMenuEntry(Severity.Block), new EffectMenuEntry(Severity.Warn) };
        ArtifactPicker = new PickerViewModel("The rule is for", 340, "Find an artifact", "No artifact matches.");
        ArtifactPicker.Chosen += OnArtifactChosen;
        LanguagePicker = new PickerViewModel("Language", 200, "Find a language", "No language matches.");
        LanguagePicker.Chosen += OnLanguageChosen;
        KindPicker = new PickerViewModel("Require or forbid", 340, "Find", "Nothing matches.");
        KindPicker.Chosen += OnKindChosen;
        if(rule == null)
            StartDraft();
        else
            Update(rule);
    }

    /// <summary>
    /// Raised when the page gains or loses unsaved edits, so that the row in the list can say so.
    /// </summary>
    public event Action? DirtyChanged;

    /// <summary>
    /// Raised when the person asks to rename the rule (F2 or the Rename button): the view puts the keyboard in the title.
    /// </summary>
    public event Action? RenameRequested;

    public string Id { get; }

    /// <summary>
    /// Try it: the code of this page and what the rule, as the page holds it now, says about the code.
    /// </summary>
    public TryItViewModel TryIt { get; } = new TryItViewModel();

    public bool IsDraft { get; }

    public bool IsSaved
    {
        get
        {
            return !IsDraft;
        }
    }

    /// <summary>
    /// The two entries of the choice "When code breaks it".
    /// </summary>
    public IReadOnlyList<EffectMenuEntry> Effects { get; }

    /// <summary>
    /// The where clause: its conditions, then the add button, as one flow.
    /// </summary>
    public ObservableCollection<object> WhereFlow { get; } = new ObservableCollection<object>();

    /// <summary>
    /// The conditions of the require clause. A rule that forbids has none.
    /// </summary>
    public ObservableCollection<ConditionViewModel> RequireFlow { get; } = new ObservableCollection<ConditionViewModel>();

    /// <summary>
    /// The require clause as one flow: its conditions, then the add button.
    /// </summary>
    public ObservableCollection<object> RequireItems { get; } = new ObservableCollection<object>();

    /// <summary>
    /// The within clause as one flow: the paths, then the add button; or the words "every file" and the add button.
    /// </summary>
    public ObservableCollection<object> WithinFlow { get; } = new ObservableCollection<object>();

    /// <summary>
    /// The name of the repository, as the words of the path picker say it.
    /// </summary>
    public string RepositoryName
    {
        get { return _changes.RepositoryName; }
    }

    /// <summary>
    /// What the message field shows while it is empty: that a finding then says the title, and the title.
    /// </summary>
    public string MessagePlaceholder
    {
        get { return Title.Trim().Length == 0 ? "Name the rule" : $"Uses the title: {Title.Trim()}"; }
    }

    public IReadOnlyList<PathPieceViewModel> Paths
    {
        get { return WithinFlow.OfType<PathPieceViewModel>().ToList(); }
    }

    /// <summary>
    /// The patterns the rule applies to, as the page holds them: a path that has no pattern yet is not one.
    /// </summary>
    public IReadOnlyList<string> PathPatterns
    {
        get { return Paths.Where(path => !path.IsNew).Select(path => path.Pattern).ToList(); }
    }

    public PathMatches? FindPathFiles(string pattern)
    {
        return _changes.FindPathFiles(LanguageId, pattern);
    }

    public PickerViewModel ArtifactPicker { get; }

    public PickerViewModel LanguagePicker { get; }

    public PickerViewModel KindPicker { get; }

    public bool IsRequire
    {
        get
        {
            return !IsForbid;
        }
    }

    public string KindWord
    {
        get
        {
            return IsForbid ? _forbidKey : _requireKey;
        }
    }

    public string KindSpokenName
    {
        get
        {
            return IsForbid ? "The rule forbids. Choose require or forbid" : "The rule requires. Choose require or forbid";
        }
    }

    public string ArtifactWords
    {
        get
        {
            return SentenceWords.Artifact(ArtifactName);
        }
    }

    public bool ArtifactIsEmpty
    {
        get
        {
            return ArtifactName == null;
        }
    }

    public string ArtifactTokenText
    {
        get
        {
            return ArtifactName == null ? "choose an artifact" : ArtifactWords;
        }
    }

    public string ArtifactSpokenName
    {
        get
        {
            return $"The rule is for: {(ArtifactName == null ? "not chosen" : ArtifactWords)}";
        }
    }

    public string LanguageText
    {
        get
        {
            return _catalog.LanguageName(LanguageId);
        }
    }

    public string LanguageSpokenName
    {
        get
        {
            return $"Language: {LanguageText}";
        }
    }

    public string SwitchWord
    {
        get
        {
            return IsOn ? "On" : "Off";
        }
    }

    public string SwitchSpokenName
    {
        get
        {
            return $"{Id}, {Title}";
        }
    }

    public string MoreSpokenName
    {
        get
        {
            return $"More for {Id}";
        }
    }

    public bool HasError
    {
        get
        {
            return ErrorText != null;
        }
    }

    public bool NoCondition
    {
        get
        {
            return !WhereFlow.OfType<ConditionViewModel>().Any();
        }
    }

    /// <summary>
    /// The first thing in the sentence that cannot be saved, in the words the page shows under the clauses: a path the picker would
    /// refuse, a property the artifact type does not have, or conditions on one property that no code can meet together. Nothing
    /// when the sentence is sound.
    /// </summary>
    public string? InvalidReason
    {
        get
        {
            var path = Paths.FirstOrDefault(candidate => candidate.IsInvalid);
            if(path != null)
                return path.InvalidText;

            var condition = WhereFlow.OfType<ConditionViewModel>().Concat(RequireFlow).FirstOrDefault(candidate => candidate.IsInvalid);
            if(condition != null)
                return condition.InvalidText;

            return ConflictText() ?? ShapeText();
        }
    }

    public bool HasInvalidCondition
    {
        get
        {
            return InvalidReason != null;
        }
    }

    public string InvalidText
    {
        get
        {
            return InvalidReason ?? string.Empty;
        }
    }

    /// <summary>
    /// The refusal as a screen reader says it: the clause it is about, then the reason, so that it is tied to its clause.
    /// </summary>
    public string InvalidSpokenName
    {
        get { return InvalidReason == null ? string.Empty : $"{InvalidClauseWord} clause: {InvalidReason}"; }
    }

    private string InvalidClauseWord
    {
        get
        {
            if(Paths.Any(candidate => candidate.IsInvalid))
                return "Within";

            var condition = WhereFlow.OfType<ConditionViewModel>().Concat(RequireFlow).FirstOrDefault(candidate => candidate.IsInvalid);
            if(condition != null)
                return condition.IsWhereClause ? "Where" : "Require";

            var inWhere = ConditionConflicts.Find(WhereFlow.OfType<ConditionViewModel>()).Count > 0
                || ClauseShape.Refusal(CompleteDrafts(WhereFlow.OfType<ConditionViewModel>()), "require") != null;
            return inWhere ? "Where" : "Require";
        }
    }

    /// <summary>
    /// Whether the sentence holds everything a rule needs: a title, an artifact type, and a property and a value in every
    /// condition, and, when the rule requires, at least one condition after "require".
    /// </summary>
    public bool IsComplete
    {
        get
        {
            return Title.Trim().Length > 0
                && _catalog.FindArtifact(LanguageId, ArtifactName) != null
                && WhereFlow.OfType<ConditionViewModel>().All(condition => condition.IsComplete)
                && !HasInvalidCondition
                && (IsForbid || (RequireFlow.Count > 0 && RequireFlow.All(condition => condition.IsComplete)));
        }
    }

    public bool CanSave
    {
        get
        {
            return SaveBlocker == null && IsUnsaved;
        }
    }

    /// <summary>
    /// What Save's tip says while it is off: what to do. The reason itself is the line under the title and Save's description.
    /// </summary>
    public string? SaveTip
    {
        get
        {
            if(Paths.Any(candidate => candidate.HasInvalidDraft))
                return "Fix the path to save it.";

            if(PendingRanges().Any(candidate => candidate.ComparisonPicker.PendingProblem != null))
                return "Fix the range to save it.";

            if(InvalidReason != null)
                return "Fix the sentence to save it.";

            if(Title.Trim().Length == 0)
                return "Name the rule to save it";

            return IsComplete ? null : "Finish the sentence to save it";
        }
    }

    public IEnumerable<PropertyDescriptor> PropertiesOffered()
    {
        return _catalog.FindArtifact(LanguageId, ArtifactName)?.Properties ?? new List<PropertyDescriptor>();
    }

    public PropertyDescriptor? FindProperty(string? propertyName)
    {
        return _catalog.FindProperty(LanguageId, ArtifactName, propertyName);
    }

    /// <summary>
    /// Takes in what the settings file says now. The switch and the effect always follow the file. The title and the sentence
    /// follow it too, unless the person has edits that are not saved: those stay, and are compared with the new file.
    /// </summary>
    public void Update(RuleDefinition rule)
    {
        _baseline = rule;
        TryIt.PathsChanged(rule.Paths);
        SetEffectFrom(rule.Enabled, rule.Severity);
        var signature = SignatureOf(rule);
        if(_justSaved || (!IsDirty && !signature.Equals(_baselineSignature)) || WhereFlow.Count == 0)
        {
            _justSaved = false;
            LoadEdits(rule);
            return;
        }

        _baselineSignature = signature;
        EditMade();
    }

    /// <summary>
    /// A change of the sentence or the title was made.
    /// </summary>
    public void EditMade()
    {
        if(_loading)
            return;

        ErrorText = null;
        var dirty = IsDraft || !Signature().Equals(_baselineSignature);
        var changed = dirty != IsDirty;
        IsDirty = dirty;
        RefreshState();
        if(changed)
            DirtyChanged?.Invoke();
    }

    /// <summary>
    /// The rule is sent here from another screen to be narrowed: the strip says where from, and a new empty condition is added to its
    /// where clause with its property picker open. Nothing is saved until "Save rule".
    /// </summary>
    public void BeginNarrowing(NarrowOrigin origin)
    {
        Origin = origin;
        AddCondition();
    }

    [RelayCommand]
    private void BackToOrigin()
    {
        foreach(var condition in WhereFlow.OfType<ConditionViewModel>())
        {
            condition.PropertyPicker.Close();
            condition.ComparisonPicker.Close();
        }

        Origin?.Back();
    }

    public void AddCondition()
    {
        var condition = new ConditionViewModel(this, ClauseKind.Where, null, Comparison.Is, new List<string>());
        WhereFlow.Insert(WhereFlow.Count - 1, condition);
        RefreshPositions();
        EditMade();
        Dispatcher.UIThread.Post(() => condition.OpenPropertyPicker(false), DispatcherPriority.Loaded);
    }

    /// <summary>
    /// The other conditions of the clause a condition stands in that are on this property.
    /// </summary>
    public IReadOnlyList<ConditionViewModel> OtherConditionsOn(ConditionViewModel condition, string propertyName)
    {
        var clause = RequireFlow.Contains(condition) ? RequireFlow.ToList() : WhereFlow.OfType<ConditionViewModel>().ToList();
        return clause.Where(other => !ReferenceEquals(other, condition) && other.PropertyName == propertyName).ToList();
    }

    public void RemoveCondition(ConditionViewModel condition)
    {
        if(RequireFlow.Contains(condition) && RequireFlow.Count <= 1)
            return;

        var wasRequirement = RequireFlow.Remove(condition);
        WhereFlow.Remove(condition);
        RefreshPositions();
        EditMade();
        if(wasRequirement && RequireFlow.Count == 1)
            RequireAddFocusRequested?.Invoke();
    }

    /// <summary>
    /// Raised just before a condition or a path is removed by its × (or the Delete key), and after: the view keeps the control the
    /// keyboard goes to, which is the next one of the sentence, or the previous when the one removed was the last.
    /// </summary>
    public event Action<object>? RemovalStarting;

    public event Action? RemovalDone;

    /// <summary>
    /// Removes a condition or a path the person took away with its ×.
    /// </summary>
    public void RemoveByButton(object item)
    {
        RemovalStarting?.Invoke(item);
        if(item is ConditionViewModel condition)
            RemoveCondition(condition);
        else if(item is PathPieceViewModel path)
            RemovePath(path);

        RemovalDone?.Invoke();
    }

    /// <summary>
    /// Raised when the keyboard has to move to the add button of the require clause: the one remove button there was went with the
    /// requirement that was removed.
    /// </summary>
    public event Action? RequireAddFocusRequested;

    /// <summary>
    /// Adds an empty requirement to the require clause, its property picker open.
    /// </summary>
    public void AddRequirement()
    {
        var condition = new ConditionViewModel(this, ClauseKind.Require, null, Comparison.Is, new List<string>()) { IsJustAdded = true };
        RequireFlow.Add(condition);
        RefreshPositions();
        EditMade();
        Dispatcher.UIThread.Post(() => condition.OpenPropertyPicker(false), DispatcherPriority.Loaded);
    }

    /// <summary>
    /// Adds an empty path to the within clause, its picker open with the field focused.
    /// </summary>
    public void AddPath()
    {
        var path = new PathPieceViewModel(this, string.Empty);
        WithinFlow.Insert(WithinFlow.Count - 1, path);
        RefreshPaths();
        Dispatcher.UIThread.Post(() => path.Open(), DispatcherPriority.Loaded);
    }

    public void RemovePath(PathPieceViewModel path)
    {
        WithinFlow.Remove(path);
        RefreshPaths();
        EditMade();
    }

    /// <summary>
    /// A path kept a new pattern.
    /// </summary>
    public void PathsChanged()
    {
        RefreshPaths();
        EditMade();
    }

    /// <summary>
    /// Opens the picker of the artifact type. A new rule opens with it open: its first token is empty.
    /// </summary>
    public void OpenArtifactPicker(bool byKeyboard)
    {
        var entries = new List<PickerEntryViewModel>();
        var language = _catalog.FindLanguage(LanguageId);
        foreach(var type in language?.ArtifactTypes ?? new List<ArtifactType>())
            entries.Add(new PickerEntryViewModel(type.Name, RuleCatalog.Words(type.Name), string.Empty, string.Empty, type.Name == ArtifactName, false));

        ArtifactPicker.Open(entries, byKeyboard);
    }

    public void OpenLanguagePicker(bool byKeyboard)
    {
        var entries = new List<PickerEntryViewModel>();
        foreach(var language in _catalog.Languages)
            entries.Add(new PickerEntryViewModel(language.Id, language.Name, string.Empty, string.Empty, language.Id == LanguageId, !IsDraft && language.Id != LanguageId));

        LanguagePicker.Open(entries, byKeyboard);
    }

    public void OpenKindPicker(bool byKeyboard)
    {
        var entries = new List<PickerEntryViewModel>
        {
            new PickerEntryViewModel(_requireKey, _requireKey, "what follows must hold", string.Empty, !IsForbid, false),
            new PickerEntryViewModel(_forbidKey, _forbidKey, "what follows must not hold", string.Empty, IsForbid, false)
        };

        KindPicker.Open(entries, byKeyboard);
    }

    /// <summary>
    /// The first picker a new rule shows open: its artifact type.
    /// </summary>
    public void OpenFirstPicker()
    {
        if(ArtifactName == null)
            OpenArtifactPicker(false);
    }

    partial void OnTitleChanged(string value)
    {
        OnPropertyChanged(nameof(SwitchSpokenName));
        OnPropertyChanged(nameof(MessagePlaceholder));
        EditMade();
    }

    partial void OnMessageChanged(string value)
    {
        EditMade();
    }

    partial void OnWhyChanged(string value)
    {
        EditMade();
    }

    partial void OnIsOnChanged(bool value)
    {
        OnPropertyChanged(nameof(SwitchWord));
    }

    partial void OnIsForbidChanged(bool value)
    {
        OnPropertyChanged(nameof(IsRequire));
        OnPropertyChanged(nameof(KindWord));
        OnPropertyChanged(nameof(KindSpokenName));
    }

    partial void OnArtifactNameChanged(string? value)
    {
        OnPropertyChanged(nameof(ArtifactWords));
        OnPropertyChanged(nameof(ArtifactIsEmpty));
        OnPropertyChanged(nameof(ArtifactTokenText));
        OnPropertyChanged(nameof(ArtifactSpokenName));
    }

    partial void OnLanguageIdChanged(string value)
    {
        OnPropertyChanged(nameof(LanguageText));
        OnPropertyChanged(nameof(LanguageSpokenName));
    }

    partial void OnSelectedEffectChanged(EffectMenuEntry? value)
    {
        if(_syncingEffect || value == null || value.Severity == Severity)
            return;

        if(IsDraft)
            SetEffectFrom(IsOn, value.Severity);
        else
            _changes.SetSeverity(Id, value.Severity);
    }

    /// <summary>
    /// Tells the bindings to read the switch and the effect again: a change was refused and they must show the file's.
    /// </summary>
    public void RefreshBindings()
    {
        OnPropertyChanged(nameof(IsOn));
        OnPropertyChanged(nameof(SelectedEffect));
    }

    [RelayCommand]
    private void ToggleSwitch()
    {
        _changes.SetEnabled(Id, !IsOn);
    }

    [RelayCommand]
    private void Rename()
    {
        RenameRequested?.Invoke();
    }

    [RelayCommand]
    private void OpenArtifact()
    {
        OpenArtifactPicker(false);
    }

    [RelayCommand]
    private void OpenLanguage()
    {
        OpenLanguagePicker(false);
    }

    [RelayCommand]
    private void OpenKind()
    {
        OpenKindPicker(false);
    }

    [RelayCommand]
    private void OpenMore()
    {
        IsMoreOpen = true;
    }

    [RelayCommand]
    private void DuplicateRule()
    {
        IsMoreOpen = false;
        _changes.DuplicateRule(Id);
    }

    [RelayCommand]
    private void RemoveRule()
    {
        IsMoreOpen = false;
        _changes.RemoveRule(Id);
    }

    [RelayCommand]
    private void Discard()
    {
        if(_baseline == null)
        {
            _changes.DiscardDraft();
            return;
        }

        CancelOpenPickers();
        if(IsDirty)
            LoadEdits(_baseline);
        else
            RefreshHead();
    }

    [RelayCommand(CanExecute = nameof(CanSave))]
    private void Save()
    {
        if(!CommitOpenDrafts() || !CanSave)
            return;

        _justSaved = true;
        var message = _baseline == null ? _changes.AddRule(ToDraft(), _copyOf) : _changes.ChangeRule(Id, ToChange());
        if(message != null)
        {
            _justSaved = false;
            ErrorText = message;
            OnPropertyChanged(nameof(HasError));
        }
    }

    private static RuleSignature SignatureOf(RuleDefinition rule)
    {
        var where = ConditionRanges.Merge(rule.Where).Select(ConditionSignatureOf).ToList();
        var require = rule.Forbid ? new List<ConditionSignature>() : ConditionRanges.Merge(rule.Require).Select(ConditionSignatureOf).ToList();

        return new RuleSignature(rule.Title, rule.ArtifactType, rule.Forbid, rule.Message ?? string.Empty, rule.Why ?? string.Empty, rule.Paths, where, require);
    }

    private static ConditionSignature ConditionSignatureOf(ConditionShape condition)
    {
        return new ConditionSignature(condition.PropertyName, condition.Comparison, condition.IsList, condition.Values);
    }

    private RuleSignature Signature()
    {
        var where = WhereFlow.OfType<ConditionViewModel>().Select(condition => condition.Signature()).ToList();
        var require = IsForbid ? new List<ConditionSignature>() : RequireFlow.Select(condition => condition.Signature()).ToList();

        return new RuleSignature(Title, ArtifactName, IsForbid, MessageToSave(), WhyToSave(), PathPatterns, where, require);
    }

    /// <summary>
    /// The message as it is written: spaces at either end are taken off, and a field of spaces is empty.
    /// </summary>
    private string MessageToSave()
    {
        return Message.Trim();
    }

    private string WhyToSave()
    {
        return Why.Trim();
    }

    /// <summary>
    /// Makes this new rule a copy of a rule: the same sentence and effect, the title "Copy of" and the rule's title. It stays
    /// unsaved, and saving it puts it right after the rule it copies.
    /// </summary>
    public void FillFrom(RuleDefinition source)
    {
        _loading = true;
        Title = $"Copy of {source.Title}";
        LanguageId = source.Language;
        ArtifactName = source.ArtifactType;
        IsForbid = source.Forbid;
        SetEffectFrom(source.Enabled, source.Severity);
        FillWords(source);
        _loading = false;
        _copyOf = source.Id;
        RefreshPositions();
        IsDirty = true;
        RefreshState();
    }

    private void StartDraft()
    {
        LanguageId = _catalog.DefaultLanguageId;
        IsOn = true;
        SetEffectFrom(true, Severity.Block);
        _loading = true;
        WhereFlow.Add(_addItem);
        WithinFlow.Add(_addPathItem);
        RequireFlow.Add(new ConditionViewModel(this, ClauseKind.Require, null, Comparison.Is, new List<string>()));
        _loading = false;
        RefreshPositions();
        IsDirty = true;
        RefreshState();
    }

    private void LoadEdits(RuleDefinition rule)
    {
        _loading = true;
        Title = rule.Title;
        LanguageId = rule.Language;
        ArtifactName = rule.ArtifactType;
        IsForbid = rule.Forbid;
        FillWords(rule);
        _loading = false;
        _baselineSignature = Signature();
        RefreshPositions();
        Origin = null;
        var changed = IsDirty;
        IsDirty = false;
        ErrorText = null;
        RefreshState();
        if(changed)
            DirtyChanged?.Invoke();
    }

    /// <summary>
    /// Fills the clauses, the paths, the message and the reason from a rule.
    /// </summary>
    private void FillWords(RuleDefinition rule)
    {
        WhereFlow.Clear();
        foreach(var condition in ConditionRanges.Merge(rule.Where))
            WhereFlow.Add(new ConditionViewModel(this, ClauseKind.Where, condition.PropertyName, condition.Comparison, condition.Values, condition.IsList, condition.IsRange));

        WhereFlow.Add(_addItem);
        RequireFlow.Clear();
        _stashedRequire = new List<ConditionViewModel>();
        if(!rule.Forbid)
            FillRequire(rule);

        WithinFlow.Clear();
        foreach(var pattern in rule.Paths)
            WithinFlow.Add(new PathPieceViewModel(this, pattern));

        WithinFlow.Add(_addPathItem);
        Message = rule.Message ?? string.Empty;
        Why = rule.Why ?? string.Empty;
    }

    private void FillRequire(RuleDefinition rule)
    {
        foreach(var condition in ConditionRanges.Merge(rule.Require))
            RequireFlow.Add(new ConditionViewModel(this, ClauseKind.Require, condition.PropertyName, condition.Comparison, condition.Values, condition.IsList, condition.IsRange));

        if(RequireFlow.Count == 0)
            RequireFlow.Add(new ConditionViewModel(this, ClauseKind.Require, null, Comparison.Is, new List<string>()));
    }

    private void SetEffectFrom(bool enabled, Severity severity)
    {
        _syncingEffect = true;
        IsOn = enabled;
        Severity = severity;
        foreach(var entry in Effects)
            entry.IsCurrent = entry.Severity == severity;

        SelectedEffect = Effects.First(entry => entry.Severity == severity);
        _syncingEffect = false;
    }

    private void RefreshPositions()
    {
        var where = WhereFlow.OfType<ConditionViewModel>().ToList();
        for(var index = 0; index < where.Count; index++)
        {
            where[index].CanRemove = true;
            where[index].ShowsAnd = index < where.Count - 1;
        }

        for(var index = 0; index < RequireFlow.Count; index++)
        {
            RequireFlow[index].CanRemove = RequireFlow.Count > 1;
            RequireFlow[index].ShowsAnd = index < RequireFlow.Count - 1;
        }

        _addItem.Text = where.Count == 0 ? "add a condition" : "and";
        SyncRequireItems();
        RefreshPaths();
        OnPropertyChanged(nameof(NoCondition));
    }

    /// <summary>
    /// Puts the require clause's flow in step with its conditions: they, then the add button. Nothing is rebuilt when it already is.
    /// </summary>
    private void SyncRequireItems()
    {
        var wanted = new List<object>(RequireFlow);
        wanted.Add(_addRequirementItem);
        if(wanted.SequenceEqual(RequireItems))
            return;

        RequireItems.Clear();
        foreach(var item in wanted)
            RequireItems.Add(item);
    }

    /// <summary>
    /// Puts the within clause in order: the words "every file" while there is no path, the paths each followed by "or" but the last,
    /// then the add button.
    /// </summary>
    private void RefreshPaths()
    {
        var paths = Paths;
        var wanted = new List<object>();
        if(paths.Count == 0)
            wanted.Add(_everyFile);

        wanted.AddRange(paths);
        wanted.Add(_addPathItem);
        if(!wanted.SequenceEqual(WithinFlow))
        {
            WithinFlow.Clear();
            foreach(var item in wanted)
                WithinFlow.Add(item);
        }

        for(var index = 0; index < paths.Count; index++)
            paths[index].ShowsOr = index < paths.Count - 1;

        _addPathItem.Text = paths.Count == 0 ? "add a path" : "or";
        _addPathItem.SpokenName = paths.Count == 0 ? "Files: every file. Add a path" : "Add a path";
        OnPropertyChanged(nameof(PathPatterns));
    }

    private void RefreshState()
    {
        foreach(var condition in WhereFlow.OfType<ConditionViewModel>().Concat(RequireFlow))
            condition.RefreshWords();

        foreach(var path in Paths)
            path.RefreshWords();

        MarkConflicts();
        OnPropertyChanged(nameof(IsComplete));
        OnPropertyChanged(nameof(CanSave));
        OnPropertyChanged(nameof(SaveTip));
        RefreshHead();
        OnPropertyChanged(nameof(HasError));
        OnPropertyChanged(nameof(InvalidReason));
        OnPropertyChanged(nameof(HasInvalidCondition));
        OnPropertyChanged(nameof(InvalidText));
        OnPropertyChanged(nameof(InvalidSpokenName));
        RefreshReasons();
        SaveCommand.NotifyCanExecuteChanged();
        TryIt.PathsChanged(PathPatterns);
        TryIt.RuleChanged(Id, RepositoryName);
        TryIt.IsSaved = !IsDraft && !IsDirty;
        TryIt.ReadsFileName = WhereFlow.OfType<ConditionViewModel>().Concat(RequireFlow).Any(condition => condition.Descriptor?.ReadsFileName == true);
        TryIt.SentenceChanged(ArtifactName == null ? "the artifact" : ArtifactWords, PropertiesOffered().Select(property => property.Name).ToList(), IsComplete ? ToPlan() : null);
    }

    /// <summary>
    /// Marks the conditions that no code can meet together, so that their tokens are drawn invalid.
    /// </summary>
    private void MarkConflicts()
    {
        var conflicting = ConditionConflicts.Find(WhereFlow.OfType<ConditionViewModel>()).Concat(ConditionConflicts.Find(IsForbid ? new List<ConditionViewModel>() : RequireFlow)).SelectMany(group => group).ToHashSet();
        foreach(var condition in WhereFlow.OfType<ConditionViewModel>().Concat(RequireFlow).Concat(_stashedRequire))
            condition.IsConflicted = conflicting.Contains(condition);
    }

    /// <summary>
    /// The reason under the clauses for conditions on one property that no code can meet: for a requirement, every artifact would
    /// break the rule; for a where condition, the rule would check none.
    /// </summary>
    private string? ConflictText()
    {
        var where = ConditionConflicts.Find(WhereFlow.OfType<ConditionViewModel>());
        if(where.Count > 0)
            return ConflictSentence(where[0], $"so no {ArtifactWords} would match and the rule would never apply.");

        var require = ConditionConflicts.Find(IsForbid ? new List<ConditionViewModel>() : RequireFlow);
        if(require.Count > 0)
            return ConflictSentence(require[0], $"so every {ArtifactWords} in these files would break the rule.");

        return null;
    }

    /// <summary>
    /// The reason under the clauses when a clause holds conditions on one property that the settings file cannot hold together.
    /// </summary>
    private string? ShapeText()
    {
        return ClauseShape.Refusal(CompleteDrafts(WhereFlow.OfType<ConditionViewModel>()), "require")
            ?? ClauseShape.Refusal(CompleteDrafts(IsForbid ? new List<ConditionViewModel>() : RequireFlow), "where");
    }

    private static List<ConditionDraft> CompleteDrafts(IEnumerable<ConditionViewModel> clause)
    {
        return clause.Where(condition => condition.IsComplete && !condition.IsInvalid).SelectMany(condition => condition.ToDrafts()).ToList();
    }

    /// <summary>
    /// What the page says of conditions on one property that cannot hold together: what no artifact can have, and what follows from it.
    /// A range whose lower end is above its upper end says only what is known, and how to mend it.
    /// </summary>
    private string ConflictSentence(IReadOnlyList<ConditionViewModel> group, string consequence)
    {
        if(group.Count == 1 && group[0].IsRange)
            return $"\"{group[0].PropertyWords} is between {group[0].RangeLow} and {group[0].RangeHigh}\" cannot be met: the lower end is above the upper end. Change either number to save the rule.";

        return $"No {ArtifactWords} can have {ConflictWords(group)}, {consequence}";
    }

    private static string ConflictWords(IReadOnlyList<ConditionViewModel> group)
    {
        return $"{group[0].PropertyWords} {string.Join(" and ", group.Select(condition => condition.ConflictPart))}";
    }

    /// <summary>
    /// The rule as the page holds it, with its edits and its switch and effect as saved, ready to be tried on code.
    /// </summary>
    private TryPlan ToPlan()
    {
        var key = Signature();
        if(_baseline == null)
        {
            var draft = ToDraft();
            return new TryPlan(key, code => _changes.TryNewRule(draft, code));
        }

        var change = ToChange();
        return new TryPlan(key, code => _changes.TryChangedRule(Id, change, code));
    }

    private RuleChange ToChange()
    {
        var where = WhereFlow.OfType<ConditionViewModel>().SelectMany(condition => condition.ToDrafts()).ToList();
        IReadOnlyList<ConditionDraft>? require = IsForbid ? null : RequireFlow.SelectMany(condition => condition.ToDrafts()).ToList();

        return new RuleChange(Title, ArtifactName, where, require, IsForbid ? true : null, PathPatterns, MessageToSave(), WhyToSave());
    }

    private RuleDraft ToDraft()
    {
        var where = WhereFlow.OfType<ConditionViewModel>().SelectMany(condition => condition.ToDrafts()).ToList();
        var require = IsForbid ? new List<ConditionDraft>() : RequireFlow.SelectMany(condition => condition.ToDrafts()).ToList();

        return new RuleDraft(Id, LanguageId, ArtifactName ?? string.Empty, Severity, Title, where, require, IsForbid, PathPatterns, MessageToSave(), WhyToSave()) { Enabled = IsOn };
    }

    private void OnArtifactChosen(PickerEntryViewModel entry)
    {
        ArtifactName = entry.Key;
        EditMade();
    }

    private void OnLanguageChosen(PickerEntryViewModel entry)
    {
        if(entry.Key == LanguageId)
            return;

        LanguageId = entry.Key;
        ArtifactName = null;
        EditMade();
    }

    private void OnKindChosen(PickerEntryViewModel entry)
    {
        var forbid = entry.Key == _forbidKey;
        if(forbid == IsForbid)
            return;

        IsForbid = forbid;
        if(forbid)
        {
            // what was required is kept, out of sight, until the rule is saved: turning back brings it back
            _stashedRequire = RequireFlow.ToList();
            RequireFlow.Clear();
        }
        else
        {
            RestoreRequire();
        }

        RefreshPositions();
        EditMade();
        if(!forbid && RequireFlow.Count == 1 && RequireFlow[0].IsEmpty)
            Dispatcher.UIThread.Post(() => RequireFlow[0].OpenPropertyPicker(false), DispatcherPriority.Loaded);
    }

    private void RestoreRequire()
    {
        RequireFlow.Clear();
        foreach(var condition in _stashedRequire)
            RequireFlow.Add(condition);

        _stashedRequire = new List<ConditionViewModel>();
        if(RequireFlow.Count == 0)
            RequireFlow.Add(new ConditionViewModel(this, ClauseKind.Require, null, Comparison.Is, new List<string>()));
    }
}
