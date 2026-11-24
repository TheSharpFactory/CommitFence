using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using CF.App.Rules;
using CF.App.Rules.Page;
using CF.App.Shell;
using CF.App.Tests.Support;
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

    private static string? NameOfRow(ViewHost window, TeamRuleRowViewModel row)
    {
        return window.Find<Grid>(grid => grid.Name == "RowRoot" && ReferenceEquals(grid.DataContext, row)) is { } root ? AutomationProperties.GetName(root) : null;
    }
}
