using CF.App.Parts;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CF.App.Rules.Page;

/// <summary>
/// One row of the table on the page of a file rule: the file's words for an effect, what the product makes of them, what a
/// push does then, and whether the file holds those words now. The three rows are the same for every file rule; only the one
/// marked changes.
/// </summary>
internal sealed partial class FileSettingRowViewModel : ObservableObject
{
    public FileSettingRowViewModel(string words, EffectKind kind, string effectWord, string push)
    {
        Words = words;
        Kind = kind;
        EffectWord = effectWord;
        Push = push;
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SpokenName), nameof(ShowsCurrentLabel))]
    public partial bool IsCurrent { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasTag), nameof(ShowsCurrentLabel), nameof(SpokenName))]
    public partial string Tag { get; set; } = string.Empty;

    public bool HasTag
    {
        get
        {
            return Tag.Length > 0;
        }
    }

    public bool ShowsCurrentLabel
    {
        get
        {
            return IsCurrent && !HasTag;
        }
    }

    public string Words { get; }

    public EffectKind Kind { get; }

    public string EffectWord { get; }

    public string Push { get; }

    public bool IsBlock
    {
        get
        {
            return Kind == EffectKind.Block;
        }
    }

    public bool IsWarn
    {
        get
        {
            return Kind == EffectKind.Warn;
        }
    }

    public bool IsOff
    {
        get
        {
            return Kind == EffectKind.Off;
        }
    }

    public string SpokenName
    {
        get
        {
            return $"{Words}: {EffectWord}. {Push}{(HasTag ? " " + Tag : IsCurrent ? " In the file now." : string.Empty)}";
        }
    }
}