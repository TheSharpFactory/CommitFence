using CF.Core.Rules;

namespace CF.App.Rules;

/// <summary>
/// What trying a rule on code answered: the result, or a message explaining why the trial was refused.
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
        return result.Status == TryStatus.NeedsFileName ? Refused(result.Reason) : new TryAnswer(result, null);
    }

    public static TryAnswer Refused(string message)
    {
        return new TryAnswer(null, message);
    }
}
