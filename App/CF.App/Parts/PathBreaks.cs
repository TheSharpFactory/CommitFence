using Avalonia.Data.Converters;

namespace CF.App.Parts;

/// <summary>
/// Adds zero-width break opportunities after path separators and joins text across hyphens and spaces. Selectable text
/// removes the layout markers before copying.
/// </summary>
internal static class PathBreaks
{
    private const string _break = "\u200B";
    private const string _join = "\u2060";

    public static readonly FuncValueConverter<string, string> Display = new FuncValueConverter<string, string>(text => After(text ?? string.Empty));

    public static string After(string text)
    {
        return text.Replace("-", _join + "-" + _join, StringComparison.Ordinal)
            .Replace(" ", _join + " " + _join, StringComparison.Ordinal)
            .Replace("\\", _join + "\\" + _break, StringComparison.Ordinal)
            .Replace("/", _join + "/" + _break, StringComparison.Ordinal);
    }

    public static string Without(string text)
    {
        return text.Replace(_break, string.Empty, StringComparison.Ordinal).Replace(_join, string.Empty, StringComparison.Ordinal);
    }
}
