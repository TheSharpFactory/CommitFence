using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace CF.App.Review;

/// <summary>
/// The Review screen for the changes a push would send. It lays the sheet out for the width it has (two columns from 1304
/// wide of window, one under), keeps the panel in view while the sheet scrolls, and brings a file to the top when the panel asks.
/// </summary>
internal sealed partial class ReviewView : UserControl
{
    private const double _twoPanesFrom = 1304;
    private const double _railWidth = 88;
    private const double _oneColumnMost = 760;
    private const double _twoColumnsMost = 1292;
    private const double _sheetTop = 24;
    private const double _sheetBottom = 32;
    private const double _wheelStep = 50;
    private const double _folderButtonLeast = 180;
    private const double _folderButtonMost = 320;
    private const double _blockMiddleAt = 0.4;
    private const double _blockTopLeast = 32;

    private ReviewViewModel? _model;

    public ReviewView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        SizeChanged += (_, e) => ApplyLayout(e.NewSize.Width + _railWidth);
        Rows.AddHandler(ScrollViewer.ScrollChangedEvent, OnRowsScrolled);
        SidePanel.SizeChanged += (_, _) => KeepPanelInView();
        SidePanel.PointerWheelChanged += OnPanelWheel;
        EmptyBlock.SizeChanged += (_, _) => PlaceEmptyBlock();
        EmptyBlockBody.SizeChanged += (_, _) => PlaceEmptyBlock();
        Rows.AddHandler(KeyDownEvent, OnFileKeyDown, RoutingStrategies.Bubble);
        AddHandler(KeyDownEvent, OnEscape, RoutingStrategies.Bubble);
    }

    /// <summary>
    /// A file's line: Right opens it and Left folds it (Enter and Space press the button).
    /// </summary>
    private void OnFileKeyDown(object? sender, KeyEventArgs e)
    {
        if(e.Source is not Button { DataContext: FileGroupViewModel group } button || !button.Classes.Contains("fileline"))
            return;

        if(e.Key == Key.Right)
            group.IsOpen = true;
        else if(e.Key == Key.Left)
            group.IsOpen = false;
        else
            return;

        e.Handled = true;
    }

    /// <summary>
    /// Escape ends a rule chosen (and closes the picker first, when it is open).
    /// </summary>
    private void OnEscape(object? sender, KeyEventArgs e)
    {
        if(e.Key == Key.Escape && _model != null && _model.EscapePressed())
            e.Handled = true;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if(_model != null)
        {
            _model.FileRevealRequested -= OnFileRevealRequested;
            _model.OpenFileRequested -= OnOpenFileRequested;
            _model.OpenLineRequested -= OnOpenLineRequested;
            _model.ChooseRequested -= OnChooseRequested;
        }

        _model = DataContext as ReviewViewModel;
        if(_model != null)
            _model.Clipboard = new TopLevelClipboard(this);

        if(_model != null)
        {
            _model.FileRevealRequested += OnFileRevealRequested;
            _model.OpenFileRequested += OnOpenFileRequested;
            _model.OpenLineRequested += OnOpenLineRequested;
            _model.ChooseRequested += OnChooseRequested;
            _ = _model.RefreshAsync();
        }

        ApplyLayout(Bounds.Width + _railWidth);
    }

    private void ApplyLayout(double windowWidth)
    {
        var twoPanes = windowWidth >= _twoPanesFrom;
        Classes.Set("onepane", !twoPanes);
        if(_model != null)
            _model.IsOnePane = !twoPanes;

        var side = twoPanes ? 32.0 : 24.0;
        ((Border)Bar.Parent!).Padding = new Thickness(side, 4, side, 12);
        Bar.MaxWidth = twoPanes ? _twoColumnsMost : _oneColumnMost;
        PlaceFolderButton(twoPanes);
    }

    /// <summary>
    /// From 1304 wide the folder button stands in the row; in one pane it takes the second line, as wide as its content.
    /// </summary>
    private void PlaceFolderButton(bool twoPanes)
    {
        // in two panes the button takes the room left on the line, at least 180 and at most 320; in one pane it is as wide as its content
        var column = BarGrid.ColumnDefinitions[4];
        column.MinWidth = twoPanes ? _folderButtonLeast : 0;
        column.MaxWidth = twoPanes ? _folderButtonMost : double.PositiveInfinity;
        column.Width = twoPanes ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
        FolderButton.MinWidth = _folderButtonLeast;
        FolderButton.MaxWidth = _folderButtonMost;
        Grid.SetRow(FolderButton, twoPanes ? 0 : 1);
        Grid.SetColumn(FolderButton, twoPanes ? 4 : 0);
        Grid.SetColumnSpan(FolderButton, twoPanes ? 1 : 7);
        FolderButton.HorizontalAlignment = twoPanes ? Avalonia.Layout.HorizontalAlignment.Stretch : Avalonia.Layout.HorizontalAlignment.Left;
        FolderButton.Margin = twoPanes ? new Thickness(0) : new Thickness(0, 8, 0, 0);
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

    private void OnRowsScrolled(object? sender, ScrollChangedEventArgs e)
    {
        if(ReferenceEquals(e.Source, Scroller))
            KeepPanelInView();
    }

    /// <summary>
    /// The panel stands over the page, not in it: it keeps its place until the page's end comes up under it, and then it goes up with
    /// the page (and so a panel higher than the room it has is seen to its end).
    /// </summary>
    private void KeepPanelInView()
    {
        if(Scroller == null || !SidePanel.IsEffectivelyVisible)
            return;

        var most = Math.Max(0, Scroller.Extent.Height - _sheetTop - _sheetBottom - SidePanel.Bounds.Height);
        var shift = -Math.Max(0, Scroller.Offset.Y - most);
        SidePanel.RenderTransform = new TranslateTransform(0, shift);
    }

    /// <summary>
    /// The wheel over the panel scrolls the page, as it did when the panel was a part of it.
    /// </summary>
    private void OnPanelWheel(object? sender, PointerWheelEventArgs e)
    {
        if(Scroller == null)
            return;

        var furthest = Math.Max(0, Scroller.Extent.Height - Scroller.Viewport.Height);
        Scroller.Offset = new Vector(Scroller.Offset.X, Math.Clamp(Scroller.Offset.Y - (e.Delta.Y * _wheelStep), 0, furthest));
        e.Handled = true;
    }

    private void OnFileRevealRequested(FileGroupViewModel group)
    {
        Dispatcher.UIThread.Post(() => BringToTop(group), DispatcherPriority.Loaded);
    }

    private void BringToTop(FileGroupViewModel group)
    {
        if(_model == null || Scroller == null)
            return;

        var index = _model.Items.IndexOf(group);
        if(index < 0)
            return;

        Rows.ScrollIntoView(index);
        Rows.UpdateLayout();
        if(Rows.ContainerFromIndex(index) is not Control container)
            return;

        var top = container.TranslatePoint(new Point(0, 0), Scroller);
        if(top != null)
            Scroller.Offset = new Vector(0, Math.Max(0, Scroller.Offset.Y + top.Value.Y));
    }

    private async void OnOpenLineRequested(string path, int line)
    {
        var launcher = TopLevel.GetTopLevel(this)?.Launcher;
        if(launcher != null)
            await Parts.SourceFileLauncher.OpenAsync(launcher, path, line);
    }

    private async void OnOpenFileRequested(string path)
    {
        var launcher = TopLevel.GetTopLevel(this)?.Launcher;
        if(launcher != null)
            await launcher.LaunchFileInfoAsync(new FileInfo(path));
    }

    /// <summary>
    /// The system's own dialog at the repository's root, for a folder or for a project file; what it answers is the folder
    /// button's new value (nothing is started).
    /// </summary>
    private async void OnChooseRequested(bool folder)
    {
        var top = TopLevel.GetTopLevel(this);
        if(top == null || _model == null)
            return;

        var start = await top.StorageProvider.TryGetFolderFromPathAsync(_model.FolderRoot);
        string? path;
        if(folder)
        {
            var folders = await top.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = "Choose a folder", AllowMultiple = false, SuggestedStartLocation = start });
            path = folders.Count > 0 ? folders[0].TryGetLocalPath() : null;
        }
        else
        {
            var projects = new FilePickerFileType("Project files") { Patterns = new[] { "*.csproj" } };
            var files = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions { Title = "Choose a project file", AllowMultiple = false, SuggestedStartLocation = start, FileTypeFilter = new[] { projects } });
            path = files.Count > 0 ? files[0].TryGetLocalPath() : null;
        }

        if(path != null)
            _model.UseTarget(path);
    }

    /// <summary>
    /// Puts the keyboard on the run button, the first stop that is the screen's own.
    /// </summary>
    public void FocusRunButton()
    {
        var button = RunButton.IsEffectivelyVisible ? RunButton : BlockRunButton;
        button.Focus(NavigationMethod.Tab);
    }

    /// <summary>
    /// Puts the block before a review across its pane, its middle at 40 percent of the pane's height and its top at least 32 below the pane's top.
    /// </summary>
    private void PlaceEmptyBlock()
    {
        if(EmptyBlock.Bounds.Height <= 0 || EmptyBlockBody.Bounds.Height <= 0)
            return;

        var top = Math.Max(_blockTopLeast, (EmptyBlock.Bounds.Height * _blockMiddleAt) - (EmptyBlockBody.Bounds.Height / 2));
        if(Math.Abs(EmptyBlockBody.Margin.Top - top) > 0.5)
            EmptyBlockBody.Margin = new Thickness(32, top, 32, 0);
    }

    /// <summary>
    /// The files and findings that are drawn: the visual descendants that are buttons of a file's line or a finding's header.
    /// </summary>
    public IReadOnlyList<Button> FileLineButtons()
    {
        return Rows.GetVisualDescendants().OfType<Button>().Where(button => button.Classes.Contains("fileline")).ToList();
    }
}