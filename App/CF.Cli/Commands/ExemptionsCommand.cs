using System.Globalization;
using CF.Core.Review;
using CF.Core.Services;

namespace CF.Cli.Commands;

/// <summary>Lists the lines of the last commit that set a rule aside: the head line, the summary, then each file with its exemptions.</summary>
internal sealed class ExemptionsCommand(IExemptionService exemptions, CommandEnvironment environment)
{
    public ExitCode Run(string[] args)
    {
        var rule = ReadRule(args, out var understood);
        if(!understood)
            return Fail(UsageText.Text);

        try
        {
            Show(exemptions.Scan(environment.CurrentFolder, false, CancellationToken.None), rule);
            return ExitCode.Passed;
        }
        catch(ExemptionScanException exception)
        {
            return Fail(exception.Message);
        }
    }

    /// <summary>The rule after --rule, or nothing; understood is false for anything but "exemptions list" with or without --rule.</summary>
    private static string? ReadRule(string[] args, out bool understood)
    {
        understood = args is [_, "list"] or [_, "list", "--rule", _];
        return args is [_, "list", "--rule", var id] ? id : null;
    }

    private void Show(ExemptionScan scan, string? rule)
    {
        var output = environment.Output;
        if(scan.HasNoCommit)
        {
            output.WriteLine(ExemptionWords.NoCommitTitle);
            output.WriteLine(scan.NoCommitSentence);
            return;
        }

        output.WriteLine(scan.HeadLine);
        var shown = rule == null ? scan.Exemptions : scan.Exemptions.Where(entry => string.Equals(entry.RuleId, rule, StringComparison.OrdinalIgnoreCase)).ToList();
        if(shown.Count == 0)
        {
            ShowNone(rule);
            return;
        }

        var counts = new ExemptionCounts(shown);
        output.WriteLine();
        output.WriteLine(counts.WithoutReason == 0 ? counts.Summary : $"{counts.Summary}. {counts.WithoutReasonButton}");
        foreach(var file in counts.Files)
        {
            output.WriteLine();
            output.WriteLine(file.Path);
            foreach(var entry in file.Exemptions)
                output.WriteLine("  " + Row(entry));
        }
    }

    private void ShowNone(string? rule)
    {
        var output = environment.Output;
        output.WriteLine();
        output.WriteLine(rule == null ? ExemptionWords.EmptyTitle : $"No exemptions of {rule}");
        if(rule != null)
            return;

        output.WriteLine(ExemptionWords.EmptySentence);
        output.WriteLine("  " + ExemptionWords.EmptyExample);
    }

    /// <summary>"line 208  TEAM010  No reason given (a finding)": the line, the rule, the form of a pragma or an attribute, the reason.</summary>
    private static string Row(ExemptionEntry entry)
    {
        var parts = new List<string> { "line " + entry.Line.ToString(CultureInfo.InvariantCulture), entry.RuleId };
        var form = ExemptionWords.FormOf(entry.Form);
        if(form != null)
            parts.Add(form);

        parts.Add(entry.IsFinding ? ExemptionWords.NoReason + " (a finding)" : ExemptionWords.ReasonOf(entry));
        return string.Join("  ", parts);
    }

    private ExitCode Fail(string message)
    {
        environment.Error.WriteLine(message);
        return ExitCode.BadSettingsOrUsage;
    }
}
