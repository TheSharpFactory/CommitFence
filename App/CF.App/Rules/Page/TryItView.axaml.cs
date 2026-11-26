using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AvaloniaEdit.Document;
using CF.App.Rules.Page.Code;

namespace CF.App.Rules.Page;

/// <summary>
/// Try it: the head with its labels and the code box. Its code does what only a view can: it fits the box to the room the page
/// has left, puts the head's labels beside the heading or under it, takes the clipboard for Paste, draws the band, the bar,
/// the line numbers and the labels of the verdicts in the editor, and makes the keys (Tab is four
/// spaces; Esc, then Tab or Shift+Tab, leaves the box).
/// </summary>
internal sealed partial class TryItView : UserControl
{
    private const double _rowHeight = 20;
    private const double _boxChrome = 2 + 36 + 8 + 10;
    private const int _fewestRows = 4;
    private const double _emptyBoxLeast = 232;
    private const int _mostRows = 24;
    private const double _headGap = 12;
    private const double _codeSize = 13;

    private readonly VerdictLines _lines = new VerdictLines();
    private readonly TokenColorizer _colorizer;
    private readonly ArtifactPillGenerator _pills;
    private readonly CodeMargin _margin;
    private TryItViewModel? _try;
    private bool _escaped;
    private bool _syncing;

    public TryItView()
    {
        InitializeComponent();
        _colorizer = new TokenColorizer(this);
        _pills = new ArtifactPillGenerator(_lines, reason => _try?.InPageWords(reason) ?? reason);
        var typeface = new Typeface("Cascadia Mono, Consolas, Menlo, DejaVu Sans Mono, monospace");
        _margin = new CodeMargin(_lines, typeface);
        SetUpEditor();
        DataContextChanged += OnDataContextChanged;
        ActualThemeVariantChanged += (_, _) => ApplyTheme();
        AttachedToVisualTree += (_, _) => ApplyTheme();
        Head.SizeChanged += (_, _) => PlaceHead();
        PasteButton.Click += OnPaste;
        ClearButton.Click += OnClear;
    }

    /// <summary>
    /// The box takes the height the page has left: at least as high as its code (at most 24 rows of it, the rest scrolls), and
    /// when the window leaves more room under it, all of it. <paramref name="room" /> is the height from the top of Try it to the
    /// bottom edge the box may reach.
    /// </summary>
    public void Fill(double room)
    {
        var rows = Math.Clamp(Editor.Document.LineCount, _fewestRows, _mostRows);
        var least = Editor.Document.TextLength == 0 ? _emptyBoxLeast : _boxChrome + (rows * _rowHeight);
        var noteHeight = PathsNote.IsVisible ? PathsNote.Bounds.Height + PathsNote.Margin.Top : 0;
        var height = Math.Max(least, room - Box.Bounds.Top - noteHeight);
        if(double.IsNaN(Box.Height) || Math.Abs(Box.Height - height) > 0.5)
            Box.Height = height;
    }

    /// <summary>
    /// Puts the keyboard in the code.
    /// </summary>
    public void FocusCode()
    {
        Editor.TextArea.Focus();
    }

    private void SetUpEditor()
    {
        var options = Editor.Options;
        options.ConvertTabsToSpaces = true;
        options.IndentationSize = 4;
        options.EnableHyperlinks = false;
        options.EnableEmailHyperlinks = false;
        options.HighlightCurrentLine = false;
        options.AllowScrollBelowDocument = false;
        options.LineHeightFactor = _rowHeight / NaturalLineHeight();
        Editor.Document = new TextDocument();
        var area = Editor.TextArea;
        area.LeftMargins.Clear();
        area.LeftMargins.Add(_margin);
        area.SelectionBorder = null;
        area.SelectionCornerRadius = 0;
        area.SelectionForeground = null;
        area.Bind(AvaloniaEdit.Editing.TextArea.SelectionBrushProperty, Editor.GetResourceObservable("for"));
        area.TextView.LineTransformers.Add(_colorizer);
        area.TextView.ElementGenerators.Add(_pills);
        area.TextView.BackgroundRenderers.Add(new BreakBandRenderer(_lines));
        area.CaretBrush = Brushes.Transparent;
        area.TextView.BackgroundRenderers.Add(new CaretRenderer(area));
        Editor.Document.TextChanged += OnTextChanged;
        area.GotFocus += (_, _) => BoxPanel.Classes.Set("editing", true);
        area.LostFocus += (_, _) => BoxPanel.Classes.Set("editing", false);
        Editor.AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
    }

    private double NaturalLineHeight()
    {
        var typeface = new Typeface(Editor.FontFamily);
        return new FormattedText("Xg", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, _codeSize, Brushes.Black).Height;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if(_try != null)
        {
            _try.Shown -= OnShown;
            _try.ExampleLoaded -= OnExampleLoaded;
            _try.PropertyChanged -= OnTryPropertyChanged;
        }

        _try = DataContext as TryItViewModel;
        if(_try == null)
            return;

        _try.Shown += OnShown;
        _try.ExampleLoaded += OnExampleLoaded;
        _try.PropertyChanged += OnTryPropertyChanged;
        _try.Clipboard = new CF.App.Review.TopLevelClipboard(this);
        TakeCodeFromModel();
        OnShown();
    }

    private void OnTryPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if(e.PropertyName == nameof(TryItViewModel.Code))
            TakeCodeFromModel();
    }

    private void TakeCodeFromModel()
    {
        if(_try == null || _syncing || Editor.Document.Text == _try.Code)
            return;

        Editor.Document.Text = _try.Code;
    }

    private void OnTextChanged(object? sender, EventArgs e)
    {
        var text = Editor.Document.Text;
        _colorizer.Scan(text);
        if(_try == null)
            return;

        _syncing = true;
        _try.Code = text;
        _syncing = false;
    }

    /// <summary>
    /// The example is in the box as if it had been pasted: the keyboard goes to the box, the caret at the start.
    /// </summary>
    private void OnExampleLoaded()
    {
        TakeCodeFromModel();
        FocusCode();
        Editor.CaretOffset = 0;
    }

    private void OnShown()
    {
        if(_try == null)
            return;

        _lines.Set(_try.Verdicts);
        _pills.Shown(_try.Verdicts);
        Editor.TextArea.TextView.Redraw();
        _margin.InvalidateVisual();
        Dispatcher.UIThread.Post(PlaceHead, DispatcherPriority.Loaded);
    }

    private void ApplyTheme()
    {
        _colorizer.ThemeChanged();
        Editor.TextArea.TextView.Redraw();
        _margin.InvalidateVisual();
    }

    /// <summary>
    /// The labels of the head stand beside the heading when both fit, and on the next line, at the left, 8 below, when they do not.
    /// </summary>
    private void PlaceHead()
    {
        MessageText.TextWrapping = TextWrapping.NoWrap;
        HeadRight.Measure(Size.Infinity);
        var fits = Heading.DesiredSize.Width + HeadRight.DesiredSize.Width + _headGap <= Head.Bounds.Width || Head.Bounds.Width == 0;
        Grid.SetRow(HeadRight, fits ? 0 : 1);
        Grid.SetColumn(HeadRight, fits ? 1 : 0);
        Grid.SetColumnSpan(HeadRight, fits ? 1 : 2);
        HeadRight.Margin = fits ? default : new Thickness(0, 8, 0, 0);
        HeadRight.HorizontalAlignment = fits ? Avalonia.Layout.HorizontalAlignment.Right : Avalonia.Layout.HorizontalAlignment.Left;
        MessageText.TextWrapping = fits ? TextWrapping.NoWrap : TextWrapping.Wrap;
    }

    private async void OnPaste(object? sender, RoutedEventArgs e)
    {
        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if(clipboard == null)
            return;

        using var data = await clipboard.TryGetDataAsync();
        var text = data == null ? null : await data.TryGetTextAsync();
        if(!string.IsNullOrEmpty(text))
            Editor.Document.Text = text;
    }

    private void OnClear(object? sender, RoutedEventArgs e)
    {
        Editor.Document.Text = string.Empty;
        FocusCode();
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if(e.Key == Key.Escape)
        {
            _escaped = true;
            e.Handled = true;
            return;
        }

        var leaving = _escaped && e.Key == Key.Tab;
        _escaped = false;
        if(!leaving)
            return;

        LeaveCode(e.KeyModifiers.HasFlag(KeyModifiers.Shift));
        e.Handled = true;
    }

    /// <summary>
    /// Moves the keyboard to the stop after the code box (Tab), or before it (Shift+Tab): Paste or Clear before, and after it the
    /// switch.
    /// </summary>
    private void LeaveCode(bool backwards)
    {
        var page = this.FindAncestorOfType<RulePageView>();
        if(page == null)
            return;

        var stops = page.TabStops().ToList();
        var next = stops.IndexOf(Editor.TextArea) + (backwards ? -1 : 1);
        if(next >= 0 && next < stops.Count)
            stops[next].Focus(NavigationMethod.Tab);
    }
}
