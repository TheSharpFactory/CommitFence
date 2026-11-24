namespace CF.Core.Code;

/// <summary>
/// Where a language's meaning comes from: what the compiler knows and syntax does not.
/// </summary>
/// <remarks>
/// It answers for the tree before a push, from answers kept from an earlier review of the same tree or by building it,
/// and it turns the answers into the meaning properties of the artifacts the reader found.
/// </remarks>
public interface IMeaningSource
{
    /// <summary>
    /// The answers for a tree that is not the one just reviewed.
    /// </summary>
    /// <remarks>
    /// Answers kept for exactly this tree, the same tools and the same settings of ours are used; otherwise the tree is
    /// built. A tree that cannot be built gives answers with gaps.
    /// </remarks>
    MeaningAnswers Collect(MeaningRequest request);

    /// <summary>
    /// The artifacts of the file with their meaning properties, and the artifacts that only meaning knows, such as
    /// usages.
    /// </summary>
    /// <remarks>
    /// An artifact the answers say nothing about, because no part of the build compiled it, is left out: it cannot be
    /// judged.
    /// </remarks>
    IReadOnlyList<Artifact> Apply(CodeFile file, IReadOnlyList<Artifact> artifacts, MeaningAnswers answers);
}