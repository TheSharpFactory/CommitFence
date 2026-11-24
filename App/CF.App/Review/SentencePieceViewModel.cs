using CF.App.Rules.Page;
using CF.Core.Rules;

namespace CF.App.Review;

/// <summary>
/// One piece of a team rule's sentence as the open finding shows it: a plain word, the artifact the rule is for, or one
/// condition of the where clause or of the require clause.
/// </summary>
internal sealed class SentencePieceViewModel
{
    private SentencePieceViewModel(SentencePieceKind kind, string lead, string text, string comparison, string value)
    {
        Kind = kind;
        Lead = lead;
        Text = text;
        Comparison = comparison;
        Value = value;
    }

    public SentencePieceKind Kind { get; }

    /// <summary>
    /// The plain word that stands before the token or tokens of the piece: "For", "where", "and", "require", "forbid".
    /// </summary>
    public string Lead { get; }

    /// <summary>
    /// The artifact or the property.
    /// </summary>
    public string Text { get; }

    public string Comparison { get; }

    public string Value { get; }

    public bool IsWord
    {
        get
        {
            return Kind == SentencePieceKind.Word;
        }
    }

    public bool IsFor
    {
        get
        {
            return Kind == SentencePieceKind.For;
        }
    }

    public bool IsWhere
    {
        get
        {
            return Kind == SentencePieceKind.Where;
        }
    }

    public bool IsRequire
    {
        get
        {
            return Kind == SentencePieceKind.Require;
        }
    }

    public bool IsCondition
    {
        get
        {
            return IsWhere || IsRequire;
        }
    }

    /// <summary>
    /// The piece as plain words, for a screen reader and for the checks.
    /// </summary>
    public string Spoken
    {
        get
        {
            return string.Join(" ", new[] { Lead, Text, Comparison, Value }.Where(part => part.Length > 0));
        }
    }

    /// <summary>
    /// The sentence of a rule in pieces that never break: "For" with the artifact; "where" or "and" with a condition's two joined
    /// tokens; "require" with its condition, or "forbid" alone. A row of pieces breaks between pieces only.
    /// </summary>
    public static IReadOnlyList<SentencePieceViewModel> Of(RuleDefinition rule)
    {
        var pieces = new List<SentencePieceViewModel> { new SentencePieceViewModel(SentencePieceKind.For, "For", SentenceWords.Artifact(rule.ArtifactType), string.Empty, string.Empty) };
        AddClause(pieces, "where", rule.Where, SentencePieceKind.Where);
        if(rule.Forbid)
            pieces.Add(new SentencePieceViewModel(SentencePieceKind.Word, "forbid", string.Empty, string.Empty, string.Empty));
        else
            AddClause(pieces, "require", rule.Require, SentencePieceKind.Require);

        return pieces;
    }

    /// <summary>
    /// The sample first rule, written as a finding writes a rule: the picture on the Rules screen when no rule is open.
    /// </summary>
    public static IReadOnlyList<SentencePieceViewModel> Specimen()
    {
        return new List<SentencePieceViewModel>
        {
            new SentencePieceViewModel(SentencePieceKind.For, "For", "if statement", string.Empty, string.Empty),
            new SentencePieceViewModel(SentencePieceKind.Where, "where", "statements in body", "is", "1"),
            new SentencePieceViewModel(SentencePieceKind.Where, "and", "body fits on one line", "is", "true"),
            new SentencePieceViewModel(SentencePieceKind.Require, "require", "braces", "is", "false")
        };
    }

    private static void AddClause(List<SentencePieceViewModel> pieces, string word, IReadOnlyList<RuleCondition> conditions, SentencePieceKind kind)
    {
        for(var index = 0; index < conditions.Count; index++)
        {
            var condition = conditions[index];
            var values = condition.ExpectedValues.Select(value => value.ToString()).ToList();
            pieces.Add(new SentencePieceViewModel(kind, index == 0 ? word : "and", SentenceWords.Property(condition.PropertyName), SentenceWords.Comparison(condition.Comparison == CF.Core.Rules.Comparison.IsOneOf), SentenceWords.Value(values)));
        }
    }
}

/// <summary>
/// What a piece of the sentence is.
/// </summary>
internal enum SentencePieceKind
{
    Word,

    For,

    Where,

    Require
}
