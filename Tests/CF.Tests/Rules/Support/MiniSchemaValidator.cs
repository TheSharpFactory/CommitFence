using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace CF.Tests.Rules.Support;

/// <summary>
/// Judges a JSON text against a schema, for the keywords the generated settings schema uses and no others: a keyword
/// it does not know stops the test, so that the schema cannot use one that the check silently skips.
/// </summary>
/// <remarks>
/// Values are compared as JSON values, not as spelled: an escaped text equals the same text written plainly, and a number
/// is a whole number when its value is, as in the draft. Patterns run with the ECMAScript option, as an editor's do, and
/// a $ matches only at the very end of the text.
/// </remarks>
internal sealed class MiniSchemaValidator
{
    private static readonly HashSet<string> _annotations = new HashSet<string>(["$schema", "$id", "$defs", "title", "description", "enumDescriptions", "then"], StringComparer.Ordinal);

    private readonly JsonElement _root;

    public MiniSchemaValidator(JsonElement root)
    {
        _root = root;
    }

    public bool IsValid(string text)
    {
        using var document = JsonDocument.Parse(text);
        return Check(_root, document.RootElement);
    }

    private bool Check(JsonElement schema, JsonElement value)
    {
        return schema.EnumerateObject().All(keyword => Holds(keyword, schema, value));
    }

    private bool Holds(JsonProperty keyword, JsonElement schema, JsonElement value)
    {
        var argument = keyword.Value;
        switch(keyword.Name)
        {
            case "$ref":
                return Check(Resolve(argument.GetString()!), value);
            case "type":
                return TypeHolds(argument, value);
            case "enum":
                return argument.EnumerateArray().Any(item => SameValue(item, value));
            case "const":
                return SameValue(argument, value);
            case "pattern":
                return value.ValueKind != JsonValueKind.String || Matches(value.GetString()!, argument.GetString()!);
            case "required":
                return value.ValueKind != JsonValueKind.Object || argument.EnumerateArray().All(name => value.TryGetProperty(name.GetString()!, out _));
            case "properties":
                return value.ValueKind != JsonValueKind.Object || PropertiesHold(argument, value);
            case "additionalProperties":
                return value.ValueKind != JsonValueKind.Object || OthersHold(argument, schema, value);
            case "propertyNames":
                return value.ValueKind != JsonValueKind.Object || value.EnumerateObject().All(property => Matches(property.Name, argument.GetProperty("pattern").GetString()!));
            case "items":
                return value.ValueKind != JsonValueKind.Array || value.EnumerateArray().All(item => Check(argument, item));
            case "minItems":
                return value.ValueKind != JsonValueKind.Array || value.GetArrayLength() >= argument.GetInt32();
            case "minProperties":
                return value.ValueKind != JsonValueKind.Object || value.EnumerateObject().Count() >= argument.GetInt32();
            case "maxProperties":
                return value.ValueKind != JsonValueKind.Object || value.EnumerateObject().Count() <= argument.GetInt32();
            case "minimum":
                return !IsNumber(value) || NumberOf(value) >= NumberOf(argument);
            case "maximum":
                return !IsNumber(value) || NumberOf(value) <= NumberOf(argument);
            case "anyOf":
                return argument.EnumerateArray().Any(option => Check(option, value));
            case "oneOf":
                return argument.EnumerateArray().Count(option => Check(option, value)) == 1;
            case "allOf":
                return argument.EnumerateArray().All(part => Check(part, value));
            case "if":
                return !Check(argument, value) || !schema.TryGetProperty("then", out var then) || Check(then, value);
            default:
                return _annotations.Contains(keyword.Name) ? true : throw new NotSupportedException("The check does not know the keyword " + keyword.Name);
        }
    }

    private JsonElement Resolve(string reference)
    {
        const string prefix = "#/$defs/";
        return _root.GetProperty("$defs").GetProperty(reference[prefix.Length..]);
    }

    private static bool TypeHolds(JsonElement argument, JsonElement value)
    {
        var names = argument.ValueKind == JsonValueKind.Array ? argument.EnumerateArray().Select(name => name.GetString()!) : [argument.GetString()!];
        return names.Any(name => IsOfType(name, value));
    }

    private static bool IsOfType(string name, JsonElement value)
    {
        return name switch
        {
            "object" => value.ValueKind == JsonValueKind.Object,
            "array" => value.ValueKind == JsonValueKind.Array,
            "string" => value.ValueKind == JsonValueKind.String,
            "boolean" => value.ValueKind is JsonValueKind.True or JsonValueKind.False,
            "number" => IsNumber(value),
            "integer" => IsNumber(value) && decimal.Truncate(NumberOf(value)) == NumberOf(value),
            _ => throw new NotSupportedException("The check does not know the type " + name)
        };
    }

    private static bool IsNumber(JsonElement value)
    {
        return value.ValueKind == JsonValueKind.Number;
    }

    /// <summary>The value of a number; one too large for a decimal is held at the decimal's own limit.</summary>
    private static decimal NumberOf(JsonElement number)
    {
        var text = number.GetRawText();
        if(decimal.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            return value;

        return text.StartsWith('-') ? decimal.MinValue : decimal.MaxValue;
    }

    /// <summary>JSON equality: the same kind and the same value, however the value was spelled.</summary>
    private static bool SameValue(JsonElement first, JsonElement second)
    {
        if(first.ValueKind != second.ValueKind)
            return false;

        switch(first.ValueKind)
        {
            case JsonValueKind.String:
                return first.GetString() == second.GetString();
            case JsonValueKind.Number:
                return NumberOf(first) == NumberOf(second);
            case JsonValueKind.Array:
                return first.GetArrayLength() == second.GetArrayLength() && first.EnumerateArray().Zip(second.EnumerateArray()).All(pair => SameValue(pair.First, pair.Second));
            case JsonValueKind.Object:
                return first.EnumerateObject().Count() == second.EnumerateObject().Count()
                    && first.EnumerateObject().All(property => second.TryGetProperty(property.Name, out var other) && SameValue(property.Value, other));
            default:
                return true;
        }
    }

    private static bool Matches(string text, string pattern)
    {
        return Regex.IsMatch(text, WithStrictEnd(pattern), RegexOptions.ECMAScript | RegexOptions.CultureInvariant);
    }

    /// <summary>A $ outside a character class matches before a final newline in .NET and only at the end in ECMAScript.</summary>
    private static string WithStrictEnd(string pattern)
    {
        var strict = new StringBuilder();
        for(var index = 0; index < pattern.Length; index++)
        {
            var letter = pattern[index];
            if(letter == '\\' && index + 1 < pattern.Length)
                strict.Append(letter).Append(pattern[++index]);
            else if(letter == '$')
                strict.Append("(?![\\s\\S])");
            else
                strict.Append(letter);
        }

        return strict.ToString();
    }

    private bool PropertiesHold(JsonElement declared, JsonElement value)
    {
        return value.EnumerateObject().All(property => !declared.TryGetProperty(property.Name, out var schema) || Check(schema, property.Value));
    }

    private bool OthersHold(JsonElement argument, JsonElement schema, JsonElement value)
    {
        var others = value.EnumerateObject().Where(property => !schema.TryGetProperty("properties", out var declared) || !declared.TryGetProperty(property.Name, out _));
        if(argument.ValueKind == JsonValueKind.False)
            return !others.Any();

        return others.All(property => Check(argument, property.Value));
    }
}
