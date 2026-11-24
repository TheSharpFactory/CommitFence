namespace CF.Core.Review;

/// <summary>
/// What an exception says, and never nothing.
/// </summary>
internal static class FailureText
{
    /// <summary>
    /// The message of the exception. An exception that gave no message is named by its type, and by the message of the
    /// exception that caused it.
    /// </summary>
    public static string Describe(Exception exception)
    {
        if(!string.IsNullOrWhiteSpace(exception.Message))
            return exception.Message;

        var cause = exception.InnerException == null || string.IsNullOrWhiteSpace(exception.InnerException.Message) ? string.Empty : ": " + exception.InnerException.Message;
        return $"{exception.GetType().Name} gave no message{cause}";
    }
}