using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CF.App.Review;

/// <summary>
/// The files of a review with no findings, folded into one count in the panel: the count is a disclosure, and what it opens is
/// the list of those files with a filter over it.
/// </summary>
internal sealed partial class CleanFilesViewModel : ObservableObject
{
    public CleanFilesViewModel(IReadOnlyList<string> files, bool withOthers)
    {
        Rows = files.Select(path => new PanelFileViewModel(path, null, null, null)).ToList();
        VisibleRows = Rows;
        CountText = files.Count.ToString(CultureInfo.CurrentCulture);
        CountSuffix = (withOthers, files.Count == 1) switch
        {
            (true, true) => " more file, clean",
            (true, false) => " more files, all clean",
            (false, true) => " file, clean",
            _ => " files, all clean"
        };
        ListName = files.Count == 1 ? "1 clean file" : $"{files.Count} clean files";
        FilterPlaceholder = files.Count == 1 ? "Filter 1 file" : $"Filter {files.Count} files";
    }

    public static string ScopeName(ReviewKind kind, string push = "the push")
    {
        return kind switch
        {
            ReviewKind.Staged => "the staged changes",
            ReviewKind.Folder => "the folder",
            ReviewKind.Push => push,
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
    }

    /// <summary>
    /// Every clean file, by path.
    /// </summary>
    public IReadOnlyList<PanelFileViewModel> Rows { get; }

    public string CountText { get; }

    public string CountSuffix { get; }

    public string Name
    {
        get { return CountText + CountSuffix; }
    }

    public string ListName { get; }

    public string FilterName { get; } = "Filter the clean files";

    public string FilterPlaceholder { get; }

    [ObservableProperty]
    public partial bool IsExpanded { get; set; }

    /// <summary>
    /// The query over the paths; a file matches when its path holds it anywhere, whatever the case.
    /// </summary>
    [ObservableProperty]
    public partial string FilterText { get; set; } = string.Empty;

    /// <summary>
    /// The files that match the query: all of them while it is empty.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FilterCountText), nameof(ClosingText), nameof(HasRowsShown))]
    public partial IReadOnlyList<PanelFileViewModel> VisibleRows { get; set; } = [];

    /// <summary>
    /// Whether the list has more files than the panel can show. The view sets it from the room it has; the filter shows only then,
    /// or while a query is typed.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FilterShown))]
    public partial bool ListIsLong { get; set; }

    public bool FilterShown
    {
        get { return ListIsLong || FilterText.Length > 0; }
    }

    public bool HasRowsShown
    {
        get { return VisibleRows.Count > 0; }
    }

    /// <summary>
    /// "6 of 60" at the right of the filter while a query is typed, nothing otherwise.
    /// </summary>
    public string FilterCountText
    {
        get { return FilterText.Length == 0 ? string.Empty : $"{VisibleRows.Count} of {Rows.Count}"; }
    }

    /// <summary>
    /// The line that ends the open list: what it holds, how many match, or that nothing matches.
    /// </summary>
    public string ClosingText
    {
        get
        {
            if(FilterText.Length == 0)
                return Rows.Count == 1 ? "The clean file is listed." : $"All {Rows.Count} clean files are listed.";

            return VisibleRows.Count == 0 ? $"No file matches “{FilterText}”." : $"{VisibleRows.Count} of {Rows.Count} clean files match.";
        }
    }

    partial void OnFilterTextChanged(string value)
    {
        var query = value.Trim();
        VisibleRows = query.Length == 0 ? Rows : Rows.Where(row => row.Path.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();
        OnPropertyChanged(nameof(FilterShown));
    }
}
