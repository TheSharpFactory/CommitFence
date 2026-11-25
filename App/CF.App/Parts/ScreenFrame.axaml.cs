using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Media;

namespace CF.App.Parts;

/// <summary>
/// The frame the five screens after Rules and Review stand in: the head of the screen that stays, and the sheet that scrolls with
/// the main column and, beside it from 1304 wide, the panel. A screen gives its name, its line, its actions, its main column and
/// its panel; the frame lays them out for the width it has.
/// </summary>
internal sealed partial class ScreenFrame : UserControl
{
    public static readonly StyledProperty<string> TitleProperty = AvaloniaProperty.Register<ScreenFrame, string>(nameof(Title), string.Empty);

    public static readonly StyledProperty<string?> MetaProperty = AvaloniaProperty.Register<ScreenFrame, string?>(nameof(Meta));

    public static readonly StyledProperty<object?> ActionsProperty = AvaloniaProperty.Register<ScreenFrame, object?>(nameof(Actions));

    public static readonly StyledProperty<object?> MainProperty = AvaloniaProperty.Register<ScreenFrame, object?>(nameof(Main));

    public static readonly StyledProperty<object?> PanelProperty = AvaloniaProperty.Register<ScreenFrame, object?>(nameof(Panel));

    public static readonly StyledProperty<object?> EmptyProperty = AvaloniaProperty.Register<ScreenFrame, object?>(nameof(Empty));

    private readonly Border _surface = new Border { Classes = { "panelsurface" } };
    private readonly ContentPresenter _panelContent = new ContentPresenter();

    static ScreenFrame()
    {
        TitleProperty.Changed.AddClassHandler<ScreenFrame>((frame, _) => frame.Head.Title = frame.Title);
        MetaProperty.Changed.AddClassHandler<ScreenFrame>((frame, _) => frame.Head.Meta = frame.Meta);
        ActionsProperty.Changed.AddClassHandler<ScreenFrame>((frame, _) => frame.Head.Actions = frame.Actions);
        MainProperty.Changed.AddClassHandler<ScreenFrame>((frame, _) => frame.TakeMain());
        PanelProperty.Changed.AddClassHandler<ScreenFrame>((frame, _) => frame.TakePanel());
        EmptyProperty.Changed.AddClassHandler<ScreenFrame>((frame, _) => frame.TakeEmpty());
    }

    public ScreenFrame()
    {
        InitializeComponent();
        _surface.Child = _panelContent;
        SizeChanged += (_, _) => ApplyLayout();
        Sheet.ScrollChanged += (_, _) => KeepPanelInView();
        _surface.SizeChanged += (_, _) => KeepPanelInView();
        _surface.PointerWheelChanged += OnPanelWheel;
        ApplyLayout();
    }

    /// <summary>
    /// The screen's name, in the head.
    /// </summary>
    public string Title
    {
        get
        {
            return GetValue(TitleProperty);
        }
        set
        {
            SetValue(TitleProperty, value);
        }
    }

    /// <summary>
    /// One line of what the screen shows, in the head.
    /// </summary>
    public string? Meta
    {
        get
        {
            return GetValue(MetaProperty);
        }
        set
        {
            SetValue(MetaProperty, value);
        }
    }

    /// <summary>
    /// The screen's actions, at the right end of the head.
    /// </summary>
    public object? Actions
    {
        get
        {
            return GetValue(ActionsProperty);
        }
        set
        {
            SetValue(ActionsProperty, value);
        }
    }

    /// <summary>
    /// The main column of the sheet.
    /// </summary>
    public object? Main
    {
        get
        {
            return GetValue(MainProperty);
        }
        set
        {
            SetValue(MainProperty, value);
        }
    }

    /// <summary>
    /// The panel: beside the main column from 1304 wide, in one pane where the main column holds a <see cref="PanelPlace"/> (or at
    /// its end). None for a screen that has no panel.
    /// </summary>
    public object? Panel
    {
        get
        {
            return GetValue(PanelProperty);
        }
        set
        {
            SetValue(PanelProperty, value);
        }
    }

    /// <summary>
    /// The block of a screen with nothing to show (an <see cref="EmptyBlock"/>): while it is set it takes the room of the sheet, and
    /// the sheet and the panel are not shown. The head stays.
    /// </summary>
    public object? Empty
    {
        get
        {
            return GetValue(EmptyProperty);
        }
        set
        {
            SetValue(EmptyProperty, value);
        }
    }

    /// <summary>
    /// Whether the frame is in its one-column form.
    /// </summary>
    public bool IsOnePane { get; private set; }

    /// <summary>
    /// The head, the main column and the panel, in the order F6 visits them (the panel only when the screen has one).
    /// </summary>
    public IReadOnlyList<Control> Regions
    {
        get
        {
            var regions = new List<Control> { Head };
            if(Empty != null)
                regions.Add(EmptyHost);
            else
                regions.Add(MainHost);

            if(Panel != null && Empty == null)
                regions.Add(_surface);

            return regions;
        }
    }

    private void TakeMain()
    {
        MainHost.Content = Main;
        PlacePanel();
    }

    private void TakeEmpty()
    {
        EmptyHost.Content = Empty;
        EmptyHost.IsVisible = Empty != null;
        Sheet.IsVisible = Empty == null;
        PlacePanel();
    }

    private void TakePanel()
    {
        _panelContent.Content = Panel;
        ApplyLayout();
    }

    private void ApplyLayout()
    {
        var twoPanes = PageLayout.IsTwoPanes(PageLayout.WindowWidthOf(Bounds.Width));
        IsOnePane = !twoPanes;
        Classes.Set("onepane", !twoPanes);
        Head.IsOnePane = !twoPanes;
        SheetRow.Padding = new Thickness(PageLayout.Side(twoPanes), PageLayout.SheetTop, PageLayout.Side(twoPanes) - PageLayout.ScrollBarWidth, PageLayout.SheetBottom);
        Column.MaxWidth = PageLayout.FrameMost(twoPanes);
        Column.Padding = twoPanes && Panel != null ? new Thickness(0, 0, PageLayout.PanelWidth + PageLayout.ColumnGap, 0) : new Thickness(0);
        PanelLayer.Padding = PageLayout.SheetPadding(twoPanes);
        PanelFrame.MaxWidth = PageLayout.FrameMost(twoPanes);
        PlacePanel();
    }

    /// <summary>
    /// Puts the panel in the second column, or, in one pane, where the screen marked its place.
    /// </summary>
    private void PlacePanel()
    {
        Detach(_surface);
        PanelLayer.IsVisible = false;
        if(Panel == null || Empty != null)
            return;

        if(!IsOnePane)
        {
            PanelHost.Child = _surface;
            PanelLayer.IsVisible = true;
            KeepPanelInView();
            return;
        }

        _surface.RenderTransform = null;
        (FindPlace() ?? EndPlace).Child = _surface;
    }

    private PanelPlace? FindPlace()
    {
        if(Main is not ILogical main)
            return null;

        return main.GetSelfAndLogicalDescendants().OfType<PanelPlace>().FirstOrDefault();
    }

    private static void Detach(Control surface)
    {
        if(surface.Parent is Decorator decorator)
            decorator.Child = null;
    }

    /// <summary>
    /// The panel stands over the sheet, not in it: it keeps its place until the sheet's end comes up under it, and then it goes up
    /// with the sheet.
    /// </summary>
    private void KeepPanelInView()
    {
        if(IsOnePane || Panel == null)
            return;

        var shift = PageLayout.PanelShift(Sheet.Offset.Y, Sheet.Extent.Height, _surface.Bounds.Height);
        _surface.RenderTransform = new TranslateTransform(0, shift);
    }

    /// <summary>
    /// The wheel over the panel scrolls the sheet, as it would if the panel were a part of it.
    /// </summary>
    private void OnPanelWheel(object? sender, PointerWheelEventArgs e)
    {
        var furthest = Math.Max(0, Sheet.Extent.Height - Sheet.Viewport.Height);
        Sheet.Offset = new Vector(Sheet.Offset.X, Math.Clamp(Sheet.Offset.Y - (e.Delta.Y * PageLayout.WheelStep), 0, furthest));
        e.Handled = true;
    }
}
