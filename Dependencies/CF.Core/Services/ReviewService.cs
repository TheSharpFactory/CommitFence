using CF.Core.Changes;
using CF.Core.Review;

namespace CF.Core.Services;

/// <summary>
/// The one entry the programs call to review code: named files go to the review of named files, and what a push, the
/// staged content or a range changes goes to the review of a tree.
/// </summary>
public sealed class ReviewService : IReviewService
{
    private readonly NamedFilesReview _namedFiles;
    private readonly TreeReview _tree;
    private readonly PushScopeFinder _pushScope;
    private readonly StagedScopeFinder? _staged;

    /// <summary>
    /// Creates the service.
    /// </summary>
    public ReviewService(NamedFilesReview namedFiles, TreeReview tree, PushScopeFinder pushScope, StagedScopeFinder? staged = null)
    {
        _namedFiles = namedFiles;
        _tree = tree;
        _pushScope = pushScope;
        _staged = staged;
    }

    /// <inheritdoc />
    public ReviewResult ReviewFiles(string settingsPath, IReadOnlyList<FileToReview> files, bool quick = false)
    {
        return _namedFiles.Review(settingsPath, files, quick);
    }

    /// <inheritdoc />
    public ReviewResult ReviewChanges(string folder, ReviewScope scope, string? settingsPath)
    {
        return _tree.Review(folder, scope, settingsPath);
    }

    /// <inheritdoc />
    public PushScopeAnswer FindPushScope(string folder)
    {
        return _pushScope.Find(folder);
    }

    /// <inheritdoc />
    public StagedAnswer FindStaged(string folder)
    {
        return _staged?.Find(folder) ?? StagedAnswer.Unknown("The staged changes cannot be read here.");
    }

    /// <inheritdoc />
    public PushScopeAnswer FindPushScope(string folder, string branch)
    {
        return _pushScope.Find(folder, branch);
    }
}