using CF.Core.Code;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CF.App.Rules.Page;

/// <summary>
/// One line of the file shown on the page of a file rule: its number, its text, and whether it is the line that sets the rule.
/// The page holds a fixed number of these and gives each the next rule's lines, so that choosing another rule changes what the
/// lines say and builds no controls.
/// </summary>
internal sealed partial class FileLineViewModel : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SpokenName))]
    public partial int Number { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsComment), nameof(SpokenName))]
    public partial string Text { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SpokenName))]
    public partial bool IsCurrent { get; set; }

    /// <summary>
    /// Whether the place holds a line: a rule near the start or the end of its file has fewer lines than places.
    /// </summary>
    [ObservableProperty]
    public partial bool IsPresent { get; set; }

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
            return $"Line {Number}: {Text}{(IsCurrent ? ". This line sets the rule." : string.Empty)}";
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
