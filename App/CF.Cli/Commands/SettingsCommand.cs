using CF.Core.Findings;
using CF.Core.Review;
using CF.Core.Rules;
using CF.Core.Rules.Settings;
using CF.Core.Services;

namespace CF.Cli.Commands;

/// <summary>Shows the settings or saves a named strictness preset.</summary>
internal sealed class SettingsCommand(ISettingsService settings, CommandEnvironment environment)
{
    public ExitCode Run(string[] args)
    {
        var folder = environment.CurrentFolder;
        try
        {
            if(args is [_, "show"])
                Show(settings.Show(folder));
            else if(args is [_, "set", "strictness", var word] && Strictness.ReadPreset(word) is { } preset)
                environment.Output.WriteLine($"Strictness set to {settings.SetStrictness(folder, preset).Preset}.");
            else
                return Fail(UsageText.Text);
        }
        catch(SettingsException exception)
        {
            return Fail(SettingsFileName.FormatError(SettingsFileName.PathIn(folder), exception));
        }
        catch(RuleChangeException exception)
        {
            return Fail(exception.Message);
        }

        return ExitCode.Passed;
    }

    private void Show(StrictnessReport report)
    {
        var headings = report.Columns.Select(preset => preset == report.Current.Preset ? $"{preset} (in force)" : preset.ToString()).Prepend("The file says").ToList();
        var widths = headings.Select(heading => Math.Max(heading.Length, "silent, none".Length)).ToList();
        environment.Output.WriteLine($"Strictness: {report.Current.Preset}");
        WriteRow(headings, widths);
        foreach(var row in report.Rows)
            WriteRow(row.Effects.Select(EffectWord).Prepend(row.Word), widths);

        environment.Output.WriteLine(report.CountSentence);
    }

    private void WriteRow(IEnumerable<string> cells, List<int> widths)
    {
        environment.Output.WriteLine(string.Join("  ", cells.Select((cell, index) => cell.PadRight(widths[index]))).TrimEnd());
    }

    private static string EffectWord(Severity? severity)
    {
        return severity switch { Severity.Block => "Blocks", Severity.Warn => "Warns", _ => "Off" };
    }

    private ExitCode Fail(string message)
    {
        environment.Error.WriteLine(message);
        return ExitCode.BadSettingsOrUsage;
    }
}