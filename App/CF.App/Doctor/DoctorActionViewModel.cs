using CF.App.Review;
using CF.Core.Setup;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CF.App.Doctor;

/// <summary>
/// One button of a fix: open a file at a line, open a web page, or copy a text. A text is copied by the part that "Copy as a prompt"
/// uses, so it reads "Copied" for 1600 ms after the click.
/// </summary>
internal sealed partial class DoctorActionViewModel : ObservableObject
{
    private readonly DoctorAction _action;
    private readonly Action<DoctorAction> _open;

    public DoctorActionViewModel(DoctorAction action, bool isPrimary, Action<DoctorAction> open, Func<ITextClipboard?> clipboard,
        string? copyName = null, bool isQuiet = false)
    {
        _action = action;
        _open = open;
        IsPrimary = isPrimary;
        IsQuiet = isQuiet;
        if(action.Kind == DoctorActionKind.CopyText)
            Copy = new CopyButtonViewModel(action.Label, () => action.Value, clipboard, isPrimary, copyName);
    }

    public bool IsPrimary { get; }

    public bool IsQuiet { get; }

    public bool IsSecondary { get { return !IsPrimary && !IsQuiet; } }

    public string Label
    {
        get { return _action.Label; }
    }

    public DoctorActionKind Kind
    {
        get { return _action.Kind; }
    }

    /// <summary>
    /// The button that copies, for an action that copies; none for the others.
    /// </summary>
    public CopyButtonViewModel? Copy { get; }

    public bool IsCopy
    {
        get { return Copy != null; }
    }

    public bool IsOpen
    {
        get { return Copy == null; }
    }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(OpenCommand))]
    public partial bool IsAvailable { get; set; } = true;

    [RelayCommand(CanExecute = nameof(IsAvailable))]
    private void Open()
    {
        if(IsAvailable)
            _open(_action);
    }
}
