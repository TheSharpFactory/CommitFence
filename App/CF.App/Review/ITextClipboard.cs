namespace CF.App.Review;

/// <summary>
/// Where the app puts text for the person to paste elsewhere. The window gives the system's clipboard; a test gives one that
/// keeps the text, so that no display is needed.
/// </summary>
internal interface ITextClipboard
{
    Task SetTextAsync(string text);
}