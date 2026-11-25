using System.Globalization;
using CF.App.Updates;
using CF.Core.Services;
using CF.Core.Updates;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CF.App.Settings;

/// <summary>
/// The release notes box of Settings: the notes of the version waiting while one waits ("What's new in 1.0.1.0"), else those of the
/// version installed ("What's in 1.0.0.1"). Folded to the headings with how many notes each holds, or shown whole.
/// </summary>
internal sealed partial class ReleaseNotesViewModel : ObservableObject
{
    private readonly UpdatesViewModel _updates;
    private readonly IProductInfoService _product;

    public ReleaseNotesViewModel(UpdatesViewModel updates, IProductInfoService product)
    {
        _updates = updates;
        _product = product;
        _updates.PropertyChanged += (_, _) => OnPropertyChanged(string.Empty);
    }

    /// <summary>
    /// Whether the notes are shown whole: false when the screen opens.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsFolded), nameof(ShowsWhole))]
    public partial bool IsShown { get; set; }

    /// <summary>
    /// The version the notes are about: the one that waits, or the one installed.
    /// </summary>
    private ReleaseInfo? Waiting
    {
        get { return _updates.AvailableRelease; }
    }

    private string? Markdown
    {
        get { return Waiting != null ? Waiting.Notes : _product.Notes?.Markdown; }
    }

    public bool HasNotes
    {
        get { return !string.IsNullOrWhiteSpace(Markdown) && ReleaseNotes.Lines(Markdown, int.MaxValue).Count > 0; }
    }

    public string Heading
    {
        get { return Waiting != null ? $"What's new in {Waiting.Version}" : $"What's in {_product.Version}"; }
    }

    /// <summary>
    /// "Released 24 Nov 2026" for the version installed; nothing for a version that waits (the release does not say when it was made) or
    /// a changelog heading with no date.
    /// </summary>
    public string Released
    {
        get
        {
            if(Waiting != null || _product.Notes?.Released is not { } date)
                return string.Empty;

            return "Released " + date.ToString("d MMM yyyy", CultureInfo.InvariantCulture);
        }
    }

    public bool HasReleased
    {
        get { return Released.Length > 0; }
    }

    /// <summary>
    /// The headings of the notes, each with its count, in the order they come.
    /// </summary>
    public IReadOnlyList<NoteHeadingViewModel> Headings
    {
        get { return HeadingsOf(ReleaseNotes.Lines(Markdown, int.MaxValue)); }
    }

    public bool HasHeadings
    {
        get { return Headings.Count > 0; }
    }

    /// <summary>
    /// The notes as shown: the whole of them, so that the items shown agree with the counts of the folded headings.
    /// </summary>
    public IReadOnlyList<NoteLineViewModel> Lines
    {
        get { return ReleaseNotes.Lines(Markdown, int.MaxValue).Select(line => new NoteLineViewModel(line)).ToList(); }
    }

    public bool ShowsFolded
    {
        get { return !IsShown; }
    }

    public bool ShowsWhole
    {
        get { return IsShown; }
    }

    /// <summary>
    /// "On the release page" is offered when the release the notes belong to has a page to open.
    /// </summary>
    public bool CanOpenReleasePage
    {
        get { return Waiting?.PageUri != null; }
    }

    [RelayCommand]
    private void ShowTheNotes()
    {
        IsShown = true;
    }

    [RelayCommand]
    private void HideTheNotes()
    {
        IsShown = false;
    }

    /// <summary>
    /// "On the release page": opens the release the notes belong to in the browser.
    /// </summary>
    [RelayCommand]
    private void OpenReleasePage()
    {
        _updates.OpenReleasePageCommand.Execute(null);
    }

    private static List<NoteHeadingViewModel> HeadingsOf(List<ReleaseNoteLine> lines)
    {
        var headings = new List<NoteHeadingViewModel>();
        var index = 0;
        while(index < lines.Count)
        {
            if(!lines[index].IsHeading)
            {
                index++;
                continue;
            }

            var name = lines[index].Text;
            var under = lines.Skip(index + 1).TakeWhile(line => !line.IsHeading).ToList();
            var notes = under.Any(line => line.IsListItem) ? under.Count(line => line.IsListItem) : under.Count(line => line.IsParagraph);
            headings.Add(new NoteHeadingViewModel(name, notes));
            index += under.Count + 1;
        }

        return headings;
    }
}
