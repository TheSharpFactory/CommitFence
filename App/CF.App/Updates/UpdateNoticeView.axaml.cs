using Avalonia.Controls;

namespace CF.App.Updates;

/// <summary>
/// The notice of an update, over the bottom of the screen: the version, the release notes, Update now, Later, and the
/// progress of the download with Cancel.
/// </summary>
internal sealed partial class UpdateNoticeView : UserControl
{
    public UpdateNoticeView()
    {
        InitializeComponent();
    }
}