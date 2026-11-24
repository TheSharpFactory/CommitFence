namespace CF.App.Rules.Page;

/// <summary>
/// The rule as the page holds it at one moment, ready to be run on any code: what the page hands to Try it, which keeps it
/// until the sentence changes.
/// </summary>
internal sealed class TryPlan
{
    private readonly Func<string, TryAnswer> _run;

    public TryPlan(string key, Func<string, TryAnswer> run)
    {
        Key = key;
        _run = run;
    }

    /// <summary>
    /// Tells two plans of the same sentence apart from two of different sentences.
    /// </summary>
    public string Key { get; }

    public TryAnswer Run(string code)
    {
        return _run(code);
    }
}
