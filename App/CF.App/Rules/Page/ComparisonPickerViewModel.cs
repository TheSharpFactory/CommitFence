using System.Collections.ObjectModel;
using System.Globalization;
using CF.Core.Code;
using CF.Core.Rules;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CF.App.Rules.Page;

/// <summary>
/// The picker a comparison token opens: the comparisons that fit the property's type (only those; one that does not fit is not
/// there), a hairline, then the value: the two entries true and false, a field for a number, a text or a pattern, a list of
/// fields for several values, or the property's own values to tick.
/// </summary>
internal sealed partial class ComparisonPickerViewModel : ObservableObject
{
    private const string _betweenKey = "Between";

    private readonly ConditionViewModel _condition;
    private bool _discard;
    private Comparison _comparisonWhenOpened;
    private bool _isListWhenOpened;
    private IReadOnlyList<string> _valuesWhenOpened = new List<string>();
    private bool _isRangeWhenOpened;

    [ObservableProperty]
    public partial bool IsOpen { get; set; }

    [ObservableProperty]
    public partial bool ShowsComparisons { get; set; }

    [ObservableProperty]
    public partial bool ShowsField { get; set; }

    [ObservableProperty]
    public partial bool ShowsEntries { get; set; }

    [ObservableProperty]
    public partial bool ShowsList { get; set; }

    [ObservableProperty]
    public partial bool ShowsRange { get; set; }

    [ObservableProperty]
    public partial string RangeLowText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string RangeHighText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool FieldIsPattern { get; set; }

    [ObservableProperty]
    public partial bool FieldIsNumber { get; set; }

    [ObservableProperty]
    public partial string FieldText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool FieldIsRefused { get; set; }

    [ObservableProperty]
    public partial string RefusalText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string AddRowText { get; set; } = "Add a value";

    public ComparisonPickerViewModel(ConditionViewModel condition)
    {
        _condition = condition;
    }

    /// <summary>
    /// Raised after the comparison changed, so that the keyboard goes to the value.
    /// </summary>
    public event Action? ValueFocusRequested;

    /// <summary>
    /// Raised to put the keyboard in the field of one row of the value list.
    /// </summary>
    public event Action<ValueRowViewModel>? RowFocusRequested;

    public ObservableCollection<PickerEntryViewModel> Comparisons { get; } = new ObservableCollection<PickerEntryViewModel>();

    public ObservableCollection<PickerEntryViewModel> ValueEntries { get; } = new ObservableCollection<PickerEntryViewModel>();

    public ObservableCollection<ValueRowViewModel> Rows { get; } = new ObservableCollection<ValueRowViewModel>();

    /// <summary>
    /// What the two ends of a range say, under their boxes: both numbers count.
    /// </summary>
    public string RangeNote
    {
        get
        {
            var low = RangeLowText.Length > 0 ? RangeLowText : "0";
            var high = RangeHighText.Length > 0 ? RangeHighText : "0";
            return $"Both ends count: {_condition.Article} {_condition.ArtifactWords} with {low} or {high} {_condition.PropertyUnit} is in.";
        }
    }

    /// <summary>
    /// Why the token this picker belongs to is refused: the comparison cannot hold with another, or no code can meet it.
    /// </summary>
    public string? ConflictReason
    {
        get { return _condition.ConflictReason; }
    }

    public bool HasConflictReason
    {
        get { return ConflictReason != null; }
    }

    /// <summary>
    /// What a screen reader says with each box of a range: why the ends typed were refused, or why the token is refused.
    /// </summary>
    public string? RangeHelp
    {
        get { return FieldIsRefused ? RefusalText : ConflictReason; }
    }

    public void ReasonChanged()
    {
        OnPropertyChanged(nameof(ConflictReason));
        OnPropertyChanged(nameof(HasConflictReason));
        OnPropertyChanged(nameof(RangeHelp));
    }

    public bool HasRefusal
    {
        get { return FieldIsRefused; }
    }

    public string FieldName
    {
        get
        {
            if(FieldIsPattern)
                return "Pattern";

            return FieldIsNumber ? "Number" : "Value";
        }
    }

    public bool Open(bool byKeyboard, string? typed = null)
    {
        _discard = false;
        _comparisonWhenOpened = _condition.Comparison;
        _isListWhenOpened = _condition.IsList;
        _isRangeWhenOpened = _condition.IsRange;
        _valuesWhenOpened = _condition.Values;
        Fill();
        var taken = typed != null && ShowsField && (!FieldIsNumber || typed.All(char.IsAsciiDigit));
        if(taken)
            FieldText = typed!;

        if(byKeyboard)
        {
            foreach(var entry in Comparisons.Concat(ValueEntries).Where(entry => entry.IsCurrent))
            {
                entry.IsHighlighted = true;
                break;
            }
        }

        IsOpen = true;
        return typed == null || taken;
    }

    /// <summary>
    /// Closes. A list of values that is complete is kept; anything else typed is dropped.
    /// </summary>
    public void Close()
    {
        IsOpen = false;
    }

    /// <summary>
    /// Closes with Esc: nothing typed is kept, and the comparison and the value are the ones the condition had when the picker
    /// opened.
    /// </summary>
    public void Escape()
    {
        var wasOpen = IsOpen;
        _discard = true;
        IsOpen = false;
        if(wasOpen)
            _condition.Restore(_comparisonWhenOpened, _isListWhenOpened, _valuesWhenOpened, _isRangeWhenOpened);
    }

    /// <summary>
    /// Keeps what is typed in the field of a number, a text or a pattern, and closes. What does not fit is not kept: the field says
    /// why.
    /// </summary>
    public void CommitField()
    {
        var text = FieldText.Trim();
        var refusal = Refusal(text);
        if(refusal != null)
        {
            Refuse(refusal);
            return;
        }

        FieldIsRefused = false;
        _condition.SetValues(new List<string> { text });
        IsOpen = false;
    }

    /// <summary>
    /// Keeps the two ends of a range and closes. An end that is not a number is not kept: the picker says why. Whether the range can
    /// be met is for the page to say, under its sentence.
    /// </summary>
    public bool CommitRange()
    {
        var low = RangeLowText.Trim();
        var high = RangeHighText.Trim();
        var refusal = RangeRefusal(low, high);
        if(refusal != null)
        {
            Refuse(refusal);
            return false;
        }

        FieldIsRefused = false;
        _condition.SetValues(new List<string> { low, high });
        IsOpen = false;
        return true;
    }

    /// <summary>
    /// Up and Down in one end of a range change it by one; a count is never below 0.
    /// </summary>
    public void StepRange(bool high, int delta)
    {
        var text = high ? RangeHighText : RangeLowText;
        var number = long.TryParse(text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var typed) ? typed : 0;
        var stepped = Math.Max(0, number + delta).ToString(CultureInfo.InvariantCulture);
        if(high)
            RangeHighText = stepped;
        else
            RangeLowText = stepped;
    }

    /// <summary>
    /// Keeps the values of the list and closes: an empty row is dropped; a value that does not fit is not kept and its row says why.
    /// </summary>
    public bool CommitList()
    {
        var filled = Rows.Where(row => row.Text.Trim().Length > 0).ToList();
        if(filled.Count == 0)
        {
            Refuse(NothingTypedWords());
            Rows[0].IsRefused = true;
            return false;
        }

        var refused = filled.FirstOrDefault(row => Refusal(row.Text.Trim()) != null);
        if(refused != null)
        {
            refused.IsRefused = true;
            Refuse(Refusal(refused.Text.Trim())!);
            return false;
        }

        FieldIsRefused = false;
        _condition.SetValues(filled.Select(row => row.Text.Trim()).ToList());
        IsOpen = false;
        return true;
    }

    /// <summary>
    /// Enter in a row: the next row, or, in an empty last row, the end of the list.
    /// </summary>
    public void EnterInRow(ValueRowViewModel row)
    {
        var index = Rows.IndexOf(row);
        if(row.Text.Trim().Length == 0 && index == Rows.Count - 1 && Rows.Count > 1)
        {
            Rows.Remove(row);
            RefreshRowNames();
            RefreshComparisonNames();
            CommitList();
            return;
        }

        if(index < Rows.Count - 1)
        {
            RowFocusRequested?.Invoke(Rows[index + 1]);
            return;
        }

        AddRow();
    }

    /// <summary>
    /// Up and Down move between the rows.
    /// </summary>
    public void MoveRow(ValueRowViewModel row, int step)
    {
        var target = Rows.IndexOf(row) + step;
        if(target >= 0 && target < Rows.Count)
            RowFocusRequested?.Invoke(Rows[target]);
    }

    /// <summary>
    /// Delete on an empty row takes the row away; there is always one.
    /// </summary>
    public bool RemoveEmptyRow(ValueRowViewModel row)
    {
        if(row.Text.Length > 0)
            return false;

        RemoveRow(row);
        return true;
    }

    public void RemoveRow(ValueRowViewModel row)
    {
        var index = Rows.IndexOf(row);
        if(index < 0)
            return;

        Rows.Remove(row);
        if(Rows.Count == 0)
            Rows.Add(new ValueRowViewModel(this, string.Empty));

        RefreshRowNames();
        RefreshComparisonNames();
        RowFocusRequested?.Invoke(Rows[Math.Min(index, Rows.Count - 1)]);
    }

    /// <summary>
    /// A row's text changed: digits only in a row of numbers, and a refusal that was shown goes.
    /// </summary>
    public void RowTyped(ValueRowViewModel row)
    {
        if(FieldIsNumber && NumberCharacters(row.Text) != row.Text)
            row.Text = NumberCharacters(row.Text);

        FieldIsRefused = false;
        RefreshComparisonNames();
    }

    /// <summary>
    /// Up and Down in the field of a number change it by one; a count is never below 0.
    /// </summary>
    public void Step(int delta)
    {
        if(!FieldIsNumber)
            return;

        var number = long.TryParse(FieldText.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var typed) ? typed : 0;
        FieldText = Math.Max(0, number + delta).ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Moves the highlight over the comparisons and the values, in the order they are drawn.
    /// </summary>
    public void MoveHighlight(int step)
    {
        var all = HighlightOrder();
        if(all.Count == 0)
            return;

        var current = all.FindIndex(entry => entry.IsHighlighted);
        var target = Math.Clamp(current + step, 0, all.Count - 1);
        foreach(var entry in all)
            entry.IsHighlighted = false;

        all[target].IsHighlighted = true;
    }

    public void ChooseHighlighted()
    {
        var entry = HighlightOrder().FirstOrDefault(candidate => candidate.IsHighlighted);
        if(entry == null)
            return;

        if(Comparisons.Contains(entry))
            ChooseComparison(entry);
        else
            ChooseValue(entry);
    }

    partial void OnFieldTextChanged(string value)
    {
        FieldIsRefused = false;
        if(FieldIsNumber && NumberCharacters(value) != value)
            FieldText = NumberCharacters(value);
    }

    partial void OnFieldIsRefusedChanged(bool value)
    {
        OnPropertyChanged(nameof(HasRefusal));
        OnPropertyChanged(nameof(RangeHelp));
    }

    partial void OnRefusalTextChanged(string value)
    {
        OnPropertyChanged(nameof(RangeHelp));
    }

    partial void OnRangeLowTextChanged(string value)
    {
        RangeTyped(nameof(RangeLowText), value);
    }

    partial void OnRangeHighTextChanged(string value)
    {
        RangeTyped(nameof(RangeHighText), value);
    }

    private void RangeTyped(string which, string value)
    {
        var kept = NumberCharacters(value);
        if(kept != value)
        {
            if(which == nameof(RangeLowText))
                RangeLowText = kept;
            else
                RangeHighText = kept;

            return;
        }

        FieldIsRefused = false;
        OnPropertyChanged(nameof(RangeNote));
        _condition.PendingChanged();
    }

    partial void OnIsOpenChanged(bool value)
    {
        if(!value && !_discard)
        {
            if(ShowsList)
                CommitList();
            else if(ShowsRange)
                KeepRangeIfNumbers();
        }

        _condition.PendingChanged();
    }

    private void KeepRangeIfNumbers()
    {
        if(!RangeHasChanged())
            return;

        var low = RangeLowText.Trim();
        var high = RangeHighText.Trim();
        if(RangeRefusal(low, high) == null)
            _condition.SetValues(new List<string> { low, high });
    }

    /// <summary>
    /// Whether the two ends typed differ from the ones the condition holds: untouched ends are never written back.
    /// </summary>
    private bool RangeHasChanged()
    {
        var values = _condition.Values;
        return values.Count != 2 || values[0] != RangeLowText.Trim() || values[1] != RangeHighText.Trim();
    }

    /// <summary>
    /// Whether the range picker is open holding an edit that the condition does not hold yet.
    /// </summary>
    public bool HasPendingChange
    {
        get { return IsOpen && ShowsRange && RangeHasChanged(); }
    }

    /// <summary>
    /// Why the ends typed cannot be kept, while the picker holds an edit of a range; nothing when it holds none or the edit is good.
    /// </summary>
    public string? PendingProblem
    {
        get { return HasPendingChange ? RangeRefusal(RangeLowText.Trim(), RangeHighText.Trim()) : null; }
    }

    /// <summary>
    /// What a number may hold while it is typed: whole numbers, with a minus sign in front as the settings file allows.
    /// </summary>
    private static string NumberCharacters(string text)
    {
        var digits = new string(text.Where(char.IsAsciiDigit).ToArray());
        return text.StartsWith('-') ? "-" + digits : digits;
    }

    private static bool IsNumberText(string text)
    {
        return NumberCharacters(text) == text && long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out _);
    }

    [RelayCommand]
    private void ChooseComparison(PickerEntryViewModel entry)
    {
        if(entry.IsDisabled)
            return;

        if(entry.Key == _betweenKey)
            _condition.MakeRange();
        else
            _condition.SetComparison(Enum.Parse<Comparison>(entry.Key));

        Fill();
        foreach(var candidate in Comparisons.Where(candidate => candidate.Key == entry.Key))
            candidate.IsHighlighted = true;

        ValueFocusRequested?.Invoke();
    }

    [RelayCommand]
    private void ChooseValue(PickerEntryViewModel entry)
    {
        if(_condition.Comparison is not (Comparison.IsOneOf or Comparison.IsNot))
        {
            _condition.SetValues(new List<string> { entry.Key });
            IsOpen = false;
            return;
        }

        var chosen = _condition.Values.Where(value => value.Length > 0).ToList();
        if(!chosen.Remove(entry.Key))
            chosen.Add(entry.Key);

        if(chosen.Count == 0)
            return;

        _condition.SetValues(chosen);
        Fill();
    }

    [RelayCommand]
    private void AddRow()
    {
        var row = new ValueRowViewModel(this, string.Empty);
        Rows.Add(row);
        RefreshRowNames();
        RowFocusRequested?.Invoke(row);
    }

    private void RefreshRowNames()
    {
        foreach(var row in Rows)
            row.RefreshNames();
    }

    private List<PickerEntryViewModel> HighlightOrder()
    {
        return (ShowsComparisons ? Comparisons : Enumerable.Empty<PickerEntryViewModel>()).Concat(ShowsEntries ? ValueEntries : Enumerable.Empty<PickerEntryViewModel>()).Where(entry => !entry.IsDisabled).ToList();
    }

    private void Refuse(string words)
    {
        RefusalText = words;
        FieldIsRefused = true;
    }

    /// <summary>
    /// Why this text cannot be the value now, or nothing when it can.
    /// </summary>
    private string? Refusal(string text)
    {
        if(_condition.Comparison == Comparison.Matches)
            return text.Length == 0 ? "Type a pattern, like *Async." : null;

        if(_condition.Descriptor?.ValueType == PropertyValueType.Number)
            return IsNumberText(text) ? null : "Type a number.";

        return text.Length == 0 ? "Type a value." : null;
    }

    /// <summary>
    /// Why these two ends cannot be a range, or nothing when they can: each is a number, and the lower is not above the upper.
    /// </summary>
    private string? RangeRefusal(string low, string high)
    {
        var refusal = Refusal(low) ?? Refusal(high);
        if(refusal != null)
            return refusal;

        return long.Parse(low, CultureInfo.InvariantCulture) > long.Parse(high, CultureInfo.InvariantCulture) ? "The lower end must not be above the upper end." : null;
    }

    private string NothingTypedWords()
    {
        return _condition.Descriptor?.ValueType == PropertyValueType.Number ? "Type a number." : "Type a value.";
    }

    private void Fill()
    {
        var property = _condition.Descriptor;
        var type = property?.ValueType ?? PropertyValueType.Text;
        Comparisons.Clear();
        var offered = RuleComparisons.For(type);
        foreach(var comparison in offered)
            Comparisons.Add(new PickerEntryViewModel(comparison.ToString(), RuleSentence.ComparisonWords(comparison, comparison == Comparison.IsNot && ManyValues), NoteFor(comparison, property), string.Empty, comparison == _condition.Comparison && !_condition.IsRange, false));

        if(type == PropertyValueType.Number)
            Comparisons.Add(new PickerEntryViewModel(_betweenKey, "is between", _condition.HasOtherConditionsOnProperty && !_condition.IsRange ? "another condition is on this property" : "from one number to another", string.Empty, _condition.IsRange, _condition.HasOtherConditionsOnProperty && !_condition.IsRange));

        ShowsComparisons = offered.Count > 1;
        ValueEntries.Clear();
        foreach(var value in ValuesToOffer(property))
            ValueEntries.Add(new PickerEntryViewModel(value, value, string.Empty, string.Empty, _condition.Values.Contains(value), false));

        ShowsEntries = ValueEntries.Count > 0;
        var comparisonNow = _condition.Comparison;
        var many = comparisonNow is Comparison.IsOneOf or Comparison.IsNot;
        ShowsRange = _condition.IsRange;
        ShowsList = !ShowsEntries && many && !ShowsRange;
        ShowsField = !ShowsEntries && !many && !ShowsRange;
        FieldIsPattern = comparisonNow == Comparison.Matches;
        FieldIsNumber = type == PropertyValueType.Number && !FieldIsPattern;
        AddRowText = property?.ValueType == PropertyValueType.Text && property.Name.Contains("name", StringComparison.OrdinalIgnoreCase) ? "Add a name" : "Add a value";
        FieldText = string.Join(" ", _condition.Values);
        RangeLowText = ShowsRange && _condition.Values.Count > 0 ? _condition.Values[0] : string.Empty;
        RangeHighText = ShowsRange && _condition.Values.Count > 1 ? _condition.Values[1] : string.Empty;
        FieldIsRefused = false;
        FillRows();
        RefreshComparisonNames();
        OnPropertyChanged(nameof(FieldName));
        OnPropertyChanged(nameof(RangeNote));
    }

    /// <summary>
    /// How many values the sentence would have now: the filled rows of a list being edited, else the values the condition holds.
    /// </summary>
    private int ValueCountNow()
    {
        return ShowsList ? Rows.Count(row => row.Text.Trim().Length > 0) : _condition.IsRange ? 1 : _condition.Values.Count;
    }

    /// <summary>
    /// Whether the condition holds several values to compare with: a range holds two numbers but compares with neither as a list.
    /// </summary>
    private bool ManyValues
    {
        get { return !_condition.IsRange && _condition.Values.Count > 1; }
    }

    /// <summary>
    /// "is not" says "is none of" once there are two values, in the entry as on the token.
    /// </summary>
    private void RefreshComparisonNames()
    {
        foreach(var entry in Comparisons.Where(entry => entry.Key == nameof(Comparison.IsNot)))
            entry.Name = RuleSentence.ComparisonWords(Comparison.IsNot, ValueCountNow() > 1);
    }

    private void FillRows()
    {
        Rows.Clear();
        if(!ShowsList)
            return;

        foreach(var value in _condition.Values)
            Rows.Add(new ValueRowViewModel(this, value));

        if(Rows.Count == 0)
            Rows.Add(new ValueRowViewModel(this, string.Empty));

        RefreshRowNames();
    }

    private static IEnumerable<string> ValuesToOffer(PropertyDescriptor? property)
    {
        if(property?.ValueType == PropertyValueType.Boolean)
            return new[] { "true", "false" };

        return property?.AllowedValues ?? new List<string>();
    }

    private string NoteFor(Comparison comparison, PropertyDescriptor? property)
    {
        if(property?.ValueType == PropertyValueType.Boolean)
            return string.Empty;

        if(property?.AllowedValues.Count > 0)
            return ListNote(comparison);

        if(property?.ValueType == PropertyValueType.Number)
        {
            return comparison switch
            {
                Comparison.Is => "exactly one number",
                Comparison.IsOneOf => "any of a list",
                Comparison.IsNot => ManyValues ? "anything but these" : "anything but this",
                Comparison.AtMost => "this or less",
                Comparison.AtLeast => "this or more",
                Comparison.Matches => string.Empty,
                _ => string.Empty
            };
        }

        return comparison switch
        {
            Comparison.Is => "exactly this text",
            Comparison.IsOneOf => "any of a list",
            Comparison.IsNot => ManyValues ? "anything but these" : "anything but this",
            Comparison.Matches => "a pattern",
            Comparison.AtMost or Comparison.AtLeast => string.Empty,
            _ => string.Empty
        };
    }

    private static string ListNote(Comparison comparison)
    {
        return comparison switch
        {
            Comparison.Is => "exactly one",
            Comparison.IsOneOf => "any of these",
            Comparison.IsNot => "none of these",
            Comparison.Matches => "a pattern",
            Comparison.AtMost or Comparison.AtLeast => string.Empty,
            _ => string.Empty
        };
    }
}
