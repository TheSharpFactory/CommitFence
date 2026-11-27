using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Styling;
using Avalonia.VisualTree;
using CF.App.Review;
using CF.App.Shell;
using CF.Core.Findings;
using CF.Core.Review;

namespace CF.App.Tests.Support;

internal static class ReviewFilesPictures
{
    private static readonly string[] _folders =
    [
        "src/Billing/", "src/Billing/Invoices/", "src/Customers/", "src/Orders/", "src/Payments/",
        "src/Payments/Refunds/", "src/Shipping/", "tests/Billing/", "tests/Orders/", "tests/Payments/"
    ];

    private static readonly string[] _names =
    [
        "Address", "Ledger", "LineItem", "Mapper", "Options", "Policy", "Query", "Repository", "Request", "Result", "Service", "Status"
    ];

    public static async Task SaveAsync(string folder, string theme)
    {
        using var shell = new ShellHost();
        using var host = new ViewHost(new MainWindow { DataContext = shell.Model, Width = 1440, Height = 900, RequestedThemeVariant = ThemeVariant.Dark });
        shell.Model.ShowReview();
        var model = shell.Model.Review;
        var clean = Enumerable.Range(0, 60).Select(index =>
        {
            var directory = _folders[index % _folders.Length];
            var name = _names[((index / _folders.Length) + index) % _names.Length];
            return Samples.Reviewed(directory + name + (directory.StartsWith("tests/", StringComparison.Ordinal) ? "Tests.cs" : ".cs"));
        }).ToList();

        await SavePanelAsync(folder, theme, shell, host, model, clean);
        await SaveCleanPageAsync(folder, theme, shell, host, model, clean);
    }

    private static async Task SavePanelAsync(string folder, string theme, ShellHost shell, ViewHost host, ReviewViewModel model, List<Coverage> clean)
    {
        const string gateway = "src/Billing/PaymentGateway.cs";
        const string totals = "src/Billing/InvoiceTotals.cs";
        var findings = new List<Finding>
        {
            Samples.Finding(gateway, 24, "TEAM001", Severity.Block, "No braces around a single-line if"),
            Samples.Finding(gateway, 41, "TEAM002", Severity.Warn, "Methods are documented"),
            Samples.Finding(totals, 12, "TEAM002", Severity.Warn, "Methods are documented")
        };
        var source = string.Join('\n', Enumerable.Repeat("", 20)) + """

            /// <summary>Charges the order's total with the card on file.</summary>
            public async Task<Receipt> ChargeAsync(Order order)
            {
                if(order.Total > 0) { await ChargeAsync(order.Card); }
                return Receipt.For(order);
            }
            """;
        shell.Review.Result = _ => Samples.Result(ReviewOutcome.Blocked, findings,
            [.. clean, Samples.Reviewed(gateway), Samples.Reviewed(totals)], new Dictionary<string, string> { [gateway] = source });
        await model.RunCommand.ExecuteAsync(null);
        foreach(var file in model.FileGroups)
        {
            file.IsOpen = file.Path == gateway;
            foreach(var finding in file.Findings)
                finding.IsOpen = file.Path == gateway && finding.IsBlock;
        }

        host.Save(folder, $"review-filesclosed-{theme}-1440.png");
        FindButton(host).Focus(NavigationMethod.Tab);
        host.Save(folder, $"review-filesfocus-{theme}-1440.png");

        host.Press(PhysicalKey.Space);
        host.Window.FocusManager?.Focus(null);
        host.Pump();
        host.Save(folder, $"review-filesopen-{theme}-1440.png");
        var filter = host.Find<TextBox>(control => control.Name == "Filter" && control.IsEffectivelyVisible)!;
        filter.Focus(NavigationMethod.Tab);
        model.PanelClean!.FilterText = "Refunds";
        host.Pump();
        host.Save(folder, $"review-fileskeys-{theme}-1440.png");

        model.PanelClean.FilterText = string.Empty;
        host.Window.FocusManager?.Focus(null);
        host.Window.Width = 900;
        host.Pump();
        model.PanelShown = true;
        var rows = host.Find<ReviewView>(view => view.IsEffectivelyVisible)!.FindControl<ItemsControl>("Rows")!;
        var tail = model.Items.ToList().FindIndex(row => row is ReviewTailViewModel);
        rows.ScrollIntoView(tail);
        host.Pump();
        var panel = ((Control)rows.ContainerFromIndex(tail)!).GetVisualDescendants().OfType<FilesPanel>()
            .Single(control => control.Classes.Contains("inline-result"));
        var page = panel.FindAncestorOfType<ScrollViewer>()!;
        var position = panel.TranslatePoint(default, page)!.Value.Y;
        page.Offset = new Vector(0, Math.Max(0, page.Offset.Y + position - 24));
        host.Save(folder, $"review-filesopen-{theme}-900.png");
    }

    private static async Task SaveCleanPageAsync(string folder, string theme, ShellHost shell, ViewHost host, ReviewViewModel model, List<Coverage> clean)
    {
        host.Window.Width = 1440;
        shell.Review.Result = _ => Samples.Result(ReviewOutcome.Clean, coverage: clean);
        await model.RunCommand.ExecuteAsync(null);
        host.Pump();
        var rows = host.Find<ReviewView>(view => view.IsEffectivelyVisible)!.FindControl<ItemsControl>("Rows")!;
        var page = rows.GetVisualDescendants().OfType<ScrollViewer>().First();
        page.ScrollToHome();
        host.Save(folder, $"review-cleanclosed-{theme}-1440.png");

        page.ScrollToEnd();
        host.Pump();
        FindButton(host).Focus(NavigationMethod.Tab);
        host.Save(folder, $"review-cleanfocus-{theme}-1440.png");

        host.Press(PhysicalKey.Enter);
        host.Pump();
        host.Find<ScrollViewer>(control => control.Name == "CleanScroller" && control.IsEffectivelyVisible)!.Focus(NavigationMethod.Tab);
        host.Save(folder, $"review-cleanopen-{theme}-1440.png");

        host.Window.Width = 900;
        host.Pump();
        var panel = rows.GetVisualDescendants().OfType<FilesPanel>().First(control => control.Classes.Contains("inline-result") && control.IsEffectivelyVisible);
        var position = panel.TranslatePoint(default, page)!.Value.Y;
        page.Offset = new Vector(0, Math.Max(0, page.Offset.Y + position - 24));
        host.Pump();
        host.Save(folder, $"review-cleanopen-{theme}-900.png");
    }

    private static ToggleButton FindButton(ViewHost host)
    {
        return host.Find<ToggleButton>(control => control.Classes.Contains("disclosure") && control.IsEffectivelyVisible)!;
    }
}
