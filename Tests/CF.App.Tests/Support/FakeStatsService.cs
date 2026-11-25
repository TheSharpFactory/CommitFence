using CF.Core.Services;

namespace CF.App.Tests.Support;

/// <summary>An in-memory statistics report for a headless Dashboard.</summary>
internal sealed class FakeStatsService : IStatsService
{
    public StatsReport? Report { get; set; }

    public StatsReport? Read(string folder)
    {
        return Report;
    }
}
