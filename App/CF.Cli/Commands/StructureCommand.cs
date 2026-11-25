using CF.Core.Ports;
using CF.Core.Review;
using CF.Core.Rules;
using CF.Core.Rules.Settings;
using CF.Core.Services;

namespace CF.Cli.Commands;

/// <summary>Shows the structure of a repository or saves one structure edit.</summary>
internal sealed class StructureCommand(IStructureService structure, IClock clock, CommandEnvironment environment)
{
    public ExitCode Run(string[] args)
    {
        var folder = environment.CurrentFolder;
        try
        {
            if(args is [_, "show"])
            {
                Show(structure.Show(folder));
                return ExitCode.Passed;
            }

            var change = Edit(folder, args);
            if(change == null)
                return Fail(UsageText.Text);

            environment.Output.WriteLine(change.Message);
            return ExitCode.Passed;
        }
        catch(SettingsException exception)
        {
            return Fail(SettingsFileName.FormatError(SettingsFileName.PathIn(folder), exception));
        }
        catch(RuleChangeException exception)
        {
            return Fail(exception.Message);
        }
    }

    private StructureChange? Edit(string folder, string[] args)
    {
        return args switch
        {
            [_, "set", "add-module", var name] => structure.AddModule(folder, name),
            [_, "set", "rename-module", var name, var newName] => structure.RenameModule(folder, name, newName),
            [_, "set", "remove-module", var name] => structure.RemoveModule(folder, name),
            [_, "set", "add-part", var name, var part] => structure.AddPart(folder, name, part),
            [_, "set", "remove-part", var name, var part] => structure.RemovePart(folder, name, part),
            [_, "set", "allow", var from, var to] => structure.SetDirection(folder, from, to, true),
            [_, "set", "stop-allowing", var from, var to] => structure.SetDirection(folder, from, to, false),
            _ => null
        };
    }

    private void Show(StructureReport report)
    {
        var output = environment.Output;
        output.WriteLine("Modules");
        foreach(var module in report.Modules)
        {
            output.WriteLine($"  {module.Name}");
            foreach(var part in module.Parts)
                output.WriteLine($"    {part}");
        }

        output.WriteLine("Allowed directions");
        foreach(var direction in report.AllowedDirections)
            output.WriteLine($"  {direction}");

        output.WriteLine(report.CountSentence);
        output.WriteLine(report.JudgmentSentence(clock.Now));
        foreach(var breach in report.ActiveBreaches)
            output.WriteLine($"  {breach.Sentence}");
    }

    private ExitCode Fail(string message)
    {
        environment.Error.WriteLine(message);
        return ExitCode.BadSettingsOrUsage;
    }
}
