using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CF.App.Review;

/// <summary>
/// A button that puts a prompt on the clipboard: its label and its icon say "Copied" for 1600 ms after a click, then both
/// return.
/// </summary>
internal sealed partial class CopyButtonViewModel : ObservableObject
{
    private static readonly TimeSpan _copiedTime = TimeSpan.FromMilliseconds(1600);

    private readonly Func<string> _text;
    private readonly Func<ITextClipboard?> _clipboard;
    private DispatcherTimer? _timer;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SpokenName))]
    public partial bool IsCopied { get; set; }

    public CopyButtonViewModel(string label, Func<string> text, Func<ITextClipboard?> clipboard, bool isPrimary = false)
    {
        Label = label;
        IsPrimary = isPrimary;
        _text = text;
        _clipboard = clipboard;
    }

    /// <summary>
    /// What the button says at rest.
    /// </summary>
    public string Label { get; }

    public bool IsPrimary { get; }

    public string SpokenName
    {
        get
        {
            return IsCopied ? "Copied" : Label;
        }
    }

    /// <summary>
    /// Brings back the label and the icon; the timer calls it when the time of "Copied" is up.
    /// </summary>
    public void EndCopied()
    {
        _timer?.Stop();
        IsCopied = false;
    }

    [RelayCommand]
    private async Task CopyAsync()
    {
        var clipboard = _clipboard();
        if(clipboard == null)
            return;

        await clipboard.SetTextAsync(_text());
        IsCopied = true;
        StartTimer();
    }

    private void StartTimer()
    {
        if(_timer == null)
        {
            _timer = new DispatcherTimer { Interval = _copiedTime };
            _timer.Tick += (_, _) => EndCopied();
        }

        _timer.Stop();
        _timer.Start();
    }
}