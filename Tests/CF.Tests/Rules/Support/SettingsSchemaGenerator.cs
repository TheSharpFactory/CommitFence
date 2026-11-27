using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using CF.Core.Code;
using CF.Core.Findings;
using CF.Core.Rules;

namespace CF.Tests.Rules.Support;

/// <summary>
/// Builds the JSON Schema of commitfence.json from the language's catalog and the comparison table, so that a property
/// added to the catalog cannot be released without the schema knowing it.
/// </summary>
/// <remarks>
/// The schema says what the settings reader accepts. What only the reader can judge stays out of it: a rule id used
/// twice, a key given twice, a direction that names a module the file does not define, a range whose lower end is above
/// its upper end, and a whole number written with a fraction or an exponent (1.0, 1e0), which the reader refuses and a
/// schema cannot tell from 1. Texts that hold only a character on which the reader and an editor's regular expressions
/// disagree about white space (U+0085, U+FEFF) are judged differently too. CommitFence itself is the last word.
/// </remarks>
internal static class SettingsSchemaGenerator
{
    public const string Address = "https://commitfence.com/schema/commitfence.schema.json";

    private const string _nonEmpty = "\\S";
    private const string _selectorPattern = "^(namespace|project|folder):[\\s\\S]+$";
    private const string _directionPattern = "^(?!\\s*([\\s\\S]*?\\S)\\s*->\\s*\\1\\s*$)(?:(?!->)[\\s\\S])*(?!->)\\S(?:(?!->)[\\s\\S])*->(?:(?!->)[\\s\\S])*(?!->)\\S(?:(?!->)[\\s\\S])*$";
    private const long _smallestInteger = long.MinValue;
    private const long _largestInteger = long.MaxValue;
    private const string _versionPattern = "^[0-9]{1,6}(\\.[0-9]{1,6}){0,3}$";

    private static readonly JsonSerializerOptions _writeOptions = new JsonSerializerOptions
    {
        WriteIndented = true,
        IndentSize = 2,
        NewLine = "\n",
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    /// <summary>The schema's text, two spaces of indent and a newline at the end.</summary>
    public static string Generate(string languageId, IArtifactCatalog catalog)
    {
        var definitions = new JsonObject
        {
            ["rule"] = RuleDefinition(languageId, catalog),
            ["structure"] = StructureDefinition(),
            ["noNulls"] = NoNulls(),
            ["integerCondition"] = IntegerCondition(),
            ["booleanCondition"] = BooleanCondition(),
            ["textCondition"] = TextCondition(null)
        };
        foreach(var artifactType in catalog.ArtifactTypes)
            definitions[ConditionsName(artifactType)] = ConditionsDefinition(artifactType);

        var schema = new JsonObject
        {
            ["$schema"] = "https://json-schema.org/draft/2020-12/schema",
            ["$id"] = Address,
            ["title"] = "CommitFence settings",
            ["description"] = "The settings file of a repository: the rules its pushes are reviewed by, how strict the .editorconfig words are, and the structure of its modules.",
            ["type"] = "object",
            ["additionalProperties"] = false,
            ["properties"] = TopLevelProperties(),
            ["$defs"] = definitions
        };

        return schema.ToJsonString(_writeOptions) + "\n";
    }

    private static JsonObject TopLevelProperties()
    {
        return new JsonObject
        {
            ["$schema"] = new JsonObject
            {
                ["type"] = "string",
                ["description"] = "The address of this schema, for editors; CommitFence ignores it. A file that has it needs CommitFence 1.0.0.7 or newer."
            },
            ["requires"] = new JsonObject
            {
                ["type"] = "string",
                ["pattern"] = _versionPattern,
                ["description"] = "The lowest version of CommitFence the repository accepts: one to four numbers separated by dots, such as 1.0.0.6. An older program refuses to review. Saving a rule that needs a newer version raises it."
            },
            ["strictness"] = StrictnessSchema(),
            ["rules"] = new JsonObject
            {
                ["type"] = "array",
                ["description"] = "The code rules, in the order they are shown.",
                ["items"] = Reference("rule")
            },
            ["structure"] = Reference("structure", "The modules of the code and the directions allowed between them.")
        };
    }

    private static JsonObject StrictnessSchema()
    {
        var presets = Enum.GetValues<StrictnessPreset>().Where(preset => preset != StrictnessPreset.Custom).Select(Lower);
        var mapping = new JsonObject
        {
            ["type"] = "object",
            ["additionalProperties"] = false,
            ["required"] = Texts(["error", "warning", "suggestion"]),
            ["properties"] = new JsonObject
            {
                ["error"] = Effect("What an error of .editorconfig does to a push."),
                ["warning"] = Effect("What a warning of .editorconfig does to a push."),
                ["suggestion"] = Effect("What a suggestion of .editorconfig does to a push.")
            }
        };

        return new JsonObject
        {
            ["description"] = "What the severity words of .editorconfig do to a push: a preset, or a mapping of error, warning and suggestion. Without it, error blocks and warning and suggestion warn.",
            ["anyOf"] = new JsonArray(new JsonObject { ["enum"] = Texts(presets) }, mapping)
        };
    }

    private static JsonObject Effect(string description)
    {
        var effects = Enum.GetValues<Severity>().Select(Lower).Append("off");
        return new JsonObject { ["description"] = description, ["enum"] = Texts(effects) };
    }

    private static JsonObject RuleDefinition(string languageId, IArtifactCatalog catalog)
    {
        var types = catalog.ArtifactTypes;
        var forKey = new JsonObject
        {
            ["description"] = "The kind of code the rule is about.",
            ["enum"] = Texts(types.Select(type => type.Name)),
            ["enumDescriptions"] = Texts(types.Select(TypeDescription))
        };
        var properties = new JsonObject
        {
            ["id"] = PatternText("The team's own identifier of the rule, such as TEAM010. No two rules share one.", _nonEmpty),
            ["language"] = new JsonObject { ["description"] = "The language the rule is for.", ["enum"] = Texts([languageId]) },
            ["for"] = forKey,
            ["severity"] = new JsonObject { ["description"] = "Whether a break blocks the push or only warns.", ["enum"] = Texts(Enum.GetValues<Severity>().Select(Lower)) },
            ["enabled"] = new JsonObject { ["type"] = "boolean", ["description"] = "Whether the rule is evaluated in a review. A rule that is off stays in the file and is shown, but never runs. The default is true." },
            ["title"] = PatternText("What the rule says, in a line.", _nonEmpty),
            ["where"] = ConditionsKey("Which artifacts the rule looks at; every artifact when left out. Every condition here must hold."),
            ["require"] = ConditionsKey("What must hold of those artifacts. Every condition here must hold. Give either require or forbid."),
            ["forbid"] = new JsonObject { ["const"] = true, ["description"] = "True when no such artifact may exist at all. Give either forbid or require." },
            ["paths"] = new JsonObject
            {
                ["type"] = "array",
                ["description"] = "The files the rule applies to, as patterns with *, ** and ?; every file when left out.",
                ["items"] = new JsonObject { ["type"] = "string" }
            },
            ["message"] = new JsonObject { ["type"] = "string", ["description"] = "The message of a finding, when the team wants other words than the title." },
            ["why"] = new JsonObject { ["type"] = "string", ["description"] = "Why the team has the rule." }
        };

        return new JsonObject
        {
            ["type"] = "object",
            ["description"] = "One code rule: for an artifact type, where some conditions hold, require that others hold or forbid it.",
            ["additionalProperties"] = false,
            ["required"] = Texts(["id", "language", "for", "severity", "title"]),
            ["properties"] = properties,
            ["oneOf"] = new JsonArray(RequiredKey("require"), RequiredKey("forbid")),
            ["allOf"] = AllTypes(languageId, types)
        };
    }

    private static JsonArray AllTypes(string languageId, IReadOnlyList<ArtifactType> types)
    {
        var all = new JsonArray();
        foreach(var type in types)
            all.Add(ConditionsOfType(languageId, type));

        return all;
    }

    private static JsonObject ConditionsOfType(string languageId, ArtifactType type)
    {
        var chosen = new JsonObject
        {
            ["properties"] = new JsonObject { ["language"] = new JsonObject { ["const"] = languageId }, ["for"] = new JsonObject { ["const"] = type.Name } },
            ["required"] = Texts(["language", "for"])
        };
        var then = new JsonObject
        {
            ["properties"] = new JsonObject { ["where"] = Reference(ConditionsName(type)), ["require"] = Reference(ConditionsName(type)) }
        };

        return new JsonObject { ["if"] = chosen, ["then"] = then };
    }

    private static JsonObject ConditionsDefinition(ArtifactType type)
    {
        var properties = new JsonObject();
        foreach(var property in type.Properties)
            properties[property.Name] = PropertySchema(property);

        return new JsonObject
        {
            ["type"] = "object",
            ["description"] = TypeDescription(type),
            ["additionalProperties"] = false,
            ["minProperties"] = 1,
            ["properties"] = properties
        };
    }

    private static string TypeDescription(ArtifactType type)
    {
        var description = type.Description;
        if(type.RuleVersion != null)
            description += " Rules for it need CommitFence " + type.RuleVersion + " or newer.";

        return description;
    }

    private static JsonObject PropertySchema(PropertyDescriptor property)
    {
        var description = property.Description;
        if(property.RuleVersion != null)
            description += " Needs CommitFence " + property.RuleVersion + " or newer.";

        switch(property.ValueType)
        {
            case PropertyValueType.Number:
                return Reference("integerCondition", description);
            case PropertyValueType.Boolean:
                return Reference("booleanCondition", description);
            default:
                return property.AllowedValues.Count == 0 ? Reference("textCondition", description) : TextCondition(property.AllowedValues, description);
        }
    }

    private static JsonObject IntegerCondition()
    {
        return new JsonObject
        {
            ["description"] = "A whole number to be equal to, a list of whole numbers (one of them), or a comparison.",
            ["anyOf"] = new JsonArray(Integer(), ListOf(Integer()), ComparisonObject(PropertyValueType.Number, Integer, Integer))
        };
    }

    private static JsonObject BooleanCondition()
    {
        return new JsonObject
        {
            ["type"] = new JsonArray("boolean", "array"),
            ["description"] = "true or false, or a list of them.",
            ["items"] = new JsonObject { ["type"] = "boolean" },
            ["minItems"] = 1
        };
    }

    private static JsonObject TextCondition(IReadOnlyList<string>? allowed, string description = "A text to be equal to, a list of texts (one of them), or a comparison.")
    {
        return new JsonObject
        {
            ["description"] = description,
            ["anyOf"] = new JsonArray(Text(allowed), ListOf(Text(allowed)), ComparisonObject(PropertyValueType.Text, () => Text(allowed), () => Text(null)))
        };
    }

    /// <summary>
    /// The object form of a condition, with one key for each comparison of the table that has one. A range is the one
    /// way two keys go together.
    /// </summary>
    private static JsonObject ComparisonObject(PropertyValueType type, Func<JsonObject> value, Func<JsonObject> pattern)
    {
        var comparisons = RuleComparisons.For(type);
        var keys = new JsonObject();
        foreach(var comparison in comparisons)
        {
            var key = RuleComparisons.ObjectKey(comparison);
            if(key != null)
                keys[key] = OperandSchema(comparison, value, pattern);
        }

        var isRange = comparisons.Contains(Comparison.AtLeast) && comparisons.Contains(Comparison.AtMost);
        var comparisonObject = new JsonObject
        {
            ["type"] = "object",
            ["description"] = isRange ? "A comparison. atLeast and atMost together make a range; the others go alone." : "A comparison; one at a time.",
            ["additionalProperties"] = false,
            ["minProperties"] = 1,
            ["maxProperties"] = isRange ? 2 : 1,
            ["properties"] = keys
        };
        if(isRange)
        {
            comparisonObject["if"] = new JsonObject { ["minProperties"] = 2 };
            comparisonObject["then"] = new JsonObject { ["required"] = Texts(["atLeast", "atMost"]) };
        }

        return comparisonObject;
    }

    private static JsonObject OperandSchema(Comparison comparison, Func<JsonObject> value, Func<JsonObject> pattern)
    {
        switch(comparison)
        {
            case Comparison.IsNot:
                return ValueOrList(value, "Is none of these.");
            case Comparison.AtLeast:
                return WithDescription(value(), "Is this or more.");
            case Comparison.AtMost:
                return WithDescription(value(), "Is this or fewer.");
            case Comparison.Matches:
                return WithDescription(pattern(), "Matches this pattern, where * stands for any text and ? for any one character.");
            default:
                throw new ArgumentOutOfRangeException(nameof(comparison));
        }
    }

    private static JsonObject WithDescription(JsonObject schema, string description)
    {
        schema["description"] = description;
        return schema;
    }

    private static JsonObject Integer()
    {
        return new JsonObject { ["type"] = "integer", ["minimum"] = _smallestInteger, ["maximum"] = _largestInteger };
    }

    private static JsonObject Text(IReadOnlyList<string>? allowed)
    {
        var value = new JsonObject { ["type"] = "string" };
        if(allowed != null)
            value["enum"] = Texts(allowed);

        return value;
    }

    private static JsonObject ListOf(JsonObject item)
    {
        return new JsonObject { ["type"] = "array", ["items"] = item, ["minItems"] = 1 };
    }

    private static JsonObject ValueOrList(Func<JsonObject> value, string description)
    {
        return new JsonObject { ["description"] = description, ["anyOf"] = new JsonArray(value(), ListOf(value())) };
    }

    private static JsonObject StructureDefinition()
    {
        var modules = new JsonObject
        {
            ["type"] = "object",
            ["description"] = "Named groups of code. Each name maps to selectors written kind:pattern, with the kind namespace, project or folder, such as namespace:*.Domain.",
            ["propertyNames"] = new JsonObject { ["pattern"] = _nonEmpty },
            ["additionalProperties"] = new JsonObject
            {
                ["type"] = "array",
                ["items"] = new JsonObject { ["type"] = "string", ["pattern"] = _selectorPattern }
            }
        };
        var directions = new JsonObject
        {
            ["type"] = "object",
            ["description"] = "Which modules may use which. Each direction is written from -> to, with the names of two different modules.",
            ["additionalProperties"] = false,
            ["properties"] = new JsonObject
            {
                ["allow"] = Pairs("The directions that are allowed."),
                ["forbid"] = Pairs("The directions that are forbidden."),
                ["noCycles"] = new JsonObject { ["type"] = "boolean", ["description"] = "Whether modules may not use each other in a circle." }
            }
        };

        return new JsonObject
        {
            ["type"] = "object",
            ["additionalProperties"] = false,
            ["properties"] = new JsonObject
            {
                ["modules"] = modules,
                ["directions"] = directions,
                ["references"] = RuleObjects("Rules about what each module may reference."),
                ["layout"] = RuleObjects("Rules about where each module's code is laid out.")
            }
        };
    }

    private static JsonObject Pairs(string description)
    {
        return new JsonObject
        {
            ["type"] = "array",
            ["description"] = description,
            ["items"] = new JsonObject { ["type"] = "string", ["pattern"] = _directionPattern }
        };
    }

    private static JsonObject RuleObjects(string description)
    {
        var item = new JsonObject { ["type"] = "object", ["additionalProperties"] = Reference("noNulls") };
        return new JsonObject { ["type"] = "array", ["description"] = description, ["items"] = item };
    }

    /// <summary>Any JSON value except null, at any depth: the reader refuses a null wherever it stands.</summary>
    private static JsonObject NoNulls()
    {
        var scalar = new JsonObject { ["type"] = new JsonArray("string", "number", "boolean") };
        var list = new JsonObject { ["type"] = "array", ["items"] = Reference("noNulls") };
        var map = new JsonObject { ["type"] = "object", ["additionalProperties"] = Reference("noNulls") };
        return new JsonObject { ["description"] = "Any value except null.", ["anyOf"] = new JsonArray(scalar, list, map) };
    }

    private static JsonObject ConditionsKey(string description)
    {
        return new JsonObject { ["type"] = "object", ["description"] = description, ["minProperties"] = 1 };
    }

    private static JsonObject RequiredKey(string key)
    {
        return new JsonObject { ["required"] = Texts([key]) };
    }

    private static JsonObject PatternText(string description, string pattern)
    {
        return new JsonObject { ["type"] = "string", ["description"] = description, ["pattern"] = pattern };
    }

    private static JsonObject Reference(string definition, string? description = null)
    {
        var reference = new JsonObject();
        if(description != null)
            reference["description"] = description;

        reference["$ref"] = "#/$defs/" + definition;
        return reference;
    }

    private static string ConditionsName(ArtifactType type)
    {
        return "conditions-" + type.Name;
    }

    private static JsonArray Texts(IEnumerable<string> values)
    {
        var array = new JsonArray();
        foreach(var value in values)
            array.Add(value);

        return array;
    }

    private static string Lower<T>(T value)
        where T : struct, Enum
    {
        return value.ToString().ToLowerInvariant();
    }
}
