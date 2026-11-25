namespace CF.Core.Services;

/// <summary>Reads a repository's local push review history and its current rule descriptions.</summary>
public interface IStatsService
{
    /// <summary>The last 30 local days, or null outside a repository.</summary>
    StatsReport? Read(string folder);
}
