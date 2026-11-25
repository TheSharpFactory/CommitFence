namespace CF.App.Exemptions;

/// <summary>
/// The first entry of the list: everything above the files (the summary, the panel in one pane, the rule chosen, the search and the
/// line above the files), which scrolls away with them. It is drawn from the screen's own model and is never replaced while the list
/// is laid out again, so that the keyboard stays in the search while the person types.
/// </summary>
internal sealed class ExemptionHeadViewModel : ExemptionListItem
{
    public ExemptionHeadViewModel(ExemptionsViewModel model)
    {
        Model = model;
    }

    public ExemptionsViewModel Model { get; }
}
