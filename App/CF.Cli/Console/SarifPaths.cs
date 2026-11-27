using System.Text;
using System.Text.RegularExpressions;
using CF.Cli.Commands;
using CF.Core.Review;

namespace CF.Cli.Console;

/// <summary>
/// What a report may say about places: a file is named by the repository-relative path the review gave it, and the text
/// of the build or of git loses every absolute path, so nothing of the machine is left in a report that a build server
/// keeps or uploads.
/// </summary>
internal sealed partial class SarifPaths
{
    public const string OtherPlace = "<path>";

    private const string _sourceRoot = "%SRCROOT%";
    private const char _backslash = (char)92;

    // case counts as it does on the system: only a path of this machine's own folders is compared here, never a file of the review
    private static readonly StringComparison _comparison = OperatingSystem.IsWindows() || OperatingSystem.IsMacOS() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    private readonly string _repositoryRoot;
    private readonly IReadOnlyList<string> _otherRoots;

    private SarifPaths(string repositoryRoot, IReadOnlyList<string> otherRoots)
    {
        _repositoryRoot = repositoryRoot;
        _otherRoots = otherRoots;
    }

    /// <summary>The paths of this review: the repository's work folder when git was used, otherwise the given folder.</summary>
    public static SarifPaths Of(ReviewResult result, string fallbackRepositoryFolder, CommandEnvironment environment)
    {
        var root = result.Repository?.WorkFolder ?? fallbackRepositoryFolder;
        var others = new List<string> { environment.CurrentFolder, environment.ProgramFolder, Path.GetDirectoryName(environment.ProgramPath) ?? string.Empty, Path.GetTempPath() };
        if(result.Repository != null)
            others.Add(result.Repository.GitDirectory);

        return new SarifPaths(Normalize(root), others.Select(Normalize).Where(folder => folder.Length > 1).OrderByDescending(folder => folder.Length).ToList());
    }

    /// <summary>
    /// The path of a file as a URI relative to the repository, with '/' separators and every part percent-encoded, or null
    /// when the file is not given relative to the repository. The review's paths come as git gives them, so a backslash is
    /// a character of a name and is never read as a separator, and no path is compared with the folder of this machine.
    /// </summary>
    public static string? RelativeUri(string file)
    {
        if(IsAbsolute(file))
            return null;

        var parts = file.Split('/', StringSplitOptions.RemoveEmptyEntries).Where(part => part != ".").ToList();
        return parts.Count == 0 || parts.Contains("..") ? null : string.Join('/', parts.Select(Uri.EscapeDataString));
    }

    /// <summary>
    /// Text that came from the build or from git: the repository's folder is the start of a path, and every other absolute
    /// path, quoted or not, with spaces in it or not, is replaced by a word.
    /// </summary>
    public string Clean(string text)
    {
        var cleaned = CleanAuthored(text);
        cleaned = QuotedPath().Replace(cleaned, OtherPlace);
        cleaned = FileUri().Replace(cleaned, OtherPlace);
        cleaned = WindowsPath().Replace(cleaned, OtherPlace);
        cleaned = UncPath().Replace(cleaned, OtherPlace);
        return UnixPath().Replace(cleaned, OtherPlace);
    }

    /// <summary>
    /// Text the team wrote (a rule's title and message, the reason of an allow comment): only the folders of this machine
    /// are taken out, because a path that is part of the team's words is theirs to write.
    /// </summary>
    public string CleanAuthored(string text)
    {
        var cleaned = Replace(text, _repositoryRoot, string.Empty, _sourceRoot);
        foreach(var folder in _otherRoots)
            cleaned = Replace(cleaned, folder, OtherPlace + "/", OtherPlace);

        return cleaned;
    }

    private static bool IsAbsolute(string file)
    {
        if(file.StartsWith('/') || file.StartsWith(@"\\", StringComparison.Ordinal))
            return true;

        return file.Length > 1 && char.IsAsciiLetter(file[0]) && file[1] == ':';
    }

    private static string Replace(string text, string folder, string whenFollowed, string whenAlone)
    {
        var result = text;
        foreach(var form in new[] { folder, folder.Replace('/', _backslash) })
        {
            result = result.Replace(form + "/", whenFollowed, _comparison)
                .Replace(form + _backslash, whenFollowed, _comparison)
                .Replace(form, whenAlone, _comparison);
        }

        return result;
    }

    private static string Normalize(string folder)
    {
        var builder = new StringBuilder(folder.Replace(_backslash, '/'));
        while(builder.Length > 1 && builder[^1] == '/')
            builder.Length--;

        return builder.ToString();
    }

    // a path in quotes ends at the closing quote, so a space inside it does not end it
    [GeneratedRegex(@"(?<q>[""'])(?:[A-Za-z]:[\\/]|\\\\|/)[^\r\n]*?\k<q>")]
    private static partial Regex QuotedPath();

    [GeneratedRegex(@"file:///?[^\s""'<>]*")]
    private static partial Regex FileUri();

    // folders of a path may hold spaces when a separator follows them; the file name at its end does not
    [GeneratedRegex(@"(?<![\w])[A-Za-z]:[\\/](?:[^\\/:*?""<>|\r\n]+[\\/])*[^\s\\/:*?""'<>|(),;\]]*")]
    private static partial Regex WindowsPath();

    [GeneratedRegex(@"\\\\[^\s\\/:*?""'<>|(),;\]]+(?:[\\/][^\\/:*?""<>|\r\n]+(?=[\\/]))*(?:[\\/][^\s\\/:*?""'<>|(),;\]]*)?")]
    private static partial Regex UncPath();

    // two folders deep at least, so that a word such as and/or or a lone /name is not taken for a place
    [GeneratedRegex(@"(?<![\w.\-/:<>~$%@\\])/(?:[^\s""'<>|*?()\[\]:,;/\\]+/)+[^\s""'<>|*?()\[\]:,;/\\]*")]
    private static partial Regex UnixPath();
}
