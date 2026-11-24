namespace CF.App.Review;

/// <summary>
/// The first entry of the page: everything above the files, which scrolls away with them. It is drawn from the screen's own model.
/// </summary>
internal sealed class ReviewHeadViewModel : ReviewListItemViewModel
{
    public ReviewHeadViewModel(ReviewViewModel model)
    {
        Model = model;
    }

    public ReviewViewModel Model { get; }
}
