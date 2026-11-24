namespace CF.Core.Review;

/// <summary>
/// Turns the outcome of a review into the exit code of the program.
/// </summary>
public static class ReviewOutcomeExtensions
{
    /// <summary>
    /// Clean and warned pass, blocked is 1, incomplete is 2.
    /// </summary>
    public static ExitCode ToExitCode(this ReviewOutcome outcome)
    {
        switch(outcome)
        {
            case ReviewOutcome.Clean:
            case ReviewOutcome.Warned:
                return ExitCode.Passed;
            case ReviewOutcome.Blocked:
                return ExitCode.Blocked;
            case ReviewOutcome.Incomplete:
                return ExitCode.Incomplete;
            default:
                throw new ArgumentOutOfRangeException(nameof(outcome), outcome, "Unknown review outcome.");
        }
    }
}