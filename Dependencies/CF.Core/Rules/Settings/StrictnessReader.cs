using System.Text.Json;
using CF.Core.Findings;

namespace CF.Core.Rules.Settings;

/// <summary>Reads the two supported shapes of the strictness key.</summary>
internal sealed class StrictnessReader(JsonPositions positions)
{
    public Strictness? Read(JsonElement value)
    {
        if(value.ValueKind == JsonValueKind.Undefined)
            return null;

        if(value.ValueKind == JsonValueKind.Object)
            return ReadCustom(value);

        var preset = value.ValueKind == JsonValueKind.String ? Strictness.ReadPreset(value.GetString()) : null;
        if(preset == null)
            throw positions.ErrorAt("strictness", "Strictness must be 'quiet', 'standard', 'strict', or a custom mapping.");

        return Strictness.For(preset.Value);
    }

    private Strictness ReadCustom(JsonElement value)
    {
        foreach(var property in value.EnumerateObject())
        {
            if(property.Name is not ("error" or "warning" or "suggestion"))
                throw positions.ErrorAt(JsonPositions.Join("strictness", property.Name), $"The key '{property.Name}' is not known in the strictness mapping.");
        }

        return new Strictness(StrictnessPreset.Custom, ReadEffect(value, "error"), ReadEffect(value, "warning"), ReadEffect(value, "suggestion"));
    }

    private Severity? ReadEffect(JsonElement value, string name)
    {
        if(!value.TryGetProperty(name, out var effect))
            throw positions.ErrorAt("strictness", $"The strictness mapping needs a value for '{name}'.");

        return (effect.ValueKind == JsonValueKind.String ? effect.GetString() : null) switch
        {
            "block" => Severity.Block,
            "warn" => Severity.Warn,
            "off" => null,
            _ => throw positions.ErrorAt(JsonPositions.Join("strictness", name), $"The effect of '{name}' must be 'block', 'warn', or 'off'.")
        };
    }
}