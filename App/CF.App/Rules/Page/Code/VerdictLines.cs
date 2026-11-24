using CF.Core.Rules;

namespace CF.App.Rules.Page.Code;

/// <summary>
/// Which lines of the code the verdicts of Try it touch: the lines of every artifact that breaks the rule, and the line each
/// artifact starts on. The band, the bar and the labels in the box are all drawn from this.
/// </summary>
internal sealed class VerdictLines
{
    private readonly HashSet<int> _breaking = new HashSet<int>();
    private readonly Dictionary<int, ArtifactVerdict> _starts = new Dictionary<int, ArtifactVerdict>();

    public void Set(IReadOnlyList<ArtifactVerdict> verdicts)
    {
        _breaking.Clear();
        _starts.Clear();
        foreach(var verdict in verdicts)
        {
            _starts.TryAdd(verdict.Span.StartLine, verdict);
            if(verdict.Outcome != TryOutcome.BreaksIt)
                continue;

            for(var line = verdict.Span.StartLine; line <= verdict.Span.EndLine; line++)
                _breaking.Add(line);
        }
    }

    public bool IsBreaking(int line)
    {
        return _breaking.Contains(line);
    }

    public ArtifactVerdict? StartingAt(int line)
    {
        return _starts.GetValueOrDefault(line);
    }
}
