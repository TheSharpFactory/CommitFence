using CommunityToolkit.Mvvm.ComponentModel;

namespace CF.App.Rules;

/// <summary>
/// One entry of the list of rules: a group heading, a row, or a line of words under a heading. The list is one flat
/// collection of these, so that a second group and a second kind of row are one more class and one more template.
/// </summary>
internal abstract class RuleListItemViewModel : ObservableObject
{
}
