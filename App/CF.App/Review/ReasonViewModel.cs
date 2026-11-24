using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CF.App.Review;

/// <summary>
/// One reason an incomplete review could not finish, in the form of a finding: what failed in the app's own words, what it is
/// about in the code font, and, under a header that opens, the tool's own message. The body's string is "about: what failed:
/// the tool's message"; a string with no second colon has no message and its header is not a button.
/// </summary>
internal sealed partial class ReasonViewModel : ObservableObject
{
    private const int _longestPathParts = 3;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SpokenName))]
    public partial bool IsOpen { get; set; }

    public ReasonViewModel(string about, string failed, string? message, bool startsOpen)
    {
        About = about;
        Failed = failed;
        Message = message ?? string.Empty;
        IsOpen = startsOpen && HasMessage;
    }

    public string About { get; }

    public string Failed { get; }

    public string Message { get; }

    public bool HasMessage
    {
        get
        {
            return Message.Length > 0;
        }
    }

    public bool HasNoMessage
    {
        get
        {
            return Message.Length == 0;
        }
    }

    public string SpokenName
    {
        get
        {
            var state = HasMessage ? (IsOpen ? ", expanded" : ", folded") : string.Empty;
            return $"{Failed}, {About}{state}";
        }
    }

    /// <summary>
    /// Cuts the body's string into its three parts. What it is about is the part before the first colon and space; of the rest,
    /// the part up to its first colon and space is what failed (its first letter raised) and the part after is the message.
    /// </summary>
    public static ReasonViewModel Parse(string about, string reason, bool startsOpen)
    {
        var split = reason.IndexOf(": ", StringComparison.Ordinal);
        var failed = split < 0 ? reason : reason.Substring(0, split);
        var message = split < 0 ? null : reason.Substring(split + 2).Trim();
        return new ReasonViewModel(CutPaths(about), Raise(CutPaths(failed)), message == null || message.Length == 0 ? null : CutPaths(message), startsOpen);
    }

    /// <summary>
    /// A path with a drive letter or a leading slash and more than three parts is an ellipsis and its last two parts. The words
    /// around a path are never cut. The app is not told where the review's private copy is, so a path inside it is cut the same way.
    /// </summary>
    public static string CutPaths(string text)
    {
        return PathPattern().Replace(text, match => CutPath(match.Value));
    }

    private static readonly char[] _pathSeparators = { '\\', '/' };

    private static string CutPath(string path)
    {
        var separator = path.Contains('\\', StringComparison.Ordinal) ? '\\' : '/';
        var parts = path.Split(_pathSeparators, StringSplitOptions.RemoveEmptyEntries);
        return parts.Length <= _longestPathParts ? path : "..." + separator + parts[^2] + separator + parts[^1];
    }

    private static string Raise(string text)
    {
        return text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text.Substring(1);
    }

    [GeneratedRegex(@"(?:(?<![A-Za-z0-9])[A-Za-z]:\\|(?<![A-Za-z0-9.\)])/)(?:[^\\/\s'""<>|:()]+[\\/])+[^\\/\s'""<>|:()]*")]
    private static partial Regex PathPattern();

    [RelayCommand]
    private void Toggle()
    {
        if(HasMessage)
            IsOpen = !IsOpen;
    }
}
