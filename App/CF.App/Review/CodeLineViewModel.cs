namespace CF.App.Review;

/// <summary>
/// One line of the code around a finding: its number, its coloured runs and whether it is the line that breaks the rule.
/// </summary>
internal sealed class CodeLineViewModel
{
    public CodeLineViewModel(int number, IReadOnlyList<CodeSegment> segments, bool isMarked, bool isBlock)
    {
        Number = number;
        Segments = segments;
        IsBreaking = isMarked;
        IsBlocking = isMarked && isBlock;
        IsWarning = isMarked && !isBlock;
    }

    public int Number { get; }

    /// <summary>
    /// Whether the box shows a second number, the line's number on the copy on disk.
    /// </summary>
    public bool TwoNumbers { get; init; }

    /// <summary>
    /// The number of the line on the copy on disk; nothing for a line that was not matched there (it may have moved or gone).
    /// </summary>
    public int? DiskNumber { get; init; }

    public bool OneNumber
    {
        get { return !TwoNumbers; }
    }

    /// <summary>
    /// The word over the first number column ("Pushed", "Staged" or "Committed"): which copy the number is of.
    /// </summary>
    public string NumberHead { get; init; } = string.Empty;

    /// <summary>
    /// What a screen reader says for the numbers of the line when the box shows two: "Pushed line 24, on disk line 31"; for the line
    /// that holds the finding it says what the finding is: "Blocking finding at staged line 24, on disk line 31", "Warning at pushed
    /// line 5, on disk line 7".
    /// </summary>
    public string SpokenNumbers
    {
        get
        {
            var onDisk = DiskNumber == null ? "not matched on disk" : $"on disk line {DiskNumber}";
            var kind = IsBlocking ? "Blocking finding at " : IsWarning ? "Warning at " : string.Empty;
            var head = kind.Length > 0 ? NumberHead.ToLowerInvariant() : NumberHead;
            return $"{kind}{head} line {Number}, {onDisk}";
        }
    }

    public string DiskNumberText
    {
        get { return DiskNumber?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty; }
    }

    public IReadOnlyList<CodeSegment> Segments { get; }

    public bool IsBreaking { get; }

    public bool IsBlocking { get; }

    public bool IsWarning { get; }
}
