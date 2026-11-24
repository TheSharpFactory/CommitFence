using System.Globalization;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Styling;
using Avalonia.VisualTree;
using CF.App.Review;

namespace CF.App.Check;

/// <summary>
/// The checks of "A folder": the app's review of a folder against <c>commitfence review --all</c>, the count by rule, a
/// rule chosen and each way out of it, the uncommitted line, a folder outside the repository, the picker and the pictures.
/// </summary>
internal sealed partial class ReviewChecks
{
    private const string _rule = "TEAM010";

    private static readonly string[] _outsideWords = { "outside" };
    private static readonly string[] _sixFolders = { "Extra1", "Extra2", "Extra3", "Extra4", "Orders", "Tidy" };

    [GeneratedRegex(@"^(?<files>\d+) files?, (?<with>\d+) with findings$")]
    private static partial Regex FolderFiles();

    [GeneratedRegex(@"^By rule: (?<list>.*)$", RegexOptions.Multiline)]
    private static partial Regex ByRuleLine();

    private (Printed Cli, string ByRule) CliAll(string folder, string? target)
    {
        var arguments = target == null ? new[] { "review", "--all" } : new[] { "review", "--all", target };
        var result = _context.Cli(folder, arguments);
        var findings = new List<Found>();
        foreach(var line in result.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var match = FindingLine().Match(line);
            if(match.Success)
                findings.Add(new Found(match.Groups["file"].Value, int.Parse(match.Groups["line"].Value, CultureInfo.InvariantCulture), match.Groups["id"].Value, match.Groups["severity"].Value == "error", ProjectSuffix().Replace(match.Groups["message"].Value, string.Empty)));
        }

        var lines = result.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var by = ByRuleLine().Match(result.Output.Replace("\r", string.Empty, StringComparison.Ordinal));
        return (new Printed(result.ExitCode, findings, lines.Length == 0 ? string.Empty : lines[^1], result.Output, result.Error), by.Success ? by.Groups["list"].Value.Trim() : string.Empty);
    }

    private static void ChooseFolder(AppWindow app)
    {
        app.Model.ShowReview();
        app.Pump(300);
        var review = app.Model.Review;
        review.SelectedChoice = review.Choices[1];
        app.Pump(300);
    }

    private static void ChooseChanges(AppWindow app)
    {
        var review = app.Model.Review;
        review.SelectedChoice = review.Choices[0];
        app.Pump(300);
    }

    /// <summary>
    /// Names the folder, presses the run button with the mouse and lets the window work until the review is shown.
    /// </summary>
    private static bool RunFolder(AppWindow app, string target)
    {
        var review = app.Model.Review;
        review.UseTarget(target);
        app.Pump(200);
        var before = review.Verdict;
        app.Click(RunButton(app));
        return app.PumpUntil(() => !review.IsBusy && (!ReferenceEquals(review.Verdict, before) || review.HasBanner), _waitSeconds * 1000);
    }

    private static string RuleList(IEnumerable<Found> findings)
    {
        return string.Join(", ", findings.GroupBy(found => found.RuleId, StringComparer.Ordinal).Select(group => (Id: group.Key, Count: group.Count())).OrderByDescending(item => item.Count).ThenBy(item => item.Id, StringComparer.Ordinal).Select(item => $"{item.Id} {item.Count}"));
    }

    private static string FileOrder(IEnumerable<Found> findings)
    {
        return string.Join(", ", findings.GroupBy(found => found.File, StringComparer.Ordinal).OrderByDescending(group => group.Count()).ThenBy(group => group.Key, StringComparer.Ordinal).Select(group => $"{group.Key} {group.Count(found => found.Blocks)}/{group.Count(found => !found.Blocks)}"));
    }

    private static string ShownOrder(ReviewViewModel review)
    {
        return string.Join(", ", review.FileGroups.Select(group => $"{group.Path} {group.BlockCount}/{group.WarnCount}"));
    }

    private static string ShownRules(ReviewViewModel review)
    {
        return string.Join(", ", review.RuleRows.Select(row => $"{row.RuleId} {row.Count}"));
    }

    /// <summary>
    /// What the window and the command line must agree on for one folder review.
    /// </summary>
    private void SameFolderReview(string caseName, AppWindow app, Printed cli, string byRule, string label)
    {
        var review = app.Model.Review;
        var verdict = review.Verdict;
        var findings = cli.Findings;
        var block = findings.Count(found => found.Blocks);
        var warn = findings.Count - block;
        _context.Report.Check(caseName, "the command line exits 1 (blocking findings are there), prints more than 20 findings in more than one rule and file", cli.ExitCode == 1 && findings.Count > 20 && findings.Select(found => found.RuleId).Distinct().Count() > 1 && findings.Select(found => found.File).Distinct().Count() > 1, $"exit {cli.ExitCode}, {findings.Count} findings", cli.Output);
        _context.Report.Check(caseName, "every finding of the command line is in the window under its file, with its rule, line, message and effect, and none more", Describe(findings) == Describe(ShownFindings(app)), "command line: " + Describe(findings), "window:       " + Describe(ShownFindings(app)));
        _context.Report.Check(caseName, "the files are the command line's, most findings first then by path, each with its blocking and warning tally", FileOrder(findings) == ShownOrder(review), "command line: " + FileOrder(findings), "window:       " + ShownOrder(review));
        _context.Report.Check(caseName, "the count by rule is the command line's By rule line: the same ids, counts and order", byRule == ShownRules(review) && byRule == RuleList(findings), "command line: " + byRule, "window:       " + ShownRules(review));
        var pieces = Pieces(app);
        var expectedPieces = string.Join(" | ", new[] { block > 0 ? $"Block:{(block == 1 ? "1 would block a push" : $"{block} would block a push")}" : null, warn > 0 ? $"Warn:{warn} would only warn" : null }.Where(piece => piece != null));
        _context.Report.Check(caseName, $"the heading is '{findings.Count} findings' with the tail 'in {label}' and the pieces say what the findings would do to a push", verdict.Kind == VerdictKind.FolderFindings && verdict.Heading == $"{findings.Count} findings" && verdict.HeadingTail == "in " + label && pieces == expectedPieces, $"{verdict.Heading} {verdict.HeadingTail}; {pieces}", "expected: " + expectedPieces);
        var facts = verdict.Facts.Select(fact => fact.Text).ToList();
        var filesMatch = facts.Count == 3 ? FolderFiles().Match(facts[1]) : Match.Empty;
        var summaryFiles = FilesReviewed().Match(cli.Summary);
        var summaryCount = summaryFiles.Success ? summaryFiles.Groups["files"].Value : filesMatch.Groups["files"].Value;
        _context.Report.Check(caseName, "the facts are 'As of the last commit', the files reviewed and the files with findings, and the duration and time of day", facts.Count == 3 && facts[0] == "As of the last commit" && filesMatch.Success && filesMatch.Groups["with"].Value == review.FileGroups.Count.ToString(CultureInfo.InvariantCulture) && filesMatch.Groups["files"].Value == summaryCount && DurationAndTime().IsMatch(facts[2]), string.Join(" | ", facts), "summary: " + cli.Summary);
        var ruleCount = review.RuleRows.Count;
        _context.Report.Check(caseName, "the count by rule's head says how many rules have findings; every row has its mark, title, count and id; more than 20 findings so every file starts folded", review.RulesHead == $"{ruleCount} rules have findings" && review.RuleRows.All(row => row.Title.Length > 0 && row.RuleId.Length > 0) && review.FileGroups.All(group => !group.IsOpen) && review.OpenAllText == "Open all", $"{review.RulesHead}, '{review.OpenAllText}'");
        var cleanFiles = FilesReviewed().IsMatch(cli.Summary) ? int.Parse(summaryFiles.Groups["files"].Value, CultureInfo.InvariantCulture) - review.FileGroups.Count : -1;
        var cleanExpected = cleanFiles <= 0 ? string.Empty : cleanFiles > 8 ? $"{cleanFiles} files have no findings." : (cleanFiles == 1 ? "1 file has no findings:" : $"{cleanFiles} files have no findings:");
        _context.Report.Check(caseName, "the clean files stand after the list: a count in words when more than 8, the names after a colon when at most 8", cleanFiles < 0 || review.FolderCleanLead == cleanExpected, $"window: '{review.FolderCleanLead}' {review.FolderCleanNames}", $"expected: '{cleanExpected}'");
        _context.Report.Check(caseName, "the run button says 'Review again' and is a second button, with no shield", review.RunText == "Review again" && HasRunStyle(app, "outlined") && !HasRunStyle(app, "main") && !review.RunShowsIcon, review.RunText);
    }

    private void FolderReview()
    {
        var folder = PushRepositories.MakeWithOldBreaches(_parent, "folder", _context.Options.SampleSettingsPath);
        using var app = _context.Open(folder);
        _context.Guarded("review-folder-choice", "the choice works and starts nothing", () => FolderChoice(app));
        _context.Guarded("review-folder-same", "the app's review equals review --all", () => FolderSame(app, folder));
        _context.Guarded("review-folder-finding", "a finding in a folder review", () => FolderFinding(app));
        _context.Guarded("review-folder-rule", "a rule chosen", () => FolderRule(app, folder));
        _context.Guarded("review-folder-rule-out", "each way out of a rule chosen", () => FolderRuleOut(app));
        _context.Guarded("review-folder-keys", "the keys of the count by rule", () => FolderKeys(app));
        _context.Guarded("review-folder-picker", "the picker and its recent folders", () => FolderPicker(app));
        _context.Guarded("review-folder-clean", "a clean folder", () => FolderClean(app, folder));
        _context.Guarded("review-folder-uncommitted", "uncommitted changes with and without", () => FolderUncommitted(folder));
        _context.Guarded("review-folder-outside", "a folder outside the repository", () => FolderOutside(app, folder));
        _context.Guarded("review-folder-switch", "moving the choice keeps each result and starts no review", () => FolderSwitch(folder));
        _context.Guarded("review-folder-pictures", "the pictures of a folder review", () => FolderPictures(folder));
    }

    private void FolderChoice(AppWindow app)
    {
        ChooseFolder(app);
        var review = app.Model.Review;
        var items = app.Find<ListBoxItem>(item => item.DataContext is ReviewChoiceViewModel).ToList();
        var texts = Texts(app);
        _context.Report.Check("review-folder-choice", "'A folder' is enabled and chosen; nothing is started: 'No folder reviewed yet' and its lead, no result, no panel, no files", items.Count == 2 && items[1].IsEnabled && items[1].IsSelected && !review.HasResult && !review.IsBusy && review.Verdict.Heading == "No folder reviewed yet" && texts.Contains("No folder reviewed yet") && texts.Contains("It is read as the last commit holds it, old findings included.") && !review.HasPanel && !review.HasRulePanel && !review.HasFiles, string.Join(" | ", texts.Take(14)));
        var folderButton = app.Find<Button>(button => button.Name == "FolderButton").First();
        _context.Report.Check("review-folder-choice", "the folder button shows the repository's name and is drawn, and the run button says 'Review now' and is a main button", folderButton.IsEffectivelyVisible && review.FolderLabel == "invoicing" && review.RunText == "Review now" && HasRunStyle(app, "main") && !HasRunStyle(app, "outlined") && review.RunShowsIcon, $"'{review.FolderLabel}', '{review.RunText}'");
        Pictures(app, "folder-before");
    }

    private void FolderSame(AppWindow app, string folder)
    {
        var done = RunFolder(app, Path.Combine(folder, "Billing"));
        _context.Report.Check("review-folder-same", "Billing: the review is shown after the run button is pressed", done && app.Model.Review.HasResult, "no result in time");
        var (cli, byRule) = CliAll(folder, "Billing");
        SameFolderReview("review-folder-same", app, cli, byRule, "Billing");
        Pictures(app, "folder");
        var whole = RunFolder(app, string.Empty);
        var (cliWhole, byRuleWhole) = CliAll(folder, null);
        _context.Report.Check("review-folder-same", "the whole repository: the review is shown", whole, "no result in time");
        SameFolderReview("review-folder-same", app, cliWhole, byRuleWhole, "invoicing");
        var project = RunFolder(app, Path.Combine(folder, "Lib.csproj"));
        var (cliProject, byRuleProject) = CliAll(folder, "Lib.csproj");
        _context.Report.Check("review-folder-same", "the project file: the review is shown", project, "no result in time");
        SameFolderReview("review-folder-same", app, cliProject, byRuleProject, "Lib.csproj");
        var back = RunFolder(app, Path.Combine(folder, "Billing"));
        _context.Report.Check("review-folder-same", "Billing again, for the next cases", back && app.Model.Review.Verdict.HeadingTail == "in Billing", app.Model.Review.Verdict.HeadingTail);
    }

    private void FolderFinding(AppWindow app)
    {
        var review = app.Model.Review;
        var group = review.FileGroups[0];
        group.IsOpen = true;
        var finding = group.Findings[0];
        finding.IsOpen = true;
        app.Pump(500);
        var head = app.Find<Button>(button => button.Classes.Contains("findinghead") && ReferenceEquals(button.DataContext, finding)).First();
        var effect = head.GetVisualDescendants().OfType<StackPanel>().First(panel => panel.Width > 0);
        var body = app.Find<StackPanel>(panel => panel.Classes.Contains("findingbody") && ReferenceEquals(panel.DataContext, finding)).First();
        var fileLine = app.Find<Button>(button => button.Classes.Contains("fileline") && ReferenceEquals(button.DataContext, group)).First();
        var left = body.TranslatePoint(new Point(0, 0), fileLine)?.X ?? double.NaN;
        var word = finding.IsBlock ? "Would block" : "Would warn";
        var verbWord = finding.IsBlock ? "block" : "warn";
        _context.Report.Check("review-folder-finding", "the effect word is 'Would block' or 'Would warn' in a 100-wide column and the finding's text stands 120 from the file's line", finding.EffectWord == word && Math.Abs(effect.Width - 100) < 0.5 && Math.Abs(left - 120 - 26) <= 1.5 && !Texts(app).Contains("Blocks") && !Texts(app).Contains("Warns"), $"'{finding.EffectWord}' width {effect.Width}, body at {left}", verbWord);
        _context.Report.Check("review-folder-finding", "the plain line ends with the sentence that it was already in the code and stops no push today", finding.SourceLine.EndsWith("It was already in the code, so it stops no push today.", StringComparison.Ordinal), finding.SourceLine);
        group.IsOpen = false;
    }

    private void FolderRule(AppWindow app, string folder)
    {
        var review = app.Model.Review;
        var (cli, byRule) = CliAll(folder, "Billing");
        var mine = cli.Findings.Where(found => found.RuleId == _rule).ToList();
        var allFiles = FilesReviewed().Match(cli.Summary).Groups["files"].Value;
        var row = review.RuleRows.First(candidate => candidate.RuleId == _rule);
        var button = app.Find<Button>(candidate => candidate.Classes.Contains("rulerow") && ReferenceEquals(candidate.DataContext, row)).First();
        app.Click(button);
        app.Pump(400);
        var verdict = review.Verdict;
        var files = mine.Select(found => found.File).Distinct(StringComparer.Ordinal).Count();
        var texts = Texts(app);
        _context.Report.Check("review-folder-rule", $"{_rule} chosen by a click: the heading and its count follow the rule, with the single piece '{mine.Count} would block a push' and the filter piece 'Only {_rule}' last", review.ChosenRule == _rule && verdict.Heading == $"{mine.Count} findings" && Pieces(app) == $"Block:{mine.Count} would block a push | Filter:{_rule}" && texts.Contains("Only") && app.Find<Button>(candidate => candidate.Classes.Contains("clearfilter") && candidate.IsEffectivelyVisible && candidate.Width == 26 && candidate.Height == 26).Count() == 1, $"{verdict.Heading}; {Pieces(app)}");
        _context.Report.Check("review-folder-rule", "the second fact says 'K of F files'", verdict.Facts[1].Text == $"{files} of {allFiles} files", verdict.Facts[1].Text);
        _context.Report.Check("review-folder-rule", "the chosen row is raised, every other row steps back and keeps its count; the head says '1 of R rules shown' with 'Show all T'", row.IsChosen && review.RuleRows.Where(candidate => candidate != row).All(candidate => candidate.IsSteppedBack && !candidate.IsChosen) && ShownRules(review) == byRule && review.RulesHead == $"1 of {review.RuleRows.Count} rules shown" && review.ShowAllText == $"Show all {cli.Findings.Count}" && texts.Contains(review.ShowAllText), $"{review.RulesHead}, '{review.ShowAllText}'");
        var shown = ShownFindings(app);
        var bars = app.Find<FractionBar>(bar => bar.IsEffectivelyVisible).ToList();
        var thin = bars.Count(bar => bar.Thickness == 4);
        _context.Report.Check("review-folder-rule", "the bars of the rows that step back are 4 high and the chosen row's bar is 8", bars.Count == 3 && thin == 2 && bars.Count(bar => bar.Thickness == 8) == 1, $"{bars.Count} bars, {thin} thin");
        _context.Report.Check("review-folder-rule", "the files shown are only the files with a finding of the rule, each with only its findings and its tally, most findings first", Describe(mine) == Describe(shown) && FileOrder(mine) == ShownOrder(review), "command line: " + FileOrder(mine), "window:       " + ShownOrder(review));
        _context.Report.Check("review-folder-rule", $"the line above the files reads '{files} files with findings of {_rule}, most findings first'", review.FilesLine == $"{files} files with findings of {_rule}, most findings first" && texts.Contains(review.FilesLine), review.FilesLine);
        Pictures(app, "folder-rule");
    }

    private void FolderRuleOut(AppWindow app)
    {
        var review = app.Model.Review;
        var all = review.Verdict.Facts.Count;
        var total = review.RuleRows.Sum(row => row.Count);
        var ways = new List<(string Name, Action Do)>
        {
            ("the clear button of the filter piece", () => app.Click(app.Find<Button>(button => button.Classes.Contains("clearfilter") && button.IsEffectivelyVisible).First())),
            ("Show all", () => app.Click(app.Find<Button>(button => button.IsEffectivelyVisible && Avalonia.Automation.AutomationProperties.GetName(button) == review.ShowAllText).First())),
            ("a click on the chosen row", () => app.Click(app.Find<Button>(button => button.Classes.Contains("rulerow") && button.IsEffectivelyVisible && ((RuleRowViewModel)button.DataContext!).IsChosen).First())),
            ("Escape", () => app.Press(PhysicalKey.Escape))
        };
        foreach(var way in ways)
        {
            var row = review.RuleRows.First(candidate => candidate.RuleId == _rule);
            if(review.ChosenRule != _rule)
            {
                app.Click(app.Find<Button>(button => button.Classes.Contains("rulerow") && ReferenceEquals(button.DataContext, row)).First());
                app.Pump(300);
            }

            var chosen = review.ChosenRule == _rule;
            way.Do();
            app.Pump(300);
            var verdict = review.Verdict;
            var ended = review.ChosenRule == null && verdict.Heading == $"{total} findings" && verdict.Facts.Count == all && !verdict.Counts.Any(piece => piece.IsFilter) && review.RuleRows.All(candidate => !candidate.IsChosen && !candidate.IsSteppedBack) && review.RulesHead == $"{review.RuleRows.Count} rules have findings" && ShownFindings(app).Count == total;
            _context.Report.Check("review-folder-rule-out", $"{way.Name} ends the choice: all {total} findings, all rows, no filter piece", chosen && ended, $"chosen before {chosen}; {verdict.Heading}, {Pieces(app)}, {review.RulesHead}");
        }
    }

    private void FolderKeys(AppWindow app)
    {
        var review = app.Model.Review;
        ScrollToTop(app);
        var rows = app.Find<Button>(button => button.Classes.Contains("rulerow") && button.IsEffectivelyVisible).ToList();
        rows[0].Focus(NavigationMethod.Tab);
        app.Pump(100);
        app.Press(PhysicalKey.ArrowDown);
        var second = ReferenceEquals(app.Window.FocusManager?.GetFocusedElement(), rows[1]);
        app.Press(PhysicalKey.ArrowUp);
        var back = ReferenceEquals(app.Window.FocusManager?.GetFocusedElement(), rows[0]);
        app.Press(PhysicalKey.Enter);
        var chosen = review.ChosenRule == review.RuleRows[0].RuleId;
        app.Pump(200);
        app.Save(_context.Options.PicturesFolder, "review-folder-focus-1440.png");
        app.Press(PhysicalKey.Escape);
        _context.Report.Check("review-folder-keys", "the rows are drawn, Down and Up move between them, Enter chooses the focused row, and Escape ends it", rows.Count == review.RuleRows.Count && second && back && chosen && review.ChosenRule == null, $"{rows.Count} rows; down {second}, up {back}, enter {chosen}");
    }

    private void FolderPicker(AppWindow app)
    {
        var review = app.Model.Review;
        var folder = review.FolderRoot;
        var button = app.Find<Button>(candidate => candidate.Name == "FolderButton").First();
        ScrollToTop(app);
        app.Click(button);
        app.Pump(500);
        var entries = review.PickerEntries;
        var labels = entries.Select(entry => entry.Label).ToList();
        var recent = entries.Where(entry => entry.Kind == FolderPickerEntryKind.Recent).ToList();
        var drawn = app.Find<TextBlock>(block => block.IsEffectivelyVisible && block.Text == "Choose a folder...").Any() && app.Find<TextBlock>(block => block.IsEffectivelyVisible && block.Text == "Choose a project file...").Any();
        _context.Report.Check("review-folder-picker", "the picker is open with the repository's name first, the folders reviewed before (newest first, with 'folder' or 'project'), the two dialog entries", review.PickerOpen && labels[0] == "invoicing" && recent.Count == 2 && recent[0].Label == "Billing" && recent[0].Note == "folder" && recent.Any(entry => entry.Note == "project") && labels[^2] == "Choose a folder..." && labels[^1] == "Choose a project file..." && drawn, string.Join(" | ", labels));
        Pictures(app, "folder-picker");
        var before = review.Verdict;
        var other = recent.First(entry => entry.Note == "project");
        review.PickEntryCommand.Execute(other);
        app.Pump(300);
        _context.Report.Check("review-folder-picker", "choosing a recent one makes it the folder, closes the picker and starts nothing: the button says 'Review now' and the verdict is as it was", !review.PickerOpen && review.FolderLabel == "Lib.csproj" && review.RunText == "Review now" && !review.IsBusy && ReferenceEquals(review.Verdict, before), $"'{review.FolderLabel}', '{review.RunText}', {folder}");
        review.PickEntryCommand.Execute(entries[0]);
        app.Pump(200);
        _context.Report.Check("review-folder-picker", "the whole repository is chosen again by its entry: the button shows the name and says 'Review now'", review.FolderLabel == "invoicing" && review.RunText == "Review now", review.FolderLabel);
        foreach(var name in _sixFolders)
            RunFolder(app, Path.Combine(folder, name));

        review.PickerOpen = true;
        app.Pump(300);
        var kept = review.PickerEntries.Where(entry => entry.Kind == FolderPickerEntryKind.Recent).Select(entry => entry.Label).ToList();
        review.PickerOpen = false;
        _context.Report.Check("review-folder-picker", "six folders were reviewed after the two: five are kept, the newest first, the oldest dropped", string.Join(", ", kept) == "Tidy, Orders, Extra4, Extra3, Extra2", string.Join(", ", kept));
        review.UseTarget(Path.Combine(folder, "Billing"));
        app.Pump(200);
    }

    private void FolderClean(AppWindow app, string folder)
    {
        var done = RunFolder(app, Path.Combine(folder, "Tidy"));
        var (cli, byRule) = CliAll(folder, "Tidy");
        var review = app.Model.Review;
        var verdict = review.Verdict;
        var facts = verdict.Facts.Select(fact => fact.Text).ToList();
        _context.Report.Check("review-folder-clean", "the command line exits 0 and prints no finding and no By rule line", cli.ExitCode == 0 && cli.Findings.Count == 0 && byRule.Length == 0, $"exit {cli.ExitCode}", cli.Output);
        _context.Report.Check("review-folder-clean", "'No findings' with the tail 'in Tidy'; no count piece, no count by rule, no files with findings", done && verdict.Kind == VerdictKind.FolderClean && verdict.Heading == "No findings" && verdict.HeadingTail == "in Tidy" && !verdict.HasCounts && !review.HasRulePanel && !review.HasFiles, $"{verdict.Heading} {verdict.HeadingTail}");
        _context.Report.Check("review-folder-clean", "the facts say 'As of the last commit' and '1 file, 0 with findings'; the clean-files line names Tidy/Tidy.cs", facts.Count == 3 && facts[0] == "As of the last commit" && facts[1] == "1 file, 0 with findings" && review.FolderCleanLead == "1 file has no findings:" && review.FolderCleanNames == "Tidy/Tidy.cs" && Texts(app).Contains("Tidy/Tidy.cs"), string.Join(" | ", facts), $"{review.FolderCleanLead} {review.FolderCleanNames}");
        Pictures(app, "folder-clean");
        RunFolder(app, Path.Combine(folder, "Billing"));
    }

    private void FolderUncommitted(string folder)
    {
        using var app = _context.Open(folder);
        ChooseFolder(app);
        var first = RunFolder(app, Path.Combine(folder, "Billing"));
        var without = app.Model.Review.Verdict.LeavesOutUncommitted || Texts(app).Contains("Uncommitted changes are not part of this review.");
        var cliWithout = _context.Cli(folder, "review", "--all", "Billing");
        PushRepositories.MakeUncommittedChange(folder);
        var second = RunFolder(app, Path.Combine(folder, "Billing"));
        var cli = CliAll(folder, "Billing");
        var withLine = app.Model.Review.Verdict.LeavesOutUncommitted && Texts(app).Contains("Uncommitted changes are not part of this review.");
        _context.Report.Check("review-folder-uncommitted", "a clean working folder: no uncommitted line, and the command line does not say that changes are uncommitted", first && !without && !cliWithout.Output.Contains("not committed", StringComparison.Ordinal), $"line drawn {without}", cliWithout.Output);
        _context.Report.Check("review-folder-uncommitted", "a changed tracked file: the line 'Uncommitted changes are not part of this review.' is drawn, the command line says the same thing in its words, and the findings are still the commit's", second && withLine && cli.Cli.Output.Contains("not committed", StringComparison.Ordinal) && Describe(cli.Cli.Findings) == Describe(ShownFindings(app)), $"line drawn {withLine}", cli.Cli.Output);
        Pictures(app, "folder-uncommitted");
        Git(folder, "checkout", "--", "Billing/Clean.cs");
    }

    private static void Git(string folder, params string[] arguments)
    {
        var start = new System.Diagnostics.ProcessStartInfo("git") { WorkingDirectory = folder, UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach(var argument in arguments)
            start.ArgumentList.Add(argument);

        using var process = System.Diagnostics.Process.Start(start) ?? throw new InvalidOperationException("git did not start");
        process.StandardOutput.ReadToEnd();
        process.StandardError.ReadToEnd();
        process.WaitForExit(60000);
    }

    private void FolderOutside(AppWindow app, string folder)
    {
        var review = app.Model.Review;
        var outside = Path.Combine(_parent, "not-in-the-repository");
        Directory.CreateDirectory(outside);
        var before = review.Verdict;
        var hadResult = review.HasResult;
        var done = RunFolder(app, outside);
        var cli = _context.Cli(folder, "review", "--all", outside);
        var sentence = (cli.Error.Length > 0 ? cli.Error : cli.Output).Trim();
        _context.Report.Check("review-folder-outside", "the command line prints the sentence and exits 3", cli.ExitCode == 3 && sentence.Length > 0 && _outsideWords.All(word => sentence.Contains(word, StringComparison.OrdinalIgnoreCase)), $"exit {cli.ExitCode}", sentence);
        _context.Report.Check("review-folder-outside", "the window shows the same sentence in the banner, the verdict is back to what it was and no result is made", done && review.HasBanner && review.BannerText == sentence && ReferenceEquals(review.Verdict, before) && review.HasResult == hadResult && Texts(app).Contains(sentence), "banner: " + review.BannerText, "command line: " + sentence);
        Pictures(app, "folder-outside");
        review.UseTarget(Path.Combine(folder, "Billing"));
        RunFolder(app, Path.Combine(folder, "Billing"));
    }

    private void FolderSwitch(string folder)
    {
        using var app = _context.Open(folder);
        ChooseFolder(app);
        var review = app.Model.Review;
        RunFolder(app, Path.Combine(folder, "Billing"));
        review.ChosenRule = null;
        var rule = review.RuleRows.First(row => row.RuleId == _rule);
        rule.ChooseCommand.Execute(null);
        app.Pump(300);
        var folderVerdict = review.Verdict;
        var folderFiles = ShownOrder(review);
        var folderLine = review.FilesLine;
        ChooseChanges(app);
        var changesBlank = !review.HasResult && review.Verdict.Heading == "No review yet" && !review.HasRulePanel && review.RunText == "Review now";
        app.Click(RunButton(app));
        app.PumpUntil(() => review.HasResult && !review.IsBusy, _waitSeconds * 1000);
        var changesVerdict = review.Verdict;
        var changesPanel = review.CleanPushLine;
        ChooseFolder(app);
        var folderBack = ReferenceEquals(review.Verdict, folderVerdict) && ShownOrder(review) == folderFiles && review.FilesLine == folderLine && review.ChosenRule == _rule && review.FolderLabel == "Billing" && review.RunText == "Review again" && review.HasRulePanel && !review.HasPanel;
        ChooseChanges(app);
        var changesBack = ReferenceEquals(review.Verdict, changesVerdict) && review.CleanPushLine == changesPanel && review.RunText == "Review again" && review.HasCleanPush && !review.HasRulePanel && !review.IsBusy;
        _context.Report.Check("review-folder-switch", "'The changes' before any push review shows the empty state and 'Review now'; no review was started by moving the choice", changesBlank, review.Verdict.Heading);
        _context.Report.Check("review-folder-switch", "back on 'A folder' the review of Billing with TEAM010 chosen is as it was (the same verdict, files and line), and the button says 'Review again'", folderBack, $"{review.Verdict.Heading}, chosen {review.ChosenRule}, '{review.RunText}'");
        _context.Report.Check("review-folder-switch", "back on 'The changes' the push's review is as it was, its list of files and not the count by rule, and the button says 'Review again'", changesBack, $"{review.Verdict.Heading}, '{review.RunText}'");
    }

    private void FolderPictures(string folder)
    {
        using var app = _context.Open(folder);
        ChooseFolder(app);
        RunFolder(app, Path.Combine(folder, "Billing"));
        var review = app.Model.Review;
        app.Model.Review.FileGroups[0].IsOpen = true;
        app.Model.Review.FileGroups[0].Findings[0].IsOpen = true;
        app.Pump(500);
        Pictures(app, "folder-open");
        review.FileGroups[0].IsOpen = false;
        app.Resize(760, 1000);
        ScrollToTop(app);
        review.PanelShown = true;
        app.Pump(400);
        app.Save(_context.Options.PicturesFolder, "review-folder-by-rule-760.png");
        review.PanelShown = false;
        app.Resize(1440, 900);
        ScrollToTop(app);
        Application.Current!.RequestedThemeVariant = ThemeVariant.Dark;
        app.Pump(500);
        review.RuleRows.First(row => row.RuleId == _rule).ChooseCommand.Execute(null);
        app.Pump(400);
        Pictures(app, "folder-dark");
        Application.Current.RequestedThemeVariant = ThemeVariant.Light;
        app.Pump(500);
        var count = review.RuleRows.Count;
        _context.Report.Check("review-folder-pictures", "the pictures are saved (a folder with findings, one file open, a rule chosen, the count by rule in one pane, clean, the picker open, the keyboard on a rule's row, the dark theme)", count > 0, $"{count} rules");
    }
}
