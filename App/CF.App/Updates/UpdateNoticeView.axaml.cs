using System.ComponentModel;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Styling;
using CF.App.Parts;

namespace CF.App.Updates;

/// <summary>
/// The notice of an update, the last row of the page: its mark, its words, the bar of a download and its buttons, laid out in one
/// row from 1304 wide and in three rows under. It arrives by growing from no height in 180 ms and leaves the same way in 140 ms
/// (at once when the system asks for less motion), and the sheet above gives way.
/// </summary>
internal sealed partial class UpdateNoticeView : UserControl
{
    private const double _arriveMilliseconds = 180;
    private const double _leaveMilliseconds = 140;
    private const double _barWidth = 280;
    private const double _rowGap = 10;

    private UpdatesViewModel? _model;
    private CancellationTokenSource? _moving;

    // whether the notice is shown as far as the arrival and the leaving know: a download raises the notice's properties on every percent,
    // and only a change between shown and hidden may move it
    private bool _isShown;

    public UpdateNoticeView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        SizeChanged += (_, _) => ApplyLayout();
        AddHandler(KeyDownEvent, OnKeyDown, Avalonia.Interactivity.RoutingStrategies.Bubble);
        ApplyLayout();
    }

    /// <summary>
    /// Whether the notice is in its one-pane form.
    /// </summary>
    public bool IsOnePane { get; private set; }

    /// <summary>
    /// How many times the notice has begun its arrival.
    /// </summary>
    public int Arrivals { get; private set; }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if(_model != null)
            _model.PropertyChanged -= OnModelChanged;

        _model = DataContext as UpdatesViewModel;
        if(_model != null)
            _model.PropertyChanged += OnModelChanged;

        StopMoving();
        Slot.Height = double.NaN;
        _isShown = _model?.IsNoticeVisible ?? false;
        IsVisible = _isShown;
    }

    private void OnModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if(e.PropertyName != nameof(UpdatesViewModel.IsNoticeVisible) || _model == null || _model.IsNoticeVisible == _isShown)
            return;

        _isShown = _model.IsNoticeVisible;
        if(_isShown)
            Arrive();
        else
            Leave();
    }

    /// <summary>
    /// Esc with the keyboard in the notice: Later or Close, and nothing while an update runs.
    /// </summary>
    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if(e.Key == Key.Escape && _model != null && _model.EscapePressed())
            e.Handled = true;
    }

    private void Arrive()
    {
        StopMoving();
        Arrivals++;
        IsVisible = true;
        if(Motion.IsReduced || Bounds.Width <= 0)
        {
            Slot.Height = double.NaN;
            return;
        }

        Strip.Measure(new Size(Bounds.Width, double.PositiveInfinity));
        Slide(0, Strip.DesiredSize.Height, _arriveMilliseconds, () => Slot.Height = double.NaN);
    }

    private void Leave()
    {
        StopMoving();
        if(Motion.IsReduced || !IsVisible || Bounds.Height <= 0)
        {
            IsVisible = false;
            Slot.Height = double.NaN;
            return;
        }

        Slide(Slot.Bounds.Height, 0, _leaveMilliseconds, () =>
        {
            IsVisible = false;
            Slot.Height = double.NaN;
        });
    }

    private async void Slide(double from, double to, double milliseconds, Action done)
    {
        _moving = new CancellationTokenSource();
        var token = _moving.Token;
        Slot.Height = from;
        var animation = new Animation
        {
            Duration = TimeSpan.FromMilliseconds(milliseconds),
            Easing = CubicBezierEasing.Standard,
            FillMode = FillMode.Forward,
            Children =
            {
                new KeyFrame { Cue = new Cue(0), Setters = { new Setter(HeightProperty, from) } },
                new KeyFrame { Cue = new Cue(1), Setters = { new Setter(HeightProperty, to) } }
            }
        };
        try
        {
            await animation.RunAsync(Slot, token);
        }
        catch(OperationCanceledException)
        {
            return;
        }

        if(!token.IsCancellationRequested)
            done();
    }

    private void StopMoving()
    {
        _moving?.Cancel();
        _moving?.Dispose();
        _moving = null;
    }

    /// <summary>
    /// One row from 1304 wide of window; under it the mark and the words, the bar across the column under them, the buttons under
    /// the bar at the right end.
    /// </summary>
    private void ApplyLayout()
    {
        var twoPanes = PageLayout.IsTwoPanes(PageLayout.WindowWidthOf(Bounds.Width));
        IsOnePane = !twoPanes;
        Classes.Set("onepane", !twoPanes);
        var side = PageLayout.Side(twoPanes);
        Strip.Padding = new Thickness(side, 12);
        Inner.MaxWidth = PageLayout.FrameMost(twoPanes);
        Layout.ColumnDefinitions[2].Width = twoPanes ? GridLength.Auto : new GridLength(0);
        Layout.ColumnDefinitions[3].Width = twoPanes ? GridLength.Auto : new GridLength(0);
        MarkHost.VerticalAlignment = twoPanes ? VerticalAlignment.Center : VerticalAlignment.Top;
        WordsWide.IsVisible = twoPanes;
        WordsStacked.IsVisible = !twoPanes;
        Place(BarHost, twoPanes ? 0 : 1, twoPanes ? 2 : 1, twoPanes ? new Thickness(0) : new Thickness(0, _rowGap, 0, 0));
        Place(ButtonsHost, twoPanes ? 0 : 2, twoPanes ? 3 : 1, twoPanes ? new Thickness(0) : new Thickness(0, _rowGap, 0, 0));
        BarHost.Width = twoPanes ? _barWidth : double.NaN;
        BarHost.HorizontalAlignment = twoPanes ? HorizontalAlignment.Left : HorizontalAlignment.Stretch;
    }

    private static void Place(Control control, int row, int column, Thickness margin)
    {
        Grid.SetRow(control, row);
        Grid.SetColumn(control, column);
        control.Margin = margin;
    }
}
