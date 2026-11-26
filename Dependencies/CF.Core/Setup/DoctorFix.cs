namespace CF.Core.Setup;

/// <summary>What a person can do about a failed check.</summary>
/// <param name="Text">The instructions to show.</param>
/// <param name="Actions">Optional actions to offer, in order.</param>
/// <param name="Excerpt">The related file and lines, when available.</param>
/// <param name="AfterMove">The same excerpt with the hook line moved, when available.</param>
public sealed record DoctorFix(DoctorText Text, IReadOnlyList<DoctorAction> Actions, DoctorExcerpt? Excerpt = null, DoctorExcerpt? AfterMove = null)
{
    /// <summary>The label for the original excerpt when a preview follows it.</summary>
    public const string NowHeading = "Now";

    /// <summary>The label for the preview of the moved hook line.</summary>
    public const string AfterMoveHeading = "After the move";
}
