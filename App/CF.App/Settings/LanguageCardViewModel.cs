using CF.Core.Services;

namespace CF.App.Settings;

/// <summary>
/// The card of a language: its name, that it is reviewed, and what the repository holds of it.
/// </summary>
internal sealed class LanguageCardViewModel
{
    public LanguageCardViewModel(LanguageReport report, string repositoryName)
    {
        Name = report.Name;
        Detail = DetailOf(report, repositoryName);
    }

    public string Name { get; }

    public string Detail { get; }

    /// <summary>
    /// "Files ending in .cs: 502 in CommitFence. A full review builds them with the repository's .NET SDK."
    /// </summary>
    private static string DetailOf(LanguageReport report, string repositoryName)
    {
        var endings = string.Join(", ", report.FileEndings);
        var count = report.FileCount switch
        {
            null => string.Empty,
            0 => $": none in {repositoryName}",
            var files => $": {files} in {repositoryName}"
        };
        var note = string.IsNullOrEmpty(report.FullReviewNote) ? string.Empty : " " + report.FullReviewNote;
        return $"Files ending in {endings}{count}.{note}";
    }
}
