using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using CF.App.Review;
using CF.App.Rules.Page;

namespace CF.App.Check;

internal sealed partial class ReviewChecks
{
    private static readonly int[] _firstFourLines = { 1, 2, 3, 4 };
    private static readonly int[] _linesAroundNine = { 6, 7, 8, 9, 10, 11, 12 };
    private static readonly int[] _lineNine = { 9 };

    private static string Stop(object? element)
    {
        if(element is not Button button)
            return element?.GetType().Name ?? "none";

        if(button.Name == "RunButton")
            return "run";

        if(button.Name == "OpenAllButton")
            return "all";

        if(button.Classes.Contains("panelfile"))
            return "panel";

        if(button.Classes.Contains("fileline"))
            return "file";

        if(button.Classes.Contains("findinghead"))
            return "finding";

        return AutomationProperties.GetName(button) == "Open the rule" ? "rule" : "other button";
    }

    private static string Focused(AppWindow app)
    {
        return Stop(app.Window.FocusManager?.GetFocusedElement());
    }

    private static string LineText(CodeLineViewModel line)
    {
        return string.Join(string.Empty, line.Segments.Select(segment => segment.Text));
    }

    private void TabOrder(AppWindow app)
    {
        var review = app.Model.Review;
        var expected = new List<string> { "run", "panel", "all" };
        foreach(var group in review.FileGroups)
        {
            expected.Add("file");
            foreach(var finding in group.Findings)
            {
                expected.Add("finding");
                if(finding.IsOpen && finding.CanOpenRule)
                    expected.Add("rule");
            }
        }

        app.Find<ReviewView>(_ => true).First().FocusRunButton();
        app.Pump(200);
        var seen = new List<string> { Focused(app) };
        for(var index = 1; index < expected.Count; index++)
        {
            app.Press(PhysicalKey.Tab);
            seen.Add(Focused(app));
        }

        _context.Report.Check("review-tab", "Tab visits the run button, the panel (one stop), Fold all, then each file's line and each finding's header and Open the rule", seen.SequenceEqual(expected), "expected: " + string.Join(", ", expected), "seen:     " + string.Join(", ", seen));
        var stops = app.Find<Button>(button => button.Classes.Contains("panelfile") && button.IsEffectivelyVisible).ToList();
        stops[0].Focus(NavigationMethod.Tab);
        app.Pump(100);
        app.Press(PhysicalKey.ArrowDown);
        var second = ReferenceEquals(app.Window.FocusManager?.GetFocusedElement(), stops[1]);
        app.Press(PhysicalKey.ArrowDown);
        var stays = ReferenceEquals(app.Window.FocusManager?.GetFocusedElement(), stops[1]);
        app.Press(PhysicalKey.ArrowUp);
        var back = ReferenceEquals(app.Window.FocusManager?.GetFocusedElement(), stops[0]);
        _context.Report.Check("review-tab", "in the panel Down moves to the next file with findings and stops at the last, and Up goes back", stops.Count == 2 && second && stays && back, $"{stops.Count} stops; down {second}, down again stays {stays}, up {back}");
    }

    private void CodeSource(AppWindow app, string folder)
    {
        File.WriteAllText(Path.Combine(folder, "Billing", "InvoiceService.cs"), "// the working folder holds other text now\n");
        var finding = app.Model.Review.FileGroups.SelectMany(group => group.Findings).First(candidate => candidate.RuleId == "IDE0005");
        finding.IsOpen = true;
        app.Pump(300);
        var lines = finding.Lines.Select(LineText).ToList();
        _context.Report.Check("review-code-source", "the file is changed in the working folder after the review; the lines shown for IDE0005 are the pushed commit's (lines 1 to 4, line 1 is 'using System.Text;')", finding.Lines.Select(line => line.Number).SequenceEqual(_firstFourLines) && lines[0] == "using System.Text;" && !lines.Any(line => line.Contains("working folder", StringComparison.Ordinal)), string.Join(" / ", lines));
    }

    private void FindingBody(AppWindow app, string folder)
    {
        var review = app.Model.Review;
        var findings = review.FileGroups.SelectMany(group => group.Findings).ToList();
        var team = findings.First(candidate => candidate.RuleId == "TEAM010");
        var file = findings.First(candidate => candidate.RuleId == "IDE0005");
        app.Rules.Reveal("TEAM010");
        app.Pump(400);
        var page = app.Rules.CurrentPage;
        var expected = new List<string> { $"For {page?.ArtifactWords}" };
        var where = page?.WhereFlow.OfType<ConditionViewModel>().ToList() ?? new List<ConditionViewModel>();
        for(var index = 0; index < where.Count; index++)
            expected.Add($"{(index == 0 ? "where" : "and")} {where[index].PropertyWords} {where[index].ComparisonWord} {where[index].ValueText}");

        expected.AddRange((page?.RequireFlow ?? new System.Collections.ObjectModel.ObservableCollection<ConditionViewModel>()).Select(condition => $"require {condition.PropertyWords} {condition.ComparisonWord} {condition.ValueText}"));
        var sentence = team.Sentence.Select(piece => piece.Spoken).ToList();
        _context.Report.Check("review-finding-body", "TEAM010: the plain line, and the sentence in the words of the rule's own page (For with the artifact, where and and with each condition, require with its condition: pieces)", team.SourceLine == "A team rule, written here. It blocks this push." && sentence.SequenceEqual(expected) && sentence.Contains("require braces is false"), "finding: " + string.Join(" | ", sentence), "page:    " + string.Join(" | ", expected));
        _context.Report.Check("review-finding-body", "IDE0005: the plain line says it is from .editorconfig and only warns; no sentence", file.SourceLine == "From .editorconfig. It only warns, so it does not stop this push." && !file.HasSentence, file.SourceLine);
        var lines = team.Lines.ToList();
        var committed = PushRepositories.Show(folder, "HEAD", "Billing/InvoiceService.cs").Replace("\r", string.Empty, StringComparison.Ordinal).Split('\n');
        var same = lines.Count == 7 && lines.All(line => LineText(line) == committed[line.Number - 1]);
        _context.Report.Check("review-finding-body", "TEAM010 shows the three lines before and after its line 9 as the pushed commit holds them, with line 9 the only line marked", same && lines.Select(line => line.Number).SequenceEqual(_linesAroundNine) && lines.Where(line => line.IsBreaking).Select(line => line.Number).SequenceEqual(_lineNine), string.Join(" / ", lines.Select(LineText)));
        var texts = Texts(app);
        var drawn = app.Find<TextBlock>(block => block.IsEffectivelyVisible && CodeText.GetSegments(block) != null && (block.Inlines?.Count ?? 0) > 0).Count();
        var forbidden = texts.Where(text => text.Contains("Apply fix", StringComparison.Ordinal) || text.Contains("Copy as a prompt", StringComparison.Ordinal) || text.Contains("Copy all as a prompt", StringComparison.Ordinal) || text.Contains("No automatic fix", StringComparison.Ordinal)).ToList();
        _context.Report.Check("review-finding-body", "the lines are drawn as coloured runs, and there is no Apply fix, no Copy as a prompt and no 'No automatic fix.'", drawn >= 11 && forbidden.Count == 0, $"{drawn} coloured lines drawn", string.Join(" | ", forbidden));
    }

    private void OpenTheRule(AppWindow app)
    {
        var review = app.Model.Review;
        var findings = review.FileGroups.SelectMany(group => group.Findings).ToList();
        var team = findings.First(candidate => candidate.RuleId == "TEAM010");
        var file = findings.First(candidate => candidate.RuleId == "IDE0005");
        app.Model.ShowReview();
        app.Pump(300);
        var teamButton = app.Find<Button>(button => button.IsEffectivelyVisible && AutomationProperties.GetName(button) == "Open the rule" && ReferenceEquals(button.DataContext, team)).Any();
        team.OpenRuleCommand.Execute(null);
        app.Pump(600);
        _context.Report.Check("review-open-rule", "Open the rule on the TEAM010 finding (a button is drawn) shows the Rules screen with TEAM010 chosen and its page open", teamButton && app.Model.ShowsRules && app.Rules.SelectedRule?.Id == "TEAM010" && app.Rules.CurrentPage?.Id == "TEAM010", $"rules shown {app.Model.ShowsRules}, chosen {app.Rules.SelectedItem?.GetType().Name}");
        app.Model.ShowReview();
        app.PumpUntil(() => app.Rules.FileRulesAreRead, 60000);
        file.IsOpen = true;
        app.Pump(300);
        var fileButton = app.Find<Button>(button => button.IsEffectivelyVisible && AutomationProperties.GetName(button) == "Open the rule" && ReferenceEquals(button.DataContext, file)).Any();
        file.OpenRuleCommand.Execute(null);
        app.Pump(600);
        _context.Report.Check("review-open-rule", "Open the rule on the IDE0005 finding shows the Rules screen with the rule of .editorconfig IDE0005 chosen and its page open", fileButton && app.Model.ShowsRules && app.Rules.SelectedFileRule?.Id == "IDE0005" && app.Rules.ShowsFilePage, $"rules shown {app.Model.ShowsRules}, chosen {app.Rules.SelectedItem?.GetType().Name}");
        app.Model.ShowReview();
        app.Pump(300);
    }

    private static ScrollViewer Sheet(AppWindow app)
    {
        return app.Find<Grid>(grid => grid.Name == "Frame").First().FindAncestorOfType<ScrollViewer>() ?? throw new InvalidOperationException("the sheet has no scroll viewer");
    }

    private static void ScrollToTop(AppWindow app)
    {
        // the sheet is not drawn while the block before a review stands in its place
        if(!app.Find<Grid>(grid => grid.Name == "Frame").Any())
            return;

        Sheet(app).Offset = default;
        app.Pump(300);
    }

    private void Folding(AppWindow app)
    {
        var review = app.Model.Review;
        ScrollToTop(app);
        var all = app.Find<Button>(button => button.Name == "OpenAllButton").First();
        var startsOpen = review.FileGroups.All(group => group.IsOpen) && review.OpenAllText == "Fold all";
        app.Click(all);
        var heads = app.Find<Button>(button => button.Classes.Contains("findinghead") && button.IsEffectivelyVisible).Count();
        var folded = review.FileGroups.All(group => !group.IsOpen) && review.OpenAllText == "Open all" && heads == 0;
        app.Click(all);
        var opened = review.FileGroups.All(group => group.IsOpen) && review.OpenAllText == "Fold all";
        _context.Report.Check("review-fold", "every file starts open and the button says 'Fold all'; pressed, every file is folded (no finding drawn) and it says 'Open all'; pressed again, every file is open", startsOpen && folded && opened, $"starts open {startsOpen}, folded {folded} (text '{review.OpenAllText}', {heads} headers drawn, open: {string.Join(",", review.FileGroups.Select(group => group.IsOpen))}), opened {opened}");
        var first = app.Find<Button>(button => button.Classes.Contains("fileline")).First();
        first.Focus(NavigationMethod.Tab);
        app.Pump(100);
        app.Press(PhysicalKey.ArrowLeft);
        var left = !review.FileGroups[0].IsOpen;
        app.Press(PhysicalKey.ArrowRight);
        var right = review.FileGroups[0].IsOpen;
        _context.Report.Check("review-fold", "on a file's line Left folds it and Right opens it", left && right, $"left {left}, right {right}");
        foreach(var finding in review.FileGroups.SelectMany(group => group.Findings))
            finding.IsOpen = true;

        app.Pump(500);
        var last = review.FileGroups[^1];
        review.PanelFiles.Last(file => file.HasFindings).ShowCommand.Execute(null);
        app.Pump(800);
        var scroller = Sheet(app);
        var line = app.Find<Button>(button => button.Classes.Contains("fileline") && ReferenceEquals(button.DataContext, last)).First();
        var top = line.TranslatePoint(new Point(0, 0), scroller)?.Y ?? double.NaN;
        var atEnd = scroller.Offset.Y >= scroller.Extent.Height - scroller.Viewport.Height - 1;
        _context.Report.Check("review-fold", "a file of the panel opens its file and brings its line to the top of the sheet (or the sheet is at its end)", last.IsOpen && scroller.Offset.Y > 0 && (Math.Abs(top) <= 2 || atEnd), $"offset {scroller.Offset.Y}, top of the line {top}, extent {scroller.Extent.Height}, viewport {scroller.Viewport.Height}");
    }

    private void OnePane(AppWindow app)
    {
        var review = app.Model.Review;
        app.Resize(760, 1000);
        ScrollToTop(app);
        var frame = app.Find<Grid>(grid => grid.Name == "Frame").First();
        var side = app.Find<FilesPanel>(panel => panel.Name == "SidePanel").First();
        var inline = app.Find<FilesPanel>(panel => panel.Name == "InlinePanel").First();
        var rows = app.Find<Button>(button => button.Classes.Contains("panelfile") && button.IsEffectivelyVisible).Count();
        var texts = Texts(app);
        _context.Report.Check("review-one-pane", "at 760 wide: one column 624 wide (the window less the rail and 24 at each side), the panel as one line (Files, '5 in this push, 2 with findings', Show) with its files not listed", review.IsOnePane && Math.Abs(frame.Bounds.Width - 624) <= 1 && !side.IsEffectivelyVisible && inline.IsEffectivelyVisible && rows == 0 && texts.Contains("Files") && texts.Contains("5 in this push, 2 with findings") && texts.Contains("Show"), $"frame {frame.Bounds.Width}, rows {rows}, onepane {review.IsOnePane}, side {side.IsEffectivelyVisible}, inline {inline.IsEffectivelyVisible}, texts {string.Join(",", texts.Take(40))}");
        var show = app.Find<Button>(button => button.IsEffectivelyVisible && (AutomationProperties.GetName(button) ?? string.Empty).StartsWith("Show the files", StringComparison.Ordinal)).First();
        app.Click(show);
        var listed = app.Find<Button>(button => button.Classes.Contains("panelfile") && button.IsEffectivelyVisible).Count();
        _context.Report.Check("review-one-pane", "Show lists the files with findings and then says Hide", listed == 2 && review.PanelToggleText == "Hide" && Texts(app).Contains("Hide"), $"{listed} files, '{review.PanelToggleText}'");
        app.Resize(1440, 900);
    }
}
