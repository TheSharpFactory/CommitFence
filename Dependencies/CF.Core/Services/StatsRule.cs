using CF.Core.Findings;
using CF.Core.Structure;

namespace CF.Core.Services;

/// <summary>A current rule and how it affects a push.</summary>
public sealed record StatsRule(string Id, string? Title, bool FromFile, Severity? Effect)
{
    /// <summary>The finding for a use that breaks a module direction.</summary>
    public const string LayerDirectionId = StructureRuleInfo.LayerDirectionId;

    /// <summary>The finding for an exemption without a reason.</summary>
    public const string MissingExemptionReasonId = "ALLOW001";
}

/// <summary>The period's findings for a rule, with its current description if it still exists.</summary>
public sealed record StatsFindingCount(string Id, long Count, StatsRule? Rule);
