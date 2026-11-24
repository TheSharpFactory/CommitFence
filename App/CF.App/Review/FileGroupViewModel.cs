using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CF.App.Review;

/// <summary>
/// A file with findings, as one line with its tally that folds its findings.
/// </summary>
internal sealed partial class FileGroupViewModel : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SpokenName))]
    public partial bool IsOpen { get; set; }

    public FileGroupViewModel(string path, IReadOnlyList<FindingViewModel> findings, bool startsOpen)
    {
        Path = path;
        Findings = findings;
        BlockCount = findings.Count(finding => finding.IsBlock);
        WarnCount = findings.Count - BlockCount;
        IsOpen = startsOpen;
    }

    public string Path { get; }

    public IReadOnlyList<FindingViewModel> Findings { get; }

    public int BlockCount { get; }

    public int WarnCount { get; }

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

    public string SpokenName
    {
        get
        {
            return $"{Path}, {Tally(BlockCount, "blocking")}, {Tally(WarnCount, "warning")}, {(IsOpen ? "expanded" : "folded")}";
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
