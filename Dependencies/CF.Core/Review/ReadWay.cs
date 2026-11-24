using CF.Core.Code;

namespace CF.Core.Review;

/// <summary>
/// One way a file was read: the artifacts found in it, and the builds (project and target framework) that compile it
/// this way.
/// </summary>
/// <remarks>
/// With no builds, the file was read as it is written, with no symbol defined.
/// </remarks>
internal sealed class ReadWay
{
    public ReadWay(IReadOnlyList<FileReading> builds, IReadOnlyList<Artifact> artifacts)
    {
        Builds = builds;
        Artifacts = artifacts;
    }

    public IReadOnlyList<FileReading> Builds { get; }

    public IReadOnlyList<Artifact> Artifacts { get; }
}