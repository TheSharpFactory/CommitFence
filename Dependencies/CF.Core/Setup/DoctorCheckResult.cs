namespace CF.Core.Setup;

/// <summary>The result of running one Doctor check.</summary>
/// <param name="Group">The heading above this check.</param>
/// <param name="Title">The check's title.</param>
/// <param name="Outcome">Whether the check ran and passed.</param>
/// <param name="Found">What the check found.</param>
/// <param name="Fix">Advice and actions for a failing check.</param>
/// <param name="Impact">The consequence used to choose the verdict when the check fails.</param>
/// <param name="SdkVersion">An SDK version found for the machine panel, if any.</param>
public sealed record DoctorCheckResult(string Group, string Title, DoctorOutcome Outcome, DoctorText Found, DoctorFix? Fix = null, DoctorImpact Impact = DoctorImpact.Review, string? SdkVersion = null);
