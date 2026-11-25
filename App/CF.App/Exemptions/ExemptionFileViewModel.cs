using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CF.App.Exemptions;

/// <summary>
/// A file with exemptions, as one line with its tally that folds its exemptions. While a filter shows only some of the file's
/// exemptions the tally says "4 of 5 exemptions".
/// </summary>
internal sealed partial class ExemptionFileViewModel : ExemptionListItem
{
    private readonly Action<ExemptionFileViewModel> _toggled = _ => { };

    public ExemptionFileViewModel(string path, IReadOnlyList<ExemptionRowViewModel> rows, int totalInFile, bool startsOpen, Action<ExemptionFileViewModel> toggled)
    {
        Path = path;
        Rows = rows;
        TotalInFile = totalInFile;
        var slash = path.LastIndexOf('/');
        Folders = slash < 0 ? string.Empty : path[..(slash + 1)];
        Name = path[(slash + 1)..];
        IsOpen = startsOpen;

        // the list is told of a change the person makes, not of how the line starts
        _toggled = toggled;
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SpokenName))]
    public partial bool IsOpen { get; set; }

    public string Path { get; }

    /// <summary>
    /// The folders of the path with the last separator: they stand in the quiet code type.
    /// </summary>
    public string Folders { get; }

    /// <summary>
    /// The file's name: it stands in the strong code type.
    /// </summary>
    public string Name { get; }

    public IReadOnlyList<ExemptionRowViewModel> Rows { get; }

    /// <summary>
    /// How many exemptions the file holds in all.
    /// </summary>
    public int TotalInFile { get; }

    public bool IsPartial
    {
        get { return Rows.Count < TotalInFile; }
    }

    /// <summary>
    /// The number of the tally, in the strong type: how many of the file's exemptions are shown.
    /// </summary>
    public string TallyNumber
    {
        get { return Rows.Count.ToString(CultureInfo.InvariantCulture); }
    }

    /// <summary>
    /// What the number counts: "exemptions", "exemption", or "of 5 exemptions" while a filter shows only some of them.
    /// </summary>
    public string TallyWords
    {
        get
        {
            var word = TotalInFile == 1 ? "exemption" : "exemptions";
            return IsPartial ? $"of {TotalInFile.ToString(CultureInfo.InvariantCulture)} {word}" : word;
        }
    }

    public string SpokenName
    {
        get { return $"{Path}, {TallyNumber} {TallyWords}, {(IsOpen ? "expanded" : "folded")}"; }
    }

    [RelayCommand]
    private void Toggle()
    {
        IsOpen = !IsOpen;
    }

    partial void OnIsOpenChanged(bool value)
    {
        _toggled(this);
    }
}
