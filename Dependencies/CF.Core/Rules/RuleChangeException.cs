namespace CF.Core.Rules;

/// <summary>
/// A change to the rules is refused, or a question about them cannot be answered, for a reason a person can read.
/// Nothing was written.
/// </summary>
public sealed class RuleChangeException : Exception
{
    /// <summary>
    /// Creates the exception with the plain reason.
    /// </summary>
    public RuleChangeException(string message)
        : base(message)
    {
    }
}