using CF.App.Review;
using CF.Core.Code;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CF.App.Parts;

/// <summary>
/// One line of a file as the lines of a file show it: its number, its text (or its coloured runs), and whether it is the line in
/// force, the one the box is about. A line may be a place that holds none, or a note such as "3 lines not shown".
/// </summary>
internal sealed partial class FileLineViewModel : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SpokenName))]
    public partial int Number { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsComment), nameof(SpokenName))]
    public partial string Text { get; set; } = string.Empty;

    /// <summary>
    /// The line in coloured runs, when the file is code that has colours; none for a plain line.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsColored))]
    public partial IReadOnlyList<CodeSegment>? Segments { get; set; }

    public bool IsColored
    {
        get { return Segments != null; }
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SpokenName))]
    public partial bool IsCurrent { get; set; }

    public bool IsFinding { get; init; }

    public bool IsMoved { get; init; }

    /// <summary>
    /// Whether the place holds a line: a rule near the start or the end of its file has fewer lines than places.
    /// </summary>
    [ObservableProperty]
    public partial bool IsPresent { get; set; }

    public bool IsHeaderOrGap
    {
        get { return !HasNumber || Text.TrimStart().StartsWith('['); }
    }

    public bool HasNumber
    {
        get
        {
            return Number > 0;
        }
    }

    public bool IsComment
    {
        get
        {
            var trimmed = Text.TrimStart();
            return trimmed.StartsWith('#') || trimmed.StartsWith(';');
        }
    }

    public string SpokenName
    {
        get
        {
            var mark = (IsCurrent, IsFinding, IsMoved) switch
            {
                (true, _, _) => ". This line sets the rule.",
                (_, true, _) => ". The finding points to this line.",
                (_, _, true) => ". The hook line moves here.",
                _ => string.Empty
            };
            return $"Line {Number}: {Text}{mark}";
        }
    }

    /// <summary>
    /// Takes this line of the file, or leaves the place empty.
    /// </summary>
    public void Take(FileLine? line, bool isCurrent)
    {
        IsPresent = line != null;
        Number = line?.Number ?? 0;
        Text = line?.Text ?? string.Empty;
        IsCurrent = isCurrent;
    }
}