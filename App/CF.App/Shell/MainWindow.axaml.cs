using System.ComponentModel;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using CF.App.Parts;

namespace CF.App.Shell;

internal sealed partial class MainWindow : Window
{
    private static readonly Dictionary<Key, Screen> _screenKeys = new Dictionary<Key, Screen>
    {
        [Key.D1] = Screen.Rules,
        [Key.NumPad1] = Screen.Rules,
        [Key.D2] = Screen.Review,
        [Key.NumPad2] = Screen.Review,
        [Key.D3] = Screen.Dashboard,
        [Key.NumPad3] = Screen.Dashboard,
        [Key.D4] = Screen.Exemptions,
        [Key.NumPad4] = Screen.Exemptions,
        [Key.D5] = Screen.Architecture,
        [Key.NumPad5] = Screen.Architecture,
        [Key.D6] = Screen.Settings,
        [Key.NumPad6] = Screen.Settings,
        [Key.D7] = Screen.Doctor,
        [Key.NumPad7] = Screen.Doctor
    };

    private MainWindowViewModel? _model;

    public MainWindow()
    {
        InitializeComponent();
        AddHandler(KeyDownEvent, OnWindowKeyDown, RoutingStrategies.Tunnel);
        Opened += OnOpened;
        Activated += OnActivated;
        DataContextChanged += OnDataContextChanged;
    }

    /// <summary>
    /// The window is up: the updates learn how to restart the app and open a page, then look for a newer version.
    /// </summary>
    private void OnOpened(object? sender, EventArgs e)
    {
        if(Model == null)
            return;

        Model.Updates.StartAndExit = StartAndExit;
        Model.Updates.OpenUri = OpenUri;
        Model.Updates.Start();
    }

    private void OnActivated(object? sender, EventArgs e)
    {
        Model?.RefreshFolder();
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if(_model != null)
            _model.PropertyChanging -= OnModelChanging;

        _model = Model;
        if(_model != null)
            _model.PropertyChanging += OnModelChanging;
    }

    private void OnModelChanging(object? sender, PropertyChangingEventArgs e)
    {
        if(e.PropertyName != nameof(MainWindowViewModel.CurrentScreen) || FocusManager?.GetFocusedElement() is not Control focused)
            return;

        if(!focused.GetVisualAncestors().Contains(Notice) && !focused.GetVisualAncestors().Contains(SettingsScreen))
            return;

        Dispatcher.UIThread.Post(() =>
        {
            if(!focused.IsEffectivelyVisible && FocusManager.GetFocusedElement() is not Control { IsEffectivelyVisible: true })
                FirstStop(Rail)?.Focus(NavigationMethod.Tab);
        }, DispatcherPriority.Loaded);
    }

    private bool StartAndExit(ProcessStartInfo startInfo)
    {
        try
        {
            using(Process.Start(startInfo))
            {
            }
        }
        catch(Exception exception) when(exception is Win32Exception || exception is InvalidOperationException)
        {
            return false;
        }

        Close();
        return true;
    }

    private async Task OpenUri(Uri uri)
    {
        await Launcher.LaunchUriAsync(uri);
    }

    private MainWindowViewModel? Model
    {
        get
        {
            return DataContext as MainWindowViewModel;
        }
    }

    /// <summary>
    /// Ctrl+1 to Ctrl+7 open the screens in the rail's order, F5 does the "again" of a screen that has one, F6 moves between the
    /// regions of the window.
    /// </summary>
    private void OnWindowKeyDown(object? sender, KeyEventArgs e)
    {
        if(Model == null)
            return;

        if(e.Key == Key.F5 && Model.CurrentModel != null)
        {
            var again = Model.CurrentModel.AgainCommand;
            if(again.CanExecute(null))
                again.Execute(null);

            e.Handled = true;
            return;
        }

        if(e.Key == Key.F6)
        {
            MoveToNextRegion(e.KeyModifiers.HasFlag(KeyModifiers.Shift) ? -1 : 1);
            e.Handled = true;
            return;
        }

        if(!e.KeyModifiers.HasFlag(KeyModifiers.Control))
            return;

        if(ScreenOfKey(e.Key) is Screen screen)
        {
            Model.CurrentScreen = screen;
            e.Handled = true;
            return;
        }

        switch(e.Key)
        {
            case Key.N:
                Model.ShowRules();
                Model.Rules.NewRule();
                e.Handled = true;
                break;
            case Key.S:
                Model.Rules.SaveOpenRule();
                e.Handled = true;
                break;
            case Key.U:
                UpdateNow();
                e.Handled = true;
                break;
            case Key.F:
                OpenSearch();
                e.Handled = true;
                break;
        }
    }

    /// <summary>
    /// Ctrl+F goes to the search of the screen that has one beside it: Exemptions, or else Rules.
    /// </summary>
    private void OpenSearch()
    {
        if(Model == null)
            return;

        if(Model.CurrentScreen == Screen.Exemptions)
        {
            ExemptionsScreen.FocusSearch();
            return;
        }

        Model.ShowRules();
        RulesScreen.FocusSearch();
    }

    /// <summary>
    /// Ctrl+U is "Update now" while a button of that name shows, in the notice or on Settings.
    /// </summary>
    private void UpdateNow()
    {
        if(Model == null || !(Model.ShowsSettings ? Model.Settings.Status.ShowsUpdateNow : Model.Updates.ShowsUpdateNow))
            return;

        if(Model.Updates.UpdateNowCommand.CanExecute(null))
            Model.Updates.UpdateNowCommand.Execute(null);
    }

    private static Screen? ScreenOfKey(Key key)
    {
        return _screenKeys.TryGetValue(key, out var screen) ? screen : null;
    }

    /// <summary>
    /// The screen that is in view.
    /// </summary>
    private Control ScreenInView()
    {
        return (Model?.CurrentScreen ?? Screen.Rules) switch
        {
            Screen.Rules => RulesScreen,
            Screen.Review => ReviewScreen,
            Screen.Dashboard => DashboardScreen,
            Screen.Exemptions => ExemptionsScreen,
            Screen.Architecture => ArchitectureScreen,
            Screen.Settings => SettingsScreen,
            Screen.Doctor => DoctorScreen,
            _ => throw new ArgumentOutOfRangeException(nameof(Model))
        };
    }

    /// <summary>
    /// The regions F6 visits, in order: the rail, the screen head, the main column, the panel (for a screen in the frame of the five;
    /// Rules and Review are one region), and the update notice while there is one.
    /// </summary>
    private List<Control> Regions()
    {
        var regions = new List<Control> { Rail };
        var screen = ScreenInView();
        var frame = screen.GetVisualDescendants().OfType<ScreenFrame>().FirstOrDefault();
        if(frame != null)
            regions.AddRange(frame.Regions);
        else
            regions.Add(screen);

        if(Notice.IsEffectivelyVisible)
            regions.Add(Notice);

        return regions;
    }

    /// <summary>
    /// F6: the keyboard goes to the first stop of the next region that has one (Shift+F6 to the previous).
    /// </summary>
    private void MoveToNextRegion(int step)
    {
        var targets = Regions().Select(region => (Region: region, Target: FirstStop(region))).Where(pair => pair.Target != null).ToList();
        if(targets.Count == 0)
            return;

        var focused = FocusManager?.GetFocusedElement() as Visual;
        var at = targets.FindIndex(pair => focused != null && (ReferenceEquals(focused, pair.Region) || focused.GetVisualAncestors().Contains(pair.Region)));
        var next = at < 0 ? (step > 0 ? 0 : targets.Count - 1) : (at + step + targets.Count) % targets.Count;
        targets[next].Target!.Focus(NavigationMethod.Tab);
    }

    /// <summary>
    /// The first thing in a region that takes the keyboard: the chosen item of the rail, or the first stop inside.
    /// </summary>
    private Control? FirstStop(Control region)
    {
        if(ReferenceEquals(region, Rail))
            return Rail.ContainerFromIndex(Rail.SelectedIndex) as Control ?? Rail;

        return region.GetVisualDescendants().OfType<Control>().FirstOrDefault(control => control.Focusable && control.IsTabStop && control.IsEffectivelyEnabled && control.IsEffectivelyVisible);
    }
}
