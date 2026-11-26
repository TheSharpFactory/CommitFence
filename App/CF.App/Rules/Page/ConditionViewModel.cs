using System.Globalization;
using CF.Core.Code;
using CF.Core.Rules;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CF.App.Rules.Page;

/// <summary>
/// One condition of the sentence: a property, how it is compared and the value or values. It is edited here and written
/// with the rule when the person saves. What its pickers offer comes from the catalog through the page.
/// </summary>
internal sealed partial class ConditionViewModel : ObservableObject
{
    private const string _quickGroup = "Read from the file: runs in quick mode";
    private const string _buildGroup = "Needs the build: full review only";

    private readonly RulePageViewModel _page;

    [ObservableProperty]
    public partial string? PropertyName { get; set; }

    public bool IsOneOf
    {
        get { return Comparison == Comparison.IsOneOf; }
    }

    public Comparison Comparison { get; private set; }

    public bool IsList { get; private set; }

    /// <summary>
    /// A range: "is at least" one number "and at most" another, as one token. Its two values are the two bounds, the lower first.
    /// </summary>
    public bool IsRange { get; private set; }

    public bool IsNotRange
    {
        get { return !IsRange; }
    }

    public string RangeLow
    {
        get { return IsRange && Values.Count > 0 ? Values[0] : string.Empty; }
    }

    public string RangeHigh
    {
        get { return IsRange && Values.Count > 1 ? Values[1] : string.Empty; }
    }

    /// <summary>
    /// Whether another condition of the same clause already stands on this property: a clause holds at most two comparisons on one
    /// property, the two ends of a range, so such a condition cannot become a range.
    /// </summary>
    public bool HasOtherConditionsOnProperty
    {
        get { return PropertyName != null && _page.OtherConditionsOn(this, PropertyName).Count > 0; }
    }

    public bool CanEditComparison
    {
        get { return PropertyName != null; }
    }

    /// <summary>
    /// A requirement that was just added and has no property yet: dismissing its first picker takes it away again.
    /// </summary>
    public bool IsJustAdded { get; set; }

    [ObservableProperty]
    public partial bool IsConflicted { get; set; }

    /// <summary>
    /// Whether the token is drawn with the edge of an invalid one: its property is missing, or it cannot hold with another.
    /// </summary>
    public bool ShowsInvalid
    {
        get { return IsInvalid || IsConflicted; }
    }

    /// <summary>
    /// Whether the property token is the refused one: the artifact type has no such property.
    /// </summary>
    public bool PropertyIsRefused
    {
        get { return IsInvalid; }
    }

    /// <summary>
    /// Whether the comparison token is the refused one: it cannot hold with another condition, or no code can meet it. The property token
    /// beside it is not marked.
    /// </summary>
    public bool ComparisonIsRefused
    {
        get { return IsConflicted; }
    }

    private void NotifyRefused()
    {
        OnPropertyChanged(nameof(ShowsInvalid));
        OnPropertyChanged(nameof(PropertyIsRefused));
        OnPropertyChanged(nameof(ComparisonIsRefused));
    }

    /// <summary>
    /// A pattern is typed code, so the value is drawn in the code style.
    /// </summary>
    public bool ValueIsCode
    {
        get { return Comparison == Comparison.Matches || ValuesAreTypedNames; }
    }

    /// <summary>
    /// Names a person types (not words the property offers, and not numbers) are code, so they are drawn in the code face.
    /// </summary>
    private bool ValuesAreTypedNames
    {
        get { return Descriptor is { ValueType: PropertyValueType.Text, AllowedValues.Count: 0 } && Values.Count > 0; }
    }

    public bool ValueIsNotCode
    {
        get { return !ValueIsCode; }
    }

    public string CodeValueText
    {
        get { return ValueIsCode ? ValueText : string.Empty; }
    }

    public string PlainValueText
    {
        get { return ValueIsCode ? string.Empty : ValueText; }
    }

    [ObservableProperty]
    public partial IReadOnlyList<string> Values { get; set; } = new List<string>();

    [ObservableProperty]
    public partial bool CanRemove { get; set; }

    [ObservableProperty]
    public partial bool ShowsAnd { get; set; }

    [ObservableProperty]
    public partial bool IsWhereClause { get; set; }

    public ConditionViewModel(RulePageViewModel page, ClauseKind clause, string? propertyName, Comparison comparison, IReadOnlyList<string> values, bool isList = false, bool isRange = false)
    {
        _page = page;
        Clause = clause;
        PropertyName = propertyName;
        Comparison = comparison;
        IsRange = isRange;
        IsList = !isRange && (isList || IsOneOf);
        Values = values;
        IsWhereClause = clause == ClauseKind.Where;
        PropertyPicker = new PickerViewModel("Property", 340, "Find a property", "No property matches.");
        PropertyPicker.Chosen += OnPropertyChosen;
        PropertyPicker.Dismissed += OnPropertyDismissed;
        ComparisonPicker = new ComparisonPickerViewModel(this);
    }

    public ClauseKind Clause { get; }

    public PickerViewModel PropertyPicker { get; }

    public ComparisonPickerViewModel ComparisonPicker { get; }

    public PropertyDescriptor? Descriptor
    {
        get
        {
            return _page.FindProperty(PropertyName);
        }
    }

    public bool IsEmpty
    {
        get
        {
            return PropertyName == null;
        }
    }

    public bool IsNotEmpty
    {
        get
        {
            return PropertyName != null;
        }
    }

    /// <summary>
    /// The property is not one of the artifact type: the language has no such property.
    /// </summary>
    public bool IsInvalid
    {
        get
        {
            return PropertyName != null && Descriptor == null;
        }
    }

    /// <summary>
    /// Whether this condition holds everything a rule needs of it: a property and a value that fits it.
    /// </summary>
    public bool IsComplete
    {
        get
        {
            return PropertyName != null && Values.Count > 0 && Values.All(value => (!CanEditComparison || value.Length > 0) && FitsKind(value));
        }
    }

    public string PropertyWords
    {
        get
        {
            return SentenceWords.Property(PropertyName);
        }
    }

    public string ComparisonWord
    {
        get
        {
            return RuleSentence.ComparisonWords(Comparison, IsList);
        }
    }

    /// <summary>
    /// The comparison as it is spoken: a range is one comparison, "is between", whatever its first bound is called.
    /// </summary>
    public string SpokenComparison
    {
        get
        {
            return IsRange ? "is between" : ComparisonWord;
        }
    }

    public string ValueText
    {
        get
        {
            return IsRange ? $"{RangeLow} and {RangeHigh}" : SentenceWords.Value(Values);
        }
    }

    /// <summary>
    /// The whole reason this condition is refused, while it is; the comparison's picker carries it.
    /// </summary>
    public string? ConflictReason
    {
        get { return _page.ReasonForCondition(this); }
    }

    public string RemoveTip
    {
        get { return IsWhereClause ? "Remove this condition" : "Remove this requirement"; }
    }

    /// <summary>
    /// What this condition says of its property when it is named beside others on the same property: "at least 50", "not 40".
    /// </summary>
    public string ConflictPart
    {
        get
        {
            if(IsRange)
                return $"between {RangeLow} and {RangeHigh}";

            var values = SentenceWords.Value(Values);
            return Comparison switch
            {
                Comparison.AtMost => $"at most {values}",
                Comparison.AtLeast => $"at least {values}",
                Comparison.IsNot => IsList ? $"none of {values}" : $"not {values}",
                Comparison.IsOneOf => $"one of {values}",
                Comparison.Matches => $"matching {values}",
                Comparison.Is => $"equal to {values}",
                _ => $"equal to {values}"
            };
        }
    }

    /// <summary>
    /// The artifact the rule is about, as the words of a note say it.
    /// </summary>
    public string ArtifactWords
    {
        get { return _page.ArtifactWords; }
    }

    /// <summary>
    /// "a" or "an" for the artifact words.
    /// </summary>
    public string Article
    {
        get { return "aeiou".Contains(char.ToLowerInvariant(ArtifactWords[0]), StringComparison.Ordinal) ? "an" : "a"; }
    }

    /// <summary>
    /// What the numbers of this property count, said as a plural: "parameter count" counts parameters, "lines" counts lines.
    /// </summary>
    public string PropertyUnit
    {
        get
        {
            var words = PropertyWords;
            if(words.EndsWith(" count", StringComparison.Ordinal))
                return words[..^" count".Length] + "s";

            return words.EndsWith(" depth", StringComparison.Ordinal) ? "levels of " + words[..^" depth".Length] : words;
        }
    }

    public string PropertyTip
    {
        get
        {
            return $"Property of {_page.ArtifactWords}";
        }
    }

    public string PropertySpokenName
    {
        get
        {
            return PropertyName == null ? "Property: not chosen. Choose a property" : $"Property: {PropertyWords}";
        }
    }

    public string ComparisonSpokenName
    {
        get
        {
            return Comparison == Comparison.Matches && !IsRange ? $"Comparison: matches the pattern {ValueText}" : $"Comparison: {SpokenComparison} {ValueText}";
        }
    }

    public string RemoveSpokenName
    {
        get
        {
            return $"{(IsWhereClause ? "Remove the condition" : "Remove the requirement")} {PropertyWords} {SpokenComparison} {ValueText}";
        }
    }

    public string InvalidText
    {
        get
        {
            return $"{_page.ArtifactWords} has no property \"{PropertyWords}\". Choose another property or remove the condition.";
        }
    }

    /// <summary>
    /// What the service is given for this condition: one draft, or the two bounds of a range.
    /// </summary>
    public IEnumerable<ConditionDraft> ToDrafts()
    {
        return ConditionRanges.Drafts(PropertyName ?? string.Empty, Comparison, IsList, Values, IsRange);
    }

    public ConditionSignature Signature()
    {
        return new ConditionSignature(PropertyName, Comparison, IsList, Values);
    }

    partial void OnIsConflictedChanged(bool value)
    {
        NotifyRefused();
    }

    /// <summary>
    /// Tells every binding that what this condition is made of may have changed: the artifact type changed, or the sentence
    /// was loaded again.
    /// </summary>
    public void RefreshWords()
    {
        OnPropertyChanged(nameof(Descriptor));
        OnPropertyChanged(nameof(IsInvalid));
        NotifyRefused();
        OnPropertyChanged(nameof(IsComplete));
        OnPropertyChanged(nameof(PropertyTip));
        OnPropertyChanged(nameof(InvalidText));
    }

    /// <summary>
    /// Opens the picker of the property. The keyboard opens it with the highlight on the current property.
    /// </summary>
    public void OpenPropertyPicker(bool byKeyboard)
    {
        var entries = new List<PickerEntryViewModel>();
        foreach(var property in _page.PropertiesOffered().OrderBy(property => property.Kind))
        {
            var group = property.Kind == PropertyKind.Syntax ? _quickGroup : _buildGroup;
            var full = property.Name != PropertyName && IsFullOn(property.Name);
            entries.Add(new PickerEntryViewModel(property.Name, RuleCatalog.Words(property.Name), full ? "already has its range here" : RuleCatalog.ValueKind(property), group, property.Name == PropertyName, full));
        }

        PropertyPicker.Open(entries, byKeyboard);
    }

    /// <summary>
    /// Whether the clause has no room for another comparison on this property: it holds a range on it, or two conditions.
    /// </summary>
    private bool IsFullOn(string propertyName)
    {
        var others = _page.OtherConditionsOn(this, propertyName);
        return others.Any(other => other.IsRange) || others.Count >= 2;
    }

    /// <summary>
    /// Opens the picker of the comparison and the value. Typed text goes into the value field when there is one that takes it;
    /// the answer is whether it did.
    /// </summary>
    public bool OpenComparisonPicker(bool byKeyboard, string? typed = null)
    {
        if(!CanEditComparison)
            return false;

        return ComparisonPicker.Open(byKeyboard, typed);
    }

    /// <summary>
    /// Flips a true or false value: Space on the comparison token of such a property.
    /// </summary>
    public bool FlipBoolean()
    {
        if(Descriptor?.ValueType != PropertyValueType.Boolean || Values.Count != 1)
            return false;

        SetValues(new List<string> { Values[0] == "true" ? "false" : "true" });
        return true;
    }

    public void SetIsOneOf(bool isOneOf)
    {
        SetComparison(isOneOf ? Comparison.IsOneOf : Comparison.Is);
    }

    /// <summary>
    /// The picker of the range holds an edit, or no longer does: the head of the page says so.
    /// </summary>
    public void PendingChanged()
    {
        _page.DraftChanged();
    }

    /// <summary>
    /// Changes how the property is compared and keeps the value where it fits: "is 40" becomes "is at most 40", "is one of 0 1"
    /// becomes "is 0", "is DisposeAsync" becomes "matches DisposeAsync". A range keeps the end its new comparison is about: the
    /// upper for "is at most", the lower for any other.
    /// </summary>
    public void SetComparison(Comparison comparison)
    {
        if(!CanEditComparison || (Comparison == comparison && !IsRange))
            return;

        var keepsMany = comparison is Comparison.IsOneOf or Comparison.IsNot;
        var keptEnd = IsRange && comparison == Comparison.AtMost && Values.Count > 1 ? 1 : 0;
        IsRange = false;
        Comparison = comparison;
        if(!keepsMany && Values.Count > 1)
            Values = new List<string> { Values[keptEnd] };

        if(Values.Count == 0)
            Values = new List<string> { FirstValueFor(Descriptor) };

        IsList = comparison == Comparison.IsOneOf || (comparison == Comparison.IsNot && Values.Count > 1);

        Changed();
    }

    /// <summary>
    /// Puts back the comparison, its form and the values the condition had, whatever was changed since.
    /// </summary>
    public void Restore(Comparison comparison, bool isList, IReadOnlyList<string> values, bool isRange)
    {
        Comparison = comparison;
        IsList = isList;
        IsRange = isRange;
        Values = values;
        Changed();
    }

    /// <summary>
    /// Makes the condition a range. From "at most" the lower end is 0; otherwise both ends start at the number it held.
    /// </summary>
    public void MakeRange()
    {
        if(!CanEditComparison || IsRange)
            return;

        var first = Values.Count > 0 && long.TryParse(Values[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out _) ? Values[0] : "0";
        var low = Comparison == Comparison.AtMost ? "0" : first;
        Comparison = Comparison.AtLeast;
        IsList = false;
        IsRange = true;
        Values = new List<string> { low, first };
        Changed();
    }

    public void SetValues(IReadOnlyList<string> values)
    {
        Values = values;
        if(Comparison == Comparison.IsNot && !IsRange)
            IsList = values.Count > 1;

        Changed();
    }

    /// <summary>
    /// Puts a property in the condition. When its kind of value is another, the comparison becomes "is" and the value the
    /// first that fits: true, 0, or an empty text.
    /// </summary>
    public void SetProperty(string name)
    {
        var before = Descriptor;
        PropertyName = name;
        var after = Descriptor;
        if(before == null || after == null || before.ValueType != after.ValueType)
        {
            Comparison = Comparison.Is;
            IsList = false;
            IsRange = false;
            Values = new List<string> { FirstValueFor(after) };
        }

        Changed();
    }

    private static string FirstValueFor(PropertyDescriptor? property)
    {
        if(property?.ValueType == PropertyValueType.Boolean)
            return "true";

        if(property?.ValueType == PropertyValueType.Number)
            return "0";

        return property?.AllowedValues.Count > 0 ? property.AllowedValues[0] : string.Empty;
    }

    private bool FitsKind(string value)
    {
        var property = Descriptor;
        if(property?.ValueType == PropertyValueType.Number)
            return long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _);

        if(property?.ValueType == PropertyValueType.Boolean)
            return value == "true" || value == "false";

        return true;
    }

    private void Changed()
    {
        OnPropertyChanged(nameof(PropertyWords));
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(IsNotEmpty));
        OnPropertyChanged(nameof(IsOneOf));
        OnPropertyChanged(nameof(IsList));
        OnPropertyChanged(nameof(IsRange));
        OnPropertyChanged(nameof(IsNotRange));
        OnPropertyChanged(nameof(RangeLow));
        OnPropertyChanged(nameof(RangeHigh));
        OnPropertyChanged(nameof(ValueIsCode));
        OnPropertyChanged(nameof(CodeValueText));
        OnPropertyChanged(nameof(PlainValueText));
        OnPropertyChanged(nameof(ValueIsNotCode));
        NotifyRefused();
        OnPropertyChanged(nameof(RemoveTip));
        OnPropertyChanged(nameof(CanEditComparison));
        OnPropertyChanged(nameof(ComparisonWord));
        OnPropertyChanged(nameof(ValueText));
        OnPropertyChanged(nameof(PropertySpokenName));
        OnPropertyChanged(nameof(ComparisonSpokenName));
        OnPropertyChanged(nameof(RemoveSpokenName));
        RefreshWords();
        _page.EditMade();
    }

    private void OnPropertyDismissed()
    {
        if(IsJustAdded && IsEmpty)
            _page.RemoveCondition(this);
    }

    private void OnPropertyChosen(PickerEntryViewModel entry)
    {
        IsJustAdded = false;
        SetProperty(entry.Key);
    }

    [RelayCommand]
    private void ChooseProperty()
    {
        OpenPropertyPicker(false);
    }

    [RelayCommand]
    private void ChooseComparison()
    {
        OpenComparisonPicker(false);
    }

    [RelayCommand]
    private void Remove()
    {
        _page.RemoveByButton(this);
    }
}
