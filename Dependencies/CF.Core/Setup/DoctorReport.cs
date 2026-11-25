namespace CF.Core.Setup;

/// <summary>The readiness verdict, its evidence, and when the diagnosis ran.</summary>
/// <param name="Checks">Results in display order.</param>
/// <param name="Machine">Facts about the current machine.</param>
/// <param name="CheckedAt">When the diagnosis finished.</param>
/// <param name="Duration">Time from start to finish.</param>
/// <param name="Heading">The verdict heading.</param>
/// <param name="Verdict">What the results mean for a push.</param>
public sealed record DoctorReport(IReadOnlyList<DoctorCheckResult> Checks, DoctorMachineFacts Machine, DateTimeOffset CheckedAt, TimeSpan Duration, string Heading, DoctorText Verdict)
{
    /// <summary>Whether all checks passed.</summary>
    public bool Ready
    {
        get { return Checks.All(check => check.Outcome == DoctorOutcome.Passes); }
    }
}
