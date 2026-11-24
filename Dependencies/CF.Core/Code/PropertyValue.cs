using System.Globalization;

namespace CF.Core.Code;

/// <summary>
/// The value of a property: text, a whole number or a boolean. Two values are equal when their type and content are.
/// </summary>
public sealed class PropertyValue : IEquatable<PropertyValue>
{
    private PropertyValue(PropertyValueType type, string text, long number, bool flag)
    {
        Type = type;
        Text = text;
        Number = number;
        Flag = flag;
    }

    /// <summary>
    /// The kind of value this is.
    /// </summary>
    public PropertyValueType Type { get; }

    /// <summary>
    /// The text, when the type is text; empty otherwise.
    /// </summary>
    public string Text { get; }

    /// <summary>
    /// The number, when the type is number; zero otherwise.
    /// </summary>
    public long Number { get; }

    /// <summary>
    /// The boolean, when the type is boolean; false otherwise.
    /// </summary>
    public bool Flag { get; }

    /// <summary>
    /// A text value.
    /// </summary>
    public static PropertyValue FromText(string text)
    {
        return new PropertyValue(PropertyValueType.Text, text, 0, false);
    }

    /// <summary>
    /// A whole-number value.
    /// </summary>
    public static PropertyValue FromNumber(long number)
    {
        return new PropertyValue(PropertyValueType.Number, string.Empty, number, false);
    }

    /// <summary>
    /// A boolean value.
    /// </summary>
    public static PropertyValue FromBoolean(bool flag)
    {
        return new PropertyValue(PropertyValueType.Boolean, string.Empty, 0, flag);
    }

    /// <inheritdoc />
    public bool Equals(PropertyValue? other)
    {
        if(other == null)
            return false;

        return Type == other.Type && Text == other.Text && Number == other.Number && Flag == other.Flag;
    }

    /// <inheritdoc />
    public override bool Equals(object? obj)
    {
        return Equals(obj as PropertyValue);
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        return HashCode.Combine(Type, Text, Number, Flag);
    }

    /// <summary>
    /// The value as a person would write it: the text itself, the number, or true or false.
    /// </summary>
    public override string ToString()
    {
        if(Type == PropertyValueType.Text)
            return Text;

        if(Type == PropertyValueType.Number)
            return Number.ToString(CultureInfo.InvariantCulture);

        return Flag ? "true" : "false";
    }
}