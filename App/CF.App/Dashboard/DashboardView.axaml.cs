using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using CF.App.Parts;
using CF.App.Shell;
using CF.Core.Services;

namespace CF.App.Dashboard;

/// <summary>Connects the chart and rule lists to the Dashboard's actions.</summary>
internal sealed partial class DashboardView : UserControl
{
    private DashboardViewModel? _model;
    private readonly EmptyBlock _empty;
    private readonly TurningRing _busy = new TurningRing
    {
        Width = 24,
        Height = 24,
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center
    };

    public DashboardView()
    {
        InitializeComponent();
        _empty = (EmptyBlock)Resources["DashboardEmpty"]!;
        _empty.Heading = StatsReport.EmptyHeading;
        _empty.Lead = StatsReport.EmptySentence;
        Frame.SizeChanged += (_, _) => FitToWidth();
        SideColumn.SizeChanged += (_, _) => SizeMainColumn();
        Chart.DayChosen += day => _model?.ChooseDay(day);
        NeverList.AddHandler(KeyDownEvent, OnRuleKey, RoutingStrategies.Tunnel);
        BrokenList.AddHandler(KeyDownEvent, OnRuleKey, RoutingStrategies.Tunnel);
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if(_model != null)
            _model.PropertyChanged -= OnModelChanged;

        _model = DataContext as DashboardViewModel;
        if(_model != null)
            _model.PropertyChanged += OnModelChanged;

        _empty.ButtonCommand = _model?.ReviewNowCommand;
        Present();
    }

    private void OnOpenDoctor(object? sender, RoutedEventArgs e)
    {
        if(TopLevel.GetTopLevel(this)?.DataContext is MainWindowViewModel shell)
            shell.ShowDoctor();
    }

    private void OnModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if(string.IsNullOrEmpty(e.PropertyName) || e.PropertyName == nameof(DashboardViewModel.BusyRing))
            Present();

    }

    private void Present()
    {
        var panel = _model?.HasReviews == true ? SideColumn : null;
        if(!ReferenceEquals(Frame.Panel, panel))
            Frame.Panel = panel;

        if(panel != null)
            FlattenPanelSurface();

        SizeMainColumn();

        if(_model?.IsEmpty == true)
            Frame.Empty = _empty;
        else
            Frame.Empty = _model?.BusyRing == true ? _busy : null;

        Frame.Sheet.IsVisible = Frame.Empty == null || _model is { IsEmpty: true, HasNotice: true };
    }

    /// <summary>
    /// The two panels are cards of their own, 16 apart, on the page: the frame's surface round them keeps no colour and no room.
    /// A style of this view cannot take them from the frame's own style, so they are set on the surface itself.
    /// </summary>
    private void FlattenPanelSurface()
    {
        var surface = SideColumn.GetVisualAncestors().OfType<Border>().FirstOrDefault(border => border.Classes.Contains("panelsurface"));
        if(surface == null || surface.Padding == default)
            return;

        surface.Padding = new Thickness(0);
        surface.Bind(Border.BackgroundProperty, surface.GetResourceObservable("page"));
    }

    private void FitToWidth()
    {
        FlattenPanelSurface();
        Chart.TwoColumns = PageLayout.IsTwoPanes(PageLayout.WindowWidthOf(Frame.Bounds.Width));
        SizeMainColumn();
    }

    private void SizeMainColumn()
    {
        MainColumn.MinHeight = Frame.IsOnePane || Frame.Panel == null ? 0 : SideColumn.Bounds.Height;
    }

    private void OnRuleKey(object? sender, KeyEventArgs e)
    {
        if(e.Key != Key.Enter || sender is not ListBox list)
            return;

        _model?.OpenRule(RowFor(e.Source) ?? list.SelectedItem as DashboardRuleRow);
        e.Handled = true;
    }

    private void OnRuleTap(object? sender, TappedEventArgs e)
    {
        _model?.OpenRule(RowFor(e.Source));
    }

    private static DashboardRuleRow? RowFor(object? source)
    {
        if(source is not Control control)
            return null;

        var item = control as ListBoxItem ?? control.FindAncestorOfType<ListBoxItem>();
        return item?.Content as DashboardRuleRow ?? item?.DataContext as DashboardRuleRow;
    }

    private void OnShowNever(object? sender, RoutedEventArgs e)
    {
        FocusList(NeverList);
    }

    private void OnShowBroken(object? sender, RoutedEventArgs e)
    {
        FocusList(BrokenList);
    }

    private static void FocusList(ListBox list)
    {
        Dispatcher.UIThread.Post(() =>
        {
            list.SelectedIndex = Math.Max(0, list.SelectedIndex);
            (list.ContainerFromIndex(list.SelectedIndex) ?? list).Focus(NavigationMethod.Tab);
        }, DispatcherPriority.Loaded);
    }
}
