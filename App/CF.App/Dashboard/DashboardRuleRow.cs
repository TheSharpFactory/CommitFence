using CF.Core.Findings;
using CF.Core.Services;

namespace CF.App.Dashboard;

/// <summary>A rule's description, link and effect for either Dashboard list.</summary>
internal sealed record DashboardRuleRow(string Id, StatsRule? Rule, long Count, long Maximum)
{
    public string Title { get { return Rule?.Title ?? Id; } }
    public bool CanOpen { get { return Rule != null; } }
    public bool Unlisted { get { return Rule == null; } }
    public bool FromFile { get { return Rule?.FromFile == true; } }
    public bool FromTeam { get { return !FromFile; } }
    private bool HasEffect { get { return Rule?.Effect != null; } }
    public bool Blocks { get { return Rule?.Effect == Severity.Block; } }
    public bool Warns { get { return Rule?.Effect == Severity.Warn; } }
    public string Effect { get { return Blocks ? "Blocks" : "Warns"; } }
    public double Share { get { return Maximum > 0 && HasEffect ? (double)Count / Maximum : 0; } }
    public string SpokenName { get { return $"{Id} {Title}{(HasEffect ? ", " + Effect : string.Empty)}{(Count > 0 ? $", {Count} findings" : string.Empty)}"; } }
}
