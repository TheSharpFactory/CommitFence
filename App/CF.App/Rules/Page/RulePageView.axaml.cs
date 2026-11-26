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
    private const double _headGap = 12;

    private const double _idLeast = 48;

    private RulePageViewModel? _page;
    private (object? Item, int Role)? _afterRemoval;

    public RulePageView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Sheet.SizeChanged += (_, _) => Fit();
        Scroller.SizeChanged += (_, _) => FitHead();
        HeadRow.SizeChanged += (_, _) => FitHead();
        RightGroup.SizeChanged += (_, _) => FitHead();
        Loaded += (_, _) => FitHead();
        Scroller.ScrollChanged += (_, _) => Head.Classes.Set("stuck", Scroller.Offset.Y > 0);
        TitleBox.TextChanged += (_, _) => FitTitle();
        LayoutUpdated += (_, _) => FitTryIt();
        AddHandler(KeyDownEvent, OnKeyDownTunnel, RoutingStrategies.Tunnel);
        AddHandler(KeyUpEvent, OnKeyUpTunnel, RoutingStrategies.Tunnel);
        AddHandler(TextInputEvent, OnTextInputTunnel, RoutingStrategies.Tunnel);
    }

    /// <summary>
    /// Whether the window shows one pane: the page then has a back button and its sides are 24.
    /// </summary>
    public void SetOnePane(bool onePane)
    {
        Classes.Set("onepane", onePane);
        HeadRow.HorizontalAlignment = onePane ? Avalonia.Layout.HorizontalAlignment.Center : Avalonia.Layout.HorizontalAlignment.Left;
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

    /// <summary>
    /// The keyboard moves to the add button of the require clause: the remove button it was on went with the requirement.
    /// </summary>
    private void FocusRequireAdd()
    {
        _afterRemoval = null;
        Avalonia.Threading.Dispatcher.UIThread.Post(() => this.GetVisualDescendants().OfType<Button>().FirstOrDefault(button => button.Classes.Contains("addrequirement"))?.Focus(NavigationMethod.Tab), Avalonia.Threading.DispatcherPriority.Loaded);
    }

    /// <summary>
    /// The reason the rule cannot be saved was followed: the keyboard goes to the token it is about, or to the title.
    /// </summary>
    private void FocusBlocker(object? target)
    {
        if(target is PathPieceViewModel { IsOpen: true })
            return;

        var stop = target == null ? null : TabStops().FirstOrDefault(candidate => ReferenceEquals(candidate.DataContext, target));
        if(stop != null)
            stop.Focus(NavigationMethod.Tab);
        else
            FocusTitle();
    }

    /// <summary>
    /// Before a condition or a path is removed: the control the keyboard will go to is the next one of the sentence, or the previous one
    /// when the part removed was the last.
    /// </summary>
    private void KeepNeighbourOf(object removed)
    {
        var stops = TabStops();
        var own = stops.Select((stop, index) => (stop, index)).Where(entry => ReferenceEquals(entry.stop.DataContext, removed)).Select(entry => entry.index).ToList();
        if(own.Count == 0)
        {
            _afterRemoval = null;
            return;
        }

        var after = own[^1] + 1;
        var before = own[0] - 1;
        if(after < stops.Count && !Head.IsVisualAncestorOf(stops[after]))
            _afterRemoval = RoleOf(stops, after);
        else
            _afterRemoval = before >= 0 ? RoleOf(stops, before) : null;
    }

    /// <summary>
    /// A control named by what it belongs to and which of that thing's controls it is, so that it can be found again when the list is
    /// rebuilt and the control itself is gone.
    /// </summary>
    private static (object? Item, int Role) RoleOf(IReadOnlyList<InputElement> stops, int index)
    {
        var item = stops[index].DataContext;
        return (item, stops.Take(index).Count(stop => ReferenceEquals(stop.DataContext, item)));
    }

    private InputElement? FindByRole((object? Item, int Role) wanted)
    {
        var own = TabStops().Where(stop => ReferenceEquals(stop.DataContext, wanted.Item)).ToList();
        return wanted.Role < own.Count ? own[wanted.Role] : null;
    }

    private void FocusNeighbour()
    {
        var target = _afterRemoval;
        _afterRemoval = null;
        if(target == null)
            return;

        // the list was rebuilt: the neighbour's control is looked for after the page has laid out its new one, once more if not yet there
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            var found = FindByRole(target.Value);
            if(found != null)
            {
                found.Focus(NavigationMethod.Tab);
                return;
            }

            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                var later = FindByRole(target.Value);
                if(later != null)
                    later.Focus(NavigationMethod.Tab);
                else
                    FocusTitle();
            }, Avalonia.Threading.DispatcherPriority.Background);
        }, Avalonia.Threading.DispatcherPriority.Loaded);
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if(_page != null)
        {
            _page.RenameRequested -= FocusTitle;
            _page.RequireAddFocusRequested -= FocusRequireAdd;
            _page.BlockerFocusRequested -= FocusBlocker;
            _page.RemovalStarting -= KeepNeighbourOf;
            _page.RemovalDone -= FocusNeighbour;
        }

        _page = DataContext as RulePageViewModel;
        if(_page != null)
        {
            _page.RenameRequested += FocusTitle;
            _page.RequireAddFocusRequested += FocusRequireAdd;
            _page.BlockerFocusRequested += FocusBlocker;
            _page.RemovalStarting += KeepNeighbourOf;
            _page.RemovalDone += FocusNeighbour;
        }

        Fit();
    }

    private void Fit()
    {
        var room = Sheet.Bounds.Width - Sheet.Padding.Left - Sheet.Padding.Right;
        if(room > 0)
            SheetContent.Width = Math.Min(_contentMost, room);

        FitTitle();
        FitHead();
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
    /// The head is as wide as the sheet's content and is one row in every state. When what it holds does not fit, first the word of
    /// the switch goes, then the words of the tag and the back button, and their signs stay; then the rule's id is cut short with an
    /// ellipsis, so that it never crowds the controls: the row never wraps.
    /// </summary>
    private void FitHead()
    {
        var room = Math.Min(_contentMost, Scroller.Bounds.Width - Sheet.Padding.Left - Sheet.Padding.Right);
        if(room > 0 && HeadRow.Width != room)
        {
            HeadRow.Width = room;
            return;
        }

        // measuring before the page is in the window would give its buttons a template before the app's styles reach them
        if(!IsLoaded)
            return;

        Head.Classes.Set("tight", false);
        Head.Classes.Set("tighter", false);
        IdText.ClearValue(MaxWidthProperty);
        if(HeadRow.Bounds.Width <= 0 || HeadNeeds() <= HeadRow.Bounds.Width)
            return;

        Head.Classes.Set("tight", true);
        if(HeadNeeds() > HeadRow.Bounds.Width)
            Head.Classes.Set("tighter", true);

        var over = HeadNeeds() - HeadRow.Bounds.Width;
        if(over > 0)
            IdText.MaxWidth = Math.Max(_idLeast, IdText.DesiredSize.Width - over);
    }

    private double HeadNeeds()
    {
        LeftGroup.Measure(Size.Infinity);
        RightGroup.Measure(Size.Infinity);
        return LeftGroup.DesiredSize.Width + RightGroup.DesiredSize.Width + _headGap;
    }

    /// <summary>
    /// The keyboard stops of the page in this order: the back button in one pane, the title,
    /// Rename, the parts of the sentence in reading order, the choice of effect, Paste, Clear, the code box, and then the
    /// switch, Discard, Save rule and the More button. The head stands first in the page's tree, so its stops, but the back
    /// button, are moved to the end.
    /// </summary>
    public IReadOnlyList<InputElement> TabStops()
    {
        var editor = TryIt.Editor;
        var all = InTabOrder(this).OfType<InputElement>()
            .Where(stop => stop.Focusable && stop.IsTabStop && stop.IsEffectivelyVisible && stop.IsEffectivelyEnabled)
            .Where(stop => stop is not ListBoxItem { IsSelected: false })
            .Where(stop => stop == editor.TextArea || (stop != editor && !editor.IsVisualAncestorOf(stop)))
            .ToList();
        var head = all.Where(Head.IsVisualAncestorOf).ToList();
        var back = head.Where(stop => stop is Button button && button.Classes.Contains("back")).ToList();

        return back.Concat(all.Except(head)).Concat(head.Except(back)).ToList();
    }

    /// <summary>
    /// The descendants of the visual in tree order, except that the items of a token flow come in the order the flow set for them:
    /// the order they are seen.
    /// </summary>
    private static IEnumerable<Visual> InTabOrder(Visual parent)
    {
        var children = parent.GetVisualChildren().ToList();
        if(parent is TokenFlow)
            children = children.OrderBy(child => child is InputElement item ? KeyboardNavigation.GetTabIndex(item) : int.MaxValue).ToList();

        foreach(var child in children)
        {
            yield return child;
            foreach(var inside in InTabOrder(child))
                yield return inside;
        }
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

    /// <summary>
    /// Typing on a path token opens its picker with the character in the field; typing a number or a text on a comparison token
    /// does the same for the value.
    /// </summary>
    private static void OnTextInputTunnel(object? sender, TextInputEventArgs e)
    {
        if(e.Source is not Button button || string.IsNullOrEmpty(e.Text) || char.IsControl(e.Text[0]))
            return;

        if(button.DataContext is PathPieceViewModel path)
        {
            path.Open(e.Text);
            e.Handled = true;
            return;
        }

        if(button.DataContext is ConditionViewModel condition && button.Classes.Contains("comparison") && condition.OpenComparisonPicker(true, e.Text))
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
        if(button.DataContext is PathPieceViewModel path)
        {
            path.Open();
            return true;
        }

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

        if(e.Key == Key.Delete && button.DataContext is PathPieceViewModel path)
        {
            path.RemoveCommand.Execute(null);
            return true;
        }

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
