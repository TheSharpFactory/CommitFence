using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CF.App.Parts;

namespace CF.App.Settings;

/// <summary>
/// The Settings screen: two columns from 1304 wide, one pane under it. In one pane, while an update waits, runs or failed, the app's
/// column comes first with its status row at the top; otherwise the repository's column comes first. The window's launcher opens the
/// settings file from the banner.
/// </summary>
internal sealed partial class SettingsView : UserControl
{
    private const double _columnGap = 48;
    private const double _statusGap = 20;
    private const double _statusLeadGap = 28;
    private const double _sectionGap = 24;

    private SettingsViewModel? _model;
    private string _arranged = string.Empty;

    public SettingsView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Frame.SizeChanged += (_, _) => Arrange();
        Arrange();
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if(_model != null)
        {
            _model.Status.PropertyChanged -= OnStatusChanged;
            _model.VersionRevealRequested -= OnVersionRevealRequested;
            _model.OpenFileRequested -= OnOpenFileRequested;
        }

        _model = DataContext as SettingsViewModel;
        if(_model != null)
        {
            _model.Status.PropertyChanged += OnStatusChanged;
            _model.VersionRevealRequested += OnVersionRevealRequested;
            _model.OpenFileRequested += OnOpenFileRequested;
        }

        Arrange();
    }

    private void OnStatusChanged(object? sender, PropertyChangedEventArgs e)
    {
        Arrange();
    }

    /// <summary>
    /// Lays the two columns side by side (48 apart) or one under the other, and the app's column in the order the state asks for. Nothing
    /// is touched while the arrangement is the same, so the keyboard stays where it is.
    /// </summary>
    private void Arrange()
    {
        var twoPanes = PageLayout.IsTwoPanes(PageLayout.WindowWidthOf(Frame.Bounds.Width));
        var appFirst = !twoPanes && _model?.Status.LeadsOnePane == true;
        var key = $"{twoPanes}|{appFirst}";
        if(key == _arranged)
            return;

        _arranged = key;
        ArrangeColumns(twoPanes, appFirst);
        ArrangeApp(appFirst);
    }

    private void ArrangeColumns(bool twoPanes, bool appFirst)
    {
        Columns.Children.Clear();
        Columns.ColumnDefinitions.Clear();
        Columns.RowDefinitions.Clear();
        if(twoPanes)
        {
            Columns.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
            Columns.ColumnDefinitions.Add(new ColumnDefinition(_columnGap, GridUnitType.Pixel));
            Columns.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
            Place(RepositoryColumn, 0, 0);
            Place(AppColumn, 0, 2);
            return;
        }

        Columns.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
        Columns.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        Columns.RowDefinitions.Add(new RowDefinition(_columnGap, GridUnitType.Pixel));
        Columns.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        Place(appFirst ? AppColumn : RepositoryColumn, 0, 0);
        Place(appFirst ? RepositoryColumn : AppColumn, 2, 0);
    }

    /// <summary>
    /// The app's column: its head, the version row, the status, the facts and the notes; the status goes first when it leads the page.
    /// </summary>
    private void ArrangeApp(bool statusLeads)
    {
        AppColumn.Children.Clear();
        var order = statusLeads
            ? new Control[] { StatusRow, AppHead, VersionRow, FactsBox, NotesBox }
            : new Control[] { AppHead, VersionRow, StatusRow, FactsBox, NotesBox };
        for(var row = 0; row < order.Length; row++)
        {
            Grid.SetRow(order[row], row);
            AppColumn.Children.Add(order[row]);
        }

        AppHead.Margin = new Thickness(0, statusLeads ? _statusLeadGap : 0, 0, 0);
        VersionRow.Margin = new Thickness(0, _sectionGap, 0, 0);
        StatusRow.Margin = new Thickness(0, statusLeads ? 0 : _statusGap, 0, 0);
        FactsBox.Margin = new Thickness(0, _statusGap, 0, 0);
        NotesBox.Margin = new Thickness(0, _sectionGap, 0, 0);
    }

    private void Place(Control column, int row, int columnIndex)
    {
        Grid.SetRow(column, row);
        Grid.SetColumn(column, columnIndex);
        Columns.Children.Add(column);
    }

    /// <summary>
    /// "What's new" in the notice: the sheet scrolls to the version row and the keyboard goes to "Update now", once the screen has been laid out.
    /// </summary>
    private void OnVersionRevealRequested()
    {
        Dispatcher.UIThread.Post(RevealVersion, DispatcherPriority.Loaded);
    }

    private void RevealVersion()
    {
        UpdateLayout();
        VersionRow.BringIntoView();
        if(UpdateNowButton.IsEffectivelyVisible)
            UpdateNowButton.Focus(NavigationMethod.Tab);
    }

    private async void OnOpenFileRequested(string path)
    {
        var launcher = TopLevel.GetTopLevel(this)?.Launcher;
        if(launcher != null)
            await launcher.LaunchFileInfoAsync(new FileInfo(path));
    }
}
