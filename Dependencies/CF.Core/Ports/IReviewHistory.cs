namespace CF.Core.Ports;

/// <summary>The local history of reviews that judged pushes from a repository.</summary>
public interface IReviewHistory
{
    /// <summary>Reads the history, leaving out and counting damaged or unsupported lines.</summary>
    ReviewHistoryRead Read(RepositoryLocation repository);

    /// <summary>Appends while the caller holds the repository's review lock.</summary>
    void Append(RepositoryLocation repository, ReviewRecord record);
}
