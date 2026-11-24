using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace CF.App.Rules;

/// <summary>
/// The list of rules: one flat, recycling list of headings and rows, and its keyboard. The list
/// is one tab stop; Up and Down move between rows and headings, Right and Left move the keyboard inside a row, Space and F4
/// act on the part that has it. The ring of the part that has the keyboard shows only when the keyboard was the last thing
/// used.
/// </summary>
internal sealed class RuleListBox : ListBox
{
    public static readonly StyledProperty<bool> IsOnePaneProperty = AvaloniaProperty.Register<RuleListBox, bool>(nameof(IsOnePane));

    /// <summary>
    /// The height of a group heading: the part of the top of the list that a heading held in view covers.
    /// </summary>
    public const double HeadingHeight = 44;

    private bool _keyboardActive;

    /// <summary>
    /// Raised when a person opens the selected rule: a click on the row (not on its switch or its effect control) or Enter
    /// on it. The argument says whether the keyboard did it.
    /// </summary>
    public event Action<bool>? OpenRequested;

    public RuleListBox()
    {
        AddHandler(PointerPressedEvent, OnPointerPressedTunnel, RoutingStrategies.Tunnel);
        AddHandler(KeyDownEvent, OnKeyDownTunnel, RoutingStrategies.Tunnel);
        SelectionChanged += OnSelectionChanged;
        ContainerPrepared += OnContainerPrepared;
        ContainerClearing += OnContainerClearing;
        GotFocus += OnGotFocusChanged;
        LostFocus += (_, _) => RefreshRing();
    }

    /// <summary>
    /// Whether the window shows one pane: the rows then end with the arrow that opens the rule, and none is drawn selected.
    /// </summary>
    public bool IsOnePane
    {
        get
        {
            return GetValue(IsOnePaneProperty);
        }
        set
        {
            SetValue(IsOnePaneProperty, value);
        }
    }

    /// <summary>
    /// The heading of the group whose entries are at the top of the list, when its own place has scrolled out of view (so
    /// that it is held in view), or nothing when the heading is itself in view. The second value is the top, in the list, of
    /// the heading of the next group when it is drawn, or infinity: it pushes the held heading out.
    /// </summary>
    public GroupHeadingViewModel? TopGroupHeading(out double nextHeadingTop)
    {
        nextHeadingTop = double.PositiveInfinity;
        if(Scroller == null || Items.Count == 0)
            return null;

        var first = FirstVisibleIndex();
        if(first < 0)
            return null;

        var headingIndex = first;
        while(headingIndex >= 0 && Items[headingIndex] is not GroupHeadingViewModel)
            headingIndex--;

        if(headingIndex < 0)
            return null;

        var ownTop = TopOf(ContainerFromIndex(headingIndex));
        if(ownTop != null && ownTop.Value >= 0)
            return null;

        for(var next = headingIndex + 1; next < Items.Count; next++)
        {
            if(Items[next] is not GroupHeadingViewModel)
                continue;

            nextHeadingTop = TopOf(ContainerFromIndex(next)) ?? double.PositiveInfinity;
            break;
        }

        return (GroupHeadingViewModel)Items[headingIndex]!;
    }

    private ScrollViewer? Scroller
    {
        get
        {
            return field ??= this.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault();
        }
    }

    private int FirstVisibleIndex()
    {
        var first = -1;
        var firstTop = double.PositiveInfinity;
        foreach(var container in GetRealizedContainers())
        {
            var top = TopOf(container);
            if(top == null || top.Value + container.Bounds.Height <= 0.5 || top.Value >= firstTop)
                continue;

            firstTop = top.Value;
            first = IndexFromContainer(container);
        }

        return first;
    }

    private double? TopOf(Control? container)
    {
        return container?.TranslatePoint(default, Scroller!)?.Y;
    }

    protected override Type StyleKeyOverride
    {
        get
        {
            return typeof(ListBox);
        }
    }

    private static void OnContainerPrepared(object? sender, ContainerPreparedEventArgs e)
    {
        e.Container.Classes.Set("heading", e.Container.DataContext is GroupHeadingViewModel);
        e.Container.Classes.Set("note", e.Container.DataContext is GroupNoteViewModel);
    }

    private static void OnContainerClearing(object? sender, ContainerClearingEventArgs e)
    {
        e.Container.Classes.Set("heading", false);
        e.Container.Classes.Set("note", false);
        e.Container.Classes.Set("ring", false);
    }

    private void OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        foreach(var removed in e.RemovedItems.OfType<RuleListItemViewModel>())
            Release(removed);

        RefreshRing();
    }

    private void OnKeyDownTunnel(object? sender, KeyEventArgs e)
    {
        if(e.Source is Button)
            return;

        _keyboardActive = true;
        if(HandleKey(e) || MoveSelection(e))
            e.Handled = true;

        RefreshRing();
    }

    private void Release(RuleListItemViewModel item)
    {
        ContainerFromItem(item)?.Classes.Set("ring", false);
        if(item is TeamRuleRowViewModel row)
        {
            row.IsRingVisible = false;
            row.Part = RowPart.Row;
            row.CloseEffectMenu();
        }

        if(item is FileRuleRowViewModel fileRow)
        {
            fileRow.IsRingVisible = false;
            fileRow.Part = RowPart.Row;
        }
    }

    private void OnGotFocusChanged(object? sender, FocusChangedEventArgs e)
    {
        if(e.NavigationMethod == NavigationMethod.Tab || e.NavigationMethod == NavigationMethod.Directional)
            _keyboardActive = true;

        if(SelectedItem == null && e.Source is StyledElement element && element.DataContext is RuleListItemViewModel focused)
            SelectedItem = focused;

        RefreshRing();
    }

    /// <summary>
    /// Puts the keyboard on the list: on the selected entry, or, when none is selected, on the first (which becomes the
    /// selected one: the keyboard is on the selected row).
    /// </summary>
    public void FocusEntry(NavigationMethod method)
    {
        var entry = SelectedItem ?? Items.OfType<RuleListItemViewModel>().FirstOrDefault(item => !(item is GroupNoteViewModel));
        if(entry == null)
            return;

        SelectedItem = entry;
        ScrollIntoView(entry);
        UpdateLayout();
        ContainerFromItem(entry)?.Focus(method);
    }

    /// <summary>
    /// Up, Down, Home, End, Page Up and Page Down select the row or heading above, below, first, last or a screenful away,
    /// and put the keyboard on it. A line of words between rows is passed over. With nothing selected the first key selects
    /// the first entry (the last, for Up and End).
    /// </summary>
    private bool MoveSelection(KeyEventArgs e)
    {
        var entries = Items.OfType<RuleListItemViewModel>().Where(item => !(item is GroupNoteViewModel)).ToList();
        if(entries.Count == 0 || e.KeyModifiers != KeyModifiers.None)
            return false;

        var current = SelectedItem is RuleListItemViewModel selected ? entries.IndexOf(selected) : -1;
        var screenful = Math.Max(1, (int)(Bounds.Height / 64));
        int target;
        switch(e.Key)
        {
            case Key.Down:
                target = current + 1;
                break;
            case Key.Up:
                target = current < 0 ? entries.Count - 1 : current - 1;
                break;
            case Key.Home:
                target = 0;
                break;
            case Key.End:
                target = entries.Count - 1;
                break;
            case Key.PageDown:
                target = current + screenful;
                break;
            case Key.PageUp:
                target = current - screenful;
                break;
            default:
                return false;
        }

        var entry = entries[Math.Clamp(target, 0, entries.Count - 1)];
        SelectedItem = entry;
        Reveal(entry);
        ContainerFromItem(entry)?.Focus(NavigationMethod.Directional);

        return true;
    }

    /// <summary>
    /// Brings the entry into view with the least scrolling, and keeps it clear of the heading that is held over the top of the
    /// list. An entry that is drawn is moved by setting the offset, which realizes at most the next row; asking the list to scroll
    /// to an item (ScrollIntoView) lays out the whole window and took 100 ms or more for each key press of a held key. Only an
    /// entry that is not drawn yet goes the long way.
    /// </summary>
    private void Reveal(object entry)
    {
        var container = ContainerFromItem(entry);
        var top = TopOf(container);
        if(Scroller == null || container == null || top == null)
        {
            ScrollIntoView(entry);
            UpdateLayout();
            return;
        }

        var inset = Scroller.Offset.Y > 0 && entry is not GroupHeadingViewModel ? HeadingHeight : 0;
        var bottom = top.Value + container.Bounds.Height + Padding.Bottom;
        var move = top.Value < inset ? top.Value - inset : bottom > Scroller.Viewport.Height ? bottom - Scroller.Viewport.Height : 0;
        if(move == 0)
            return;

        var furthest = Math.Max(0, Scroller.Extent.Height - Scroller.Viewport.Height);
        Scroller.Offset = new Vector(Scroller.Offset.X, Math.Clamp(Scroller.Offset.Y + move, 0, furthest));
    }

    private void OnPointerPressedTunnel(object? sender, PointerPressedEventArgs e)
    {
        _keyboardActive = false;
        var container = (e.Source as Visual)?.FindAncestorOfType<ListBoxItem>(true);
        if(container?.DataContext is RuleListItemViewModel item)
        {
            SelectedItem = item;
            container.Focus(NavigationMethod.Pointer);
        }

        RefreshRing();
        var onAControl = (e.Source as Visual)?.FindAncestorOfType<Button>(true) != null || (e.Source as Visual)?.FindAncestorOfType<ToggleButton>(true) != null;
        if(container?.DataContext is (TeamRuleRowViewModel or FileRuleRowViewModel) && !onAControl)
            OpenRequested?.Invoke(false);
    }

    private bool HandleKey(KeyEventArgs e)
    {
        if(SelectedItem is GroupHeadingViewModel heading)
            return HandleHeadingKey(heading, e);

        if(SelectedItem is TeamRuleRowViewModel row)
            return HandleRowKey(row, e);

        if(SelectedItem is FileRuleRowViewModel fileRow)
            return HandleFileRowKey(fileRow, e);

        return false;
    }

    /// <summary>
    /// The keys of a row of .editorconfig: Right goes from the row to the line of the file and Left back (a rule the file
    /// switches off has no line, so Right goes nowhere), Enter on the line opens the file there, Enter on the row opens the rule.
    /// Space, F4 and Alt+Down change nothing: the product does not change that file.
    /// </summary>
    private bool HandleFileRowKey(FileRuleRowViewModel row, KeyEventArgs e)
    {
        switch(e.Key)
        {
            case Key.Right:
                row.MoveRight();
                return true;
            case Key.Left:
                row.MoveLeft();
                return true;
            case Key.Space:
                return row.Part == RowPart.Line && OpenTheFile(row);
            case Key.Enter:
                return (row.Part == RowPart.Line && OpenTheFile(row)) || OpenByKeyboard();
            case Key.F4:
                return true;
            default:
                return e.Key == Key.Down && e.KeyModifiers.HasFlag(KeyModifiers.Alt);
        }
    }

    private static bool OpenTheFile(FileRuleRowViewModel row)
    {
        row.OpenFileCommand.Execute(null);
        return true;
    }

    private static bool HandleHeadingKey(GroupHeadingViewModel heading, KeyEventArgs e)
    {
        switch(e.Key)
        {
            case Key.Enter:
            case Key.Space:
                heading.Toggle();
                return true;
            case Key.Left:
                heading.Fold();
                return true;
            case Key.Right:
                heading.Open();
                return true;
            default:
                return false;
        }
    }

    private bool HandleRowKey(TeamRuleRowViewModel row, KeyEventArgs e)
    {
        if(row.IsEffectMenuOpen)
            return HandleMenuKey(row, e);

        if(e.Key == Key.F4 || (e.Key == Key.Down && e.KeyModifiers.HasFlag(KeyModifiers.Alt)))
        {
            row.OpenEffectMenu();
            return true;
        }

        switch(e.Key)
        {
            case Key.Right:
                row.MoveRight();
                return true;
            case Key.Left:
                row.MoveLeft();
                return true;
            case Key.Space:
                ActOnPart(row, true);
                return true;
            case Key.Enter:
                return ActOnPart(row, false) || OpenByKeyboard();
            default:
                return false;
        }
    }

    private bool OpenByKeyboard()
    {
        OpenRequested?.Invoke(true);
        return true;
    }

    private static bool ActOnPart(TeamRuleRowViewModel row, bool spaceOnRow)
    {
        if(row.Part == RowPart.Effect)
        {
            row.OpenEffectMenu();
            return true;
        }

        if(row.Part == RowPart.Switch || spaceOnRow)
        {
            row.ToggleSwitchCommand.Execute(null);
            return true;
        }

        return false;
    }

    private static bool HandleMenuKey(TeamRuleRowViewModel row, KeyEventArgs e)
    {
        switch(e.Key)
        {
            case Key.Up:
                row.MoveHighlight(-1);
                return true;
            case Key.Down:
                row.MoveHighlight(1);
                return true;
            case Key.Enter:
            case Key.Space:
                row.ChooseHighlighted();
                return true;
            case Key.Escape:
                row.CloseEffectMenu();
                return true;
            case Key.Tab:
                row.CloseEffectMenu();
                return false;
            default:
                return false;
        }
    }

    private void RefreshRing()
    {
        var show = _keyboardActive && IsKeyboardFocusWithin;
        if(SelectedItem is TeamRuleRowViewModel row)
        {
            row.IsRingVisible = show;
            SetRowRing(row, show && row.Part == RowPart.Row);
            return;
        }

        if(SelectedItem is FileRuleRowViewModel fileRow)
        {
            fileRow.IsRingVisible = show;
            SetRowRing(fileRow, show && fileRow.Part == RowPart.Row);
            return;
        }

        if(SelectedItem != null)
            SetRowRing(SelectedItem, show);
    }

    private void SetRowRing(object item, bool on)
    {
        var container = ContainerFromItem(item);
        container?.Classes.Set("ring", on);
    }
}
