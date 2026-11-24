using CF.App.Parts;
using CF.Core.Code;

namespace CF.App.Rules;

/// <summary>A grouped line of scope descriptions beneath a rule title.</summary>
internal sealed class ExceptionLine
{
    public ExceptionLine(IGrouping<string, FileRulePlace> group)
    {
        Text = FileRule.DescribeGroup(group);
        var locations = group.ToList();
        Kind = Page.FileRulePageViewModel.PlaceKind(locations[0]);
        Parts = locations.Select((place, index) => new ExceptionLocation(index == 0 ? group.Key : string.Empty,
            index == 0 ? " for " : index == locations.Count - 1 ? "and " : string.Empty,
            place.Pattern, $", line {place.Line}" + (index < locations.Count - 1 ? "," : string.Empty))).ToList();
    }

    public string Text { get; }
    public EffectKind Kind { get; }
    public IReadOnlyList<ExceptionLocation> Parts { get; }
}

/// <summary>One pattern and its setting line, kept together when the row wraps.</summary>
internal sealed record ExceptionLocation(string Effect, string Join, string Pattern, string Location);