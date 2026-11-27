using CF.Core.Code;

namespace CF.Core.Rules;

/// <summary>
/// Builds the rule a draft or a change describes.
/// </summary>
/// <remarks>
/// It does not judge it: the strict reader does that, on the text the rule would be written as, so that a change is
/// refused for exactly the reasons a file would be.
/// </remarks>
internal sealed class RuleEditor
{
    private readonly IReadOnlyList<ILanguage> _languages;

    public RuleEditor(IReadOnlyList<ILanguage> languages)
    {
        _languages = languages;
    }

    /// <summary>
    /// Refuses an id that a new rule may not have: letters, digits, '-', '_' and '.' only. A settings file that already
    /// holds another kind of id is still read as it is; only making a new id is limited.
    /// </summary>
    public static void CheckNewId(string id)
    {
        if(RuleId.NewRuleProblem(id, []) != null)
            throw new RuleChangeException($"'{id}' is not a rule id. An id is letters, digits, '-', '_' and '.', with no space.");
    }

    public RuleDefinition Create(RuleDraft draft)
    {
        CheckNewId(draft.Id);
        var language = ChooseLanguage(draft.Language);
        var where = ToConditions(draft.Where, language, draft.ArtifactType, "require");
        var require = ToConditions(draft.Require, language, draft.ArtifactType, "where");

        return new RuleDefinition(draft.Id, language, draft.ArtifactType, draft.Severity, draft.Title, where, require, draft.Forbid, draft.Paths, NothingWhenEmpty(draft.Message), NothingWhenEmpty(draft.Why), draft.Enabled);
    }

    public RuleDefinition Change(RuleDefinition rule, RuleChange change)
    {
        var artifactType = change.ArtifactType ?? rule.ArtifactType;
        var where = change.Where == null ? rule.Where : ToConditions(change.Where, rule.Language, artifactType, "require");
        var require = change.Require == null ? rule.Require : ToConditions(change.Require, rule.Language, artifactType, "where");
        (var forbid, require) = ReconcileForbidAndRequire(rule, change, require);

        return new RuleDefinition(rule.Id, rule.Language, artifactType, rule.Severity, change.Title ?? rule.Title, where, require, forbid, change.Paths ?? rule.Paths, Replace(rule.Message, change.Message), Replace(rule.Why, change.Why), rule.Enabled);
    }

    /// <summary>
    /// Whether the changed rule forbids, and what it requires.
    /// </summary>
    /// <remarks>
    /// A change that says forbid decides it; a change that gives requirements turns a forbidding rule into a requiring
    /// one; otherwise the rule keeps what it had. A rule that is made to forbid, with no requirements given in the same
    /// change, loses the requirements it had.
    /// </remarks>
    private static (bool Forbid, IReadOnlyList<RuleCondition> Require) ReconcileForbidAndRequire(RuleDefinition rule, RuleChange change, IReadOnlyList<RuleCondition> require)
    {
        var forbid = change.Forbid ?? (change.Require == null && rule.Forbid);
        if(change.Forbid == true && change.Require == null)
            return (forbid, new List<RuleCondition>());

        return (forbid, require);
    }

    private static string? Replace(string? current, string? replacement)
    {
        return replacement == null ? current : NothingWhenEmpty(replacement);
    }

    private static string? NothingWhenEmpty(string? text)
    {
        return string.IsNullOrEmpty(text) ? null : text;
    }

    private string ChooseLanguage(string? languageId)
    {
        if(!string.IsNullOrWhiteSpace(languageId))
            return languageId;

        if(_languages.Count == 1)
            return _languages[0].Id;

        throw new RuleChangeException($"Say which language the rule is for. Languages: {string.Join(", ", _languages.Select(language => language.Id))}.");
    }

    private List<RuleCondition> ToConditions(IReadOnlyList<ConditionDraft> drafts, string languageId, string artifactTypeName, string otherClause)
    {
        var refusal = ClauseShape.Refusal(drafts, otherClause);
        if(refusal != null)
            throw new RuleChangeException(refusal);

        var artifactType = _languages.FirstOrDefault(language => language.Id == languageId)?.Catalog.FindArtifactType(artifactTypeName);
        return drafts.Select(draft => ToCondition(draft, artifactType)).ToList();
    }

    private static RuleCondition ToCondition(ConditionDraft draft, ArtifactType? artifactType)
    {
        var descriptor = artifactType?.FindProperty(draft.PropertyName);
        var values = draft.Values.Select(text => ToValue(text, descriptor)).ToList();

        return new RuleCondition(draft.PropertyName, draft.Comparison, values, draft.IsList);
    }

    /// <summary>
    /// A typed text becomes the kind of value its property holds. A text that does not fit stays text, and the strict
    /// reader then refuses it with the property's own message.
    /// </summary>
    private static PropertyValue ToValue(string text, PropertyDescriptor? descriptor)
    {
        if(descriptor?.ValueType == PropertyValueType.Number && long.TryParse(text, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var number))
            return PropertyValue.FromNumber(number);

        if(descriptor?.ValueType == PropertyValueType.Boolean && bool.TryParse(text, out var flag))
            return PropertyValue.FromBoolean(flag);

        return PropertyValue.FromText(text);
    }
}