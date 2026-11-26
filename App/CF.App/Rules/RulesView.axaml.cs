using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.VisualTree;
using CF.App.Doctor;
using CF.App.Parts;
using CF.App.Review;
using CF.App.Rules.Page;

namespace CF.App.Rules;

/// <summary>
/// The Rules screen. Its code does what only a view can: the widths of its two columns, which page is drawn, the keys of the
/// search field and of the page (Esc and Alt+Left go back in one pane), and opening the settings file in the program the
/// system uses for it.
/// </summary>
internal sealed partial class RulesView : UserControl
{
    private const double _railWidth = 88;
    private const double _twoPanesFrom = 1304;
    private const double _listLeftOver = 784;
    private const double _listLeast = 540;
    private const double _listMost = 656;
    private const int _pagesKept = 8;
    private const double _blockMiddleAt = 0.4;
    private const double _blockTopLeast = 32;

    private static readonly TimeSpan _settleTime = TimeSpan.FromMilliseconds(150);

    private RulesViewModel? _model;
    private FileRulePageView? _filePageView;
    private bool _warmed;
    private RulePageViewModel? _lastCurrent;
    private long _lastCurrentTime;
    private readonly List<RulePageViewModel> _recent = new List<RulePageViewModel>();
    private readonly DispatcherTimer _buildTimer = new DispatcherTimer { Interval = _settleTime };
    private GroupHeadingViewModel? _stuck;
    private double _windowWidth = 1440;

    public RulesView()
    {
        InitializeComponent();
        _buildTimer.Tick += (_, _) =>
        {
            _buildTimer.Stop();
            ShowCurrentPage();
        };
        DataContextChanged += OnDataContextChanged;
        SizeChanged += OnSizeChanged;
        PagePane.SizeChanged += (_, _) => PlaceNoRuleBlock();
        NoRuleBlock.SizeChanged += (_, _) => PlaceNoRuleBlock();
        Rows.OpenRequested += OnOpenRequested;
        Rows.AddHandler(ScrollViewer.ScrollChangedEvent, OnRowsScrolled);
        Rows.GotFocus += OnRowsGotFocus;
        ListColumn.AddHandler(KeyDownEvent, OnListColumnKeyDown, RoutingStrategies.Tunnel);
        PagePane.AddHandler(KeyDownEvent, OnPageKeyDown, RoutingStrategies.Bubble);
    }

    /// <summary>
    /// The number of controls the pages of the rules have made so far: every control under the page host. It does not grow
    /// when a rule is chosen again.
    /// </summary>
    public int PageControlCount
    {
        get
        {
            return CountBelow(PageHost);
        }
    }

    /// <summary>
    /// Puts the keyboard in the search field (Ctrl+F).
    /// </summary>
    public void FocusSearch()
    {
        Search.Focus();
        Search.SelectAll();
    }

    /// <summary>
    /// The view of the page that is drawn now, or nothing.
    /// </summary>
    public RulePageView? ShownPage()
    {
        return PageHost.Children.OfType<RulePageView>().FirstOrDefault(view => view.IsVisible);
    }

    /// <summary>
    /// The view of the page of a rule of .editorconfig when it is drawn, or nothing.
    /// </summary>
    public FileRulePageView? ShownFilePage()
    {
        return _filePageView is { IsVisible: true } ? _filePageView : null;
    }

    /// <summary>
    /// The group heading that is drawn over the top of the list because its own place has scrolled out of view, or nothing.
    /// </summary>
    public GroupHeadingViewModel? StuckHeading
    {
        get
        {
            return StickyHeading.IsVisible ? _stuck : null;
        }
    }

    private static int CountBelow(Visual parent)
    {
        var count = 0;
        foreach(var child in parent.GetVisualChildren())
            count += 1 + CountBelow(child);

        return count;
    }

    private void OnSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        _windowWidth = e.NewSize.Width + _railWidth;
        ApplyLayout();
    }

    /// <summary>
    /// Two panes from 1304 wide: the list column is the window's width less 784, never under 540 and never over 656. Under
    /// 1304 the window shows one pane: the list, or, once a rule is opened, its page over the list.
    /// </summary>
    private void ApplyLayout()
    {
        var twoPanes = _windowWidth >= _twoPanesFrom;
        if(_model != null)
            _model.IsOnePane = !twoPanes;

        var pageOver = !twoPanes && _model?.ShowsPage == true;
        var listWidth = Math.Clamp(_windowWidth - _listLeftOver, _listLeast, _listMost);
        Columns.ColumnDefinitions[0].Width = twoPanes ? new GridLength(listWidth) : pageOver ? new GridLength(0) : new GridLength(1, GridUnitType.Star);
        Columns.ColumnDefinitions[1].Width = twoPanes || pageOver ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
        ListColumn.IsVisible = twoPanes || !pageOver;
        PagePane.IsVisible = twoPanes || pageOver;
        ShowCurrentPage();
    }

    /// <summary>
    /// Shows the page of the rule that is open. A rule's page is made the first time it is shown and kept, so choosing
    /// another rule hides one page and shows another and builds nothing.
    /// </summary>
    private void ShowCurrentPage()
    {
        if(_model == null)
            return;

        foreach(var stale in PageHost.Children.OfType<RulePageView>().Where(view => view.DataContext is RulePageViewModel page && !_model.Holds(page)).ToList())
        {
            stale.DataContext = null;
            PageHost.Children.Remove(stale);
        }

        var current = _model.CurrentPage;
        var shows = _model.ShowsPage;
        var made = current != null && PageHost.Children.OfType<RulePageView>().Any(view => ReferenceEquals(view.DataContext, current));
        if(current != null && shows && !made && ChangedJustBefore(current))
        {
            foreach(var view in PageHost.Children.OfType<RulePageView>())
                view.IsVisible = false;

            _buildTimer.Stop();
            _buildTimer.Start();
            ShowFilePage();
            return;
        }

        if(current != null && shows && !made)
            AddPageView(current);

        foreach(var view in PageHost.Children.OfType<RulePageView>())
        {
            view.IsVisible = shows && ReferenceEquals(view.DataContext, current);
            view.SetOnePane(_model.IsOnePane);
        }

        LetGoOfOldPages(current);
        ShowFilePage();
    }

    /// <summary>
    /// Whether the open rule changed less than a moment ago (a key held down, say): the page of a rule that has none yet is then
    /// not made for each rule that goes by, but once the choice has stayed for a moment.
    /// </summary>
    private bool ChangedJustBefore(RulePageViewModel current)
    {
        var now = Environment.TickCount64;
        if(ReferenceEquals(current, _lastCurrent))
            return false;

        var fast = now - _lastCurrentTime < _settleTime.TotalMilliseconds;
        _lastCurrent = current;
        _lastCurrentTime = now;

        return fast;
    }

    /// <summary>
    /// A page is a view of some 300 controls with a code editor in it, and a team can have hundreds of rules: only the pages
    /// of the rules opened last are kept as views (the rule's own state, edits included, is kept in its page model and
    /// comes back with the view). The measurement of 400 team rules held 130,000 controls and 1.7 GB when every page was kept.
    /// </summary>
    private void LetGoOfOldPages(RulePageViewModel? current)
    {
        if(current != null)
        {
            _recent.Remove(current);
            _recent.Add(current);
        }

        while(_recent.Count > _pagesKept)
        {
            var oldest = _recent[0];
            _recent.RemoveAt(0);
            foreach(var view in PageHost.Children.OfType<RulePageView>().Where(view => ReferenceEquals(view.DataContext, oldest)).ToList())
            {
                view.DataContext = null;
                PageHost.Children.Remove(view);
            }
        }
    }

    /// <summary>
    /// The page of a rule of .editorconfig is one view, made the first time one is chosen and bound to whichever file rule is
    /// chosen after that.
    /// </summary>
    private void ShowFilePage()
    {
        if(_model == null)
            return;

        if(_model.ShowsFilePage && _filePageView == null)
        {
            _filePageView = new FileRulePageView { DataContext = _model.FilePage, IsVisible = false };
            PageHost.Children.Add(_filePageView);
        }

        if(_filePageView == null)
            return;

        _filePageView.IsVisible = _model.ShowsFilePage;
        _filePageView.SetOnePane(_model.IsOnePane);
    }

    private void AddPageView(RulePageViewModel page)
    {
        var view = new RulePageView { DataContext = page };
        PageHost.Children.Add(view);
        if(page.IsDraft)
            Dispatcher.UIThread.Post(page.OpenFirstPicker, DispatcherPriority.Loaded);
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if(_model != null)
        {
            _model.OpenFileRequested -= OnOpenFileRequested;
            _model.OpenLineRequested -= OnOpenLineRequested;
            _model.PropertyChanged -= OnModelPropertyChanged;
            _model.Items.CollectionChanged -= OnItemsChanged;
            _model.FileRulesArrived -= WarmUp;
        }

        _model = DataContext as RulesViewModel;
        if(_model != null)
        {
            _model.Clipboard = new TopLevelClipboard(this);
            _model.OpenFileRequested += OnOpenFileRequested;
            _model.OpenLineRequested += OnOpenLineRequested;
            _model.PropertyChanged += OnModelPropertyChanged;
            _model.Items.CollectionChanged += OnItemsChanged;
            _model.FileRulesArrived += WarmUp;
            _model.SettingsAppeared += OnSettingsAppeared;
            _windowWidth = Bounds.Width + _railWidth;
            ApplyLayout();
        }
    }

    private void OnModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if(e.PropertyName == nameof(RulesViewModel.ShowsPage) || e.PropertyName == nameof(RulesViewModel.ShowsFilePage) || e.PropertyName == nameof(RulesViewModel.CurrentPage) || e.PropertyName == nameof(RulesViewModel.IsOnePane))
            ApplyLayout();

        if(e.PropertyName == nameof(RulesViewModel.SetupConfirmation) && _model?.SetupConfirmation != null)
            Dispatcher.UIThread.Post(ShowSetupConfirmation, DispatcherPriority.Loaded);

        if(e.PropertyName == nameof(RulesViewModel.SetupError) && _model?.SetupError != null)
            Dispatcher.UIThread.Post(AnnounceSetupError, DispatcherPriority.Loaded);
    }

    private void ShowSetupConfirmation()
    {
        if(_model?.SetupConfirmation is not { } message)
            return;

        NewRuleButton.Focus(NavigationMethod.Tab);
        var confirmation = FindSetupMessage<TextBlock>(message);
        if(confirmation != null)
            ControlAutomationPeer.CreatePeerForElement(confirmation).RaisePropertyChangedEvent(AutomationElementIdentifiers.NameProperty, null, message);
    }

    private void OnSettingsAppeared()
    {
        Dispatcher.UIThread.Post(() => NewRuleButton.Focus(NavigationMethod.Tab), DispatcherPriority.Loaded);
    }

    private void AnnounceSetupError()
    {
        if(_model?.SetupError is not { } message)
            return;

        var error = FindSetupMessage<DoctorTextBlock>(message);
        if(error != null)
            ControlAutomationPeer.CreatePeerForElement(error).RaisePropertyChangedEvent(AutomationElementIdentifiers.NameProperty, null, message);
    }

    private T? FindSetupMessage<T>(string message) where T : Control
    {
        return this.GetVisualDescendants().OfType<T>()
            .FirstOrDefault(text => text.IsEffectivelyVisible && AutomationProperties.GetName(text) == message);
    }

    private async void OnOpenLineRequested(string path, int line)
    {
        var launcher = TopLevel.GetTopLevel(this)?.Launcher;
        if(launcher != null)
            await SourceFileLauncher.OpenAsync(launcher, path, line);
    }

    private async void OnOpenFileRequested(string path)
    {
        var launcher = TopLevel.GetTopLevel(this)?.Launcher;
        if(launcher != null)
            await launcher.LaunchFileInfoAsync(new FileInfo(path));
    }

    /// <summary>
    /// A rule was opened from the list. In one pane its page comes over the list; in two panes, Enter puts the keyboard in the
    /// title of the page that is already beside the list.
    /// </summary>
    private void OnOpenRequested(bool byKeyboard)
    {
        if(_model == null)
            return;

        if(_model.IsOnePane)
        {
            _model.OpenSelectedRule();
            Dispatcher.UIThread.Post(FocusShownPageEntry, DispatcherPriority.Loaded);
            return;
        }

        if(byKeyboard)
            Dispatcher.UIThread.Post(FocusShownPageTitle, DispatcherPriority.Loaded);
    }

    private void FocusShownPageEntry()
    {
        if(ShownFilePage() is { } filePage)
            filePage.FocusEntry();
        else
            ShownPage()?.FocusEntry();
    }

    private void FocusShownPageTitle()
    {
        if(ShownFilePage() is { } filePage)
            filePage.FocusEntry();
        else
            ShownPage()?.FocusTitle();
    }

    /// <summary>
    /// "Open file" is the stop between the filter and the list. Its button is in
    /// the heading, which is inside the list; so when Tab brings the keyboard from the filter into the list and the heading of
    /// .editorconfig is in view, the keyboard goes to that button first.
    /// </summary>
    private void OnRowsGotFocus(object? sender, FocusChangedEventArgs e)
    {
        if(e.NavigationMethod != NavigationMethod.Tab || e.OldFocusedElement is not Visual from || Rows.IsVisualAncestorOf(from) || !ListColumn.IsVisualAncestorOf(from) || from is not (SegmentedList or Control { Parent: SegmentedList } or ListBoxItem))
            return;

        var open = SetupLineButton() ?? OpenFileButton();
        if(open != null)
            Dispatcher.UIThread.Post(() => open.Focus(NavigationMethod.Tab), DispatcherPriority.Input);
    }

    private BusyButton? SetupLineButton()
    {
        return Rows.GetVisualDescendants().OfType<BusyButton>().FirstOrDefault(button => button.IsEffectivelyVisible && IsInView(button));
    }

    private Button? OpenFileButton()
    {
        return Rows.GetVisualDescendants().OfType<Button>().FirstOrDefault(button => button.IsEffectivelyVisible && Avalonia.Automation.AutomationProperties.GetName(button) == "Open file" && IsInView(button));
    }

    private bool IsInView(Control control)
    {
        var top = control.TranslatePoint(default, Rows)?.Y;
        return top != null && top.Value >= 0 && top.Value + control.Bounds.Height <= Rows.Bounds.Height;
    }

    private void FocusFilter()
    {
        var filter = ListColumn.GetVisualDescendants().OfType<SegmentedList>().FirstOrDefault();
        var item = filter?.GetVisualDescendants().OfType<ListBoxItem>().FirstOrDefault(candidate => candidate.IsSelected) ?? filter?.GetVisualDescendants().OfType<ListBoxItem>().FirstOrDefault();
        item?.Focus(NavigationMethod.Tab);
    }

    /// <summary>
    /// Tab on "Open file" goes on to the list, Shift+Tab on it or on the list goes back to the filter.
    /// </summary>
    private void OnListColumnKeyDown(object? sender, KeyEventArgs e)
    {
        if(e.Key != Key.Tab || e.Source is not Control source || !Rows.IsVisualAncestorOf(source))
            return;

        var shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        var onOpenFile = source is Button button && Avalonia.Automation.AutomationProperties.GetName(button) == "Open file";
        var setup = SetupLineButton();
        var onSetup = ReferenceEquals(source, setup);
        var onCopy = source is Button { DataContext: CopyButtonViewModel };
        if(!onOpenFile && !onSetup && !onCopy && !(shift && source is ListBoxItem))
            return;

        e.Handled = true;
        if(shift)
        {
            if(onSetup || setup?.Focus(NavigationMethod.Tab) != true)
                FocusFilter();

            return;
        }

        if(onSetup && (FocusCopyStarter() || OpenFileButton()?.Focus(NavigationMethod.Tab) == true))
            return;

        if(!onCopy || OpenFileButton()?.Focus(NavigationMethod.Tab) != true)
            Rows.FocusEntry(NavigationMethod.Tab);
    }

    private bool FocusCopyStarter()
    {
        var copy = Rows.GetVisualDescendants().OfType<Button>()
            .FirstOrDefault(button => button.IsEffectivelyVisible && button.DataContext is CopyButtonViewModel);
        return copy?.Focus(NavigationMethod.Tab) == true;
    }

    /// <summary>
    /// Builds, once and out of sight, what the first scroll into the second group and the first choice of a rule of the file
    /// would otherwise build while a frame waits (about 100 ms each): the held copy of a heading and the page of a file rule.
    /// Both are shown for one layout pass inside this one job and hidden again before anything is drawn.
    /// </summary>
    private void WarmUp()
    {
        if(_model == null || _warmed)
            return;

        _warmed = true;
        var heading = _model.Items.OfType<GroupHeadingViewModel>().FirstOrDefault();
        if(heading != null)
        {
            StickyHeading.Content = heading;
            StickyHeading.IsVisible = true;
            StickyHeading.UpdateLayout();
            StickyHeading.IsVisible = false;
            StickyHeading.Content = null;
        }

        if(_filePageView == null)
        {
            _filePageView = new FileRulePageView { DataContext = _model.FilePage, IsVisible = true };
            PageHost.Children.Add(_filePageView);
            _filePageView.UpdateLayout();
            _filePageView.IsVisible = false;
        }
    }

    private void OnItemsChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        Dispatcher.UIThread.Post(UpdateStickyHeading, DispatcherPriority.Loaded);
    }

    private void OnRowsScrolled(object? sender, ScrollChangedEventArgs e)
    {
        UpdateStickyHeading();
    }

    /// <summary>
    /// Keeps the heading of the group whose rules are at the top of the list in view: when that heading's own place has
    /// scrolled out, a copy of it is drawn over the top of the list, and the next group's heading pushes it out as it arrives.
    /// </summary>
    private void UpdateStickyHeading()
    {
        var owner = Rows.TopGroupHeading(out var nextTop);
        if(owner == null)
        {
            StickyHeading.IsVisible = false;
            return;
        }

        if(!ReferenceEquals(_stuck, owner))
        {
            _stuck = owner;
            StickyHeading.Content = owner;
        }

        StickyHeading.Margin = new Thickness(8, Math.Min(0, nextTop - RuleListBox.HeadingHeight), 12, 0);
        StickyHeading.IsVisible = true;
    }

    private void OnPageKeyDown(object? sender, KeyEventArgs e)
    {
        var back = e.Key == Key.Escape || (e.Key == Key.Left && e.KeyModifiers.HasFlag(KeyModifiers.Alt));
        if(_model == null || e.Handled || !back || !_model.IsOnePane)
            return;

        _model.BackCommand.Execute(null);
        Rows.FocusEntry(NavigationMethod.Directional);
        e.Handled = true;
    }

    /// <summary>
    /// Puts the block that says there is no rule to open across the page pane, its middle at 40 percent of the pane's visible height and
    /// its top at least 32 below the pane's top.
    /// </summary>
    private void PlaceNoRuleBlock()
    {
        var pane = NoRuleBlock.Parent as Control;
        if(pane == null || pane.Bounds.Height <= 0 || NoRuleBlock.Bounds.Height <= 0)
            return;

        var top = Math.Max(_blockTopLeast, (pane.Bounds.Height * _blockMiddleAt) - (NoRuleBlock.Bounds.Height / 2));
        if(Math.Abs(NoRuleBlock.Margin.Top - top) > 0.5)
            NoRuleBlock.Margin = new Thickness(32, top, 32, 0);
    }

    private void OnSearchKeyDown(object? sender, KeyEventArgs e)
    {
        if(_model == null)
            return;

        if(e.Key == Key.Down)
        {
            _model.SelectedItem ??= _model.Items.FirstOrDefault(item => item is TeamRuleRowViewModel);
            Rows.FocusEntry(NavigationMethod.Directional);
            e.Handled = true;
        }
        else if(e.Key == Key.Escape)
        {
            if(_model.HasSearchText)
                _model.SearchText = string.Empty;
            else
                Rows.FocusEntry(NavigationMethod.Directional);

            e.Handled = true;
        }
    }
}
