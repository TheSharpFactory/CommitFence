using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.VisualTree;
using CF.App.Rules;

namespace CF.App.Check;

internal sealed partial class FileRuleChecks
{
    private static string ColourOf(IBrush? brush)
    {
        return brush is ISolidColorBrush solid ? $"#{solid.Color.R:X2}{solid.Color.G:X2}{solid.Color.B:X2}" : "(not a solid colour)";
    }

    private void Repairs()
    {
        var folder = FolderWithRepositoryFile(_context);
        using var app = OpenReady(_context, folder);
        var ids = app.Rules.Items.OfType<FileRuleRowViewModel>().Select(row => row.Id).ToList();
        _context.Report.Check("file-repairs", "the rows of the second group are in the order of their ids, compared as plain character codes", ids.Count > 50 && ids.SequenceEqual(ids.OrderBy(id => id, StringComparer.Ordinal)), string.Join(", ", ids.Take(12)));
        var row = app.Rules.Items.OfType<FileRuleRowViewModel>().First(candidate => candidate.Id == "IDE0055");
        app.Rules.SelectedItem = row;
        app.Rows.ScrollIntoView(row);
        app.Pump(500);
        var open = app.Find<Button>(button => AutomationPropertiesName(button) == "Open file" && button.IsEffectivelyVisible && button.Background != null).FirstOrDefault();
        var atLine = app.Find<Button>(button => button.Name == "OpenAtLine" && button.IsEffectivelyVisible).FirstOrDefault();
        var outlined = open != null && atLine != null && new[] { open, atLine }.All(button => ColourOf(button.Background) == "#FFFFFF" && ColourOf(button.BorderBrush) == "#CDCDCD" && button.BorderThickness == new Thickness(1) && Math.Abs(button.Bounds.Height - 28) < 0.5);
        _context.Report.Check("file-repairs", "Open file and Open at line 378 are small outlined buttons: page colour, a border of 1 in line-strong, 28 high", outlined, $"Open file: {Describe(open)}", $"Open at line: {Describe(atLine)}");
        var link = app.Rows.GetVisualDescendants().OfType<Button>().FirstOrDefault(button => button.Classes.Contains("linelink") && button.IsEffectivelyVisible);
        var words = link?.GetVisualDescendants().OfType<TextBlock>().FirstOrDefault();
        var clear = link != null && words != null ? words.TranslatePoint(new Point(0, 0), link)?.X ?? -1 : -1;
        _context.Report.Check("file-repairs", "the words of a row's line link stand at least 6 clear of the left edge, so the ring of 2 does not cut the first letter", clear >= 6, $"clear by {clear}");
        var fileEnd = RightEdge(app, app.Find<RulesView>(_ => true).First().ShownFilePage()?.FindControl<StackPanel>("SheetContent"));
        var team = app.Rules.Items.OfType<TeamRuleRowViewModel>().First(candidate => candidate.Id == "TEAM010");
        app.Rules.SelectedItem = team;
        app.Pump(600);
        var teamEnd = RightEdge(app, app.Find<RulesView>(_ => true).First().ShownPage()?.FindControl<StackPanel>("SheetContent"));
        _context.Report.Check("file-repairs", "at 1440 the content of the page of a rule of the file and of a team rule both end at 1408", Math.Abs(fileEnd - 1408) <= 1 && Math.Abs(teamEnd - 1408) <= 1, $"file page ends at {fileEnd}, team page at {teamEnd}");
    }

    private static string? AutomationPropertiesName(Control control)
    {
        return Avalonia.Automation.AutomationProperties.GetName(control);
    }

    private static string Describe(Button? button)
    {
        return button == null ? "(not found)" : $"background {ColourOf(button.Background)}, border {ColourOf(button.BorderBrush)} {button.BorderThickness}, height {button.Bounds.Height}";
    }

    private static double RightEdge(AppWindow app, Control? content)
    {
        if(content == null)
            return double.NaN;

        return (content.TranslatePoint(new Point(content.Bounds.Width, 0), app.Window)?.X) ?? double.NaN;
    }
}
