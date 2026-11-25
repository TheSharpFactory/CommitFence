using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;
using CF.App.Parts;

namespace CF.App.Exemptions;

/// <summary>
/// The Exemptions screen. It lays the sheet out for the width it has (two columns from 1304 wide of window, one under), keeps the panel
/// "By rule" in view while the page scrolls, and carries the keys: Right and Left on a file or an exemption, Esc to end the search, a
/// chosen rule or the view of those with no reason. The window's launcher opens the files.
/// </summary>
internal sealed partial class ExemptionsView : UserControl
{
    private ExemptionsViewModel? _model;

    public ExemptionsView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        SizeChanged += (_, e) => ApplyLayout(PageLayout.WindowWidthOf(e.NewSize.Width));
        Rows.AddHandler(ScrollViewer.ScrollChangedEvent, OnRowsScrolled);
        Rows.AddHandler(KeyDownEvent, OnRowKeyDown, RoutingStrategies.Bubble);
        SidePanel.SizeChanged += (_, _) => KeepPanelInView();
        SidePanel.PointerWheelChanged += OnPanelWheel;
        AddHandler(KeyDownEvent, OnEscape, RoutingStrategies.Bubble);
    }

    /// <summary>
    /// The scroll viewer of the list of rows: the first one under it (the ones in its rows come after).
    /// </summary>
    private ScrollViewer? Scroller
    {
        get
        {
            return field ??= Rows.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault();
        }
    }

    /// <summary>
    /// Ctrl+F: the keyboard goes to the search, with its text selected. The search stands in the head of the list, which may have
    /// scrolled away: the list goes back to its top first.
    /// </summary>
    public void FocusSearch()
    {
        if(Scroller != null)
            Scroller.Offset = new Vector(0, 0);

        Rows.UpdateLayout();
        var search = Rows.GetVisualDescendants().OfType<TextBox>().FirstOrDefault(box => box.Name == "Search");
        search?.Focus();
        search?.SelectAll();
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if(_model != null)
            _model.OpenLineRequested -= OnOpenLineRequested;

        _model = DataContext as ExemptionsViewModel;
        if(_model != null)
            _model.OpenLineRequested += OnOpenLineRequested;

        ApplyLayout(PageLayout.WindowWidthOf(Bounds.Width));
    }

    private void ApplyLayout(double windowWidth)
    {
        var twoPanes = PageLayout.IsTwoPanes(windowWidth);
        Classes.Set("onepane", !twoPanes);
        Head.IsOnePane = !twoPanes;
        if(_model != null)
            _model.IsOnePane = !twoPanes;
    }

    private void OnRowsScrolled(object? sender, ScrollChangedEventArgs e)
    {
        if(ReferenceEquals(e.Source, Scroller))
            KeepPanelInView();
    }

    /// <summary>
    /// The panel stands over the page, not in it: it keeps its place until the page's end comes up under it, and then it goes up with
    /// the page.
    /// </summary>
    private void KeepPanelInView()
    {
        if(Scroller == null || !SidePanel.IsEffectivelyVisible)
            return;

        var shift = PageLayout.PanelShift(Scroller.Offset.Y, Scroller.Extent.Height, SidePanel.Bounds.Height);
        SidePanel.RenderTransform = new TranslateTransform(0, shift);
    }

    /// <summary>
    /// The wheel over the panel scrolls the page, as it would if the panel were a part of it.
    /// </summary>
    private void OnPanelWheel(object? sender, PointerWheelEventArgs e)
    {
        if(Scroller == null)
            return;

        var furthest = Math.Max(0, Scroller.Extent.Height - Scroller.Viewport.Height);
        Scroller.Offset = new Vector(Scroller.Offset.X, Math.Clamp(Scroller.Offset.Y - (e.Delta.Y * PageLayout.WheelStep), 0, furthest));
        e.Handled = true;
    }

    /// <summary>
    /// A file's line or an exemption: Right opens it and Left folds it (Enter and Space press the button).
    /// </summary>
    private void OnRowKeyDown(object? sender, KeyEventArgs e)
    {
        if(e.Source is not Button button || (e.Key != Key.Right && e.Key != Key.Left))
            return;

        var open = e.Key == Key.Right;
        if(button.DataContext is ExemptionFileViewModel file && button.Classes.Contains("exfile"))
            file.IsOpen = open;
        else if(button.DataContext is ExemptionRowViewModel row && button.Classes.Contains("exrow"))
            row.IsOpen = open;
        else
            return;

        e.Handled = true;
    }

    /// <summary>
    /// Esc ends a text in the search first; then a chosen rule or the view of those with no reason.
    /// </summary>
    private void OnEscape(object? sender, KeyEventArgs e)
    {
        if(e.Key != Key.Escape || _model == null)
            return;

        if(e.Source is TextBox { Name: "Search" } && _model.HasSearch)
            _model.SearchText = string.Empty;
        else if(!_model.EscapePressed())
            return;

        e.Handled = true;
    }

    private async void OnOpenLineRequested(string path, int line)
    {
        var launcher = TopLevel.GetTopLevel(this)?.Launcher;
        if(launcher != null)
            await SourceFileLauncher.OpenAsync(launcher, path, line);
    }

    /// <summary>
    /// The files and exemptions that are drawn: the visual descendants that are buttons of a file's line or an exemption's header.
    /// </summary>
    public IReadOnlyList<Button> RowButtons()
    {
        return Rows.GetVisualDescendants().OfType<Button>().Where(button => button.Classes.Contains("exfile") || button.Classes.Contains("exrow")).ToList();
    }
}
