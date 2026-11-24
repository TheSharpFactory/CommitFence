namespace CF.Core.Code;

/// <summary>
/// Everything in a file that an artifact's properties were read from, so that a change to any of it counts as touching
/// the artifact.
/// </summary>
/// <remarks>
/// The artifact is shown where it is; its reach can be wider.
/// </remarks>
public sealed class ArtifactReach
{
    /// <summary>
    /// Creates a reach from the artifact's own span, the other spans its properties depend on and its documentation
    /// zone.
    /// </summary>
    public ArtifactReach(SourceSpan own, IReadOnlyList<SourceSpan> dependsOn, SourceSpan? documentation)
    {
        Own = own;
        DependsOn = dependsOn;
        Documentation = documentation;
    }

    /// <summary>
    /// The span of the artifact itself, its attributes and its body included.
    /// </summary>
    public SourceSpan Own { get; }

    /// <summary>
    /// Spans outside the artifact that some of its properties were read from, such as the header of the type it is
    /// declared in.
    /// </summary>
    public IReadOnlyList<SourceSpan> DependsOn { get; }

    /// <summary>
    /// The lines of the documentation comment above the artifact.
    /// </summary>
    /// <remarks>
    /// When it has none, an empty span (its last line is before its first) that marks the place directly above the
    /// artifact, so that deleting the whole comment is seen. Nothing when the artifact has no documentation property.
    /// </remarks>
    public SourceSpan? Documentation { get; }

    /// <summary>
    /// A reach that is the artifact's own span and nothing else.
    /// </summary>
    public static ArtifactReach OfSpan(SourceSpan own)
    {
        return new ArtifactReach(own, new List<SourceSpan>(), null);
    }
}