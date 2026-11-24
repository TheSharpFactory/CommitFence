using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CF.App.Review;

/// <summary>
/// One row of the count by rule: the effect mark, the rule's title, its number of findings, its id and a bar. A click chooses
/// the rule, or shows all again when it is the chosen one.
/// </summary>
internal sealed partial class RuleRowViewModel : ObservableObject
{
    private readonly Action<RuleRowViewModel> _choose;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SpokenName))]
    public partial bool IsChosen { get; set; }

    /// <summary>
    /// Another rule is chosen: this row steps back and keeps what it says.
    /// </summary>
    [ObservableProperty]
    public partial bool IsSteppedBack { get; set; }

    public RuleRowViewModel(string ruleId, string title, int count, bool isBlock, double fraction, bool hasBar, Action<RuleRowViewModel> choose)
    {
        RuleId = ruleId;
        Title = title;
        Count = count;
        IsBlock = isBlock;
        Fraction = fraction;
        HasBar = hasBar;
        _choose = choose;
    }

    public string RuleId { get; }

    public string Title { get; }

    public int Count { get; }

    public string CountText
    {
        get
        {
            return Count.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
    }

    public bool IsBlock { get; }

    public bool IsWarn
    {
        get
        {
            return !IsBlock;
        }
    }

    /// <summary>
    /// The rule's count over the largest count, from 0 to 1.
    /// </summary>
    public double Fraction { get; }

    /// <summary>
    /// When every rule has the same count there are no bars.
    /// </summary>
    public bool HasBar { get; }

    public bool ShowsBlockBar
    {
        get
        {
            return HasBar && IsBlock;
        }
    }

    public bool ShowsWarnBar
    {
        get
        {
            return HasBar && IsWarn;
        }
    }

    public string SpokenName
    {
        get
        {
            var state = IsChosen ? ", chosen: press to show all again" : string.Empty;
            return $"{Title}, {RuleId}, {Count} {(Count == 1 ? "finding" : "findings")}, {(IsBlock ? "would block a push" : "would only warn")}{state}";
        }
    }

    [RelayCommand]
    private void Choose()
    {
        _choose(this);
    }
}
