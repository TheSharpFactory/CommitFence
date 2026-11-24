using System.Text.Json;
using CF.Core.Code;

namespace CF.Core.Rules.Settings;

/// <summary>
/// Turns the where or the require object of a rule into conditions, checking each against the artifact type's catalog.
/// </summary>
internal sealed class ConditionReader
{
    private readonly JsonPositions _positions;

    public ConditionReader(JsonPositions positions)
    {
        _positions = positions;
    }

    public IReadOnlyList<RuleCondition> Read(Dictionary<string, JsonElement>? entries, ArtifactType artifactType, string path)
    {
        var conditions = new List<RuleCondition>();
        if(entries == null)
            return conditions;

        if(entries.Count == 0)
            throw _positions.ErrorAt(path, $"'{LastKey(path)}' holds no conditions; leave it out instead.");

        foreach(var entry in entries)
            conditions.Add(ReadCondition(entry.Key, entry.Value, artifactType, JsonPositions.Join(path, entry.Key)));

        return conditions;
    }

    private static string LastKey(string path)
    {
        return path.Substring(path.LastIndexOf('.') + 1);
    }

    private RuleCondition ReadCondition(string name, JsonElement element, ArtifactType artifactType, string path)
    {
        var descriptor = artifactType.FindProperty(name);
        if(descriptor == null)
            throw _positions.ErrorAt(path, UnknownPropertyMessage(name, artifactType));

        if(element.ValueKind != JsonValueKind.Array)
            return new RuleCondition(name, Comparison.Is, new[] { ReadValue(element, descriptor, artifactType, path) });

        return ReadOneOf(element, descriptor, artifactType, path);
    }

    private RuleCondition ReadOneOf(JsonElement array, PropertyDescriptor descriptor, ArtifactType artifactType, string path)
    {
        var values = new List<PropertyValue>();
        var index = 0;
        foreach(var element in array.EnumerateArray())
        {
            values.Add(ReadValue(element, descriptor, artifactType, path + "[" + index + "]"));
            index++;
        }

        if(values.Count == 0)
            throw _positions.ErrorAt(path, $"The list for '{descriptor.Name}' is empty; give at least one value.");

        return new RuleCondition(descriptor.Name, Comparison.IsOneOf, values);
    }

    private PropertyValue ReadValue(JsonElement element, PropertyDescriptor descriptor, ArtifactType artifactType, string path)
    {
        var value = ToValue(element);
        if(value == null || value.Type != descriptor.ValueType)
            throw _positions.ErrorAt(path, $"The property '{descriptor.Name}' of {artifactType.Name} takes {Describe(descriptor.ValueType)}.");

        if(value.Type == PropertyValueType.Text && descriptor.AllowedValues.Count > 0 && !descriptor.AllowedValues.Contains(value.Text))
            throw _positions.ErrorAt(path, $"'{value.Text}' is not a value of '{descriptor.Name}'. Use one of: {string.Join(", ", descriptor.AllowedValues)}.");

        return value;
    }

    private static PropertyValue? ToValue(JsonElement element)
    {
        switch(element.ValueKind)
        {
            case JsonValueKind.String:
                return PropertyValue.FromText(element.GetString() ?? string.Empty);
            case JsonValueKind.True:
                return PropertyValue.FromBoolean(true);
            case JsonValueKind.False:
                return PropertyValue.FromBoolean(false);
            case JsonValueKind.Number:
                return element.TryGetInt64(out var number) ? PropertyValue.FromNumber(number) : null;
            default:
                return null;
        }
    }

    private static string Describe(PropertyValueType type)
    {
        switch(type)
        {
            case PropertyValueType.Text:
                return "text";
            case PropertyValueType.Number:
                return "a whole number";
            default:
                return "true or false";
        }
    }

    private static string UnknownPropertyMessage(string name, ArtifactType artifactType)
    {
        var known = string.Join(", ", artifactType.Properties.Select(property => property.Name));
        return $"'{name}' is not a property of {artifactType.Name}. Known properties: {known}.";
    }
}