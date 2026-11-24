using System.Collections.ObjectModel;
using System.Globalization;
using CF.Core.Code;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CF.App.Rules.Page;

/// <summary>
/// The picker a comparison token opens: the comparisons that fit the property, a
/// hairline, then the value: two entries for true or false, a field for a number or a text, the possible values for a list.
/// The comparisons are the two the rule service has, "is" and "is one of".
/// </summary>
internal sealed partial class ComparisonPickerViewModel : ObservableObject
{
    private const string _is = "is";
    private const string _isOneOf = "is one of";

    private readonly ConditionViewModel _condition;

    [ObservableProperty]
    public partial bool IsOpen { get; set; }

    [ObservableProperty]
    public partial bool ShowsField { get; set; }

    [ObservableProperty]
    public partial bool ShowsEntries { get; set; }

    [ObservableProperty]
    public partial string FieldText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool FieldIsRefused { get; set; }

    public ComparisonPickerViewModel(ConditionViewModel condition)
    {
        _condition = condition;
    }

    public ObservableCollection<PickerEntryViewModel> Comparisons { get; } = new ObservableCollection<PickerEntryViewModel>();

    public ObservableCollection<PickerEntryViewModel> ValueEntries { get; } = new ObservableCollection<PickerEntryViewModel>();

    public string FieldName
    {
        get
        {
            return _condition.IsOneOf ? "Values, separated by commas" : "Value";
        }
    }

    public void Open(bool byKeyboard)
    {
        Fill();
        if(byKeyboard)
        {
            foreach(var entry in Comparisons.Concat(ValueEntries).Where(entry => entry.IsCurrent))
            {
                entry.IsHighlighted = true;
                break;
            }
        }

        IsOpen = true;
    }

    public void Close()
    {
        IsOpen = false;
    }

    /// <summary>
    /// Keeps what is typed in the field of a number or a text, and closes. A number that is not a number is not kept.
    /// </summary>
    public void CommitField()
    {
        var parts = _condition.IsOneOf ? FieldText.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries) : new[] { FieldText.Trim() };
        if(parts.Length == 0 || !parts.All(FitsTheKind))
        {
            FieldIsRefused = true;
            return;
        }

        FieldIsRefused = false;
        _condition.SetValues(parts);
        IsOpen = false;
    }

    /// <summary>
    /// Up and Down in the field of a number change it by one.
    /// </summary>
    public void Step(int delta)
    {
        if(_condition.Descriptor?.ValueType != PropertyValueType.Number)
            return;

        if(long.TryParse(FieldText.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var number))
            FieldText = (number + delta).ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Moves the highlight over the comparisons and the values, in the order they are drawn.
    /// </summary>
    public void MoveHighlight(int step)
    {
        var all = Comparisons.Concat(ValueEntries).ToList();
        var current = all.FindIndex(entry => entry.IsHighlighted);
        var target = Math.Clamp(current + step, 0, all.Count - 1);
        foreach(var entry in all)
            entry.IsHighlighted = false;

        all[target].IsHighlighted = true;
    }

    public void ChooseHighlighted()
    {
        var entry = Comparisons.Concat(ValueEntries).FirstOrDefault(candidate => candidate.IsHighlighted);
        if(entry == null)
            return;

        if(Comparisons.Contains(entry))
            ChooseComparison(entry);
        else
            ChooseValue(entry);
    }

    [RelayCommand]
    private void ChooseComparison(PickerEntryViewModel entry)
    {
        _condition.SetIsOneOf(entry.Key == _isOneOf);
        Fill();
        foreach(var candidate in Comparisons.Where(candidate => candidate.Key == entry.Key))
            candidate.IsHighlighted = true;
    }

    [RelayCommand]
    private void ChooseValue(PickerEntryViewModel entry)
    {
        if(!_condition.IsOneOf)
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

    private bool FitsTheKind(string text)
    {
        if(text.Length == 0)
            return false;

        if(_condition.Descriptor?.ValueType == PropertyValueType.Number)
            return long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out _);

        return true;
    }

    private void Fill()
    {
        var property = _condition.Descriptor;
        Comparisons.Clear();
        Comparisons.Add(new PickerEntryViewModel(_is, _is, string.Empty, string.Empty, !_condition.IsOneOf, false));
        if(property?.ValueType != PropertyValueType.Boolean)
            Comparisons.Add(new PickerEntryViewModel(_isOneOf, _isOneOf, string.Empty, string.Empty, _condition.IsOneOf, false));

        ValueEntries.Clear();
        foreach(var value in ValuesToOffer(property))
            ValueEntries.Add(new PickerEntryViewModel(value, value, string.Empty, string.Empty, _condition.Values.Contains(value), false));

        ShowsEntries = ValueEntries.Count > 0;
        ShowsField = !ShowsEntries;
        FieldText = string.Join(", ", _condition.Values);
        FieldIsRefused = false;
        OnPropertyChanged(nameof(FieldName));
    }

    private static IEnumerable<string> ValuesToOffer(PropertyDescriptor? property)
    {
        if(property?.ValueType == PropertyValueType.Boolean)
            return new[] { "true", "false" };

        return property?.AllowedValues ?? new List<string>();
    }
}
