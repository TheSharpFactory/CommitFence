using CF.Core.Structure;

namespace CF.Core.Rules;

/// <summary>
/// What the settings file holds, once read and checked.
/// </summary>
public sealed class RuleSettings
{
    /// <summary>
    /// Creates the settings.
    /// </summary>
    public RuleSettings(string? requires, IReadOnlyList<RuleDefinition> rules, StructureRules structure, Strictness? configuredStrictness = null, string? schema = null)
    {
        Requires = requires;
        Rules = rules;
        ActiveRules = rules.Where(rule => rule.Enabled).ToList();
        Structure = structure;
        ConfiguredStrictness = configuredStrictness;
        Schema = schema;
    }

    /// <summary>The strictness in force, including the default when no key was written.</summary>
    public Strictness Strictness
    {
        get { return ConfiguredStrictness ?? Strictness.Standard; }
    }

    /// <summary>The strictness explicitly written in the settings file, if any.</summary>
    public Strictness? ConfiguredStrictness { get; }

    /// <summary>The address of the JSON schema the file names for editors, as written; nothing when the file has no $schema key.</summary>
    public string? Schema { get; }

    /// <summary>
    /// The lowest version of the product the repository accepts, as written; nothing when not stated.
    /// </summary>
    public string? Requires { get; }

    /// <summary>
    /// The rules, in the order of the file.
    /// </summary>
    public IReadOnlyList<RuleDefinition> Rules { get; }

    /// <summary>
    /// The rules that are on, in the order of the file: the ones a review runs.
    /// </summary>
    public IReadOnlyList<RuleDefinition> ActiveRules { get; }

    /// <summary>
    /// The structure rules: modules and the directions between them; empty when the file has no structure part.
    /// </summary>
    public StructureRules Structure { get; }

    /// <summary>
    /// The rule with this identifier, or nothing.
    /// </summary>
    public RuleDefinition? FindRule(string id)
    {
        foreach(var rule in Rules)
        {
            if(rule.Id == id)
                return rule;
        }

        return null;
    }
}