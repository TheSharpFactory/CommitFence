using System.Collections.ObjectModel;
using Avalonia.Threading;
using CF.Core.Code;
using CF.Core.Findings;
using CF.Core.Rules;
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
    private bool _loading;
    private bool _justSaved;
    private bool _syncingEffect;
    private string _baselineSignature = string.Empty;
    private RuleDefinition? _baseline;
    private string? _copyOf;
    private IReadOnlyList<string> _copyPaths = new List<string>();
    private string? _copyMessage;
    private string? _copyWhy;

    [ObservableProperty]
    public partial string Title { get; set; } = string.Empty;

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
    public partial string? ErrorText { get; set; }

    [ObservableProperty]
    public partial bool IsMoreOpen { get; set; }

    public RulePageViewModel(RuleCatalog catalog, IRuleChanges changes, string id, RuleDefinition? rule)
    {
        _catalog = catalog;
        _changes = changes;
        Id = id;
        IsDraft = rule == null;
        _addItem = new AddConditionItem(this);
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

    public string SaveStateSpokenName
    {
        get
        {
            return IsDirty ? "Unsaved changes" : "Saved";
        }
    }

    public bool ShowsSaved
    {
        get
        {
            return !IsDirty;
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
    /// The first condition whose property the artifact type does not have, or nothing.
    /// </summary>
    public ConditionViewModel? InvalidCondition
    {
        get
        {
            return WhereFlow.OfType<ConditionViewModel>().Concat(RequireFlow).FirstOrDefault(condition => condition.IsInvalid);
        }
    }

    public bool HasInvalidCondition
    {
        get
        {
            return InvalidCondition != null;
        }
    }

    public string InvalidText
    {
        get
        {
            return InvalidCondition?.InvalidText ?? string.Empty;
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
                && (IsForbid || (RequireFlow.Count > 0 && RequireFlow.All(condition => condition.IsComplete)));
        }
    }

    public bool CanSave
    {
        get
        {
            return IsComplete && (IsDirty || IsDraft);
        }
    }

    public string SaveTip
    {
        get
        {
            return Title.Trim().Length == 0 ? "Name the rule to save it" : "Finish the sentence to save it";
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
        SetEffectFrom(rule.Enabled, rule.Severity);
        var signature = SignatureOf(rule);
        if(_justSaved || (!IsDirty && signature != _baselineSignature) || WhereFlow.Count == 0)
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
        var dirty = IsDraft || Signature() != _baselineSignature;
        var changed = dirty != IsDirty;
        IsDirty = dirty;
        RefreshState();
        if(changed)
            DirtyChanged?.Invoke();
    }

    public void AddCondition()
    {
        var condition = new ConditionViewModel(this, ClauseKind.Where, null, false, new List<string>());
        WhereFlow.Insert(WhereFlow.Count - 1, condition);
        RefreshPositions();
        EditMade();
        Dispatcher.UIThread.Post(() => condition.OpenPropertyPicker(false), DispatcherPriority.Loaded);
    }

    public void RemoveCondition(ConditionViewModel condition)
    {
        WhereFlow.Remove(condition);
        RequireFlow.Remove(condition);
        RefreshPositions();
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

        LoadEdits(_baseline);
    }

    [RelayCommand(CanExecute = nameof(CanSave))]
    private void Save()
    {
        _justSaved = true;
        var message = _baseline == null ? _changes.AddRule(ToDraft(), _copyOf) : _changes.ChangeRule(Id, ToChange());
        if(message != null)
        {
            _justSaved = false;
            ErrorText = message;
            OnPropertyChanged(nameof(HasError));
        }
    }

    private static string SignatureOf(RuleDefinition rule)
    {
        var where = string.Join(";", rule.Where.Select(ConditionSignature));
        var require = rule.Forbid ? string.Empty : string.Join(";", rule.Require.Select(ConditionSignature));

        return $"{rule.Title}|{rule.ArtifactType}|{rule.Forbid}|{where}|{require}";
    }

    private static string ConditionSignature(RuleCondition condition)
    {
        return $"{condition.PropertyName}|{condition.Comparison == Comparison.IsOneOf}|{string.Join(",", condition.ExpectedValues.Select(value => value.ToString()))}";
    }

    private static List<string> ValuesOf(RuleCondition condition)
    {
        return condition.ExpectedValues.Select(value => value.ToString()).ToList();
    }

    private string Signature()
    {
        var where = string.Join(";", WhereFlow.OfType<ConditionViewModel>().Select(condition => condition.Signature()));
        var require = IsForbid ? string.Empty : string.Join(";", RequireFlow.Select(condition => condition.Signature()));

        return $"{Title}|{ArtifactName}|{IsForbid}|{where}|{require}";
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
        WhereFlow.Clear();
        foreach(var condition in source.Where)
            WhereFlow.Add(new ConditionViewModel(this, ClauseKind.Where, condition.PropertyName, condition.Comparison == Comparison.IsOneOf, ValuesOf(condition)));

        WhereFlow.Add(_addItem);
        RequireFlow.Clear();
        if(!source.Forbid)
            FillRequire(source);

        _loading = false;
        _copyOf = source.Id;
        _copyPaths = source.Paths;
        _copyMessage = source.Message;
        _copyWhy = source.Why;
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
        RequireFlow.Add(new ConditionViewModel(this, ClauseKind.Require, null, false, new List<string>()));
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
        WhereFlow.Clear();
        foreach(var condition in rule.Where)
            WhereFlow.Add(new ConditionViewModel(this, ClauseKind.Where, condition.PropertyName, condition.Comparison == Comparison.IsOneOf, ValuesOf(condition)));

        WhereFlow.Add(_addItem);
        RequireFlow.Clear();
        if(!rule.Forbid)
            FillRequire(rule);

        _loading = false;
        _baselineSignature = Signature();
        RefreshPositions();
        var changed = IsDirty;
        IsDirty = false;
        ErrorText = null;
        RefreshState();
        if(changed)
            DirtyChanged?.Invoke();
    }

    private void FillRequire(RuleDefinition rule)
    {
        foreach(var condition in rule.Require)
            RequireFlow.Add(new ConditionViewModel(this, ClauseKind.Require, condition.PropertyName, condition.Comparison == Comparison.IsOneOf, ValuesOf(condition)));

        if(RequireFlow.Count == 0)
            RequireFlow.Add(new ConditionViewModel(this, ClauseKind.Require, null, false, new List<string>()));
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
            RequireFlow[index].CanRemove = false;
            RequireFlow[index].ShowsAnd = index < RequireFlow.Count - 1;
        }

        _addItem.Text = where.Count == 0 ? "add a condition" : "and";
        OnPropertyChanged(nameof(NoCondition));
    }

    private void RefreshState()
    {
        foreach(var condition in WhereFlow.OfType<ConditionViewModel>().Concat(RequireFlow))
            condition.RefreshWords();

        OnPropertyChanged(nameof(IsComplete));
        OnPropertyChanged(nameof(CanSave));
        OnPropertyChanged(nameof(SaveTip));
        OnPropertyChanged(nameof(ShowsSaved));
        OnPropertyChanged(nameof(SaveStateSpokenName));
        OnPropertyChanged(nameof(HasError));
        OnPropertyChanged(nameof(InvalidCondition));
        OnPropertyChanged(nameof(HasInvalidCondition));
        OnPropertyChanged(nameof(InvalidText));
        SaveCommand.NotifyCanExecuteChanged();
        TryIt.SentenceChanged(ArtifactName == null ? "the artifact" : ArtifactWords, PropertiesOffered().Select(property => property.Name).ToList(), IsComplete ? ToPlan() : null);
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
        var where = WhereFlow.OfType<ConditionViewModel>().Select(condition => condition.ToDraft()).ToList();
        IReadOnlyList<ConditionDraft>? require = IsForbid ? null : RequireFlow.Select(condition => condition.ToDraft()).ToList();

        return new RuleChange(Title, ArtifactName, where, require, IsForbid ? true : null, null, null, null);
    }

    private RuleDraft ToDraft()
    {
        var where = WhereFlow.OfType<ConditionViewModel>().Select(condition => condition.ToDraft()).ToList();
        var require = IsForbid ? new List<ConditionDraft>() : RequireFlow.Select(condition => condition.ToDraft()).ToList();

        return new RuleDraft(Id, LanguageId, ArtifactName ?? string.Empty, Severity, Title, where, require, IsForbid, _copyPaths, _copyMessage, _copyWhy) { Enabled = IsOn };
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
        RequireFlow.Clear();
        if(!forbid)
            RequireFlow.Add(new ConditionViewModel(this, ClauseKind.Require, null, false, new List<string>()));

        RefreshPositions();
        EditMade();
        if(!forbid)
            Dispatcher.UIThread.Post(() => RequireFlow[0].OpenPropertyPicker(false), DispatcherPriority.Loaded);
    }
}
