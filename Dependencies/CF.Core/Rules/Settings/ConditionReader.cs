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
            ReadCondition(conditions, entry.Key, entry.Value, artifactType, JsonPositions.Join(path, entry.Key));

        return conditions;
    }

    private static string LastKey(string path)
    {
        return path.Substring(path.LastIndexOf('.') + 1);
    }

    private void ReadCondition(List<RuleCondition> conditions, string name, JsonElement element, ArtifactType artifactType, string path)
    {
        var descriptor = artifactType.FindProperty(name);
        if(descriptor == null)
            throw _positions.ErrorAt(path, UnknownPropertyMessage(name, artifactType));

        switch(element.ValueKind)
        {
            case JsonValueKind.Object:
                conditions.AddRange(ReadObject(element, descriptor, artifactType, path));
                break;
            case JsonValueKind.Array:
                conditions.Add(ReadList(element, descriptor, artifactType, path, Comparison.IsOneOf));
                break;
            default:
                conditions.Add(new RuleCondition(name, Comparison.Is, [ReadValue(element, descriptor, artifactType, path)]));
                break;
        }
    }

    private List<RuleCondition> ReadObject(JsonElement element, PropertyDescriptor descriptor, ArtifactType artifactType, string path)
    {
        var conditions = new List<RuleCondition>();
        foreach(var entry in element.EnumerateObject())
        {
            var entryPath = JsonPositions.Join(path, entry.Name);
            var comparison = RuleComparisons.FromObjectKey(entry.Name);
            if(comparison == null)
                throw _positions.ErrorAt(entryPath, $"The comparison '{entry.Name}' is not known.");

            if(!RuleComparisons.For(descriptor.ValueType).Contains(comparison.Value))
                throw _positions.ErrorAt(entryPath, $"The property '{descriptor.Name}' of {artifactType.Name} cannot use '{entry.Name}'.");

            conditions.Add(ReadOperand(entry.Value, descriptor, artifactType, entryPath, comparison.Value));
        }

        CheckObject(conditions, path, descriptor.Name);
        return conditions;
    }

    private RuleCondition ReadOperand(JsonElement operand, PropertyDescriptor descriptor, ArtifactType artifactType, string path, Comparison comparison)
    {
        if(comparison == Comparison.IsNot && operand.ValueKind == JsonValueKind.Array)
            return ReadList(operand, descriptor, artifactType, path, comparison);

        var value = ReadValue(operand, descriptor, artifactType, path, checkAllowedValues: comparison != Comparison.Matches);
        return new RuleCondition(descriptor.Name, comparison, [value]);
    }

    private void CheckObject(List<RuleCondition> conditions, string path, string name)
    {
        if(conditions.Count == 1)
            return;

        if(conditions.Count == 2)
        {
            var lower = conditions.Find(condition => condition.Comparison == Comparison.AtLeast);
            var upper = conditions.Find(condition => condition.Comparison == Comparison.AtMost);
            if(lower != null && upper != null)
            {
                if(lower.ExpectedValues[0].Number > upper.ExpectedValues[0].Number)
                    throw _positions.ErrorAt(JsonPositions.Join(path, "atLeast"), $"The range for '{name}' has 'atLeast' above 'atMost'.");

                return;
            }
        }

        throw _positions.ErrorAt(path, $"Give one comparison for '{name}', or use 'atLeast' and 'atMost' together.");
    }

    private RuleCondition ReadList(JsonElement array, PropertyDescriptor descriptor, ArtifactType artifactType, string path, Comparison comparison)
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

        return new RuleCondition(descriptor.Name, comparison, values, isList: true);
    }

    private PropertyValue ReadValue(JsonElement element, PropertyDescriptor descriptor, ArtifactType artifactType, string path, bool checkAllowedValues = true)
    {
        var value = ToValue(element);
        if(value == null || value.Type != descriptor.ValueType)
            throw _positions.ErrorAt(path, $"The property '{descriptor.Name}' of {artifactType.Name} takes {Describe(descriptor.ValueType)}.");

        if(checkAllowedValues && value.Type == PropertyValueType.Text && descriptor.AllowedValues.Count > 0 && !descriptor.AllowedValues.Contains(value.Text))
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