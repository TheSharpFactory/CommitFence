using CF.Core.Rules;

namespace CF.App.Rules.Page;

/// <summary>
/// What a rule says, kept field by field and item by item, so that two rules are the same only when every field and every
/// list item is the same. It is compared as a whole, never joined into one text that two different rules could share.
/// </summary>
internal sealed class RuleSignature : IEquatable<RuleSignature>
{
    private readonly string _title;
    private readonly string? _artifactType;
    private readonly bool _forbid;
    private readonly string _message;
    private readonly string _why;
    private readonly IReadOnlyList<string> _paths;
    private readonly IReadOnlyList<ConditionSignature> _where;
    private readonly IReadOnlyList<ConditionSignature> _require;

    public RuleSignature(string title, string? artifactType, bool forbid, string message, string why, IReadOnlyList<string> paths, IReadOnlyList<ConditionSignature> where, IReadOnlyList<ConditionSignature> require)
    {
        _title = title;
        _artifactType = artifactType;
        _forbid = forbid;
        _message = message;
        _why = why;
        _paths = paths;
        _where = where;
        _require = require;
    }

    public bool Equals(RuleSignature? other)
    {
        return other != null
            && _title == other._title
            && _artifactType == other._artifactType
            && _forbid == other._forbid
            && _message == other._message
            && _why == other._why
            && _paths.SequenceEqual(other._paths)
            && _where.SequenceEqual(other._where)
            && _require.SequenceEqual(other._require);
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as RuleSignature);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(_title, _artifactType, _forbid, _paths.Count, _where.Count, _require.Count);
    }
}

/// <summary>
/// One condition of a rule, kept as its property, its comparison, its form and each of its values on its own.
/// </summary>
internal sealed class ConditionSignature : IEquatable<ConditionSignature>
{
    private readonly string? _propertyName;
    private readonly Comparison _comparison;
    private readonly bool _isList;
    private readonly IReadOnlyList<string> _values;

    public ConditionSignature(string? propertyName, Comparison comparison, bool isList, IReadOnlyList<string> values)
    {
        _propertyName = propertyName;
        _comparison = comparison;
        _isList = isList;
        _values = values;
    }

    public bool Equals(ConditionSignature? other)
    {
        return other != null && _propertyName == other._propertyName && _comparison == other._comparison && _isList == other._isList && _values.SequenceEqual(other._values);
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as ConditionSignature);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(_propertyName, _comparison, _isList, _values.Count);
    }
}
