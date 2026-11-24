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

    [ObservableProperty]
    public partial bool IsOneOf { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<string> Values { get; set; } = new List<string>();

    [ObservableProperty]
    public partial bool CanRemove { get; set; }

    [ObservableProperty]
    public partial bool ShowsAnd { get; set; }

    [ObservableProperty]
    public partial bool IsWhereClause { get; set; }

    public ConditionViewModel(RulePageViewModel page, ClauseKind clause, string? propertyName, bool isOneOf, IReadOnlyList<string> values)
    {
        _page = page;
        Clause = clause;
        PropertyName = propertyName;
        IsOneOf = isOneOf;
        Values = values;
        IsWhereClause = clause == ClauseKind.Where;
        PropertyPicker = new PickerViewModel("Property", 340, "Find a property", "No property matches.");
        PropertyPicker.Chosen += OnPropertyChosen;
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
            return PropertyName != null && Values.Count > 0 && Values.All(value => value.Length > 0 && FitsKind(value));
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
            return SentenceWords.Comparison(IsOneOf);
        }
    }

    public string ValueText
    {
        get
        {
            return SentenceWords.Value(Values);
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
            return $"Comparison: {ComparisonWord} {ValueText}";
        }
    }

    public string RemoveSpokenName
    {
        get
        {
            return $"Remove the condition: {PropertyWords} {ComparisonWord} {ValueText}";
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
    /// What the service is given for this condition.
    /// </summary>
    public ConditionDraft ToDraft()
    {
        return new ConditionDraft(PropertyName ?? string.Empty, Values, IsOneOf);
    }

    public string Signature()
    {
        return $"{PropertyName}|{IsOneOf}|{string.Join(",", Values)}";
    }

    /// <summary>
    /// Tells every binding that what this condition is made of may have changed: the artifact type changed, or the sentence
    /// was loaded again.
    /// </summary>
    public void RefreshWords()
    {
        OnPropertyChanged(nameof(Descriptor));
        OnPropertyChanged(nameof(IsInvalid));
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
            entries.Add(new PickerEntryViewModel(property.Name, RuleCatalog.Words(property.Name), RuleCatalog.ValueKind(property), group, property.Name == PropertyName, false));
        }

        PropertyPicker.Open(entries, byKeyboard);
    }

    public void OpenComparisonPicker(bool byKeyboard)
    {
        ComparisonPicker.Open(byKeyboard);
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
        if(IsOneOf == isOneOf)
            return;

        IsOneOf = isOneOf;
        if(!isOneOf && Values.Count > 1)
            Values = new List<string> { Values[0] };

        Changed();
    }

    public void SetValues(IReadOnlyList<string> values)
    {
        Values = values;
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
            IsOneOf = false;
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
        OnPropertyChanged(nameof(ComparisonWord));
        OnPropertyChanged(nameof(ValueText));
        OnPropertyChanged(nameof(PropertySpokenName));
        OnPropertyChanged(nameof(ComparisonSpokenName));
        OnPropertyChanged(nameof(RemoveSpokenName));
        RefreshWords();
        _page.EditMade();
    }

    private void OnPropertyChosen(PickerEntryViewModel entry)
    {
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
        _page.RemoveCondition(this);
    }
}
