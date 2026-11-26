using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using CF.App.Rules;
using CF.App.Rules.Page;
using CF.App.Shell;
using CF.App.Tests.Support;
using CF.Core.Rules;
using Xunit;

namespace CF.App.Tests;

/// <summary>
/// A person who uses only the keyboard, in a window narrow enough for one pane: the list, a switch, a rule's page, the box of Try it,
/// and the way back.
/// </summary>
public sealed class RulesKeyboardTests
{
    [AvaloniaFact]
    public async Task KeyboardUserCanEditAndLeaveTheRulePage()
    {
        using var shell = new ShellHost();
        var model = shell.Model.Rules;
        await Signals.FileRulesArrived(model);
        var settingsPath = Path.Combine(shell.RepositoryPath, "commitfence.json");
        using var window = new ViewHost(new MainWindow { DataContext = shell.Model, Width = 900, Height = 900 });
        var view = window.Find<RulesView>(_ => true)!;
        Assert.True(model.IsOnePane);

        // into the list, on a row that is named for a screen reader
        var row = model.Items.OfType<TeamRuleRowViewModel>().First(candidate => candidate.Id == "TEAM002");
        model.SelectedItem = row;
        view.Rows.FocusEntry(NavigationMethod.Directional);
        window.Pump();
        Assert.False(model.ShowsPage);
        Assert.StartsWith("TEAM002, Methods are documented", NameOfRow(window, row), StringComparison.Ordinal);
        Assert.Contains(" On.", NameOfRow(window, row), StringComparison.Ordinal);

        // Right goes from the row to its effect and to its switch; Space turns the rule off
        window.Press(PhysicalKey.ArrowRight);
        window.Press(PhysicalKey.ArrowRight);
        Assert.Equal(RowPart.Switch, row.Part);
        window.Press(PhysicalKey.Space);
        Assert.False(shell.Rules.ListRules(settingsPath).Single(rule => rule.Id == "TEAM002").Enabled);
        Assert.False(row.IsOn);
        Assert.Contains(" Off.", NameOfRow(window, row), StringComparison.Ordinal);
        var toggle = window.Find<ToggleButton>(button => ReferenceEquals(button.DataContext, row))!;
        Assert.Equal("TEAM002, Methods are documented", AutomationProperties.GetName(toggle));
        Assert.False(toggle.IsChecked);
        window.Press(PhysicalKey.ArrowLeft);
        window.Press(PhysicalKey.ArrowLeft);
        Assert.Equal(RowPart.Row, row.Part);

        // Enter opens the page over the list, and F2 puts the keyboard in the title
        window.Press(PhysicalKey.Enter);
        var page = model.CurrentPage!;
        Assert.True(model.ShowsPage);
        Assert.Equal("TEAM002", page.Id);
        var title = window.Find<TextBox>(box => box.Name == "TitleBox" && box.IsEffectivelyVisible)!;
        Assert.Equal("Title of the rule", AutomationProperties.GetName(title));
        var artifact = window.Find<Button>(button => button.Name == "ArtifactButton" && button.IsEffectivelyVisible)!;
        artifact.Focus(NavigationMethod.Tab);
        window.Pump();
        window.Press(PhysicalKey.F2);
        Assert.True(window.HasFocusWithin(title));

        // typing makes an edit, which is announced as a live region, and Ctrl+S writes it
        window.Type(" (checked)");
        Assert.True(page.IsDirty);
        Assert.Contains("(checked)", page.Title, StringComparison.Ordinal);
        var unsaved = window.Find<Control>(control => control.IsEffectivelyVisible && AutomationProperties.GetName(control) == "Unsaved changes")!;
        Assert.Equal(AutomationLiveSetting.Polite, AutomationProperties.GetLiveSetting(unsaved));
        window.Press(PhysicalKey.S, RawInputModifiers.Control);
        Assert.False(page.IsDirty);
        Assert.Equal(page.Title, shell.Rules.ListRules(settingsPath).Single(rule => rule.Id == "TEAM002").Title);
        Assert.NotNull(window.Find<Control>(control => control.IsEffectivelyVisible && AutomationProperties.GetName(control) == "Saved"));

        // in the box of Try it Tab is four spaces, and Escape then Tab leaves it
        var tryIt = window.Find<TryItView>(candidate => candidate.IsEffectivelyVisible)!;
        tryIt.FocusCode();
        window.Pump();
        window.Press(PhysicalKey.Tab);
        Assert.Equal("    ", tryIt.Editor.Document.Text);
        window.Press(PhysicalKey.Escape);
        window.Press(PhysicalKey.Tab);
        Assert.False(window.HasFocusWithin(tryIt.Editor));
        Assert.Equal("    ", tryIt.Editor.Document.Text);
        Assert.True(model.ShowsPage);

        // Escape from the page is the way back to the list, with the keyboard on it
        window.Press(PhysicalKey.Escape);
        Assert.False(model.ShowsPage);
        Assert.True(window.HasFocusWithin(view.Rows));
    }

    [AvaloniaFact]
    public async Task EscapeClosesAPickerAndPutsTheKeyboardBackOnItsTokenAndEveryRemoveButtonNamesWhatItRemoves()
    {
        const string rule = """{"rules":[{"id":"TEAM005","language":"csharp","for":"method","severity":"block","title":"Short methods","paths":["src/Billing/**"],"where":{"declaredAccess":"public","lines":{"atMost":40}},"require":{"documentationPresent":true}}]}""";
        using var shell = new ShellHost(rule);
        await Signals.FileRulesArrived(shell.Model.Rules);
        using var window = new ViewHost(new MainWindow { DataContext = shell.Model, Width = 1440, Height = 900 });
        shell.Model.ShowRules();
        window.Pump();
        shell.Model.Rules.SelectedItem = shell.Model.Rules.Items.OfType<TeamRuleRowViewModel>().Single();
        window.Pump();
        var page = shell.Model.Rules.CurrentPage!;

        // each × says what it removes
        Assert.Equal("Remove the path src/Billing/**", page.Paths[0].RemoveSpokenName);
        var where = page.WhereFlow.OfType<ConditionViewModel>().ToList();
        Assert.Equal("Remove the condition lines is at most 40", where[1].RemoveSpokenName);

        // a clause's own refusal is a live region that names its clause
        where[1].SetProperty("declaredAccess");
        where[1].SetComparison(Comparison.IsNot);
        where[1].SetValues(["public"]);
        window.Pump();
        var refusal = window.Find<Control>(control => control is Grid && control.IsEffectivelyVisible && AutomationProperties.GetName(control)?.StartsWith("Where clause: No method can have", StringComparison.Ordinal) == true)!;
        Assert.Equal(AutomationLiveSetting.Polite, AutomationProperties.GetLiveSetting(refusal));
        page.DiscardCommand.Execute(null);
        window.Pump();
        where = page.WhereFlow.OfType<ConditionViewModel>().ToList();

        // Escape in each picker closes it and the keyboard stands on the token that opened it
        var path = window.Find<Button>(button => button.Name == "PathButton" && button.IsEffectivelyVisible)!;
        path.Command!.Execute(null);
        window.Pump();
        Assert.True(page.Paths[0].IsOpen);
        window.Press(PhysicalKey.Escape);
        Assert.False(page.Paths[0].IsOpen);
        Assert.True(path.IsFocused);

        var comparison = window.Find<Button>(button => button.Name == "ComparisonButton" && button.IsEffectivelyVisible)!;
        comparison.Command!.Execute(null);
        window.Pump();
        Assert.True(where[0].ComparisonPicker.IsOpen);
        window.Press(PhysicalKey.Escape);
        Assert.False(where[0].ComparisonPicker.IsOpen);
        Assert.True(comparison.IsFocused);

        var property = window.Find<Button>(button => button.Name == "PropertyButton" && button.IsEffectivelyVisible)!;
        property.Command!.Execute(null);
        window.Pump();
        Assert.True(where[0].PropertyPicker.IsOpen);
        window.Press(PhysicalKey.Escape);
        Assert.False(where[0].PropertyPicker.IsOpen);
        Assert.True(property.IsFocused);
    }

    [AvaloniaFact]
    public async Task TabAndShiftTabVisitTheClauseInTheOrderItIsSeenWithTheAddButtonInTheKeywordColumn()
    {
        const string rule = """{"rules":[{"id":"TEAM005","language":"csharp","for":"method","severity":"block","title":"Short methods","paths":["src/Billing/**"],"where":{"declaredAccess":"public"},"require":{"documentationPresent":true,"lines":{"atMost":40}}}]}""";
        using var shell = new ShellHost(rule);
        await Signals.FileRulesArrived(shell.Model.Rules);
        using var window = new ViewHost(new MainWindow { DataContext = shell.Model, Width = 1440, Height = 900 });
        shell.Model.ShowRules();
        window.Pump();
        shell.Model.Rules.SelectedItem = shell.Model.Rules.Items.OfType<TeamRuleRowViewModel>().Single();
        window.Pump();
        var page = shell.Model.Rules.CurrentPage!;
        page.RequireFlow[1].MakeRange();
        page.RequireFlow[1].SetValues(["50", "40"]);
        window.Pump();

        // the add button stands in the keyword's column, left of the flow
        var flow = window.Find<TokenFlow>(candidate => candidate.IsEffectivelyVisible && candidate.Children.Any(child => child is ContentPresenter { Content: AddRequirementItem }))!;
        var add = window.Find<Button>(button => button.Classes.Contains("addrequirement") && button.IsEffectivelyVisible)!;
        Assert.True(add.TranslatePoint(new Point(0, 0), flow)!.Value.X < 0);

        // Tab and Shift+Tab visit the controls row by row, and the button that stands level with the last row comes before that row's controls
        var controls = flow.GetVisualDescendants().OfType<Button>().Where(button => button.Focusable && button.IsEffectivelyVisible).ToList();
        var addTop = add.TranslatePoint(new Point(0, 0), flow)!.Value.Y;
        var seen = controls.Where(control => !ReferenceEquals(control, add)).ToList();
        var level = seen.FindIndex(control => Middle(control, flow) >= addTop);
        Assert.True(level > 0);
        seen.Insert(level, add);

        var forward = new List<Control>();
        controls[0].Focus(NavigationMethod.Tab);
        for(var step = 0; step < controls.Count; step++)
        {
            forward.Add((Control)window.Window.FocusManager!.GetFocusedElement()!);
            window.Press(PhysicalKey.Tab);
        }

        Assert.Equal(seen.Select(Describe), forward.Select(Describe));

        // within a condition the keys visit what is drawn first: the comparison, then the remove button to its right
        var removes = forward.Select((control, index) => (control, index)).Where(stop => stop.control.Classes.Contains("remove")).ToList();
        Assert.NotEmpty(removes);
        Assert.All(removes, stop => Assert.Equal("ComparisonButton", forward[stop.index - 1].Name));

        var backward = new List<Control>();
        seen[^1].Focus(NavigationMethod.Tab);
        for(var step = 0; step < controls.Count; step++)
        {
            backward.Add((Control)window.Window.FocusManager!.GetFocusedElement()!);
            window.Press(PhysicalKey.Tab, RawInputModifiers.Shift);
        }

        backward.Reverse();
        Assert.Equal(seen.Select(Describe), backward.Select(Describe));
    }

    private static double Middle(Control control, Visual flow)
    {
        return control.TranslatePoint(new Point(0, 0), flow)!.Value.Y + (control.Bounds.Height / 2);
    }

    private static string Describe(Control control)
    {
        return $"{control.Name}:{AutomationProperties.GetName(control)}";
    }

    private static string? NameOfRow(ViewHost window, TeamRuleRowViewModel row)
    {
        return window.Find<Grid>(grid => grid.Name == "RowRoot" && ReferenceEquals(grid.DataContext, row)) is { } root ? AutomationProperties.GetName(root) : null;
    }
}
