using CommunityToolkit.Mvvm.ComponentModel;

namespace CF.App.Rules;

/// <summary>
/// One segment of the filter: its word, and how many rules it would show.
/// </summary>
internal sealed partial class FilterSegmentViewModel : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CountText), nameof(SpokenName))]
    public partial int Count { get; set; }

    public FilterSegmentViewModel(RuleFilter filter, string label)
    {
        Filter = filter;
        Label = label;
    }

    public RuleFilter Filter { get; }

    public string Label { get; }

    public bool ShowsBlockMark
    {
        get
        {
            return Filter == RuleFilter.Blocks;
        }
    }

    public bool ShowsWarnMark
    {
        get
        {
            return Filter == RuleFilter.Warns;
        }
    }

    public bool ShowsOffMark
    {
        get
        {
            return Filter == RuleFilter.Off;
        }
    }

    public string CountText
    {
        get
        {
            return Count.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
    }

    /// <summary>
    /// What a screen reader says for the segment: the word and the count.
    /// </summary>
    public string SpokenName
    {
        get
        {
            return $"{Label}, {Count} {(Count == 1 ? "rule" : "rules")}";
        }
    }
}
