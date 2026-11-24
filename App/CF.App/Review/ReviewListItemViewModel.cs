using CommunityToolkit.Mvvm.ComponentModel;

namespace CF.App.Review;

/// <summary>
/// One entry of the page of a review: the head (the verdict and what stands under it), a file's line, a finding, or the tail.
/// The page is one flat collection of these, so that a second kind of row is one more class and one more template.
/// </summary>
internal abstract class ReviewListItemViewModel : ObservableObject
{
}
