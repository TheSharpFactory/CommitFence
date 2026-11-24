using CF.Core.Changes;
using CF.Core.Ports;
using CF.Core.Rules.Settings;

namespace CF.Core.Review;

/// <summary>
/// Where the review draws its failure boundaries: which exceptions belong to the whole review and so pass every
/// boundary, and which are failures of one part.
/// </summary>
/// <remarks>
/// A part that fails is recorded as not reviewed and the review goes on with the next part.
/// </remarks>
internal static class ReviewBoundary
{
    /// <summary>
    /// Whether the exception has its own meaning higher up, for the whole review: wrong settings, a scope that cannot
    /// be established, a lock that timed out.
    /// </summary>
    /// <remarks>
    /// A boundary around one part lets it pass unchanged.
    /// </remarks>
    public static bool PassesThrough(Exception exception)
    {
        return exception is SettingsException || exception is ScopeException || exception is TimeoutException;
    }

    /// <summary>
    /// Whether the exception is a failure the review expects: git, the disk or its permissions failed.
    /// </summary>
    public static bool IsExpected(Exception exception)
    {
        var cause = Unwrap(exception);
        return cause is GitException || cause is IOException || cause is UnauthorizedAccessException;
    }

    /// <summary>
    /// What the exception says. A failure nobody expected is named by its kind as well, so that it is not mistaken for
    /// an ordinary one.
    /// </summary>
    public static string MessageOf(Exception exception)
    {
        var cause = Unwrap(exception);
        return IsExpected(cause) ? cause.Message : cause.GetType().Name + ": " + cause.Message;
    }

    /// <summary>
    /// The first exception a parallel query wrapped in an aggregate (one for each project that failed, all alike); any
    /// other exception as it is.
    /// </summary>
    private static Exception Unwrap(Exception exception)
    {
        return exception is AggregateException aggregate && aggregate.InnerExceptions.Count > 0 ? aggregate.Flatten().InnerExceptions[0] : exception;
    }
}