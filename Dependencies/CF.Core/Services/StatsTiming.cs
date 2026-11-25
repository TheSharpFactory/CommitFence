using System.Globalization;

namespace CF.Core.Services;

/// <summary>A review's time and size, in the local calendar.</summary>
public sealed record StatsReviewTime(DateTimeOffset EndedAt, long Milliseconds, int FilesReviewed)
{
    /// <summary>The slowest review's date, time and file count.</summary>
    public string Details()
    {
        return $"{EndedAt.ToString("ddd d MMM, HH:mm", CultureInfo.InvariantCulture)}, {FilesReviewed} {(FilesReviewed == 1 ? "file" : "files")}";
    }
}

/// <summary>One band of review durations and its accompanying sentence.</summary>
public sealed record StatsTimeBand(string Label, int Count, string Note);

/// <summary>The median full review, the longest review and four bands for the whole period.</summary>
public sealed record StatsTiming(double? TypicalMilliseconds, StatsReviewTime? Slowest, IReadOnlyList<StatsTimeBand> Bands)
{
    /// <summary>Formats a duration consistently on the Dashboard and the command line.</summary>
    public static string Seconds(double milliseconds)
    {
        return (milliseconds / 1000).ToString("0.#", CultureInfo.InvariantCulture) + " s";
    }
}
