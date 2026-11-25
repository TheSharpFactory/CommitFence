namespace CF.App.Doctor;

/// <summary>
/// Lets a path or a command break after a separator when it does not fit: a zero-width space follows each separator, which text
/// layout takes as a place to break. It changes how a text wraps and nothing it says, so a text that can be selected takes the
/// spaces out again when it is copied.
/// </summary>
internal static class PathBreaks
{
    public const string Space = "​";

    public static string After(string text)
    {
        return text.Replace("\\", "\\" + Space, StringComparison.Ordinal).Replace("/", "/" + Space, StringComparison.Ordinal);
    }

    public static string Without(string text)
    {
        return text.Replace(Space, string.Empty, StringComparison.Ordinal);
    }
}
