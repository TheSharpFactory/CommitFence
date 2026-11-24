namespace CF.Languages.CSharp.Meaning;

/// <summary>
/// The facts of the meaning analyzer in a compiler log cannot be trusted: the closing entry is missing or does not
/// match, or a fact is not written as it should be. The message says which.
/// </summary>
internal sealed class MeaningLogException : Exception
{
    public MeaningLogException(string message)
        : base(message)
    {
    }
}