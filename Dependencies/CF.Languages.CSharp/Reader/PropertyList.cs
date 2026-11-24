using CF.Core.Code;

namespace CF.Languages.CSharp.Reader;

/// <summary>
/// Collects the properties of one artifact while its reader works through the syntax.
/// </summary>
internal sealed class PropertyList
{
    private readonly List<Property> _properties = new List<Property>();

    public PropertyList Syntax(string name, string text)
    {
        return Add(name, PropertyKind.Syntax, PropertyValue.FromText(text));
    }

    public PropertyList Syntax(string name, long number)
    {
        return Add(name, PropertyKind.Syntax, PropertyValue.FromNumber(number));
    }

    public PropertyList Syntax(string name, bool flag)
    {
        return Add(name, PropertyKind.Syntax, PropertyValue.FromBoolean(flag));
    }

    public IReadOnlyList<Property> ToList()
    {
        return _properties;
    }

    private PropertyList Add(string name, PropertyKind kind, PropertyValue value)
    {
        _properties.Add(new Property(name, kind, value));
        return this;
    }
}