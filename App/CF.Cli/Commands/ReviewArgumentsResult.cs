namespace CF.Cli.Commands;

/// <summary>
/// What reading the arguments of review gave: the arguments, or the plain sentence that says what is wrong.
/// </summary>
internal sealed class ReviewArgumentsResult
{
    private ReviewArgumentsResult(ReviewArguments? arguments, string error)
    {
        Arguments = arguments;
        Error = error;
    }

    /// <summary>
    /// The arguments, or nothing when they are wrong.
    /// </summary>
    public ReviewArguments? Arguments { get; }

    /// <summary>
    /// What is wrong, or an empty text when nothing is.
    /// </summary>
    public string Error { get; }

    public static ReviewArgumentsResult Valid(ReviewArguments arguments)
    {
        return new ReviewArgumentsResult(arguments, string.Empty);
    }

    public static ReviewArgumentsResult Wrong(string error)
    {
        return new ReviewArgumentsResult(null, error);
    }
}