using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
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
    public void CommitFenceModulesAreAboveWhatTheyUseInADeterministicLayout()
    {
        var report = StructureSamples.Own();
        Assert.Equal(11, report.Modules.Count);
        Assert.Equal(28, report.AllowedDirections.Count);
        var layers = new ModuleLayers(report);
        var diagram = new DiagramLayout(layers, "core-review", report.AllowedDirections, null, 876);
        var again = new DiagramLayout(new ModuleLayers(report), "core-review", report.AllowedDirections, null, 876);
        Assert.All(report.AllowedDirections, edge =>
        {
            Assert.True(layers.Level[edge.From] > layers.Level[edge.To]);
            Assert.True(diagram.Boxes[edge.From].Bottom < diagram.Boxes[edge.To].Y);
        });
        Assert.Equal(diagram.Boxes.Values, again.Boxes.Values);
        Assert.Equal(diagram.Lines.SelectMany(line => line.Points), again.Lines.SelectMany(line => line.Points));
    }

    [Theory]
    [InlineData(4)]
    [InlineData(24)]
    public void IncomingLinesFromSeveralLayersHaveDistinctPortsAndUncrossedSegments(int users)
    {
        var names = Enumerable.Range(0, users + 2).Select(i => $"module-{i:D2}").ToList();
        var selected = names[^1];
        var directions = new List<StructureDirection>();
        for(var i = 0; i < names.Count - 1; i++)
            directions.Add(new StructureDirection(names[i], names[i + 1]));

        directions.AddRange(names.Take(users).Select(name => new StructureDirection(name, selected)));
        var report = FakeStructureService.Build(names.Select(name => StructureSamples.Module(name, name)), directions);
        var layout = new DiagramLayout(new ModuleLayers(report), selected, report.AllowedDirections, null, 750);
        var lines = layout.Lines.Where(line => line.Kind == DiagramLineKind.Incoming).ToList();
        Assert.Equal(users + 1, lines.Count);
        Assert.Equal(lines.SelectMany(line => line.Points).Count(), lines.SelectMany(line => line.Points).Distinct().Count());
        var paths = lines.Select(line => line.Points.Zip(line.Points.Skip(1)).ToList()).ToList();
        for(var i = 0; i < paths.Count; i++)
        {
            foreach(var other in paths.Skip(i + 1))
                Assert.DoesNotContain(paths[i], left => other.Exists(right => Cross(left, right)));
        }

        var ports = lines.Select(line => line.Points[^1].X).Order().ToList();
        Assert.All(ports.Zip(ports.Skip(1)), pair => Assert.Equal(22, pair.Second - pair.First));
        var reversed = FakeStructureService.Build(report.Modules, directions.Select(edge => new StructureDirection(edge.To, edge.From)));
        var outLayout = new DiagramLayout(new ModuleLayers(reversed), selected, reversed.AllowedDirections, null, 750);
        var outPorts = outLayout.Lines.Select(line => line.Points[0].X).Order().ToList();
        Assert.Equal(users + 1, outPorts.Count);
        Assert.All(outPorts.Zip(outPorts.Skip(1)), pair => Assert.Equal(16, pair.Second - pair.First));
        Assert.All(outLayout.Lines.SelectMany(line => line.Points), point => Assert.InRange(point.X, 0, outLayout.Width));
    }

    [Fact]
    public void ThirtyModulesStayWithinTheCanvasAndNeverOverlap()
    {
        var report = StructureSamples.Large();
        var diagram = new DiagramLayout(new ModuleLayers(report), "invoicing-app", report.AllowedDirections, null, 876);
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
        FindButton(host, "core-review").Focus();
        host.Press(PhysicalKey.ArrowUp);
        Assert.Equal("core-services", model.SelectedName);

        var module = model.SelectedName;
        var direction = new StructureDirection(module, "core-review");
        FindButton(host, "Stop allowing core-review").RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        host.Pump();
        Assert.Contains("stop allowing " + direction, shell.Structure.Calls);
        Assert.DoesNotContain(direction, model.Directions);
        view.Mode.SelectedIndex = 1;
        host.Pump();
        Assert.NotNull(host.Find<Button>(button => AutomationProperties.GetName(button) == module + " to core-review: not allowed"));
        view.Mode.SelectedIndex = 0;
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
    public void SpaceAllowsTheMarkedDirectionAndAWriteRefusalDisablesEditing()
    {
        using var shell = new ShellHost();
        var broken = StructureSamples.Own(StructureSamples.Broken());
        var reverse = new StructureDirection("core-review", "core-rules");
        shell.Structure.Report = FakeStructureService.Build(broken.Modules, broken.AllowedDirections.Where(edge => edge != reverse), broken.LastJudgment);
        var model = shell.Model.Architecture;
        model.AgainCommand.Execute(null);
        var view = new ArchitectureView { DataContext = model };
        using var host = new ViewHost(view, 1440, 900);
        view.Mode.SelectedIndex = 1;
        host.Pump();
        FindButton(host, "core-rules to core-review: breaks").Focus();
        host.Press(PhysicalKey.Space);
        Assert.Contains("allow core-rules -> core-review", shell.Structure.Calls);
        Assert.Empty(model.ActiveBreaches);
        var layout = new DiagramLayout(model.Layers, model.SelectedName, model.Directions, model.DrawnBreach, 876);
        Assert.Contains(layout.Lines, line => line.Kind == DiagramLineKind.Outgoing && line.Direction == new StructureDirection("core-rules", "core-review"));

        shell.Structure.RefuseWrite = true;
        FindButton(host, "core-rules to core-review: allowed").Focus();
        host.Press(PhysicalKey.Space);
        Assert.Equal("commitfence.json cannot be written: it is read-only.", model.Banner);
        Assert.False(model.CanEdit);
        Assert.False(view.NewButton.IsEffectivelyEnabled);
        Assert.All(host.FindAll<Button>(button => button.Classes.Contains("arch-cell")), button => Assert.False(button.IsEffectivelyEnabled));
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

        model.ToggleDirection("a", "b");
        Assert.True(model.IsAllowed("a", "b"));
        Assert.NotNull(model.DirectionRefusal("b", "a"));
        model.ToggleDirection("a", "b");
        Assert.Empty(model.Directions);
        model.ToggleDirection("b", "a");
        Assert.True(model.IsAllowed("b", "a"));
    }

    private static bool Cross((DiagramPoint First, DiagramPoint Second) left, (DiagramPoint First, DiagramPoint Second) right)
    {
        var top = Math.Max(left.First.Y, right.First.Y);
        var bottom = Math.Min(left.Second.Y, right.Second.Y);
        return top <= bottom && (At(left, top) - At(right, top)) * (At(left, bottom) - At(right, bottom)) <= 0;
    }

    private static double At((DiagramPoint First, DiagramPoint Second) segment, double y)
    {
        return segment.First.X + ((segment.Second.X - segment.First.X) * (y - segment.First.Y) / (segment.Second.Y - segment.First.Y));
    }

    private static Button FindButton(ViewHost host, string name)
    {
        return Assert.IsType<Button>(host.Find<Button>(button => button.IsEffectivelyVisible && AutomationProperties.GetName(button) == name));
    }
}
