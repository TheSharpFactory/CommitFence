using System.Globalization;
using CF.Core.Review;

namespace CF.Core.Services;

/// <summary>The number of push reviews of each outcome on a local day.</summary>
public sealed record StatsDay(DateOnly Date, int Refused, int Incomplete, int Allowed)
{
    /// <summary>The number of reviews in the three groups.</summary>
    public int Total { get { return Refused + Incomplete + Allowed; } }

    /// <summary>The review count for the chosen day.</summary>
    public string ReviewsText { get { return Total == 0 ? "No review" : $"{Total} {(Total == 1 ? "review" : "reviews")}"; } }

    /// <summary>The date for the chosen day.</summary>
    public string Label(bool today)
    {
        return today ? "Today, " + Date.ToString("d MMM", CultureInfo.InvariantCulture) :
            Date.ToString("dddd d MMM", CultureInfo.InvariantCulture);
    }

    /// <summary>The counts with their labels, in chart order.</summary>
    public string CountsText()
    {
        return string.Join(", ", Counts().Select(piece => $"{piece.Count} {piece.Label}"));
    }

    /// <summary>The groups with reviews and the mark shown for each.</summary>
    public IReadOnlyList<StatsReviewCount> Counts()
    {
        return new List<StatsReviewCount>
        {
            new StatsReviewCount(ReviewOutcome.Blocked, Refused, "blocked"),
            new StatsReviewCount(ReviewOutcome.Incomplete, Incomplete, "incomplete"),
            new StatsReviewCount(ReviewOutcome.Clean, Allowed, "allowed")
        }.Where(piece => piece.Count > 0).ToList();
    }
}

/// <summary>One review outcome group and the words shown for it.</summary>
public sealed record StatsReviewCount(ReviewOutcome Kind, int Count, string Label);
