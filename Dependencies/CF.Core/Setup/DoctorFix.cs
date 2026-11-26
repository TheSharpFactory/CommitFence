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

    /// <summary>The label for the preview of an inserted hook line.</summary>
    public const string AfterAddingHeading = "After adding";

    /// <summary>The insertion offered by Doctor, when the script can be edited safely.</summary>
    public HookAddition? Addition { get; init; }

    /// <summary>The caption for configuration to copy, when offered.</summary>
    public DoctorText? EntryHeading { get; init; }

    /// <summary>How to merge the entry into an existing configuration.</summary>
    public DoctorText? EntryHint { get; init; }
}
