using Avalonia.Styling;
using CF.App.Architecture;
using CF.App.Shell;
using CF.Core.Ports;
using CF.Core.Review;

namespace CF.App.Tests.Support;

/// <summary>Captures each Architecture state with the application's shell and shared frame.</summary>
internal static class ArchitecturePictures
{
    public static async Task SaveAsync(string folder, string theme)
    {
        using var shell = new ShellHost(repositoryName: "CommitFence");
        shell.Structure.Report = StructureSamples.Own(StructureSamples.Clean());
        shell.Model.CurrentScreen = Screen.Architecture;
        var model = shell.Model.Architecture;
        model.Choose("core-review");
        var window = new MainWindow
        {
            DataContext = shell.Model,
            Width = 1440,
            Height = 900,
            RequestedThemeVariant = theme == "light" ? ThemeVariant.Light : ThemeVariant.Dark
        };
        using var host = new ViewHost(window);
        var view = host.Find<ArchitectureView>(candidate => candidate.IsEffectivelyVisible)!;
        host.Save(folder, $"architecture-{theme}-1440.png");
        view.Mode.SelectedIndex = 1;
        window.Height = 1000;
        host.Save(folder, $"architecture-table-{theme}-1440.png");
        view.Mode.SelectedIndex = 0;

        window.Width = 760;
        window.Height = 1000;
        host.Save(folder, $"architecture-{theme}-760.png");
        view.OpenModuleSwitcher();
        host.Save(folder, $"architecture-switch-{theme}-760.png");
        view.PickerPopup.IsOpen = false;

        var broken = StructureSamples.Broken();
        shell.Structure.Report = StructureSamples.Own(broken);
        shell.Review.Result = _ => new ReviewResult(ReviewOutcome.Blocked, [], [], [], "The push is blocked.", TimeSpan.Zero)
        {
            StructureJudgment = broken
        };
        shell.Model.ShowReview();
        await shell.Model.Review.RunCommand.ExecuteAsync(null);
        shell.Model.CurrentScreen = Screen.Architecture;
        model.Choose("core-rules");
        host.Save(folder, $"architecture-trouble-{theme}-760.png");
        window.Width = 1440;
        window.Height = 900;
        host.Save(folder, $"architecture-trouble-{theme}-1440.png");
        view.Mode.SelectedIndex = 1;
        window.Height = 1000;
        host.Save(folder, $"architecture-troubletable-{theme}-1440.png");
        view.Mode.SelectedIndex = 0;

        var other = new StructureJudgment(Samples.Now, null,
            [new StructureBreach("csharp", "core-rules", "CSharpReader.Read", "RuleSet", "Dependencies/CF.Languages.CSharp/CSharpReader.cs", 41)]);
        shell.Structure.Report = StructureSamples.Own(other);
        shell.Review.Result = _ => new ReviewResult(ReviewOutcome.Blocked, [], [], [], "The push is blocked.", TimeSpan.Zero)
        {
            StructureJudgment = other
        };
        shell.Model.ShowReview();
        await shell.Model.Review.RunCommand.ExecuteAsync(null);
        shell.Model.CurrentScreen = Screen.Architecture;
        model.AgainCommand.Execute(null);
        model.Choose("csharp");
        window.Width = 1440;
        window.Height = 900;
        host.Save(folder, $"architecture-other-{theme}-1440.png");

        shell.Structure.Report = StructureSamples.Large(StructureSamples.Clean());
        model.AgainCommand.Execute(null);
        model.Choose("invoicing-app");
        window.Height = 900;
        host.Save(folder, $"architecture-large-{theme}-1440.png");
        window.Width = 760;
        window.Height = 1100;
        host.Save(folder, $"architecture-large-{theme}-760.png");
        view.Mode.SelectedIndex = 1;
        window.Width = 1440;
        window.Height = 1240;
        host.Save(folder, $"architecture-largetable-{theme}-1440.png");

        shell.Structure.Report = FakeStructureService.Build([], []);
        model.AgainCommand.Execute(null);
        window.Height = 900;
        host.Save(folder, $"architecture-empty-{theme}-1440.png");
    }
}
