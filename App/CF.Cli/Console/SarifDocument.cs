using System.Text.Json.Serialization;

namespace CF.Cli.Console;

/// <summary>The SARIF 2.1.0 log of one review: only the parts a review fills.</summary>
internal sealed record SarifLog(
    [property: JsonPropertyName("$schema")] string Schema,
    string Version,
    IReadOnlyList<SarifRun> Runs);

internal sealed record SarifRun(
    SarifTool Tool,
    IReadOnlyList<SarifInvocation> Invocations,
    IReadOnlyList<SarifResult> Results,
    SarifRunProperties Properties,
    IReadOnlyList<SarifVersionControl>? VersionControlProvenance);

/// <summary>The repository and the commit that was reviewed, which a code host needs to place an upload.</summary>
internal sealed record SarifVersionControl(string RepositoryUri, string RevisionId);

internal sealed record SarifTool(SarifDriver Driver);

internal sealed record SarifDriver(string Name, string Version, IReadOnlyList<SarifRule> Rules);

internal sealed record SarifRule(string Id, SarifText ShortDescription, SarifRuleProperties Properties);

internal sealed record SarifRuleProperties([property: JsonPropertyName("commitfence/source")] string Source);

internal sealed record SarifText(string Text);

internal sealed record SarifInvocation(
    bool ExecutionSuccessful,
    int ExitCode,
    IReadOnlyList<SarifNotification>? ToolExecutionNotifications);

internal sealed record SarifNotification(string Level, SarifText Message);

internal sealed record SarifResult(
    string RuleId,
    int RuleIndex,
    string Level,
    SarifText Message,
    IReadOnlyList<SarifLocation>? Locations,
    IReadOnlyList<SarifSuppression>? Suppressions);

internal sealed record SarifLocation(SarifPhysicalLocation PhysicalLocation);

internal sealed record SarifPhysicalLocation(SarifArtifactLocation ArtifactLocation, SarifRegion? Region);

internal sealed record SarifArtifactLocation(string Uri, string UriBaseId);

internal sealed record SarifRegion(int StartLine, int? StartColumn);

internal sealed record SarifSuppression(string Kind, string Status, string Justification, SarifLocation? Location);

/// <summary>What a review says that SARIF has no place for, namespaced so no other tool's property is taken.</summary>
internal sealed record SarifRunProperties(
    [property: JsonPropertyName("commitfence/outcome")] string Outcome,
    [property: JsonPropertyName("commitfence/exitCode")] int ExitCode,
    [property: JsonPropertyName("commitfence/quick")] bool Quick,
    [property: JsonPropertyName("commitfence/commit")] string? Commit,
    [property: JsonPropertyName("commitfence/treeReviewed")] string? TreeReviewed,
    [property: JsonPropertyName("commitfence/baseTreeReviewed")] string? BaseTreeReviewed,
    [property: JsonPropertyName("commitfence/leavesOutUncommittedWork")] bool LeavesOutUncommittedWork,
    [property: JsonPropertyName("commitfence/summary")] string Summary,
    [property: JsonPropertyName("commitfence/notRun")] IReadOnlyList<string> NotRun,
    [property: JsonPropertyName("commitfence/coverage")] SarifCoverage Coverage);

/// <summary>How much was judged: the count of what was reviewed, then every entry that was not, or that is the build.</summary>
internal sealed record SarifCoverage(int Reviewed, IReadOnlyList<SarifCoverageEntry> Entries);

internal sealed record SarifCoverageEntry(string What, string Kind, string Status, string Reason);
