using Avalonia.Controls;
using Avalonia.Input.Platform;

namespace CF.App.Review;

/// <summary>
/// The system's clipboard, reached through the window a control stands in.
/// </summary>
internal sealed class TopLevelClipboard : ITextClipboard
{
    private readonly Control _control;

    public TopLevelClipboard(Control control)
    {
        _control = control;
    }

    public async Task SetTextAsync(string text)
    {
        var clipboard = TopLevel.GetTopLevel(_control)?.Clipboard;
        if(clipboard != null)
            await clipboard.SetTextAsync(text);
    }
}