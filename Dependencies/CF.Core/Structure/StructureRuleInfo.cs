namespace CF.Core.Structure;

/// <summary>
/// What the structure rules are called in a review. The settings file gives modules and directions, not rule
/// identifiers, so every breach of the directions is reported under one fixed identifier.
/// </summary>
internal static class StructureRuleInfo
{
    public const string LayerDirectionId = "ARCH001";
}