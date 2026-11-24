using CF.App.Review;

namespace CF.App.Tests.Support;

/// <summary>
/// A clipboard that keeps the last text put on it, so that a test reads it without a display.
/// </summary>
internal sealed class FakeClipboard : ITextClipboard
{
    public string? Text { get; private set; }

    public Task SetTextAsync(string text)
    {
        Text = text;
        return Task.CompletedTask;
    }
}