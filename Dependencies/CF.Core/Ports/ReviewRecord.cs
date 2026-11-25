using System.Text.Json.Serialization;

namespace CF.Core.Ports;

/// <summary>The facts kept from a completed review of a push.</summary>
public sealed record ReviewRecord(
    [property: JsonRequired] int Format,
    [property: JsonRequired] DateTimeOffset EndedAt,
    [property: JsonRequired] ReviewSource Source,
    [property: JsonRequired] RecordedReviewOutcome Outcome,
    [property: JsonRequired] long DurationMilliseconds,
    [property: JsonRequired] bool Full,
    [property: JsonRequired] int FilesReviewed,
    [property: JsonRequired] IReadOnlyList<RecordedRuleCount> Findings)
{
    /// <summary>The format this version writes.</summary>
    public const int CurrentFormat = 1;
}

/// <summary>How a push review ended, as kept in the history format.</summary>
public enum RecordedReviewOutcome
{
    /// <summary>The review found nothing.</summary>
    Clean,

    /// <summary>The review found warnings.</summary>
    Warned,

    /// <summary>The review refused the push.</summary>
    Blocked,

    /// <summary>The review could not finish.</summary>
    Incomplete
}

/// <summary>The number of findings recorded for one rule.</summary>
public sealed record RecordedRuleCount(string RuleId, int Count);
