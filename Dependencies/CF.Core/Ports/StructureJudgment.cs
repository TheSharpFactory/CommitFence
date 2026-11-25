namespace CF.Core.Ports;

/// <summary>A completed judgment of layer directions, from a review of changes or of a folder.</summary>
/// <param name="EndedAt">When the review ended.</param>
/// <param name="Folder">The reviewed folder's name, or null for a review of changes.</param>
/// <param name="Breaches">The uses the review found that broke a layer direction.</param>
public sealed record StructureJudgment(DateTimeOffset EndedAt, string? Folder, IReadOnlyList<StructureBreach> Breaches);
