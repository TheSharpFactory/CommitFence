using CF.Core.Rules;

namespace CF.App.Rules;

/// <summary>
/// What trying a rule on code answered: the result, or the message of the refusal that saving the same rule would have given.
/// </summary>
internal sealed class TryAnswer
{
    private TryAnswer(RuleTryResult? result, string? refusal)
    {
        Result = result;
        Refusal = refusal;
    }

    public RuleTryResult? Result { get; }

    public string? Refusal { get; }

    public static TryAnswer Of(RuleTryResult result)
    {
        return new TryAnswer(result, null);
    }

    public static TryAnswer Refused(string message)
    {
        return new TryAnswer(null, message);
    }
}
