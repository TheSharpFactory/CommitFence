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

    public DoctorActionViewModel(DoctorAction action, bool isPrimary, Action<DoctorAction> open, Func<ITextClipboard?> clipboard)
    {
        _action = action;
        _open = open;
        IsPrimary = isPrimary;
        if(action.Kind == DoctorActionKind.CopyText)
            Copy = new CopyButtonViewModel(action.Label, () => action.Value, clipboard, isPrimary);
    }

    public bool IsPrimary { get; }

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

    [RelayCommand]
    private void Open()
    {
        _open(_action);
    }
}
