namespace CF.Core.Services;

/// <summary>A language of this build with its file endings and the files of it in the last commit.</summary>
/// <param name="Name">The name of the language, as shown to people.</param>
/// <param name="FileEndings">The endings of the file names the language owns, such as .cs.</param>
/// <param name="FileCount">How many files of the language the last commit holds, or null when it cannot be told (not a repository, no commit, git cannot be run).</param>
/// <param name="FullReviewNote">One sentence about what a full review does with its files, or null.</param>
public sealed record LanguageReport(string Name, IReadOnlyList<string> FileEndings, int? FileCount, string? FullReviewNote);
