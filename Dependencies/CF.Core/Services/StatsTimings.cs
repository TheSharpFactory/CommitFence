namespace CF.Core.Services;

/// <summary>Groups reviews by elapsed time and describes the median and the slow tail.</summary>
internal static class StatsTimings
{
    public static StatsTiming From(IReadOnlyList<(StatsReviewTime Time, bool Full)> reviews)
    {
        var full = reviews.Where(review => review.Full).Select(review => review.Time.Milliseconds).Order().ToList();
        var typical = Median(full);
        var slowest = reviews.Count == 0 ? null : reviews.MaxBy(review => (review.Time.Milliseconds, review.Time.EndedAt)).Time;
        var byBand = reviews.ToLookup(review => BandOf(review.Time.Milliseconds), review => review.Time);
        var quick = reviews.Count(review => !review.Full && BandOf(review.Time.Milliseconds) == 0);
        var quickNote = quick == 0 ? string.Empty : $"{quick} of them quick {(quick == 1 ? "review" : "reviews")}, which build nothing";
        var bands = new List<StatsTimeBand>
        {
            new StatsTimeBand("Under 5 s", byBand[0].Count(), Notes(typical, 0, WithSlowest(quickNote, byBand[0], slowest))),
            new StatsTimeBand("5 to 10 s", byBand[1].Count(), Notes(typical, 1, WithSlowest(string.Empty, byBand[1], slowest))),
            new StatsTimeBand("10 to 20 s", byBand[2].Count(), Notes(typical, 2, Middle(byBand[2], slowest))),
            new StatsTimeBand("Over 20 s", byBand[3].Count(), Notes(typical, 3, Slow(byBand[3], slowest)))
        };
        return new StatsTiming(typical, slowest, bands);
    }

    private static double? Median(List<long> full)
    {
        if(full.Count == 0)
            return null;

        var middle = full.Count / 2;
        return full.Count % 2 == 0 ? (full[middle - 1] / 2.0) + (full[middle] / 2.0) : full[middle];
    }

    private static int BandOf(double milliseconds)
    {
        return milliseconds switch { < 5000 => 0, < 10000 => 1, <= 20000 => 2, _ => 3 };
    }

    private static string Notes(double? typical, int band, string detail)
    {
        if(typical is not { } median || BandOf(median) != band)
            return detail;

        var note = $"Typical: {StatsTiming.Seconds(median)}";
        return detail.Length == 0 ? note : detail + ". " + note;
    }

    private static string Middle(IEnumerable<StatsReviewTime> times, StatsReviewTime? slowest)
    {
        var ordered = times.OrderBy(time => time.Milliseconds).ToList();
        var seconds = ordered.Count > 3 ? [] : ordered.Where(time => !ReferenceEquals(time, slowest))
            .Select(time => StatsTiming.Seconds(time.Milliseconds)).ToList();
        var names = seconds.Count switch
        {
            1 => seconds[0],
            2 => $"{seconds[0]} and {seconds[1]}",
            3 => $"{seconds[0]}, {seconds[1]} and {seconds[2]}",
            _ => string.Empty
        };
        return WithSlowest(names, ordered, slowest);
    }

    private static string Slow(IEnumerable<StatsReviewTime> times, StatsReviewTime? slowest)
    {
        var slow = times.OrderBy(time => time.Milliseconds).ThenBy(time => time.EndedAt).ToList();
        var names = string.Join(", ", slow.Where(time => !ReferenceEquals(time, slowest)).Select(time => StatsTiming.Seconds(time.Milliseconds)));
        return WithSlowest(names, slow, slowest);
    }

    private static string WithSlowest(string names, IEnumerable<StatsReviewTime> band, StatsReviewTime? slowest)
    {
        if(slowest == null || !band.Any(time => ReferenceEquals(time, slowest)))
            return names;

        var details = $"slowest: {StatsTiming.Seconds(slowest.Milliseconds)}, {slowest.Details()}";
        return names.Length == 0 ? "The " + details : names + ", and the " + details;
    }
}
