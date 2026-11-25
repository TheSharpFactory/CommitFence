using CommunityToolkit.Mvvm.Input;

namespace CF.App.Shell;

/// <summary>
/// What the window asks of a screen after Rules and Review: F5 does the screen's "again" (read the history again, check the
/// machine again, ...), which each screen words for itself.
/// </summary>
internal interface IScreenModel
{
    IRelayCommand AgainCommand { get; }
}
