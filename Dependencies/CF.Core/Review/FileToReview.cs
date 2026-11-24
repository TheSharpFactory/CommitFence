namespace CF.Core.Review;

/// <summary>
/// A file a review is asked to look at: the path it is reported under and the place it is read from.
/// </summary>
public sealed class FileToReview
{
    /// <summary>
    /// Creates a file to review.
    /// </summary>
    public FileToReview(string reportedPath, string readPath)
    {
        ReportedPath = reportedPath;
        ReadPath = readPath;
    }

    /// <summary>
    /// The path findings are reported under, relative to the repository.
    /// </summary>
    public string ReportedPath { get; }

    /// <summary>
    /// The path the file's text is read from, through the file system port.
    /// </summary>
    public string ReadPath { get; }
}