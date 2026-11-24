using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace CF.App.Tests.Support;

/// <summary>
/// A window, headless, that holds one view (or the real main window) and takes keys the way the keyboard gives them. Nothing
/// is shown on the screen, and nothing waits on the clock: the interface does its queued work when the test asks.
/// </summary>
internal sealed class ViewHost : IDisposable
{
    public ViewHost(Window window)
    {
        Window = window;
        Window.Show();
        Pump();
    }

    public ViewHost(Control content, double width, double height)
        : this(new Window { Content = content, Width = width, Height = height })
    {
    }

    public Window Window { get; }

    /// <summary>
    /// Runs the queued work of the interface: jobs, layout and a frame, until nothing is left that it posted by itself.
    /// </summary>
    public void Pump()
    {
        for(var round = 0; round < 4; round++)
        {
            Dispatcher.UIThread.RunJobs();
            Window.UpdateLayout();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        }

        Dispatcher.UIThread.RunJobs();
    }

    public void Press(PhysicalKey key, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        Window.KeyPressQwerty(key, modifiers);
        Window.KeyReleaseQwerty(key, modifiers);
        Pump();
    }

    public void Type(string text)
    {
        Window.KeyTextInput(text);
        Pump();
    }

    public T? Find<T>(Func<T, bool> test)
        where T : Visual
    {
        return Window.GetVisualDescendants().OfType<T>().FirstOrDefault(test);
    }

    public IReadOnlyList<T> FindAll<T>(Func<T, bool> test)
        where T : Visual
    {
        return Window.GetVisualDescendants().OfType<T>().Where(test).ToList();
    }

    public bool HasFocusWithin(Visual visual)
    {
        return Window.FocusManager?.GetFocusedElement() is Visual focused && (ReferenceEquals(focused, visual) || focused.GetVisualAncestors().Contains(visual));
    }

    /// <summary>
    /// Saves what the window shows as a picture, in the folder, under the name.
    /// </summary>
    public void Save(string folder, string name)
    {
        Directory.CreateDirectory(folder);
        Pump();

        // the first frame taken after a change can be the one before it, so it is taken and let go
        Window.CaptureRenderedFrame()?.Dispose();
        Pump();
        using var frame = Window.CaptureRenderedFrame() ?? throw new InvalidOperationException("no frame was drawn");
        frame.Save(Path.Combine(folder, name), PngBitmapEncoderOptions.Default);
    }

    public void Dispose()
    {
        Window.Close();
    }
}
