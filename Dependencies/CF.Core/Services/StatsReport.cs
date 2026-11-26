using System.Globalization;

namespace CF.Core.Services;

/// <summary>The Dashboard and stats command's shared view of a repository's push reviews.</summary>
public sealed record StatsReport(IReadOnlyList<StatsDay> Days, DateOnly? Oldest, int UnreadableLines, StatsTiming Timing,
    IReadOnlyList<StatsFindingCount> MostBroken, IReadOnlyList<StatsRule> NeverFired, int RulesInForce, string? ReadError = null)
{
    /// <summary>The explanation shown when there are no reviews.</summary>
    public const string EmptySentence = "This counts push reviews on this machine from the hook, the command line, and Review.";

    /// <summary>The heading shown when the history is empty.</summary>
    public const string EmptyHeading = "No reviews yet";

    /// <summary>
    /// The notice for a history that could not be read: in full when some of it was read, since the numbers may then leave reviews
    /// out, plain when none of it was. The reason is a clause of the sentence.
    /// </summary>
    public string? ReadErrorNotice
    {
        get
        {
            if(ReadError == null)
                return null;

            var reason = Clause(ReadError);
            return Oldest == null ? $"The history of reviews could not be read ({reason})." :
                $"The history of reviews could not be read in full ({reason}), so the numbers below may leave reviews out.";
        }
    }

    /// <summary>Totals for the chart.</summary>
    public StatsDay Totals
    {
        get { return new StatsDay(Days[^1].Date, Days.Sum(day => day.Refused), Days.Sum(day => day.Incomplete), Days.Sum(day => day.Allowed)); }
    }

    /// <summary>The date range shown in the screen head.</summary>
    public string Period
    {
        get
        {
            var first = Oldest is { } oldest && oldest > Days[0].Date && oldest <= Days[^1].Date ? oldest : Days[0].Date;
            return $"Reviews of pushes on this machine, {first.ToString("d MMM", CultureInfo.InvariantCulture)} to {Days[^1].Date.ToString("d MMM", CultureInfo.InvariantCulture)}";
        }
    }

    /// <summary>The explanation of the damaged lines excluded from the report.</summary>
    public string? UnreadableNotice
    {
        get
        {
            return UnreadableLines switch
            {
                0 => null,
                1 => "1 review in the history could not be read and is left out.",
                _ => $"{UnreadableLines} reviews in the history could not be read and are left out."
            };
        }
    }

    /// <summary>A reason as the end of a sentence's clause: its first letter in lower case and its final stop removed.</summary>
    private static string Clause(string reason)
    {
        var text = reason.Trim().TrimEnd('.');
        return text.Length == 0 ? text : char.ToLowerInvariant(text[0]) + text[1..];
    }

    /// <summary>How many active rules found nothing in the period.</summary>
    public string NeverFiredSentence
    {
        get
        {
            return NeverFired.Count == 0 ? "Every rule in force found something in 30 days." :
                $"{NeverFired.Count} of the {RulesInForce} rules in force found nothing in 30 days.";
        }
    }
}
