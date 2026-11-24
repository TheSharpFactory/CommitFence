namespace CF.App.Review;

/// <summary>
/// One line of the code around a finding: its number, its coloured runs and whether it is the line that breaks the rule.
/// </summary>
internal sealed class CodeLineViewModel
{
    public CodeLineViewModel(int number, IReadOnlyList<CodeSegment> segments, bool isBreaking)
    {
        Number = number;
        Segments = segments;
        IsBreaking = isBreaking;
    }

    public int Number { get; }

    public IReadOnlyList<CodeSegment> Segments { get; }

    public bool IsBreaking { get; }
}
