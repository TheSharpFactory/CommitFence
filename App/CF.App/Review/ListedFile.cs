using CF.App.Rules.Page;

namespace CF.App.Review;

/// <summary>
/// One file in the list under the head of a result before a review: its path and what is said of it at the right, such as "31 lines"
/// or "changed".
/// </summary>
internal sealed record ListedFile(PathFileLine Path, string Note);
