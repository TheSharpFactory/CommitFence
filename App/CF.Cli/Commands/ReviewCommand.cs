using CF.Cli.Console;
using CF.Core.Review;
using CF.Core.Services;

namespace CF.Cli.Commands;

/// <summary>
/// Reviews the named files and prints the result. The files are reported relative to the folder of the settings file,
/// which is the repository.
/// </summary>
internal sealed class ReviewCommand
{
    private readonly IReviewService _reviewService;
    private readonly string _settingsPath;
    private readonly string _currentFolder;
    private readonly IReadOnlyList<string> _files;
    private readonly ReviewReportWriter _reportWriter;
    private readonly bool _quick;

    public ReviewCommand(IReviewService reviewService, CommandEnvironment environment, string settingsPath, ReviewArguments arguments, string version)
    {
        _reviewService = reviewService;
        _settingsPath = settingsPath;
        _currentFolder = environment.CurrentFolder;
        _files = arguments.Files;
        _reportWriter = new ReviewReportWriter(arguments, environment, version, Path.GetDirectoryName(settingsPath) ?? environment.CurrentFolder, countByRule: false);
        _quick = arguments.Quick;
    }

    public ExitCode Run()
    {
        var result = _reviewService.ReviewFiles(_settingsPath, _files.Select(ToFileToReview).ToList(), _quick);

        return _reportWriter.Write(result);
    }

    private FileToReview ToFileToReview(string path)
    {
        var fullPath = Path.GetFullPath(path, _currentFolder);
        var repositoryFolder = Path.GetDirectoryName(_settingsPath) ?? _currentFolder;
        var reportedPath = Path.GetRelativePath(repositoryFolder, fullPath).Replace('\\', '/');

        return new FileToReview(reportedPath, fullPath);
    }
}