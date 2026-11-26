using CF.Core.Services;

namespace CF.App.Dashboard;

/// <summary>The text and bar of a band of time.</summary>
internal sealed record DashboardTimeBand(string Label, int Count, double Share)
{
    public bool HasBar { get { return Count > 0; } }
    public string SpokenName { get { return $"{Label}, {Count} {(Count == 1 ? "review" : "reviews")}"; } }

    public static IReadOnlyList<DashboardTimeBand> Of(StatsTiming timing)
    {
        var maximum = timing.Bands.Select(band => band.Count).DefaultIfEmpty(0).Max();
        return timing.Bands.Select(band => new DashboardTimeBand(band.Label, band.Count,
            maximum == 0 ? 0 : (double)band.Count / maximum)).ToList();
    }

    public static string Summary(StatsTiming timing)
    {
        var times = new List<string>();
        if(timing.TypicalMilliseconds is { } typical)
            times.Add($"**{StatsTiming.Seconds(typical)}** typical");

        if(timing.Slowest is { } slowest)
            times.Add($"**{StatsTiming.Seconds(slowest.Milliseconds)}** slowest");

        return string.Join(", ", times);
    }

    public static string Slowest(StatsTiming timing)
    {
        return timing.Slowest is { } review ? $"The slowest: **{StatsTiming.Seconds(review.Milliseconds)}**, {review.Details()}" : string.Empty;
    }
}
