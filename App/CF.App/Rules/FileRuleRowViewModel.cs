using CF.App.Parts;
using CF.Core.Code;
using CF.Core.Findings;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CF.App.Rules;

/// <summary>
/// The row of a rule that .editorconfig sets: its id, its title, what the file's setting does to a push as a fact, and the
/// line of the file that sets it. It holds no control that changes the rule: the product does not change that file.
/// </summary>
internal sealed partial class FileRuleRowViewModel : RuleListItemViewModel
{
    private const string _fileName = ".editorconfig";

    private readonly Action<int> _openFile;
    private FileRulePlace? _displayed;

    public IReadOnlyList<FileEffectScope> OtherEffects { get; private set; } = [];
    public bool IsScopedOnly { get { return OtherEffects.Count > 0; } }
    public bool HasDetails { get { return IsScopedOnly || HasPlaces; } }
    public bool Varies { get; private set; }
    public bool HasPlaces { get { return _places.Count > 1; } }
    public string? ElsewhereText { get { return HasPlaces && !Varies && Rule.Main == null ? "not set elsewhere" : null; } }
    public IReadOnlyList<FileRowPlace> VisiblePlaces { get; private set; } = [];
    private List<FileRowPlace> _places = [];
    private int _placeFocus = -1;

    public int HiddenPlaces { get { return _places.Count - VisiblePlaces.Count; } }
    public bool ShowsMorePlaces { get { return HiddenPlaces > 0; } }
    public string MorePlacesText { get { return $"{HiddenPlaces} more places"; } }
    public string MorePlacesName { get { return $"{MorePlacesText} of {Id}, open the rule"; } }
    public bool RingOnMorePlaces { get { return IsRingVisible && _placeFocus == VisiblePlaces.Count; } }

    public event Action? OpenPageRequested;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RingOnRow), nameof(RingOnLine), nameof(RingOnMorePlaces))]
    public partial RowPart Part { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RingOnRow), nameof(RingOnLine), nameof(RingOnMorePlaces))]
    public partial bool IsRingVisible { get; set; }

    public FileRuleRowViewModel(FileRule rule, Action<int> openFile)
    {
        _openFile = openFile;
        Rule = rule;
        Id = rule.Id;
        (IdFamily, IdNumber) = SplitId(rule.Id);
        Update(rule);
    }

    /// <summary>
    /// The rule as the body listed it.
    /// </summary>
    public FileRule Rule { get; private set; }

    public string Id { get; }

    public string IdFamily { get; }

    public string IdNumber { get; }

    public bool IsUnset
    {
        get
        {
            return !Varies && _displayed == null;
        }
    }

    public string UnsetTip
    {
        get
        {
            return $"Not set for all {Rule.Language} files in {_fileName}, so the rule's own default holds for them. Change it in the file.";
        }
    }

    public string Title
    {
        get
        {
            return Rule.Title ?? string.Empty;
        }
    }

    /// <summary>
    /// The rule is on in the file: the mark and the word of its effect are drawn.
    /// </summary>
    public bool ShowsEffect
    {
        get
        {
            return Varies || _displayed?.State == FileRuleState.On;
        }
    }

    /// <summary>
    /// The file switches the rule off: "Off in the file" stands across the effect's place and the line's.
    /// </summary>
    public bool IsOffInFile
    {
        get
        {
            return !Varies && _displayed?.State == FileRuleState.Off;
        }
    }

    /// <summary>
    /// The file's setting says nothing the product can state as an effect (its own default, or a word the build does not
    /// understand): the command line's word stands in the effect's place.
    /// </summary>
    public bool ShowsStateWord
    {
        get
        {
            return !Varies && !IsUnset && _displayed?.State is FileRuleState.OwnDefault or FileRuleState.NotUnderstood;
        }
    }

    public bool MarkIsBlock
    {
        get
        {
            return ShowsEffect && !Varies && EffectKind == EffectKind.Block;
        }
    }

    public bool MarkIsWarn
    {
        get
        {
            return ShowsEffect && !Varies && EffectKind == EffectKind.Warn;
        }
    }

    public bool MarkIsOff { get { return ShowsEffect && !Varies && EffectKind == EffectKind.Off; } }

    public EffectKind EffectKind
    {
        get
        {
            return _displayed?.Severity == Severity.Block ? EffectKind.Block : EffectKind.Warn;
        }
    }

    public string EffectWord
    {
        get
        {
            return Varies ? "Varies" : _displayed?.Severity == Severity.Block ? "Blocks" : "Warns";
        }
    }

    /// <summary>
    /// The command line's word for a state with no sentence of its own: "default", "not understood".
    /// </summary>
    public string StateWord
    {
        get
        {
            return _displayed?.Effect ?? string.Empty;
        }
    }

    /// <summary>
    /// The title is weight 400 in ink-2 for a rule the file switches off.
    /// </summary>
    public bool RuleIsOff
    {
        get
        {
            return IsOffInFile;
        }
    }

    /// <summary>
    /// The setting shown in the row has a link to its line in the file.
    /// </summary>
    public bool ShowsLine
    {
        get
        {
            return !HasPlaces && Line != null;
        }
    }

    private int? Line
    {
        get { return _displayed?.Line; }
    }

    public string LineText
    {
        get
        {
            return $"line {Line}";
        }
    }

    public string LineSection { get { return $"[{_displayed?.Pattern}]"; } }
    public string LineTipEnd { get { return $" sets it to {_displayed?.Setting}. Opens the file at this line."; } }

    public string LineSpokenName
    {
        get
        {
            return $"Set on line {Line} of {_fileName}. Open the file there.";
        }
    }

    /// <summary>
    /// What a row says when it is shown to the pointer: the whole title, and for a rule the file switches off, how.
    /// </summary>
    public string OffToolTip
    {
        get
        {
            if(_displayed == null)
                return string.Empty;

            if(_displayed.IsOffByStrictness)
                return $"Off under the current strictness. {_fileName} sets it to {_displayed.Setting} on line {Line}.";

            return $"Off for {Rule.Scope(_displayed)}: {_displayed.Setting}, on line {Line} of {_fileName}. Change it in the file.";
        }
    }

    public string StateToolTip
    {
        get
        {
            return _displayed?.State == FileRuleState.OwnDefault
                ? $"{_fileName} sets this rule to default, on line {Line}: the rule's own default applies, and what it does to a push is not known here."
                : $"{_fileName} sets this rule to '{_displayed?.Setting}', on line {Line}, and the build does not understand that word, so it has no effect the product can state.";
        }
    }

    public bool RingOnRow
    {
        get
        {
            return IsRingVisible && Part == RowPart.Row;
        }
    }

    public bool RingOnLine
    {
        get
        {
            return !HasPlaces && IsRingVisible && Part == RowPart.Line;
        }
    }

    /// <summary>
    /// What a screen reader says for the whole row.
    /// </summary>
    private string MainSpokenName
    {
        get
        {
            var title = Rule.Title == null ? string.Empty : $", {Rule.Title}";
            if(IsUnset)
                return $"{Id}{title}. From {_fileName}, on and off set in the file. Not set for all {Rule.Language} files.";

            if(_displayed?.IsOffByStrictness == true)
                return $"{Id}{title}. From {_fileName}. Off under the current strictness.";

            if(IsOffInFile)
                return $"{Id}{title}. From {_fileName}, on and off set in the file. Off in the file.";

            if(ShowsStateWord)
                return $"{Id}{title}. From {_fileName}, on and off set in the file. {StateWord}. Set on line {Line} of {_fileName}.";

            return $"{Id}{title}. From {_fileName}, on and off set in the file. On. {(_displayed?.Severity == Severity.Block ? "Blocks a push" : "Only warns")}. Set on line {Line} of {_fileName}.";
        }
    }

    public string SpokenName
    {
        get
        {
            return Rule.Note == null ? SpokenNameOfSettings : $"{SpokenNameOfSettings} {Rule.Note}";
        }
    }

    private string SpokenNameOfSettings
    {
        get
        {
            if(HasPlaces)
            {
                var label = Rule.Title == null ? Id : $"{Id}, {Rule.Title}";
                var matching = string.Join("; ", _places.Where(place => !place.IsDimmed).Select(place => place.SpokenName));
                var others = string.Join("; ", _places.Where(place => place.IsDimmed).Select(place => place.SpokenName));
                var effect = IsOffInFile ? "Off" : ShowsStateWord ? StateWord : EffectWord;
                return $"{label}. {effect}. {matching}." + (others.Length == 0 ? "" : $" Also: {others}.")
                    + (ElsewhereText is { } elsewhere ? $" {elsewhere}." : "");
            }

            if(IsScopedOnly)
                return $"{Id}, {Title}. {OtherEffects[0].Spoken}. Set at line {Line}.";

            if(Rule.Exceptions.Count == 0)
                return MainSpokenName;

            var summary = Rule.ExceptionLines().Select(line => line.Replace("Blocks for ", "it blocks a push for ", StringComparison.Ordinal)
                .Replace("Warns for ", "it only warns for ", StringComparison.Ordinal).Replace("Off for ", "it is off for ", StringComparison.Ordinal));
            var count = Rule.Exceptions.Count;
            return $"{MainSpokenName} Set {(IsUnset ? "only" : "differently")} for {count} {(count == 1 ? "pattern" : "patterns")}: {string.Join("; ", summary)}.";
        }
    }

    /// <summary>
    /// Takes in what the file says now, keeping this row as the object the list holds.
    /// </summary>
    public void Update(FileRule rule)
    {
        Rule = rule;
        _displayed = rule.ListedPlace;
        var places = rule.Places.ToList();
        Varies = places.Select(place => place.Effect).Distinct().Skip(1).Any();
        _places = places.Count > 1
            ? places.OrderBy(place => place == rule.Main).ThenBy(place => place.Line)
                .Select(place => new FileRowPlace(place, place == rule.Main, _openFile)).ToList()
            : [];
        VisiblePlaces = _places.Count <= 4 ? _places : _places.Take(2).Concat(_places.Where(place => place.IsEverywhereElse)).ToList();
        OtherEffects = rule.Main == null && places.Count == 1 ? [DescribeScope(places[0])] : [];
        ResetPart();
        OnPropertyChanged(string.Empty);
    }

    public void ApplyFilter(RuleFilter filter)
    {
        foreach(var place in _places)
            place.ApplyFilter(filter);

        OnPropertyChanged(nameof(SpokenName));
    }

    public static bool MatchesEffect(FileRulePlace place, RuleFilter filter)
    {
        return filter switch
        {
            RuleFilter.Blocks => place is { State: FileRuleState.On, Severity: Severity.Block },
            RuleFilter.Warns => place is { State: FileRuleState.On, Severity: Severity.Warn },
            RuleFilter.Off => place.State == FileRuleState.Off,
            RuleFilter.All => false,
            _ => throw new ArgumentOutOfRangeException(nameof(filter))
        };
    }

    private static FileEffectScope DescribeScope(FileRulePlace place)
    {
        return new FileEffectScope(Page.FileRulePageViewModel.PlaceKind(place), "Only in ", place.Pattern,
            $"{place.Effect} only in {place.Pattern}; not set elsewhere", ", not set elsewhere");
    }

    public void MoveRight()
    {
        if(HasPlaces)
            FocusPlace(Math.Min(_placeFocus + 1, VisiblePlaces.Count - (ShowsMorePlaces ? 0 : 1)));
        else if(ShowsLine)
            Part = RowPart.Line;
    }

    public void MoveLeft()
    {
        if(HasPlaces)
            FocusPlace(Math.Max(-1, _placeFocus - 1));
        else
            Part = RowPart.Row;
    }

    public void ResetPart()
    {
        FocusPlace(-1);
    }

    private void FocusPlace(int index)
    {
        _placeFocus = index;
        Part = index < 0 ? RowPart.Row : RowPart.Line;
        UpdatePlaceRings();
    }

    partial void OnIsRingVisibleChanged(bool value)
    {
        UpdatePlaceRings();
    }

    private void UpdatePlaceRings()
    {
        for(var index = 0; index < VisiblePlaces.Count; index++)
            VisiblePlaces[index].IsFocused = IsRingVisible && _placeFocus == index;

        OnPropertyChanged(nameof(RingOnMorePlaces));
    }

    [RelayCommand]
    private void OpenFile()
    {
        if(HasPlaces && _placeFocus == VisiblePlaces.Count)
            OpenPage();
        else if(HasPlaces && _placeFocus >= 0)
            VisiblePlaces[_placeFocus].OpenCommand.Execute(null);
        else if(Line is int line)
            _openFile(line);
    }

    [RelayCommand]
    private void OpenPage()
    {
        OpenPageRequested?.Invoke();
    }

    private static (string Family, string Number) SplitId(string id)
    {
        var letters = id.TakeWhile(char.IsLetter).Count();
        return (id.Substring(0, letters), id.Substring(letters));
    }
}
internal sealed record FileEffectScope(EffectKind Kind, string Text, string? Pattern, string Spoken, string Suffix = "");
