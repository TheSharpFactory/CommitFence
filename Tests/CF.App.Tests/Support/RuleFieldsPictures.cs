using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Styling;
using CF.App.Review;
using CF.App.Rules;
using CF.App.Rules.Page;
using CF.App.Shell;
using CF.Core.Changes;
using CF.Core.Findings;
using CF.Core.Review;
using CF.Core.Rules;

namespace CF.App.Tests.Support;

/// <summary>
/// The rule page with where it applies, what a finding says, several requirements and the comparisons, and Review of the staged
/// changes with the way from a finding's code to the file.
/// </summary>
internal static class RuleFieldsPictures
{
    private const string _commit = "4f2a91c";

    private const string _rule = """
        {
            "rules": [
                {
                    "id": "TEAM001", "language": "csharp", "for": "if-statement", "severity": "block",
                    "title": "No braces around a single-line if",
                    "where": { "statementsInBody": 1, "bodyFitsOnOneLine": true },
                    "require": { "braces": false }
                },
                {
                    "id": "TEAM005", "language": "csharp", "for": "method", "severity": "block",
                    "title": "Short, documented async methods",
                    "paths": ["src/Billing/**", "src/Payments/**"],
                    "where": { "name": { "matches": "*Async" }, "declaredAccess": "public" },
                    "require": { "documentationPresent": true, "lines": { "atMost": 40 } },
                    "message": "A public async method in Billing is documented and at most 40 lines long.",
                    "why": "These methods talk to the payment provider. When a payment fails at night, whoever is on call has to read them fast, so each one says what it does and fits on one screen."
                }
            ]
        }
        """;

    private const string _fileRule = """
        {
            "rules": [
                {
                    "id": "TEAM006", "language": "csharp", "for": "class", "severity": "block",
                    "title": "A class is named like its file",
                    "require": { "isNamedLikeFile": true }
                }
            ]
        }
        """;

    private static readonly string[] _payments =
    [
        "src/Payments/CardCharge.cs", "src/Payments/PaymentRetry.cs", "src/Payments/PaymentStatus.cs", "src/Payments/Refunds/RefundQueue.cs",
        "src/Payments/Refunds/RefundRequest.cs", "src/Payments/Refunds/RefundStatus.cs", "src/Payments/Webhooks/WebhookReader.cs",
        "src/Payments/Webhooks/WebhookSigner.cs", "src/Payments/Webhooks/WebhookRetry.cs"
    ];

    public static async Task SaveAsync(string folder, string theme)
    {
        await SaveRulesAsync(folder, theme);
        await SaveReviewAsync(folder, theme);
    }

    private static async Task SaveRulesAsync(string folder, string theme)
    {
        using var shell = new ShellHost(_rule);
        shell.Languages.Tracked = _payments;
        var window = new MainWindow
        {
            DataContext = shell.Model,
            Width = 1440,
            Height = 900,
            RequestedThemeVariant = ThemeVariant.Dark
        };
        using var host = new ViewHost(window);
        var rules = shell.Model.Rules;
        shell.Model.ShowRules();
        host.Pump();

        rules.SelectedItem = rules.Items.OfType<TeamRuleRowViewModel>().Single(row => row.Id == "TEAM001");
        host.Pump();
        host.Save(folder, $"rules-empty-{theme}-1440.png");

        rules.SelectedItem = rules.Items.OfType<TeamRuleRowViewModel>().Single(row => row.Id == "TEAM005");
        var page = rules.CurrentPage!;
        host.Pump();
        host.Save(folder, $"rules-fields-{theme}-1440.png");

        // the head stuck over a scrolled sheet, saved at 10:38; then the x in its states: hover on the first path, pressed on the second,
        // the keyboard on declared access
        shell.Clock.Now = new DateTimeOffset(2026, 11, 24, 10, 38, 0, TimeSpan.Zero);
        var written = page.Message;
        page.Message = written + "!";
        page.SaveCommand.Execute(null);
        page.Message = written;
        page.SaveCommand.Execute(null);
        host.Pump();
        Scroll(host, 70);
        host.Save(folder, $"rules-head-saved-{theme}-1440.png");
        ScrollToTop(host);
        var removes = host.FindAll<Button>(button => button.Classes.Contains("remove") && button.IsEffectivelyVisible);
        ((IPseudoClasses)removes[0].Classes).Set(":pointerover", true);
        ((IPseudoClasses)removes[1].Classes).Set(":pressed", true);
        removes[3].Focus(NavigationMethod.Tab);
        host.Pump();
        host.Save(folder, $"rules-x-states-{theme}-1440.png");
        ((IPseudoClasses)removes[0].Classes).Set(":pointerover", false);
        ((IPseudoClasses)removes[1].Classes).Set(":pressed", false);
        host.Window.FocusManager?.Focus(null);
        host.Pump();

        var payments = page.Paths[1];
        payments.Open();
        host.Pump();
        host.Save(folder, $"rules-pathedit-{theme}-1440.png");
        payments.FieldText = "C:\\invoicing\\src\\Payments";
        host.Pump();
        host.Save(folder, $"rules-pathbad-{theme}-1440.png");
        payments.Cancel();

        page.AddRequirement();
        var added = page.RequireFlow[^1];
        added.OpenPropertyPicker(false);
        host.Pump();
        host.Save(folder, $"rules-reqadd-{theme}-1440.png");
        added.PropertyPicker.Escape();

        var lines = page.RequireFlow[1];
        lines.OpenComparisonPicker(false);
        host.Pump();
        host.Save(folder, $"rules-cmpnum-{theme}-1440.png");
        lines.ComparisonPicker.Escape();

        var where = page.WhereFlow.OfType<ConditionViewModel>().ToList();
        where[0].OpenComparisonPicker(false);
        host.Pump();
        host.Save(folder, $"rules-cmptext-{theme}-1440.png");
        where[0].ComparisonPicker.Escape();

        where[1].SetComparison(Comparison.IsNot);
        where[1].SetValues(["internal", "private"]);
        where[1].OpenComparisonPicker(false);
        host.Pump();
        host.Save(folder, $"rules-cmplist-{theme}-1440.png");
        where[1].ComparisonPicker.Escape();

        where[0].SetComparison(Comparison.IsNot);
        where[0].SetValues(["DisposeAsync", "StartAsync"]);
        where[0].OpenComparisonPicker(false);
        host.Pump();
        host.Save(folder, $"rules-valuelist-{theme}-1440.png");
        where[0].ComparisonPicker.Escape();

        // a list too long for its row shows its first value and "+1"; the whole list is in the tooltip of the token
        host.Window.FocusManager?.Focus(null);
        ScrollToTop(host);
        var shortened = host.Find<Button>(button => button.Classes.Contains("comparison") && button.IsEffectivelyVisible && ReferenceEquals(button.DataContext, where[0]))!;
        ToolTip.SetIsOpen(shortened, true);
        host.Pump();
        host.Save(folder, $"rules-short-tooltip-{theme}-1440.png");
        ToolTip.SetIsOpen(shortened, false);
        host.Pump();

        // a range is one token: "is between 1 and 5"; the head says Unsaved while it is scrolled under
        page.DiscardCommand.Execute(null);
        host.Pump();
        page.AddCondition();
        host.Pump();
        var number = page.WhereFlow.OfType<ConditionViewModel>().Last();
        number.PropertyPicker.Escape();
        number.SetProperty("parameterCount");
        number.MakeRange();
        number.SetValues(["1", "5"]);
        host.Pump();
        host.Window.FocusManager?.Focus(null);
        ScrollToTop(host);
        host.Save(folder, $"rules-range-{theme}-1440.png");
        Scroll(host, 70);
        host.Save(folder, $"rules-head-unsaved-{theme}-1440.png");
        ScrollToTop(host);

        // "is between": the number picker with its two boxes
        number.OpenComparisonPicker(false);
        host.Pump();
        host.Save(folder, $"rules-between-{theme}-1440.png");
        number.ComparisonPicker.Escape();

        // the range no method can meet: one token outlined, the reason under the clauses and in the head
        page.DiscardCommand.Execute(null);
        host.Pump();
        var least = page.RequireFlow[1];
        least.MakeRange();
        least.SetValues(["50", "40"]);
        host.Pump();
        host.Window.FocusManager?.Focus(null);
        ScrollToTop(host);
        host.Save(folder, $"rules-rangebad-{theme}-1440.png");
        Scroll(host, 70);
        host.Save(folder, $"rules-head-cannot-save-{theme}-1440.png");
        ScrollToTop(host);

        page.DiscardCommand.Execute(null);
        host.Pump();
        var message = host.Find<TextBox>(box => box.Name == "MessageBox" && box.IsEffectivelyVisible)!;
        message.BringIntoView();
        message.Focus(NavigationMethod.Tab);
        message.CaretIndex = message.Text?.Length ?? 0;
        host.Save(folder, $"rules-focus-{theme}-1440.png");

        // narrow: a value with no slash or space, wider than the room, and the last condition with its add button
        page.DiscardCommand.Execute(null);
        host.Pump();
        window.Width = 900;
        host.Pump();
        rules.OpenSelectedRule();
        host.Pump();
        var narrowWhere = page.WhereFlow.OfType<ConditionViewModel>().ToList();
        narrowWhere[0].SetValues(["AVeryLongMethodNameThatHasNoSlashAndNoSpaceInItAtAllSoItCannotBreakAnywhereButInsideItselfAsync"]);
        page.AddCondition();
        var narrowLast = page.WhereFlow.OfType<ConditionViewModel>().Last();
        narrowLast.IsJustAdded = false;
        narrowLast.PropertyPicker.Escape();
        narrowLast.SetProperty("name");
        narrowLast.SetComparison(Comparison.Matches);
        narrowLast.SetValues(["*Payments/AVeryLongFolderNameWithNoSlashAndNoSpaceInItAtAllSoItCannotBreakAnywhereButInsideItselfAsyncWithMore*"]);
        narrowLast.PropertyPicker.Escape();
        narrowLast.ComparisonPicker.Escape();
        host.Pump();
        host.Window.FocusManager?.Focus(null);
        host.Save(folder, $"rules-narrow-{theme}-900.png");
        window.Width = 1440;
        host.Pump();
        page.DiscardCommand.Execute(null);
        host.Pump();

        // a rule that cannot be tried on pasted code: the command to run in a terminal
        using var fileShell = new ShellHost(_fileRule);
        window.DataContext = fileShell.Model;
        fileShell.Model.ShowRules();
        host.Pump();
        fileShell.Model.Rules.SelectedItem = fileShell.Model.Rules.Items.OfType<TeamRuleRowViewModel>().Single(row => row.Id == "TEAM006");
        host.Pump();
        host.Window.FocusManager?.Focus(null);
        host.Save(folder, $"rules-tryfile-{theme}-1440.png");

        // the same rule with an unsaved change: the command runs the saved rule, so the box asks to save first
        var filePage = fileShell.Model.Rules.CurrentPage!;
        filePage.Title += " now";
        host.Pump();
        host.Window.FocusManager?.Focus(null);
        host.Save(folder, $"rules-tryfile-unsaved-{theme}-1440.png");
        await Task.CompletedTask;
    }

    private static void Scroll(ViewHost host, double offset)
    {
        host.Find<ScrollViewer>(scroller => scroller.Name == "Scroller" && scroller.IsEffectivelyVisible)!.Offset = new Vector(0, offset);
        host.Pump();
    }

    private static void ScrollToTop(ViewHost host)
    {
        host.Find<ScrollViewer>(scroller => scroller.Name == "Scroller" && scroller.IsEffectivelyVisible)!.Offset = default;
        host.Pump();
    }

    private static async Task SaveReviewAsync(string folder, string theme)
    {
        using var scratch = new TestFolder();
        using var shell = new ShellHost(_rule, parentFolder: scratch.Path);
        var reviewed = "using System;\n\npublic sealed class PaymentGateway\n{\n    public async Task<ChargeResult> ChargeAsync(Order order)\n    {\n        var result = await _provider.SendAsync(order);\n        return result;\n    }\n}\n";
        var path = Path.Combine(shell.RepositoryPath, "src", "Billing", "PaymentGateway.cs");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, reviewed);
        var refs = PushedRefs.FromRefs([new PushedRef("refs/heads/main", _commit + new string('0', 33), "refs/heads/main", new string('0', 40))]);
        shell.Review.PushScope = _ => PushScopeAnswer.Found(ReviewScope.ForPush("origin", "https://example.com/r.git", refs), "main", "origin");
        var staged = new[] { "src/Billing/PaymentGateway.cs", "src/Billing/Ledger.cs", "src/Orders/OrderLine.cs", "tests/Billing/PaymentGatewayTests.cs" };
        shell.Review.StagedNow = _ => StagedAnswer.Of(staged, 2, true, _commit, sizes: [new StagedFileSize(staged[0], 31), new StagedFileSize(staged[1], 14), new StagedFileSize(staged[2], 9), new StagedFileSize(staged[3], 9)]);
        shell.Review.Result = Findings;
        var window = new MainWindow
        {
            DataContext = shell.Model,
            Width = 1440,
            Height = 900,
            RequestedThemeVariant = ThemeVariant.Dark
        };
        using var host = new ViewHost(window);
        var review = shell.Model.Review;
        review.EditorName = "Visual Studio Code";
        shell.Model.ShowReview();
        review.ShowRulesLine(new RulesOn(4, 3));

        // the staged changes: files staged and not reviewed yet, then the review
        review.SelectedChoice = review.Choices[1];
        await review.RefreshAsync();
        host.Pump();
        host.Save(folder, $"review-ready-{theme}-1440.png");
        await review.RunCommand.ExecuteAsync(null);
        foreach(var group in review.FileGroups)
            group.IsOpen = true;

        review.FileGroups.Single(group => group.Path.EndsWith("PaymentGateway.cs", StringComparison.Ordinal)).Findings[0].IsOpen = true;
        host.Pump();
        host.Save(folder, $"review-staged-{theme}-1440.png");
        window.Height = 900;

        // only warnings: no blocking findings in the staged changes, on the warning tile
        shell.Review.Result = WarningsOnly;
        await review.RunCommand.ExecuteAsync(null);
        foreach(var group in review.FileGroups)
            group.IsOpen = true;

        host.Pump();
        host.Save(folder, $"review-staged-pass-{theme}-1440.png");
        shell.Review.Result = Findings;
        await review.RunCommand.ExecuteAsync(null);
        foreach(var group in review.FileGroups)
            group.IsOpen = true;

        review.FileGroups.Single(group => group.Path.EndsWith("PaymentGateway.cs", StringComparison.Ordinal)).Findings[0].IsOpen = true;
        host.Pump();

        // the copy on disk has moved on since the files were staged; then the file is gone
        var stagedFinding = review.FileGroups.Single(group => group.Path.EndsWith("PaymentGateway.cs", StringComparison.Ordinal)).Findings[0];
        File.WriteAllText(path, "// a note\n// another\n" + reviewed);
        stagedFinding.IsOpen = false;
        stagedFinding.IsOpen = true;
        host.Window.FocusManager?.Focus(null);
        host.Save(folder, $"review-staged-moved-{theme}-1440.png");
        File.Delete(path);
        stagedFinding.IsOpen = false;
        stagedFinding.IsOpen = true;
        host.Save(folder, $"review-staged-gone-{theme}-1440.png");
        File.WriteAllText(path, reviewed);

        // nothing is staged
        var changed = new[] { new UnstagedFile("src/Billing/PaymentGateway.cs", false), new UnstagedFile("src/Billing/InvoiceTotals.cs", false), new UnstagedFile("src/Billing/RefundPolicy.cs", true), new UnstagedFile("src/Payments/CardCharge.cs", false), new UnstagedFile("tests/Billing/InvoiceTotalsTests.cs", false) };
        shell.Review.StagedNow = _ => StagedAnswer.Of([], 5, false, _commit, unstagedFiles: changed);
        review.SelectedChoice = review.Choices[0];
        review.SelectedChoice = review.Choices[1];
        using var empty = new ShellHost(_rule);
        empty.Review.StagedNow = _ => StagedAnswer.Of([], 5, false, _commit, unstagedFiles: changed);
        window.DataContext = empty.Model;
        empty.Model.ShowReview();
        empty.Model.Review.SelectedChoice = empty.Model.Review.Choices[1];
        await empty.Model.Review.RefreshAsync();
        host.Pump();
        host.Save(folder, $"review-nothing-{theme}-1440.png");

        // "Check again" with the index still empty says so; and when the index cannot be read it offers "Try again"
        await empty.Model.Review.CheckAgainCommand.ExecuteAsync(null);
        host.Pump();
        host.Save(folder, $"review-nothing-checked-{theme}-1440.png");
        empty.Review.StagedNow = _ => StagedAnswer.Unknown("The index could not be read: git did not answer.");
        await empty.Model.Review.CheckAgainCommand.ExecuteAsync(null);
        host.Pump();
        host.Save(folder, $"review-unreadable-{theme}-1440.png");
        window.DataContext = shell.Model;

        // a push: the button on the code, focused; then the copy on disk has changed; then it is gone
        review.SelectedChoice = review.Choices[0];
        await review.RunCommand.ExecuteAsync(null);
        foreach(var group in review.FileGroups)
            group.IsOpen = true;

        var pushed = review.FileGroups.Single(group => group.Path.EndsWith("PaymentGateway.cs", StringComparison.Ordinal)).Findings[0];
        pushed.IsOpen = true;
        host.Pump();
        var open = host.Find<Button>(button => button.IsEffectivelyVisible && button.Command == pushed.OpenSourceCommand)!;
        open.Focus(NavigationMethod.Tab);
        host.Save(folder, $"review-open-{theme}-1440.png");
        File.WriteAllText(path, "// a note\n// another\n" + reviewed);
        pushed.IsOpen = false;
        pushed.IsOpen = true;
        host.Window.FocusManager?.Focus(null);
        host.Save(folder, $"review-moved-{theme}-1440.png");
        File.Delete(path);
        pushed.IsOpen = false;
        pushed.IsOpen = true;
        host.Save(folder, $"review-gone-{theme}-1440.png");
        window.Height = 900;
    }

    private static ReviewResult WarningsOnly(ReviewScope _)
    {
        return Build(true);
    }

    private static ReviewResult Findings(ReviewScope _)
    {
        return Build(false);
    }

    private static ReviewResult Build(bool warningsOnly)
    {
        var source = "using System;\n\npublic sealed class PaymentGateway\n{\n    public async Task<ChargeResult> ChargeAsync(Order order)\n    {\n        var result = await _provider.SendAsync(order);\n        return result;\n    }\n}\n";
        var findings = new[]
        {
            Samples.Finding("src/Billing/PaymentGateway.cs", 5, "TEAM005", Severity.Block, "A public async method in Billing is documented and at most 40 lines long."),
            Samples.Finding("src/Billing/PaymentGateway.cs", 8, "TEAM001", Severity.Warn, "No braces around a single-line if"),
            Samples.Finding("src/Billing/Ledger.cs", 3, "TEAM001", Severity.Warn, "No braces around a single-line if"),
            Samples.Finding("src/Orders/OrderLine.cs", 12, "TEAM001", Severity.Block, "No braces around a single-line if")
        };
        var coverage = new[] { Samples.Reviewed("src/Billing/PaymentGateway.cs"), Samples.Reviewed("src/Billing/Ledger.cs"), Samples.Reviewed("src/Orders/OrderLine.cs"), Samples.Reviewed("tests/Billing/PaymentGatewayTests.cs") };
        var sources = new Dictionary<string, string>
        {
            ["src/Billing/PaymentGateway.cs"] = source,
            ["src/Billing/Ledger.cs"] = "namespace Billing;\n\npublic sealed class Ledger\n{\n}\n",
            ["src/Orders/OrderLine.cs"] = "namespace Orders;\n\npublic sealed class OrderLine\n{\n    public int Quantity { get; set; }\n}\n"
        };
        return warningsOnly
            ? Samples.Result(ReviewOutcome.Warned, findings.Where(finding => finding.Severity == Severity.Warn).ToList(), coverage, sources, commitReviewed: _commit + new string('0', 33))
            : Samples.Result(ReviewOutcome.Blocked, findings, coverage, sources, commitReviewed: _commit + new string('0', 33));
    }
}
