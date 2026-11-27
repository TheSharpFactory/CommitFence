using CF.App.Parts;
using CF.Core.Code;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CF.App.Rules;

/// <summary>A visible setting in a file rule row and the link to its source.</summary>
internal sealed partial class FileRowPlace(FileRulePlace place, bool isEverywhereElse, Action<int> openFile) : ObservableObject
{
    [ObservableProperty]
    public partial bool IsFocused { get; set; }

    [ObservableProperty]
    public partial bool IsDimmed { get; set; }

    public bool IsEverywhereElse { get; } = isEverywhereElse;

    public EffectKind Kind { get { return Page.FileRulePageViewModel.PlaceKind(place); } }

    public bool IsBlock { get { return Kind == EffectKind.Block; } }
    public bool IsWarn { get { return Kind == EffectKind.Warn; } }
    public bool IsOff { get { return Kind == EffectKind.Off; } }

    public string Effect { get { return place.Effect; } }

    public string Where { get { return IsEverywhereElse ? "everywhere else" : "in"; } }

    public IReadOnlyList<string> Pattern { get; } = [place.Pattern];

    public string LineText { get { return $"line {place.Line}"; } }

    public string LinkName { get { return $"Line {place.Line} of .editorconfig, {(IsEverywhereElse ? "everywhere else" : place.Pattern)}, {place.Setting}"; } }

    public string Section { get { return $"[{place.Pattern}]"; } }

    public string TipEnd { get { return $" sets it to {place.Setting}. Opens the file at this line."; } }

    public void ApplyFilter(RuleFilter filter)
    {
        IsDimmed = filter != RuleFilter.All && !FileRuleRowViewModel.MatchesEffect(place, filter);
    }

    public string SpokenName
    {
        get { return $"{Effect.ToLowerInvariant()} {Where}{(IsEverywhereElse ? "" : " " + place.Pattern)}, {LineText}"; }
    }

    [RelayCommand]
    private void Open()
    {
        openFile(place.Line);
    }
}
