using System.Globalization;
using CF.Core.Review;
using CF.Core.Services;
using CF.Core.Setup;

namespace CF.Cli.Commands;

/// <summary>Prints the shared Doctor report in plain text.</summary>
internal sealed class DoctorCommand(IDoctorService doctor, TextWriter output)
{
    public ExitCode Run(string folder)
    {
        var report = doctor.Check(folder);
        output.WriteLine(report.Heading);
        output.WriteLine(report.Verdict);
        ShowChecks(report.Checks);
        ShowMachine(report.Machine);
        output.WriteLine();
        output.WriteLine(FormattableString.Invariant($"Checked {report.CheckedAt:yyyy-MM-dd HH:mm:ss zzz}, in {report.Duration.TotalSeconds:0.0} s"));

        return report.Ready ? ExitCode.Passed : ExitCode.Blocked;
    }

    private void ShowChecks(IReadOnlyList<DoctorCheckResult> checks)
    {
        string? group = null;
        foreach(var check in checks)
        {
            if(group != check.Group)
            {
                group = check.Group;
                output.WriteLine();
                output.WriteLine(group);
            }

            output.WriteLine($"{Mark(check.Outcome)} {check.Title}");
            output.WriteLine($"  {check.Found}");
            if(check.Fix != null)
                ShowFix(check.Fix);
        }
    }

    private void ShowFix(DoctorFix fix)
    {
        output.WriteLine($"  Fix: {fix.Text}");
        if(fix.Excerpt != null)
        {
            output.WriteLine($"  {fix.Excerpt.Path}");
            if(fix.AfterMove != null)
                output.WriteLine($"  {DoctorFix.NowHeading}");

            ShowExcerpt(fix.Excerpt);
        }

        if(fix.AfterMove != null)
        {
            output.WriteLine($"  {DoctorFix.AfterMoveHeading}");
            ShowExcerpt(fix.AfterMove);
        }

        foreach(var action in fix.Actions)
        {
            if(action.Kind != DoctorActionKind.OpenFile)
                output.WriteLine($"  {action.Label}: {action.Value}");
        }
    }

    private void ShowExcerpt(DoctorExcerpt excerpt)
    {
        foreach(var line in excerpt.Lines)
        {
            var mark = excerpt.Marks(line.Number) ? '>' : ' ';
            output.WriteLine($"  {mark} {line.Number.ToString(CultureInfo.InvariantCulture)} | {line.Text}");
        }
    }

    private void ShowMachine(DoctorMachineFacts machine)
    {
        output.WriteLine();
        output.WriteLine("This machine");
        output.WriteLine($"  Program   {machine.ProgramPath}");
        output.WriteLine($"  Version   {machine.ProgramVersion}");
        output.WriteLine($"  Git       {machine.GitVersion ?? "Not found"}");
        output.WriteLine($"  .NET SDK  {machine.SdkVersion ?? "Unknown"}");
        output.WriteLine($"  Data      {machine.DataFolder ?? "Unknown"}");
    }

    private static string Mark(DoctorOutcome outcome)
    {
        return outcome switch
        {
            DoctorOutcome.Passes => "[Pass]",
            DoctorOutcome.Fails => "[Fail]",
            DoctorOutcome.NotChecked => "[Not checked]",
            _ => throw new ArgumentOutOfRangeException(nameof(outcome))
        };
    }
}
