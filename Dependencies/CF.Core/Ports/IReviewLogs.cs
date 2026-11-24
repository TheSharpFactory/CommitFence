namespace CF.Core.Ports;

/// <summary>
/// Where a review keeps the logs of the language tools, in the product's data folder and never in the reviewed
/// repository.
/// </summary>
public interface IReviewLogs
{
    /// <summary>
    /// Creates a new, empty folder for one review's logs and returns its path. A folder is never reused, so a log found
    /// in it was written by this review. Old folders are removed.
    /// </summary>
    string CreateFolder(RepositoryLocation repository);
}