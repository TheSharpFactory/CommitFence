using CF.Core.Services;

namespace CF.App.Dashboard;

/// <summary>The text and bar of a band of time.</summary>
internal sealed record DashboardTimeBand(string Label, int Count, double Width, string Note)
{
    public bool HasBar { get { return Count > 0; } }

    public static IReadOnlyList<DashboardTimeBand> Of(StatsTiming timing)
    {
        var maximum = timing.Bands.Max(band => band.Count);
        return timing.Bands.Select(band => new DashboardTimeBand(band.Label, band.Count,
            band.Count == 0 ? 0 : Math.Max(6, 300.0 * band.Count / maximum), Emphasize(band.Note, timing))).ToList();
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

    private static string Emphasize(string note, StatsTiming timing)
    {
        if(timing.TypicalMilliseconds is { } typical)
        {
            var seconds = StatsTiming.Seconds(typical);
            note = note.Replace("Typical: " + seconds, $"Typical: **{seconds}**", StringComparison.Ordinal);
        }

        if(timing.Slowest is { } slowest)
        {
            var seconds = StatsTiming.Seconds(slowest.Milliseconds);
            note = note.Replace($"slowest: {seconds}", $"slowest: **{seconds}**", StringComparison.Ordinal);
        }

        return note;
    }
}
