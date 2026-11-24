using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace CF.App.Rules.Page;

/// <summary>
/// The page of a rule. Its code does what only a view can: the keys for the tokens (open a
/// picker with F4 or Alt+Down, Delete removes a condition, Space flips a true or false value, F2 renames), the width of the
/// title (as wide as its text), and which line the right end of the first row stands on.
/// </summary>
internal sealed partial class RulePageView : UserControl
{
    private const double _titleFontSize = 28;
    private const double _titleRoom = 10;
    private const double _titleLetterSpacing = -0.7;
    private const double _contentMost = 900;
    private const double _titleLeast = 160;
    private const double _renameRoom = 110;

    private RulePageViewModel? _page;

    public RulePageView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Sheet.SizeChanged += (_, _) => Fit();
        FirstRow.SizeChanged += (_, _) => PlaceRightGroup();
        RightGroup.SizeChanged += (_, _) => PlaceRightGroup();
        TitleBox.TextChanged += (_, _) => FitTitle();
        LayoutUpdated += (_, _) => FitTryIt();
        AddHandler(KeyDownEvent, OnKeyDownTunnel, RoutingStrategies.Tunnel);
        AddHandler(KeyUpEvent, OnKeyUpTunnel, RoutingStrategies.Tunnel);
    }

    /// <summary>
    /// Whether the window shows one pane: the page then has a back button and its sides are 24.
    /// </summary>
    public void SetOnePane(bool onePane)
    {
        Classes.Set("onepane", onePane);
    }

    /// <summary>
    /// Puts the keyboard on the first stop of the page: the back button in one pane, the title otherwise. In one pane the list
    /// is gone when the page comes over it, and the keyboard goes with the person.
    /// </summary>
    public void FocusEntry()
    {
        var back = this.GetVisualDescendants().OfType<Button>().FirstOrDefault(button => button.Classes.Contains("back") && button.IsEffectivelyVisible);
        if(back != null)
            back.Focus(NavigationMethod.Tab);
        else
            FocusTitle();
    }

    /// <summary>
    /// Puts the keyboard in the title with the caret at its end (F2, Rename, Enter on a row).
    /// </summary>
    public void FocusTitle()
    {
        TitleBox.Focus();
        TitleBox.CaretIndex = TitleBox.Text?.Length ?? 0;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if(_page != null)
            _page.RenameRequested -= FocusTitle;

        _page = DataContext as RulePageViewModel;
        if(_page != null)
            _page.RenameRequested += FocusTitle;

        Fit();
    }

    private void Fit()
    {
        var room = Sheet.Bounds.Width - Sheet.Padding.Left - Sheet.Padding.Right;
        if(room > 0)
            SheetContent.Width = Math.Min(_contentMost, room);

        FitTitle();
        PlaceRightGroup();
    }

    /// <summary>
    /// Try it takes the height the page has left: from its own top to the bottom of the viewport, less the sheet's bottom
    /// padding. A page that is not shown has nothing to fit.
    /// </summary>
    private void FitTryIt()
    {
        if(!IsEffectivelyVisible || Scroller.Viewport.Height <= 0)
            return;

        var top = TryIt.TranslatePoint(new Point(0, 0), Sheet);
        if(top != null)
            TryIt.Fill(Scroller.Viewport.Height - top.Value.Y - Sheet.Padding.Bottom);
    }

    /// <summary>
    /// The title is as wide as its text and wraps when the text is wider than the page: its width is measured from the text.
    /// </summary>
    private void FitTitle()
    {
        var text = string.IsNullOrEmpty(TitleBox.Text) ? TitleBox.PlaceholderText ?? string.Empty : TitleBox.Text;
        var typeface = new Typeface(TitleBox.FontFamily, TitleBox.FontStyle, TitleBox.FontWeight, TitleBox.FontStretch);
        var measured = new FormattedText(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, typeface, _titleFontSize, Brushes.Black);
        var most = Math.Max(_titleLeast, SheetContent.Bounds.Width - _renameRoom);
        TitleBox.Width = Math.Clamp(Math.Ceiling(measured.WidthIncludingTrailingWhitespace + (text.Length * _titleLetterSpacing)) + _titleRoom, _titleLeast, most);
    }

    /// <summary>
    /// The right end of the first row stands beside the left end when both fit, and under it, at the right, when they do not.
    /// </summary>
    private void PlaceRightGroup()
    {
        var fits = LeftGroup.DesiredSize.Width + RightGroup.DesiredSize.Width + 10 <= FirstRow.Bounds.Width || FirstRow.Bounds.Width == 0;
        Grid.SetRow(RightGroup, fits ? 0 : 1);
        Grid.SetColumn(RightGroup, fits ? 1 : 0);
        Grid.SetColumnSpan(RightGroup, fits ? 1 : 2);
        RightGroup.Margin = fits ? default : new Thickness(0, 8, 0, 0);
    }

    /// <summary>
    /// The keyboard stops of the page in this order: the back button in one pane, the title,
    /// Rename, the parts of the sentence in reading order, the choice of effect, Paste, Clear, the code box, and then the
    /// switch, Discard, Save rule and the More button. The row that holds the switch stands first in the page's tree, so its
    /// stops are moved to the end.
    /// </summary>
    public IReadOnlyList<InputElement> TabStops()
    {
        var editor = TryIt.Editor;
        var all = this.GetVisualDescendants().OfType<InputElement>()
            .Where(stop => stop.Focusable && stop.IsTabStop && stop.IsEffectivelyVisible && stop.IsEffectivelyEnabled)
            .Where(stop => stop is not ListBoxItem { IsSelected: false })
            .Where(stop => stop == editor.TextArea || (stop != editor && !editor.IsVisualAncestorOf(stop)))
            .ToList();
        var last = all.Where(RightGroup.IsVisualAncestorOf).ToList();

        return all.Except(last).Concat(last).ToList();
    }

    private bool MoveAlongTabStops(KeyEventArgs e)
    {
        var focused = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() as Visual;
        var editor = TryIt.Editor;
        if(focused == null || focused == editor.TextArea || editor.IsVisualAncestorOf(focused))
            return false;

        var stops = TabStops();
        var here = stops.ToList().FindIndex(stop => stop == focused || stop.IsVisualAncestorOf(focused));
        var next = here + (e.KeyModifiers.HasFlag(KeyModifiers.Shift) ? -1 : 1);
        if(here < 0 || next < 0 || next >= stops.Count)
            return false;

        stops[next].Focus(NavigationMethod.Tab);
        return true;
    }

    private void OnKeyDownTunnel(object? sender, KeyEventArgs e)
    {
        if(e.Key == Key.Tab && (e.KeyModifiers & (KeyModifiers.Control | KeyModifiers.Alt)) == 0 && MoveAlongTabStops(e))
        {
            e.Handled = true;
            return;
        }

        if(e.Key == Key.F2)
        {
            FocusTitle();
            e.Handled = true;
            return;
        }

        if(e.Source is not Button button)
            return;

        if(Handle(button, e))
            e.Handled = true;
    }

    private void OnKeyUpTunnel(object? sender, KeyEventArgs e)
    {
        if(e.Key == Key.Space && e.Source is Button { DataContext: ConditionViewModel condition } button && button.Classes.Contains("comparison") && condition.Descriptor?.ValueType == CF.Core.Code.PropertyValueType.Boolean)
            e.Handled = true;
    }

    /// <summary>
    /// Opens the picker of the token that has the keyboard, with the highlight on the entry in force.
    /// </summary>
    private static bool OpenPickerOf(Button button)
    {
        if(button.DataContext is ConditionViewModel condition)
        {
            if(button.Classes.Contains("comparison"))
                condition.OpenComparisonPicker(true);
            else
                condition.OpenPropertyPicker(true);

            return true;
        }

        if(button.DataContext is not RulePageViewModel page)
            return false;

        switch(button.Name)
        {
            case "ArtifactButton":
                page.OpenArtifactPicker(true);
                return true;
            case "LanguageButton":
                page.OpenLanguagePicker(true);
                return true;
            case "KindButton":
                page.OpenKindPicker(true);
                return true;
            default:
                return false;
        }
    }

    private static bool Handle(Button button, KeyEventArgs e)
    {
        var isToken = button.Classes.Contains("token") || button.Classes.Contains("kw");
        if(isToken && (e.Key == Key.F4 || (e.Key == Key.Down && e.KeyModifiers.HasFlag(KeyModifiers.Alt))))
            return OpenPickerOf(button);

        if(button.DataContext is not ConditionViewModel condition)
            return false;

        if(e.Key == Key.Delete && condition.CanRemove)
        {
            condition.RemoveCommand.Execute(null);
            return true;
        }

        return e.Key == Key.Space && button.Classes.Contains("comparison") && condition.FlipBoolean();
    }
}
