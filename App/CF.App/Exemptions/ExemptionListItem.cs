using CommunityToolkit.Mvvm.ComponentModel;

namespace CF.App.Exemptions;

/// <summary>
/// One entry of the list under the head of the screen: a file's line, an exemption, or the tail. The list is one flat collection of
/// these, so that each kind is one class and one template.
/// </summary>
internal abstract class ExemptionListItem : ObservableObject
{
}
