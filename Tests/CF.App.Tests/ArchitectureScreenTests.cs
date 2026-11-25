using System.Globalization;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using CF.App.Architecture;
using CF.App.Tests.Support;
using CF.Core.Ports;
using CF.Core.Services;
using Xunit;

namespace CF.App.Tests;

/// <summary>Layout and editing of the repository's module graph.</summary>
public sealed class ArchitectureScreenTests
{
    [Fact]
    public void RandomModuleSetsAreRoutedWithoutCrossingOverlapOrLeavingTheCanvas()
    {
        var drawings = 0;
        var lines = 0;
        foreach(var (count, density, sets, viewport) in new List<(int, double, int, double)> { (11, .3, 4, 876), (30, .12, 2, 876), (60, .08, 1, 876), (2, 0, 6, 400), (3, .1, 6, 520), (6, .4, 8, 420) })
        {
            for(var seed = 1; seed <= sets; seed++)
            {
                var random = new Random((seed * 7919) + count);
                var report = RandomSet(count, density, random);
                var layers = new ModuleLayers(report);
                foreach(var module in report.Modules)
                {
                    var others = report.Modules.Where(other => other.Name != module.Name && !report.AllowedDirections.Contains(new StructureDirection(module.Name, other.Name))).ToList();
                    var breaches = new List<StructureBreach?> { null };
                    if(others.Count > 0)
                    {
                        var target = others[random.Next(others.Count)].Name;
                        breaches.Add(new StructureBreach(module.Name, target, "A.B", "C", "src/a.cs", 1));
                        breaches.Add(new StructureBreach(target, module.Name, "A.B", "C", "src/a.cs", 1));
                    }

                    foreach(var breach in breaches)
                    {
                        var layout = new DiagramLayout(layers, module.Name, report.AllowedDirections, breach, viewport);
                        Assert.Equal(string.Empty, RouteCheck.Problem(layout));
                        var again = new DiagramLayout(layers, module.Name, report.AllowedDirections, breach, viewport);
                        Assert.Equal(layout.Boxes.Values, again.Boxes.Values);
                        Assert.Equal(layout.Lines.SelectMany(line => line.Points), again.Lines.SelectMany(line => line.Points));
                        drawings++;
                        lines += layout.Lines.Count;
                    }
                }
            }
        }

        Assert.True(drawings > 300);
        Assert.True(lines > 1000);
    }

    [Fact]
    public void ModulesThatMayUseEachOtherShareARowAndEveryDirectionBetweenThemIsDrawn()
    {
        var edges = new List<StructureDirection>();
        foreach(var pair in "a>b b>a b>c c>a c>d e>a e>b".Split(' '))
            edges.Add(new StructureDirection(pair[..1], pair[2..]));

        var report = FakeStructureService.Build(new List<string> { "a", "b", "c", "d", "e" }.Select(name => StructureSamples.Module(name, name)), edges);
        var layers = new ModuleLayers(report);
        Assert.Equal(new List<string> { "a", "b", "c" }, layers.Rows.Single(row => row.Count == 3).Order().ToList());
        var foot = ListFoot.Text(layers, edges.Count, []);
        Assert.DoesNotContain("only modules in the layers after its own", foot, StringComparison.Ordinal);
        Assert.Contains("a, b and c may use each other: they share a layer.", foot, StringComparison.Ordinal);
        foreach(var chosen in new List<string> { "a", "b", "c" })
        {
            foreach(var breach in new List<StructureBreach?> { null, new StructureBreach(chosen, "d", "A.B", "C", "src/a.cs", 1) })
            {
                var layout = new DiagramLayout(layers, chosen, edges, breach, 876);
                Assert.Equal(string.Empty, RouteCheck.Problem(layout));
                var drawn = layout.Lines.Where(line => line.Kind != DiagramLineKind.Breach).Select(line => line.Direction).ToList();
                Assert.Equal(edges.Where(edge => edge.From == chosen || edge.To == chosen).OrderBy(edge => edge.ToString()), drawn.OrderBy(edge => edge.ToString()));
            }
        }
    }

    [Fact]
    public void BoxesOfVeryUnequalWidthsStayInsideTheCanvas()
    {
        var wide = new string('w', 27);
        var report = FakeStructureService.Build(new List<string> { wide, "ab" }.Select(name => StructureSamples.Module(name, name)), []);
        var layers = new ModuleLayers(report);
        foreach(var viewport in new List<double> { 400, 340, 876 })
        {
            foreach(var chosen in new List<string> { wide, "ab" })
            {
                var layout = new DiagramLayout(layers, chosen, [], null, viewport);
                Assert.Equal(string.Empty, RouteCheck.Problem(layout));
                Assert.All(layout.Boxes.Values, box => Assert.InRange(box.X, 0, layout.Width - box.Width));
            }
        }
    }

    [Fact]
    public void CommitFenceModulesStandAboveWhatTheyUseInTheSameOrderWhicheverIsChosen()
    {
        var report = StructureSamples.Own();
        Assert.Equal(11, report.Modules.Count);
        Assert.Equal(28, report.AllowedDirections.Count);
        var layers = new ModuleLayers(report);
        var breach = new StructureBreach("core-rules", "core-review", "A.B", "C", "src/a.cs", 1);
        var rowsOf = new List<List<List<string>>>();
        foreach(var module in report.Modules)
        {
            foreach(var drawn in new List<StructureBreach?> { null, breach })
            {
                var layout = new DiagramLayout(layers, module.Name, report.AllowedDirections, drawn, 876);
                Assert.All(report.AllowedDirections, edge =>
                {
                    Assert.True(layers.Level[edge.From] > layers.Level[edge.To]);
                    Assert.True(layout.Boxes[edge.From].Bottom < layout.Boxes[edge.To].Y);
                });
                rowsOf.Add(layers.Rows.Select(row => row.OrderBy(name => layout.Boxes[name].X).ToList()).ToList());
            }
        }

        Assert.All(rowsOf, rows => Assert.Equal(layers.Rows.Select(row => row.ToList()), rows));
    }

    [Fact]
    public void ThirtyModulesStayWithinTheCanvasAndNeverOverlap()
    {
        var report = StructureSamples.Large();
        foreach(var viewport in new List<double> { 848, 818 })
        {
            var diagram = new DiagramLayout(new ModuleLayers(report), "invoicing-app", report.AllowedDirections, null, viewport);
            Assert.True(diagram.Width <= viewport, "Thirty modules fit the canvas of a 1440 window, with or without a scrollbar.");
            var boxes = diagram.Boxes.Values.ToList();
            Assert.Equal(30, boxes.Count);
            for(var i = 0; i < boxes.Count; i++)
            {
                var box = boxes[i];
                Assert.InRange(box.X, 0, diagram.Width - box.Width);
                Assert.InRange(box.Y, 0, diagram.Height - DiagramLayout.BoxHeight);
                Assert.DoesNotContain(boxes.Skip(i + 1), other => box.Row == other.Row && box.X < other.Right && other.X < box.Right);
            }
        }
    }

    [AvaloniaFact]
    public void KeyboardChoosesOnTheDiagramAndEditsAndUndoFollowTheService()
    {
        using var shell = new ShellHost();
        shell.Structure.Report = StructureSamples.Own();
        var model = shell.Model.Architecture;
        model.AgainCommand.Execute(null);
        model.Choose("core-review");
        var view = new ArchitectureView { DataContext = model };
        using var host = new ViewHost(view, 1440, 900);
        FindButton(host, "core-review, layer 4").Focus();
        host.Press(PhysicalKey.ArrowUp);
        Assert.Equal("core-services", model.SelectedName);

        var module = model.SelectedName;
        var direction = new StructureDirection(module, "core-review");
        FindButton(host, "Stop allowing core-review").RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        host.Pump();
        Assert.Contains("stop allowing " + direction, shell.Structure.Calls);
        Assert.DoesNotContain(direction, model.Directions);
        view.AllowAnother.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        host.Pump();
        host.Press(PhysicalKey.Enter);
        var added = Assert.Single(shell.Structure.Calls, call => call.StartsWith("allow ", StringComparison.Ordinal));
        Assert.Contains(model.Directions, edge => "allow " + edge == added);

        model.RemoveModuleCommand.Execute(null);
        Assert.True(model.HasUndo);
        Assert.DoesNotContain(model.Report.Modules, item => item.Name == module);
        model.UndoCommand.Execute(null);
        Assert.Equal(module, model.SelectedName);
        Assert.Contains(model.Directions, edge => "allow " + edge == added);
    }

    [AvaloniaFact]
    public void TheDiagramNumbersItsRowsFromTheTopAndOnlyTheChosenModulesNumberIsBold()
    {
        using var shell = new ShellHost();
        shell.Structure.Report = StructureSamples.Own();
        var model = shell.Model.Architecture;
        model.AgainCommand.Execute(null);
        model.Choose("core-review");
        var view = new ArchitectureView { DataContext = model };
        using var host = new ViewHost(view, 1440, 900);
        var numbers = host.FindAll<TextBlock>(text => text.GetVisualAncestors().Contains(view.Diagram) && text.Text?.All(char.IsDigit) == true);
        Assert.Equal(Enumerable.Range(1, model.Layers.Rows.Count).Select(layer => layer.ToString(CultureInfo.InvariantCulture)), numbers.Select(number => number.Text));
        var bold = Assert.Single(numbers, number => number.FontWeight == FontWeight.SemiBold);
        Assert.Equal("4", bold.Text);
        Assert.Equal(Canvas.GetTop(FindButton(host, "core-review, layer 4")) + 9, Canvas.GetTop(bold));

        model.Choose("core-rules");
        host.Pump();
        numbers = host.FindAll<TextBlock>(text => text.GetVisualAncestors().Contains(view.Diagram) && text.Text?.All(char.IsDigit) == true);
        Assert.Equal("5", Assert.Single(numbers, number => number.FontWeight == FontWeight.SemiBold).Text);
    }

    [AvaloniaFact]
    public void AllowingTheBrokenDirectionFromThePanelRedrawsItAndAWriteRefusalDisablesEditing()
    {
        using var shell = new ShellHost();
        var broken = StructureSamples.Own(StructureSamples.Broken());
        var reverse = new StructureDirection("core-review", "core-rules");
        shell.Structure.Report = FakeStructureService.Build(broken.Modules, broken.AllowedDirections.Where(edge => edge != reverse), broken.LastJudgment);
        var model = shell.Model.Architecture;
        model.AgainCommand.Execute(null);
        var view = new ArchitectureView { DataContext = model };
        using var host = new ViewHost(view, 1440, 900);
        Assert.Equal("core-rules", model.SelectedName);
        var allow = Assert.IsType<Button>(host.Find<Button>(button => button.IsEffectivelyVisible && TextsIn(button).Contains("Allow this direction")));
        Assert.True(allow.IsEffectivelyEnabled);
        allow.Command!.Execute(null);
        host.Pump();
        Assert.Contains("allow core-rules -> core-review", shell.Structure.Calls);
        Assert.Empty(model.ActiveBreaches);
        var layout = new DiagramLayout(model.Layers, model.SelectedName, model.Directions, model.DrawnBreach, 876);
        Assert.Contains(layout.Lines, line => line.Kind == DiagramLineKind.Outgoing && line.Direction == new StructureDirection("core-rules", "core-review"));

        shell.Structure.RefuseWrite = true;
        FindButton(host, "Stop allowing core-review").RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        host.Pump();
        Assert.Equal("commitfence.json cannot be written: it is read-only.", model.Banner);
        Assert.False(model.CanEdit);
        Assert.False(view.NewButton.IsEffectivelyEnabled);
    }

    [AvaloniaFact]
    public void EveryDirectionListsTheModulesByLayerMarksTheChosenOneAndTheArrowKeysChoose()
    {
        using var shell = new ShellHost();
        shell.Structure.Report = StructureSamples.Own(StructureSamples.Broken());
        var model = shell.Model.Architecture;
        model.AgainCommand.Execute(null);
        model.Choose("core-review");
        var view = new ArchitectureView { DataContext = model };
        using var host = new ViewHost(view, 1440, 1000);
        view.Mode.SelectedIndex = 1;
        host.Pump();
        var rows = host.FindAll<Button>(button => button.Classes.Contains("arch-row"));
        Assert.Equal(model.Layers.Order, rows.Select(row => (string)row.Tag!));
        Assert.Equal(Enumerable.Range(1, model.Layers.Rows.Count).Select(layer => $"Layer {layer}"),
            host.FindAll<TextBlock>(text => text.Text?.StartsWith("Layer ", StringComparison.Ordinal) == true).Select(text => text.Text));
        var chosen = Assert.Single(rows, row => row.Classes.Contains("chosen"));
        Assert.Equal("core-review", chosen.Tag);
        Assert.Single(rows, row => row.IsTabStop);
        Assert.Equal(rows.Count, host.FindAll<Button>(button => button.IsEffectivelyVisible && button.GetVisualAncestors().Contains(view.Every)).Count);

        var rules = rows.Single(row => (string)row.Tag! == "core-rules");
        Assert.Contains("which breaks a direction", AutomationProperties.GetName(rules), StringComparison.Ordinal);
        var texts = TextsIn(rules);
        Assert.Equal("core-rules", texts[0]);
        Assert.Equal(new List<string> { "core-review", "Breaks" }, texts.Skip(1).Take(2).ToList());
        Assert.Contains("core-rules, in layer 5, uses core-review, in layer 4: that use breaks a direction.", view.Foot.Text, StringComparison.Ordinal);
        Assert.StartsWith("Every direction: 28. Each row is a module and the modules it may use.", view.Foot.Text, StringComparison.Ordinal);

        var order = model.Layers.Order.ToList();
        var at = order.IndexOf("core-review");
        FindRow(host, "core-review").Focus();
        host.Press(PhysicalKey.ArrowDown);
        Assert.Equal(order[at + 1], model.SelectedName);
        Assert.True(host.HasFocusWithin(FindRow(host, order[at + 1])));
        host.Press(PhysicalKey.End);
        Assert.Equal(order[^1], model.SelectedName);
        host.Press(PhysicalKey.Home);
        Assert.Equal(order[0], model.SelectedName);
    }

    [AvaloniaFact]
    public void ChoosingAModuleRestylesTheListWhereItStandsAndAChangedStructureRebuildsIt()
    {
        using var shell = new ShellHost();
        shell.Structure.Report = StructureSamples.Own(StructureSamples.Broken());
        var model = shell.Model.Architecture;
        model.AgainCommand.Execute(null);
        model.Choose("core-review");
        var view = new ArchitectureView { DataContext = model };
        using var host = new ViewHost(view, 1440, 1000);
        view.Mode.SelectedIndex = 1;
        host.Pump();
        var before = host.FindAll<Button>(button => button.Classes.Contains("arch-row"));
        FindRow(host, "core-review").Focus();
        host.Press(PhysicalKey.ArrowDown);
        var after = host.FindAll<Button>(button => button.Classes.Contains("arch-row"));
        Assert.Equal(before, after);
        Assert.Equal("core-rules", Assert.Single(after, row => row.Classes.Contains("chosen")).Tag);
        Assert.Equal("core-rules", Assert.Single(after, row => row.IsTabStop).Tag);
        Assert.True(host.HasFocusWithin(FindRow(host, "core-rules")));
        var tokensOfRules = host.FindAll<Border>(border => border.BorderThickness.Left > 1 && TextsIn(border).SequenceEqual(new List<string> { "core-rules" }));
        Assert.NotEmpty(tokensOfRules);
        Assert.DoesNotContain(host.FindAll<Border>(border => border.BorderThickness.Left > 1 && TextsIn(border).SequenceEqual(new List<string> { "core-review" })), border => border.Height == 26);

        model.SetDirection("core-rules", "csharp", true);
        host.Pump();
        Assert.DoesNotContain(FindRow(host, "core-rules"), before);
        Assert.Contains("csharp", TextsIn(FindRow(host, "core-rules")));
    }

    [AvaloniaFact]
    public void SummaryAndModuleBoxUseTheLastJudgmentAndOnlyLinkToAHeldReview()
    {
        using var shell = new ShellHost();
        var clock = shell.Clock;
        var changes = new ManualChanges();
        var opened = Samples.Folder(shell.RepositoryPath);
        StructureJudgment? held = null;
        StructureJudgment? requested = null;
        using var model = new ArchitectureViewModel(shell.Structure, clock, opened, changes, judgment => judgment == held, judgment => requested = judgment);
        shell.Structure.Report = StructureSamples.Own();
        model.AgainCommand.Execute(null);
        Assert.Equal("Not judged yet: no review has run", model.Judgment);
        Assert.Equal("11 modules, 28 allowed directions", model.Count);

        var clean = StructureSamples.Clean() with { Folder = "src/Billing" };
        shell.Structure.Report = StructureSamples.Own(clean);
        model.AgainCommand.Execute(null);
        Assert.StartsWith("Nothing breaks them in the last review of src/Billing, today ", model.Judgment, StringComparison.Ordinal);

        held = StructureSamples.Broken();
        shell.Structure.Report = StructureSamples.Own(held);
        model.AgainCommand.Execute(null);
        model.Choose("core-rules");
        Assert.Equal("1 use breaks a direction", model.Judgment);
        Assert.Contains("SettingsScanner.Scan uses ReadingRules", Assert.Single(model.ShownBreaches).Sentence, StringComparison.Ordinal);
        Assert.NotNull(model.DirectionRefusal("core-rules", "core-review"));
        Assert.True(model.CanOpenReview);
        model.OpenReviewCommand.Execute(null);
        Assert.Same(held, requested);
        held = null;
        Assert.False(model.CanOpenReview);

        var modules = model.Report.Modules.Take(3).ToList();
        shell.Structure.Report = FakeStructureService.Build(modules, [], requested) with
        {
            Directions = new StructureDirectionLists(null, null)
        };
        changes.Raise();
        Assert.Equal("3 modules, no directions set: every use is allowed", model.Count);
        Assert.False(model.HasJudgedBreaches);
        Assert.Null(model.DrawnBreach);
        Assert.Empty(model.ShownBreaches);
        model.AllowBreachCommand.Execute(null);
        Assert.Empty(shell.Structure.Calls);
    }

    [AvaloniaFact]
    public void ANewModuleCanBeCancelledAndOutsideChangesAreReadAgain()
    {
        using var shell = new ShellHost();
        var changes = new ManualChanges();
        using var model = new ArchitectureViewModel(shell.Structure, shell.Clock, Samples.Folder(shell.RepositoryPath), changes, _ => false, _ => { });
        model.AgainCommand.Execute(null);
        Assert.False(model.HasModules);
        model.NewModuleCommand.Execute(null);
        Assert.Equal("module-1", model.SelectedName);
        Assert.True(model.EditingName);
        model.CancelName();
        Assert.False(model.HasModules);
        Assert.Contains("undo add module-1", shell.Structure.Calls);

        shell.Structure.Report = StructureSamples.Own();
        changes.Raise();
        Assert.Equal(11, model.Report.Modules.Count);
        model.NewModuleCommand.Execute(null);
        Assert.Equal("module-12", model.SelectedName);
        model.FinishName("tools");
        Assert.Equal("tools", model.SelectedName);
        Assert.False(model.EditingName);

        model.StartRename();
        shell.Structure.Report = StructureSamples.Own();
        changes.Raise();
        Assert.False(model.EditingName);
        var fallback = model.SelectedName;
        model.FinishName("renamed");
        Assert.Equal(fallback, model.SelectedName);
        Assert.DoesNotContain("rename " + fallback, shell.Structure.Calls);
    }

    [AvaloniaFact]
    public void EitherDirectionCanBeFirstAndRemovingItLetsTheOtherBeAdded()
    {
        using var shell = new ShellHost();
        shell.Structure.Report = FakeStructureService.Build([StructureSamples.Module("a", "A"), StructureSamples.Module("b", "B")], []);
        var model = shell.Model.Architecture;
        model.AgainCommand.Execute(null);
        Assert.Null(model.DirectionRefusal("a", "b"));
        Assert.Null(model.DirectionRefusal("b", "a"));

        model.SetDirection("a", "b", true);
        Assert.True(model.IsAllowed("a", "b"));
        Assert.NotNull(model.DirectionRefusal("b", "a"));
        model.SetDirection("a", "b", false);
        Assert.Empty(model.Directions);
        model.SetDirection("b", "a", true);
        Assert.True(model.IsAllowed("b", "a"));
    }

    // Forward directions, a few that go back (so some modules may use each other), and names from one letter to thirty.
    private static StructureReport RandomSet(int count, double density, Random random)
    {
        var names = Enumerable.Range(0, count).Select(i => i % 4 == 0 ? $"m{i}-{new string('x', 20 + random.Next(9))}" : i % 4 == 1 ? "m" : $"m{i}-{new string('x', random.Next(10))}").Distinct().ToList();
        var directions = new List<StructureDirection>();
        for(var i = 0; i < names.Count; i++)
        {
            for(var j = i + 1; j < names.Count; j++)
            {
                if(random.NextDouble() < density)
                    directions.Add(new StructureDirection(names[i], names[j]));

                if(random.NextDouble() < density / 4)
                    directions.Add(new StructureDirection(names[j], names[i]));
            }
        }

        return FakeStructureService.Build(names.Select(name => StructureSamples.Module(name, name)), directions);
    }

    private static List<string> TextsIn(Control row)
    {
        return row.GetVisualDescendants().OfType<TextBlock>().Select(text => text.Text ?? string.Empty).ToList();
    }

    private static Button FindRow(ViewHost host, string name)
    {
        return Assert.IsType<Button>(host.Find<Button>(button => button.Classes.Contains("arch-row") && (string?)button.Tag == name));
    }

    private static Button FindButton(ViewHost host, string name)
    {
        return Assert.IsType<Button>(host.Find<Button>(button => button.IsEffectivelyVisible && AutomationProperties.GetName(button) == name));
    }
}
