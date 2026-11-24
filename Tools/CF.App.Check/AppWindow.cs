using System.Diagnostics;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using Avalonia.VisualTree;
using CF.App.Rules;
using CF.App.Shell;
using CF.Composition;

namespace CF.App.Check;

/// <summary>
/// The app's real main window, opened headless on a folder: it can be given keys and clicks, resized, drawn to a picture,
/// and its pixels read.
/// </summary>
internal sealed class AppWindow : IDisposable
{
    private static bool _platformStarted;

    public AppWindow(CommitFenceServices services, string folder, double width, double height)
    {
        StartPlatform();
        Model = new MainWindowViewModel(services, folder);
        Window = new MainWindow
        {
            DataContext = Model,
            Width = width,
            Height = height
        };
        Window.Show();
        Pump(300);
    }

    public MainWindow Window { get; }

    public MainWindowViewModel Model { get; }

    public RulesViewModel Rules
    {
        get { return Model.Rules; }
    }

    public RuleListBox Rows
    {
        get { return Window.GetVisualDescendants().OfType<RuleListBox>().First(); }
    }

    /// <summary>
    /// Starts the headless platform with Skia drawing, once, so that the pictures are what the app draws.
    /// </summary>
    public static void StartPlatform()
    {
        if(_platformStarted)
            return;

        AppBuilder.Configure<CF.App.Shell.App>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
            .WithInterFont()
            .SetupWithoutStarting();
        _platformStarted = true;
    }

    public void Resize(double width, double height)
    {
        Window.Width = width;
        Window.Height = height;
        Pump(300);
    }

    /// <summary>
    /// Lets the interface do its work for this long: jobs, layout, drawing, animations and file watching.
    /// </summary>
    public void Pump(int milliseconds)
    {
        var clock = Stopwatch.StartNew();
        do
        {
            Dispatcher.UIThread.RunJobs();
            Window.UpdateLayout();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Thread.Sleep(16);
        }
        while(clock.ElapsedMilliseconds < milliseconds);

        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>
    /// Lets the interface work until the condition holds, for at most the time given.
    /// </summary>
    public bool PumpUntil(Func<bool> condition, int timeoutMilliseconds)
    {
        var clock = Stopwatch.StartNew();
        while(clock.ElapsedMilliseconds < timeoutMilliseconds)
        {
            if(condition())
                return true;

            Pump(50);
        }

        return condition();
    }

    public void Press(PhysicalKey key, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        Window.KeyPressQwerty(key, modifiers);
        Window.KeyReleaseQwerty(key, modifiers);
        Pump(60);
    }

    public void Click(Control control)
    {
        var point = CenterOf(control);
        Window.MouseMove(point, RawInputModifiers.None);
        Window.MouseDown(point, MouseButton.Left, RawInputModifiers.None);
        Window.MouseUp(point, MouseButton.Left, RawInputModifiers.None);
        Pump(120);
    }

    /// <summary>
    /// Moves the pointer to the corner of the window, away from every control, so that no control is drawn as hovered.
    /// </summary>
    public void MovePointerAway()
    {
        Window.MouseMove(new Point(2, 2), RawInputModifiers.None);
        Pump(150);
    }

    public Point CenterOf(Visual visual)
    {
        var size = visual.Bounds.Size;
        return visual.TranslatePoint(new Point(size.Width / 2, size.Height / 2), Window) ?? throw new InvalidOperationException("the control is not on the window");
    }

    public IEnumerable<T> Find<T>(Func<T, bool> test)
        where T : Visual
    {
        return Window.GetVisualDescendants().OfType<T>().Where(test);
    }

    /// <summary>
    /// The picture of the window as the screen would show it.
    /// </summary>
    public WriteableBitmap Capture()
    {
        Pump(120);
        // the first frame taken after a change can be the one before it, so it is taken and let go
        Window.CaptureRenderedFrame()?.Dispose();
        Pump(60);
        return Window.CaptureRenderedFrame() ?? throw new InvalidOperationException("no frame was drawn");
    }

    public string Save(string folder, string name)
    {
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, name);
        using var frame = Capture();
        frame.Save(path, PngBitmapEncoderOptions.Default);

        return path;
    }

    /// <summary>
    /// The colour of one pixel of a picture, as #RRGGBB.
    /// </summary>
    public static string PixelAt(WriteableBitmap frame, int x, int y)
    {
        using var buffer = frame.Lock();
        var offset = (y * buffer.RowBytes) + (x * 4);
        var first = Marshal.ReadByte(buffer.Address, offset);
        var second = Marshal.ReadByte(buffer.Address, offset + 1);
        var third = Marshal.ReadByte(buffer.Address, offset + 2);
        if(buffer.Format == PixelFormat.Bgra8888)
            return $"#{third:X2}{second:X2}{first:X2}";

        return $"#{first:X2}{second:X2}{third:X2}";
    }

    public void Dispose()
    {
        Window.Close();
        Model.Dispose();
    }
}
