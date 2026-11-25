using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
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
    private bool _checkAgainHadKeyboard;
    private bool _keyboardWasInFix;

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
            _model.FocusCheckAgainRequested -= OnFocusCheckAgainRequested;
            _model.ChecksReplacing -= OnChecksReplacing;
        }

        _model = DataContext as DoctorViewModel;
        if(_model != null)
        {
            _model.PropertyChanged += OnModelChanged;
            _model.OpenFileRequested += OnOpenFileRequested;
            _model.OpenWebRequested += OnOpenWebRequested;
            _model.FocusCheckAgainRequested += OnFocusCheckAgainRequested;
            _model.ChecksReplacing += OnChecksReplacing;
            _model.Clipboard = new TopLevelClipboard(this);
        }

        Fit();
    }

    /// <summary>
    /// The panel stands only once a report has been read; in one pane it is 40 below the checks. While the checks run, "Check again"
    /// is disabled and so cannot keep the keyboard: it gets it back when they are done.
    /// </summary>
    private void OnModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        Fit();
        if(_model == null)
            return;

        if(_model.IsChecking)
        {
            _checkAgainHadKeyboard = _checkAgainHadKeyboard || CheckAgainButton.IsFocused;
            return;
        }

        if(!_checkAgainHadKeyboard)
            return;

        _checkAgainHadKeyboard = false;
        CheckAgainButton.Focus(NavigationMethod.Tab);
    }

    private void Fit()
    {
        Frame.Panel = _model?.HasReport == true ? MachinePanel : null;
        var onePane = !PageLayout.IsTwoPanes(PageLayout.WindowWidthOf(Frame.Bounds.Width));
        MachinePlace.Margin = new Avalonia.Thickness(0, onePane && _model?.HasReport == true ? _paneGap : 0, 0, 0);
    }

    /// <summary>
    /// Only the buttons of a fix are controls among the checks, so the keyboard inside them is the keyboard inside a fix.
    /// </summary>
    private void OnChecksReplacing()
    {
        _keyboardWasInFix = GroupList.IsKeyboardFocusWithin;
    }

    private void OnFocusCheckAgainRequested()
    {
        if(_keyboardWasInFix)
            CheckAgainButton.Focus(NavigationMethod.Tab);

        _keyboardWasInFix = false;
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
