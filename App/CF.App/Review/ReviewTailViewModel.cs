namespace CF.App.Review;

/// <summary>
/// The last entry of the page: the line that closes the list of files, and the files of a folder that have no findings.
/// </summary>
internal sealed class ReviewTailViewModel : ReviewListItemViewModel
{
    public ReviewTailViewModel(ReviewViewModel model)
    {
        Model = model;
    }

    public ReviewViewModel Model { get; }
}
