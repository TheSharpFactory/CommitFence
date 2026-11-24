using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using CF.Core.Rules;
using AvaloniaEdit.Rendering;

namespace CF.App.Rules.Page.Code;

/// <summary>
/// Puts one label after the last character of the first line of every artifact the rule was tried on, 24 from it: "Breaks the
/// rule", "Follows it" or "Not covered", with the reason when there is one. A label that is new, or has changed kind, grows
/// in (motion 12); one that was there before is drawn as it was, so typing does not set it moving again. The editor centres an
/// inline object on its row and makes the row taller when the object is taller than the room, so the label stands in a slot 4
/// high and, centred on it, is drawn over the row's 20 without making the row taller.
/// </summary>
internal sealed class ArtifactPillGenerator : VisualLineElementGenerator
{
    private const double _gap = 24;
    private const double _slotHeight = 4;

    private readonly VerdictLines _lines;
    private readonly Func<string, string> _inPageWords;
    private readonly HashSet<string> _seen = new HashSet<string>(StringComparer.Ordinal);
    private readonly HashSet<string> _fresh = new HashSet<string>(StringComparer.Ordinal);

    public ArtifactPillGenerator(VerdictLines lines, Func<string, string> inPageWords)
    {
        _lines = lines;
        _inPageWords = inPageWords;
    }

    /// <summary>
    /// Takes in the verdicts just shown: the labels that were not there before are the ones that will grow in.
    /// </summary>
    public void Shown(IReadOnlyList<ArtifactVerdict> verdicts)
    {
        var now = verdicts.Select(KeyOf).ToHashSet(StringComparer.Ordinal);
        _fresh.Clear();
        _fresh.UnionWith(now.Where(key => !_seen.Contains(key)));
        _seen.Clear();
        _seen.UnionWith(now);
    }

    public override int GetFirstInterestedOffset(int startOffset)
    {
        var line = CurrentContext.Document.GetLineByOffset(startOffset);
        return _lines.StartingAt(line.LineNumber) != null && line.EndOffset >= startOffset ? line.EndOffset : -1;
    }

    public override VisualLineElement? ConstructElement(int offset)
    {
        var line = CurrentContext.Document.GetLineByOffset(offset);
        var verdict = _lines.StartingAt(line.LineNumber);
        if(verdict == null)
            return null;

        var label = new TryLabel
        {
            Kind = KindOf(verdict.Outcome),
            Word = WordOf(verdict.Outcome),
            Reason = verdict.Outcome == TryOutcome.FollowsIt ? string.Empty : FirstClause(_inPageWords(verdict.Reason)),
            InCode = true,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(_gap, 0, 0, 0)
        };
        var spoken = $"{WordOf(verdict.Outcome)}: {_inPageWords(verdict.Reason)}";
        ToolTip.SetTip(label, spoken);
        AutomationProperties.SetName(label, spoken);
        if(_fresh.Remove(KeyOf(verdict)))
            label.Appear();

        return new InlineObjectElement(0, new Border { Height = _slotHeight, Child = label });
    }

    /// <summary>
    /// The body's reason is a whole sentence ("braces is true, the rule requires false"); inside the code the label gives its
    /// first clause, so that it fits beside the line, and the whole reason is in the tooltip and the label's name.
    /// </summary>
    private static string FirstClause(string reason)
    {
        var comma = reason.IndexOf(',', StringComparison.Ordinal);
        return comma < 0 ? reason : reason[..comma];
    }

    private static string KeyOf(ArtifactVerdict verdict)
    {
        return $"{verdict.Span.StartLine}|{verdict.Outcome}|{verdict.Reason}";
    }

    private static TryKind KindOf(TryOutcome outcome)
    {
        switch(outcome)
        {
            case TryOutcome.BreaksIt:
                return TryKind.Breaks;
            case TryOutcome.FollowsIt:
                return TryKind.Follows;
            default:
                return TryKind.NotCovered;
        }
    }

    private static string WordOf(TryOutcome outcome)
    {
        switch(outcome)
        {
            case TryOutcome.BreaksIt:
                return "Breaks the rule";
            case TryOutcome.FollowsIt:
                return "Follows it";
            default:
                return "Not covered";
        }
    }
}
