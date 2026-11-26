namespace CF.App.Exemptions;

internal sealed record ExemptionRuleGroupHeading(string Title, string CountText, ExemptionRuleSource Source, bool FollowsGroup)
{
    public bool IsCustom { get { return Source == ExemptionRuleSource.Custom; } }
    public bool IsFromFile { get { return Source == ExemptionRuleSource.File; } }

    public string SpokenName
    {
        get { return $"{Title}, {CountText}"; }
    }
}
