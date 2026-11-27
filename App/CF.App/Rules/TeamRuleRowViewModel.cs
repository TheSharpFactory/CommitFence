using CF.App.Parts;
using CF.Core.Findings;
using CF.Core.Rules;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CF.App.Rules;

/// <summary>
/// The row of a rule the team wrote: its id, its title, what it is for, the control for what it does to a push, and its
/// switch. A change is passed to the list, which makes it through the rule service at once.
/// </summary>
internal sealed partial class TeamRuleRowViewModel : RuleListItemViewModel
{
    private readonly IRuleChanges _changes;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SpokenName))]
    public partial bool HasIdClash { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RuleIsOff), nameof(SpokenName), nameof(EffectSpokenName), nameof(MarkIsBlock), nameof(MarkIsWarn), nameof(OffEffectWord))]
    public partial bool IsOn { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EffectKind), nameof(EffectWord), nameof(IsBlocks), nameof(SpokenName), nameof(EffectSpokenName), nameof(MarkIsBlock), nameof(MarkIsWarn))]
    public partial Severity Severity { get; set; }

    [ObservableProperty]
    public partial string Title { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RingOnRow), nameof(RingOnEffect), nameof(RingOnSwitch))]
    public partial RowPart Part { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RingOnRow), nameof(RingOnEffect), nameof(RingOnSwitch))]
    public partial bool IsRingVisible { get; set; }

    [ObservableProperty]
    public partial bool IsEffectMenuOpen { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsUnsaved), nameof(SpokenName))]
    public partial bool HasUnsavedEdits { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsUnsaved))]
    public partial bool PageIsBeside { get; set; }

    private string LanguageName { get; }

    public TeamRuleRowViewModel(RuleDefinition rule, string languageName, IRuleChanges changes)
    {
        Id = rule.Id;
        (IdFamily, IdNumber) = SplitId(rule.Id);
        LanguageName = languageName;
        _changes = changes;
        Entries = new List<EffectMenuEntry> { new EffectMenuEntry(Severity.Block), new EffectMenuEntry(Severity.Warn) };
        Update(rule);
    }

    public string Id { get; }

    public string IdFamily { get; }

    public string IdNumber { get; }

    /// <summary>
    /// The two entries of the effect menu, in the order they are drawn.
    /// </summary>
    public IReadOnlyList<EffectMenuEntry> Entries { get; }

    public string ArtifactText { get; private set; } = string.Empty;

    public string LanguageText
    {
        get
        {
            return LanguageName;
        }
    }

    /// <summary>
    /// The row says "Unsaved" when its rule has edits that are not saved, unless the page is open beside the list: then the
    /// first row of the page says it, and this one does not.
    /// </summary>
    public bool ShowsUnsaved
    {
        get
        {
            return HasUnsavedEdits && !PageIsBeside;
        }
    }

    public bool RuleIsOff
    {
        get
        {
            return !IsOn;
        }
    }

    /// <summary>
    /// The mark is the red stop sign: the rule is on and blocks.
    /// </summary>
    public bool MarkIsBlock
    {
        get
        {
            return IsOn && Severity == Severity.Block;
        }
    }

    /// <summary>
    /// The mark is the amber triangle: the rule is on and only warns.
    /// </summary>
    public bool MarkIsWarn
    {
        get
        {
            return IsOn && Severity == Severity.Warn;
        }
    }

    /// <summary>
    /// What a rule that is off would do when it is switched on: the words of the control under "Off".
    /// </summary>
    public string OffEffectWord
    {
        get
        {
            return Severity == Severity.Block ? "Blocks when on" : "Warns when on";
        }
    }

    public bool IsBlocks
    {
        get
        {
            return Severity == Severity.Block;
        }
    }

    public EffectKind EffectKind
    {
        get
        {
            return Severity == Severity.Block ? EffectKind.Block : EffectKind.Warn;
        }
    }

    public string EffectWord
    {
        get
        {
            return Severity == Severity.Block ? "Blocks" : "Warns";
        }
    }

    public bool RingOnRow
    {
        get
        {
            return IsRingVisible && Part == RowPart.Row;
        }
    }

    public bool RingOnEffect
    {
        get
        {
            return IsRingVisible && Part == RowPart.Effect;
        }
    }

    public bool RingOnSwitch
    {
        get
        {
            return IsRingVisible && Part == RowPart.Switch;
        }
    }

    /// <summary>
    /// What a screen reader says for the whole row.
    /// </summary>
    public string SpokenName
    {
        get
        {
            return $"{Id}, {Title}. Custom, for {ArtifactText} in {LanguageName}. {(IsOn ? "On" : "Off")}. {PushWords}.{(HasUnsavedEdits ? " Unsaved changes." : string.Empty)}{(HasIdClash ? ", same ID as the new rule" : string.Empty)}";
        }
    }

    /// <summary>
    /// What a screen reader says for the control of the effect.
    /// </summary>
    public string EffectSpokenName
    {
        get
        {
            return IsOn ? $"When code breaks it: {(Severity == Severity.Block ? "blocks the push" : "only warns")}" : $"When code breaks it: {(Severity == Severity.Block ? "blocks a push" : "only warns")} when on";
        }
    }

    /// <summary>
    /// What a screen reader says for the switch: the rule; its state is read as on or off.
    /// </summary>
    public string SwitchSpokenName
    {
        get
        {
            return $"{Id}, {Title}";
        }
    }

    private string PushWords
    {
        get
        {
            if(IsOn)
                return Severity == Severity.Block ? "Blocks a push" : "Only warns";

            return Severity == Severity.Block ? "Blocks a push when on" : "Only warns when on";
        }
    }

    /// <summary>
    /// Takes in what the settings file says now, keeping this row as the object the list holds.
    /// </summary>
    public void Update(RuleDefinition rule)
    {
        ArtifactText = rule.ArtifactType.Replace('-', ' ');
        Title = rule.Title;
        IsOn = rule.Enabled;
        Severity = rule.Severity;
        foreach(var entry in Entries)
            entry.IsCurrent = entry.Severity == rule.Severity;

        OnPropertyChanged(nameof(ArtifactText));
        OnPropertyChanged(nameof(SpokenName));
        OnPropertyChanged(nameof(SwitchSpokenName));
    }

    /// <summary>
    /// Tells every binding to read the row again: after a change was refused, a switch that the person flipped must go
    /// back to what the file says.
    /// </summary>
    public void RefreshBindings()
    {
        OnPropertyChanged(nameof(IsOn));
        OnPropertyChanged(nameof(Severity));
    }

    public void MoveRight()
    {
        Part = Part == RowPart.Row ? RowPart.Effect : RowPart.Switch;
    }

    public void MoveLeft()
    {
        Part = Part == RowPart.Switch ? RowPart.Effect : RowPart.Row;
    }

    /// <summary>
    /// Opens the menu of the effect for the keyboard: the entry in force has the keyboard.
    /// </summary>
    public void OpenEffectMenu()
    {
        foreach(var entry in Entries)
            entry.IsHighlighted = entry.IsCurrent;

        IsEffectMenuOpen = true;
    }

    public void CloseEffectMenu()
    {
        IsEffectMenuOpen = false;
    }

    public void MoveHighlight(int step)
    {
        var current = Entries.ToList().FindIndex(entry => entry.IsHighlighted);
        var next = Math.Clamp(current + step, 0, Entries.Count - 1);
        for(var index = 0; index < Entries.Count; index++)
            Entries[index].IsHighlighted = index == next;
    }

    public void ChooseHighlighted()
    {
        var entry = Entries.FirstOrDefault(candidate => candidate.IsHighlighted);
        if(entry != null)
            Choose(entry);
    }

    [RelayCommand]
    private void ToggleSwitch()
    {
        _changes.SetEnabled(Id, !IsOn);
    }

    [RelayCommand]
    private void ToggleEffectMenu()
    {
        if(IsEffectMenuOpen)
        {
            CloseEffectMenu();
            return;
        }

        foreach(var entry in Entries)
            entry.IsHighlighted = false;

        IsEffectMenuOpen = true;
    }

    [RelayCommand]
    private void Choose(EffectMenuEntry entry)
    {
        CloseEffectMenu();
        _changes.SetSeverity(Id, entry.Severity);
    }

    private static (string Family, string Number) SplitId(string id)
    {
        var letters = id.TakeWhile(char.IsLetter).Count();
        return (id.Substring(0, letters), id.Substring(letters));
    }
}
