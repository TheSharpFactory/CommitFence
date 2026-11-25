namespace CF.App.Settings;

/// <summary>
/// A row of the strictness table: a word the file uses and its cell under each column.
/// </summary>
internal sealed class StrictnessRowViewModel
{
    public StrictnessRowViewModel(string word, IReadOnlyList<StrictnessCellViewModel> cells)
    {
        Word = word;
        Cells = cells;
    }

    public string Word { get; }

    public IReadOnlyList<StrictnessCellViewModel> Cells { get; }

    public string SpokenName
    {
        get { return $"{Word}: " + string.Join(", ", Cells.Select(cell => cell.Word)); }
    }
}
