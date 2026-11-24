using CF.App.Parts;
using CF.Core.Code;
using CommunityToolkit.Mvvm.Input;

namespace CF.App.Rules.Page;

/// <summary>The source and action for one setting on the rule page.</summary>
internal sealed partial class FilePlaceViewModel
{
    private readonly Action<int> _open;
    private readonly FileRulePlace _place;

    public FilePlaceViewModel(FileRule rule, FileRulePlace place, Action<int> open)
    {
        _place = place;
        _open = open;
        Scope = rule.Scope(place);
        ShowsHeading = rule.Exceptions.Count > 0;
        IsException = place != rule.Main;
        var lines = new List<FileLineViewModel>();
        foreach(var source in place.Lines)
        {
            if(lines.Count > 0 && source.Number > lines[^1].Number + 1)
                lines.Add(new FileLineViewModel { Text = $"{source.Number - lines[^1].Number - 1} lines not shown", IsPresent = true });

            var line = new FileLineViewModel();
            line.Take(source, source.Number == place.Line);
            lines.Add(line);
        }

        Lines = lines;
    }

    public IReadOnlyList<FileLineViewModel> Lines { get; }
    public bool ShowsHeading { get; }
    public bool IsException { get; }
    public string Scope { get; }
    public string Effect
    {
        get
        {
            return _place.Effect;
        }
    }

    public EffectKind Kind
    {
        get
        {
            return FileRulePageViewModel.PlaceKind(_place);
        }
    }

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

    public string ButtonText
    {
        get
        {
            return $"Open at line {_place.Line}";
        }
    }

    public string ButtonName
    {
        get
        {
            return $"Open .editorconfig at line {_place.Line}";
        }
    }

    public string SpokenName
    {
        get
        {
            return $"{Effect} for {Scope}, line {_place.Line}";
        }
    }

    [RelayCommand]
    private void Open()
    {
        _open(_place.Line);
    }
}