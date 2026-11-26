using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CF.App.Review;

/// <summary>
/// A file with findings, as one line with its tally that folds its findings.
/// </summary>
internal sealed partial class FileGroupViewModel : ReviewListItemViewModel
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SpokenName))]
    public partial bool IsOpen { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasFixedFindings), nameof(FixedText), nameof(SpokenCounts), nameof(SpokenName))]
    [NotifyPropertyChangedFor(nameof(BlockCount), nameof(WarnCount), nameof(HasBlock), nameof(HasWarn), nameof(BlockText), nameof(WarnText))]
    public partial int FixedCount { get; private set; }

    public FileGroupViewModel(string path, IReadOnlyList<FindingViewModel> findings, bool startsOpen)
    {
        Path = path;
        Findings = findings;
        IsOpen = startsOpen;
        RefreshFixedCount();
    }

    public string Path { get; }

    public IReadOnlyList<FindingViewModel> Findings { get; }

    public void RefreshFixedCount()
    {
        FixedCount = Findings.Count(finding => finding.IsFixed);
    }

    public int BlockCount
    {
        get { return Findings.Count(finding => finding.ShowsBlock); }
    }

    public int WarnCount
    {
        get { return Findings.Count(finding => finding.ShowsWarn); }
    }

    public bool HasBlock
    {
        get
        {
            return BlockCount > 0;
        }
    }

    public bool HasWarn
    {
        get
        {
            return WarnCount > 0;
        }
    }

    public string BlockText
    {
        get
        {
            return BlockCount.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
    }

    public string WarnText
    {
        get
        {
            return WarnCount.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
    }

    public int Total
    {
        get
        {
            return Findings.Count;
        }
    }

    public bool HasFixedFindings
    {
        get { return FixedCount > 0; }
    }

    public string FixedText
    {
        get { return string.Create(CultureInfo.InvariantCulture, $"{FixedCount} fixed"); }
    }

    public string SpokenCounts
    {
        get
        {
            var reviewed = $"{Tally(BlockCount, "blocking")}, {Tally(WarnCount, "warning")}";
            return HasFixedFindings ? $"{reviewed}, {FixedText}" : reviewed;
        }
    }

    public string SpokenName
    {
        get
        {
            return $"{Path}, {SpokenCounts}, {(IsOpen ? "expanded" : "folded")}";
        }
    }

    [RelayCommand]
    private void Toggle()
    {
        IsOpen = !IsOpen;
    }

    private static string Tally(int count, string word)
    {
        return $"{count} {word}";
    }
}
