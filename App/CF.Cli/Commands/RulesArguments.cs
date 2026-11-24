using CF.Core.Findings;

namespace CF.Cli.Commands;

/// <summary>
/// The arguments after the word rules: the subcommand, the rule id, what that subcommand also takes, and an optional
/// --settings path.
/// </summary>
internal sealed class RulesArguments
{
    public RulesArguments(string subcommand, string ruleId, string? settingsPath, Severity? severity, string? file, RuleFieldArguments fields, string? before, string? newRuleId, string? error)
    {
        Subcommand = subcommand;
        RuleId = ruleId;
        SettingsPath = settingsPath;
        Severity = severity;
        File = file;
        Fields = fields;
        Before = before;
        NewRuleId = newRuleId;
        Error = error;
    }

    public string Subcommand { get; }

    public string RuleId { get; }

    public string? SettingsPath { get; }

    /// <summary>
    /// The severity rules severity was given.
    /// </summary>
    public Severity? Severity { get; }

    /// <summary>
    /// The file rules try reads, or nothing for standard input.
    /// </summary>
    public string? File { get; }

    public RuleFieldArguments Fields { get; }

    /// <summary>
    /// The rule that rules add puts the new rule before, or nothing for the end.
    /// </summary>
    public string? Before { get; }

    /// <summary>
    /// The id rules copy gives the copy.
    /// </summary>
    public string? NewRuleId { get; }

    public string? Error { get; }

    public static RulesArguments Parse(string[] args)
    {
        return new RulesArgumentsReader(args).Read();
    }
}