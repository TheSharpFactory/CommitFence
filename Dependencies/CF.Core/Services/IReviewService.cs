using CF.Core.Changes;
using CF.Core.Review;
using CF.Core.Rules.Settings;

namespace CF.Core.Services;

/// <summary>
/// Reviews code against the rules of a settings file.
/// </summary>
public interface IReviewService
{
    /// <summary>
    /// Reviews the given files against the rules of the settings file. A file that cannot be read or does not parse
    /// makes the result incomplete.
    /// </summary>
    /// <exception cref="SettingsException">The settings file is missing or wrong.</exception>
    ReviewResult ReviewFiles(string settingsPath, IReadOnlyList<FileToReview> files);

    /// <summary>
    /// Reviews what a push, the staged content or a range changes, or the whole of a folder as the last commit holds
    /// it, in the repository the folder belongs to.
    /// </summary>
    /// <remarks>
    /// The code is read from a private copy of the tree under review, never from the working folder, and only findings
    /// on what the change touched are kept. When the scope cannot be established the result is incomplete, never a
    /// pass. The settings file is read from that tree unless a path is given.
    /// </remarks>
    /// <exception cref="SettingsException">The settings file is missing or wrong.</exception>
    /// <exception cref="ReviewTargetException">The folder or project file of a whole review cannot be used.</exception>
    ReviewResult ReviewChanges(string folder, ReviewScope scope, string? settingsPath);

    /// <summary>
    /// Finds what a push of the folder's current branch would send, or the plain reason there is none.
    /// </summary>
    PushScopeAnswer FindPushScope(string folder);
}