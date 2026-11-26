using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using CF.App.Parts;
using CF.App.Review;

namespace CF.App.Doctor;

/// <summary>
/// The Doctor screen: the verdict, the checks and the panel "This machine" in the frame. The window's launcher opens the files and pages of the fixes, and its clipboard takes the texts they copy.
/// </summary>
internal sealed partial class DoctorView : UserControl
{
    private const double _paneGap = 40;

    private DoctorViewModel? _model;
    private (Button Button, DoctorCheckViewModel? Check, DoctorActionViewModel? Action)? _focusBeforeCheck;

    public DoctorView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Frame.SizeChanged += (_, _) => Fit();
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if(_model != null)
        {
            _model.PropertyChanged -= OnModelChanged;
            _model.OpenFileRequested -= OnOpenFileRequested;
            _model.OpenWebRequested -= OnOpenWebRequested;
            _model.ChecksFinished -= OnChecksFinished;
            _model.ChecksStarting -= OnChecksStarting;
        }

        _focusBeforeCheck = null;
        _model = DataContext as DoctorViewModel;
        if(_model != null)
        {
            _model.PropertyChanged += OnModelChanged;
            _model.OpenFileRequested += OnOpenFileRequested;
            _model.OpenWebRequested += OnOpenWebRequested;
            _model.ChecksFinished += OnChecksFinished;
            _model.ChecksStarting += OnChecksStarting;
            _model.Clipboard = new TopLevelClipboard(this);
        }

        Fit();
    }

    /// <summary>
    /// The panel stands only once a report has been read; in one pane it is 40 below the checks.
    /// </summary>
    private void OnModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        Fit();
    }

    private void Fit()
    {
        Frame.Panel = _model?.HasReport == true ? MachinePanel : null;
        var onePane = !PageLayout.IsTwoPanes(PageLayout.WindowWidthOf(Frame.Bounds.Width));
        MachinePlace.Margin = new Avalonia.Thickness(0, onePane && _model?.HasReport == true ? _paneGap : 0, 0, 0);
    }

    private void OnChecksStarting()
    {
        var button = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() as Button;
        _focusBeforeCheck = button != null && (button == CheckAgainButton || GroupList.IsKeyboardFocusWithin)
            ? (button, ContextOf<DoctorCheckViewModel>(button), ContextOf<DoctorActionViewModel>(button))
            : null;
    }

    private void OnChecksFinished()
    {
        var previous = _focusBeforeCheck;
        _focusBeforeCheck = null;

        var focused = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement();
        if(previous is not { } saved || !IsEffectivelyVisible || (focused != null && focused != saved.Button))
            return;

        if(saved.Check is { } check && saved.Action is { } action)
        {
            GroupList.UpdateLayout();
            var replacement = GroupList.GetVisualDescendants().OfType<Button>().FirstOrDefault(button =>
                button.IsEffectivelyVisible && ContextOf<DoctorCheckViewModel>(button) is { } row && row.Group == check.Group && row.Title == check.Title
                && ContextOf<DoctorActionViewModel>(button) is { } fix && fix.Kind == action.Kind && fix.Label == action.Label);
            if(replacement?.Focus(NavigationMethod.Tab) == true)
                return;
        }

        CheckAgainButton.Focus(NavigationMethod.Tab);
    }

    private static T? ContextOf<T>(Control control) where T : class
    {
        return control.GetSelfAndVisualAncestors().OfType<Control>().Select(ancestor => ancestor.DataContext).OfType<T>().FirstOrDefault();
    }

    private async void OnOpenFileRequested(string path, int? line)
    {
        var launcher = TopLevel.GetTopLevel(this)?.Launcher;
        if(launcher == null)
            return;

        if(line == null)
            await launcher.LaunchFileInfoAsync(new FileInfo(path));
        else
            await SourceFileLauncher.OpenAsync(launcher, path, line.Value);
    }

    private async void OnOpenWebRequested(string address)
    {
        var launcher = TopLevel.GetTopLevel(this)?.Launcher;
        if(launcher != null && Uri.TryCreate(address, UriKind.Absolute, out var uri))
            await launcher.LaunchUriAsync(uri);
    }
}
