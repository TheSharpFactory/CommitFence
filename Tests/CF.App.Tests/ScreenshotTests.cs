using Avalonia.Headless.XUnit;
using Avalonia.Styling;
using CF.App.Review;
using CF.App.Rules;
using CF.App.Shell;
using CF.App.Tests.Support;
using CF.Core.Code;
using CF.Core.Findings;
using CF.Core.Review;
using Xunit;

namespace CF.App.Tests;

/// <summary>
/// Not a test of behaviour: the pictures of the screens, for the designer and the people who judge the design. They are saved when
/// the environment variable CF_SCREENSHOTS names a folder; without it nothing is done.
/// </summary>
public sealed class ScreenshotTests
{
    private const string _variable = "CF_SCREENSHOTS";

    [AvaloniaFact]
    public async Task EachScreenIsSavedWhenAFolderIsNamed()
    {
        var folder = Environment.GetEnvironmentVariable(_variable);
        if(string.IsNullOrWhiteSpace(folder))
            Assert.Skip(_variable + " names no folder.");

        foreach(var theme in new[] { "light", "dark" })
        {
            var fileRules = new[]
            {
                RulesHost.ScopedRule(),
                RulesHost.FileRule("CA1000", "Do not declare static members on generic types", FileRuleState.On, Severity.Block, 4),
                RulesHost.FileRule("CA1822", "Mark members as static", FileRuleState.On, Severity.Warn, 8, "warning"),
                RulesHost.FileRule("IDE0005", "Remove unnecessary usings", FileRuleState.Off, null, 12, "silent")
            };
            using var shell = new ShellHost(fileRules: fileRules);
            shell.Review.Result = _ => RefusedPush();
            var arrived = Signals.FileRulesArrived(shell.Model.Rules);
            var window = new MainWindow
            {
                DataContext = shell.Model,
                Width = 1440,
                Height = 900,
                RequestedThemeVariant = theme == "light" ? ThemeVariant.Light : ThemeVariant.Dark
            };
            using var host = new ViewHost(window);
            await arrived;

            // Rules, with a rule open beside the list
            Assert.NotNull(shell.Model.Rules.SelectedRule);
            host.Save(folder, $"rules-{theme}-1440x900.png");

            shell.Model.Rules.SelectedItem = shell.Model.Rules.Items.OfType<FileRuleRowViewModel>().Single(row => row.Id == "IDE0055");
            host.Pump();
            host.Save(folder, $"rules-places-{theme}-1440.png");
            window.Width = 760;
            host.Pump();
            host.Save(folder, $"rules-places-{theme}-760.png");
            shell.Model.Rules.OpenSelectedRule();
            host.Save(folder, $"rule-page-{theme}-760.png");
            window.Width = 1440;
            host.Pump();

            // Review, after a push the review refused
            shell.Model.ShowReview();
            await shell.Model.Review.RunCommand.ExecuteAsync(null);
            Assert.Equal(VerdictKind.Refused, shell.Model.Review.Verdict.Kind);
            host.Save(folder, $"review-{theme}-1440x900.png");

            // the same, right after a click on the two buttons that copy a prompt: both read "Copied", and at 760 wide
            var openFinding = shell.Model.Review.FileGroups.SelectMany(group => group.Findings).First(finding => finding.IsOpen);
            await openFinding.Copy.CopyCommand.ExecuteAsync(null);
            await shell.Model.Review.CopyAll.CopyCommand.ExecuteAsync(null);
            Assert.True(openFinding.Copy.IsCopied && shell.Model.Review.CopyAll.IsCopied);
            host.Pump();
            host.Save(folder, $"review-copied-{theme}-1440x900.png");
            window.Width = 760;
            host.Pump();
            host.Save(folder, $"review-copied-{theme}-760x900.png");
            window.Width = 1440;
            host.Pump();

            shell.Review.Result = _ => ScopedPush();
            await shell.Model.Review.RunCommand.ExecuteAsync(null);
            foreach(var group in shell.Model.Review.FileGroups)
            {
                group.IsOpen = true;
                foreach(var finding in group.Findings)
                    finding.IsOpen = true;
            }

            window.Height = 1180;
            host.Save(folder, $"review-places-{theme}-1440.png");
            window.Width = 760;
            window.Height = 1500;
            host.Save(folder, $"review-places-{theme}-760.png");
            window.Width = 1440;
            window.Height = 900;
            // the notice of a newer version, over the screen
            shell.Model.ShowRules();
            shell.Updater.Result = FakeUpdater.Available("1.1.0.0", "The first update.\n\n### What's new\n\n- Automatic updates. The program looks for a newer release.\n- The command line comes with the app.\n\n### Downloads\n\n| a | b |");
            await shell.Model.Updates.CheckAsync(true);
            Assert.True(shell.Model.Updates.CanUpdate);
            host.Save(folder, $"update-{theme}-1440x900.png");
        }
    }

    private static ReviewResult ScopedPush()
    {
        const string source = "src/Billing/InvoiceTotals.cs";
        const string test = "tests/Billing/InvoiceTests.cs";
        var findings = new[]
        {
            Samples.Finding(source, 4, "IDE0055", Severity.Block, "Fix formatting", FindingSource.EditorConfig),
            Samples.Finding(test, 4, "IDE0055", Severity.Warn, "Fix formatting", FindingSource.EditorConfig)
        };
        var files = new Dictionary<string, string>
        {
            [source] = "class InvoiceTotals\n{\n    public int Sum(int x, int y)\n    { return x+y; }\n}\n",
            [test] = "class InvoiceTests\n{\n    void Check()\n    { var sum=new InvoiceTotals().Sum(1, 2); }\n}\n"
        };
        return Samples.Result(ReviewOutcome.Blocked, findings, [Samples.Reviewed(source), Samples.Reviewed(test)], files);
    }

    private static ReviewResult RefusedPush()
    {
        var findings = new[]
        {
            Samples.Finding("src/Billing/InvoiceTotals.cs", 5, "TEAM001", Severity.Block, "No braces around a single-line if"),
            Samples.Finding("src/Billing/InvoiceTotals.cs", 9, "TEAM002", Severity.Warn, "Methods are documented"),
            Samples.Finding("src/Billing/Ledger.cs", 3, "TEAM002", Severity.Warn, "Methods are documented"),
            Samples.Finding("src/Orders/OrderLine.cs", 12, "TEAM004", Severity.Warn, "No partial classes")
        };
        var coverage = new[] { Samples.Reviewed("src/Billing/InvoiceTotals.cs"), Samples.Reviewed("src/Billing/Ledger.cs"), Samples.Reviewed("src/Orders/OrderLine.cs"), Samples.Reviewed("src/Orders/Order.cs") };
        var totals = "namespace Billing;\n\npublic sealed class InvoiceTotals\n{\n    public int Total(int[] lines)\n    {\n        var sum = 0;\n        foreach(var line in lines)\n            if(line > 0) { sum += line; }\n\n        return sum;\n    }\n}\n";
        var sources = new Dictionary<string, string>
        {
            ["src/Billing/InvoiceTotals.cs"] = totals,
            ["src/Billing/Ledger.cs"] = "namespace Billing;\n\npublic sealed class Ledger\n{\n    public void Post(int amount)\n    {\n    }\n}\n",
            ["src/Orders/OrderLine.cs"] = "namespace Orders;\n\npublic partial class OrderLine\n{\n    public int Quantity { get; set; }\n}\n"
        };
        return Samples.Result(ReviewOutcome.Blocked, findings, coverage, sources);
    }
}