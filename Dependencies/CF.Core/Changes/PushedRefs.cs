namespace CF.Core.Changes;

/// <summary>
/// The ref lines git gave a pre-push hook, or the reason they cannot be trusted.
/// </summary>
public sealed class PushedRefs
{
    private PushedRefs(IReadOnlyList<PushedRef> refs, string? problem)
    {
        Refs = refs;
        Problem = problem;
    }

    /// <summary>
    /// No lines at all, which is what git gives when a push is already up to date.
    /// </summary>
    public static PushedRefs None { get; } = new PushedRefs(new List<PushedRef>(), null);

    /// <summary>
    /// The refs, in the order git gave them.
    /// </summary>
    public IReadOnlyList<PushedRef> Refs { get; }

    /// <summary>
    /// Why the input cannot be trusted, such as a line that is cut off; nothing when it can.
    /// </summary>
    public string? Problem { get; }

    /// <summary>
    /// The refs read from well-formed input.
    /// </summary>
    public static PushedRefs FromRefs(IReadOnlyList<PushedRef> refs)
    {
        return new PushedRefs(refs, null);
    }

    /// <summary>
    /// Input that cannot be trusted, with the reason.
    /// </summary>
    public static PushedRefs Broken(string problem)
    {
        return new PushedRefs(new List<PushedRef>(), problem);
    }
}