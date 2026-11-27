using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data.Converters;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace CF.App.Review;

/// <summary>
/// The count of the clean files and the list it opens, inside its own outlined card. The list shows whole rows only: as many as
/// the window leaves under the card in two panes, at most twelve in one pane. The filter is there only when the list has more
/// files than that.
/// </summary>
internal sealed partial class CleanFilesDisclosure : UserControl
{
    private const double _rowHeight = 28;
    private const double _filterHeight = 40;
    private const double _closingHeight = 30;
    private const double _frame = 15;
    private const double _gapBelow = 24;
    private const int _cappedRows = 12;
    private const int _fewestRows = 3;

    private static readonly FontFamily _interface = new("avares://Avalonia.Fonts.Inter/Assets#Inter, Segoe UI, Helvetica Neue, sans-serif");

    public static readonly StyledProperty<bool> CapRowsProperty =
        AvaloniaProperty.Register<CleanFilesDisclosure, bool>(nameof(CapRows));

    /// <summary>
    /// The face of the query: the code face once something is typed, the interface's own while the field shows its hint.
    /// </summary>
    public static readonly FuncValueConverter<string?, FontFamily> QueryFont = new(
        text => string.IsNullOrEmpty(text) ? _interface : (Application.Current?.FindResource("code-font") as FontFamily ?? _interface));

    public CleanFilesDisclosure()
    {
        InitializeComponent();
        LayoutUpdated += (_, _) => FitList();
        Disclosure.TemplateApplied += OnTemplateApplied;
        OpenBody.AddHandler(KeyDownEvent, OnListKey, RoutingStrategies.Tunnel);
    }

    /// <summary>
    /// Whether the list is capped at twelve rows (one pane) instead of taking the room the window leaves (two panes).
    /// </summary>
    public bool CapRows
    {
        get { return GetValue(CapRowsProperty); }
        set { SetValue(CapRowsProperty, value); }
    }

    private ToggleButton? Header
    {
        get { return Disclosure.GetVisualDescendants().OfType<ToggleButton>().FirstOrDefault(); }
    }

    private void OnTemplateApplied(object? sender, TemplateAppliedEventArgs e)
    {
        if(e.NameScope.Find<ToggleButton>("PART_Header") is not { } header)
            return;

        header.Classes.CollectionChanged += (_, _) => Ring.IsVisible = header.Classes.Contains(":focus-visible");
    }

    private void FitList()
    {
        if(DataContext is not CleanFilesViewModel model || !Disclosure.IsExpanded || !IsEffectivelyVisible || Header is not { } header)
            return;

        var fitsWithout = _cappedRows;
        var fitsWith = _cappedRows;
        if(!CapRows)
        {
            var window = TopLevel.GetTopLevel(this);
            var bottom = window == null ? null : header.TranslatePoint(new Point(0, header.Bounds.Height), window);
            if(window == null || bottom is not { } position)
                return;

            var shift = (this.FindAncestorOfType<FilesPanel>()?.RenderTransform as TranslateTransform)?.Y ?? 0;
            var room = window.ClientSize.Height - position.Y + shift - _closingHeight - _frame - _gapBelow;
            fitsWithout = Math.Max(_fewestRows, (int)Math.Floor(room / _rowHeight));
            fitsWith = Math.Max(_fewestRows, (int)Math.Floor((room - _filterHeight) / _rowHeight));
        }

        var isLong = model.Rows.Count > fitsWithout;
        var rows = isLong || model.FilterText.Length > 0 ? fitsWith : fitsWithout;
        var height = rows * _rowHeight;
        if(Math.Abs(ListRegion.MaxHeight - height) >= 1)
            ListRegion.MaxHeight = height;

        if(model.ListIsLong != isLong)
            model.ListIsLong = isLong;
    }

    private void OnCollapsing(object? sender, RoutedEventArgs e)
    {
        var focused = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() as Visual;
        if(focused != null && OpenBody.IsVisualAncestorOf(focused))
            Header?.Focus(NavigationMethod.Tab);
    }

    private void OnFilterKey(object? sender, KeyEventArgs e)
    {
        if(e.Key != Key.Escape || DataContext is not CleanFilesViewModel { FilterText.Length: > 0 } model)
            return;

        model.FilterText = string.Empty;
        e.Handled = true;
    }

    private void OnListKey(object? sender, KeyEventArgs e)
    {
        if(e.Source is not ScrollViewer scroller)
            return;

        switch(e.Key)
        {
            case Key.Up:
                scroller.LineUp();
                break;
            case Key.Down:
                scroller.LineDown();
                break;
            case Key.PageUp:
                scroller.PageUp();
                break;
            case Key.PageDown:
                scroller.PageDown();
                break;
            case Key.Home:
                scroller.ScrollToHome();
                break;
            case Key.End:
                scroller.ScrollToEnd();
                break;
            case Key.Escape:
                Disclosure.SetCurrentValue(Expander.IsExpandedProperty, false);
                break;
            default:
                return;
        }

        e.Handled = true;
    }
}
