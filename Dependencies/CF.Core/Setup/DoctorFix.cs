namespace CF.Core.Setup;

/// <summary>What a person can do about a failed check.</summary>
/// <param name="Text">The instructions to show.</param>
/// <param name="Actions">Optional actions to offer, in order.</param>
/// <param name="Excerpt">The related file and lines, when available.</param>
public sealed record DoctorFix(DoctorText Text, IReadOnlyList<DoctorAction> Actions, DoctorExcerpt? Excerpt = null);
