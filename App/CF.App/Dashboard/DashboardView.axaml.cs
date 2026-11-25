using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using CF.App.Parts;
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
        _empty = new EmptyBlock { Heading = StatsReport.EmptyHeading, Lead = StatsReport.EmptySentence, ButtonText = "Review now" };
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

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if(this.TryFindResource("i-review", out var icon) && icon is Geometry geometry)
            _empty.ButtonIcon = geometry;
    }

    private void OnModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if(string.IsNullOrEmpty(e.PropertyName) || e.PropertyName == nameof(DashboardViewModel.BusyRing))
            Present();

    }

    private void Present()
    {
        var panel = _model?.HasReviews == true ? MostBroken : null;
        if(!ReferenceEquals(Frame.Panel, panel))
            Frame.Panel = panel;

        if(_model?.IsEmpty == true)
            Frame.Empty = _empty;
        else
            Frame.Empty = _model?.BusyRing == true ? _busy : null;

        Frame.Sheet.IsVisible = Frame.Empty == null || _model is { IsEmpty: true, HasNotice: true };
    }

    private void OnChartFocus(object? sender, FocusChangedEventArgs e)
    {
        SetChartFocus(e.NavigationMethod != NavigationMethod.Pointer);
    }

    private void OnChartBlur(object? sender, RoutedEventArgs e)
    {
        SetChartFocus(false);
    }

    private void SetChartFocus(bool keyboard)
    {
        ChartRing.IsVisible = keyboard;
        DayHelp.IsVisible = keyboard;
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
