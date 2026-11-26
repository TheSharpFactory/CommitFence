using CF.Core.Services;
using CF.Core.Setup;

namespace CF.App.Tests.Support;

/// <summary>Setup responses for the Rules and Doctor view models.</summary>
internal sealed class FakeSetupService : ISetupService
{
    public Func<string, SetupReport>? Start { get; set; }

    public string? WorktreeRoot { get; init; }

    public Func<string, HookAddition, SetupReport>? Add { get; set; }

    public SetupReport Install(string programPath, InstallOptions options)
    {
        throw new NotSupportedException();
    }

    public SetupReport Init(string folder, HookKind hook = HookKind.PrePush, bool print = false)
    {
        throw new NotSupportedException();
    }

    public bool HasSettings(string path)
    {
        return File.Exists(path);
    }

    public SetupReport StartSettings(string folder)
    {
        if(Start != null)
            return Start(folder);

        File.WriteAllText(Path.Combine(folder, "commitfence.json"), SetupService.StarterSettings);
        return new SetupReport([], false) { SettingsCreated = true, WorktreeRoot = WorktreeRoot ?? folder };
    }

    public SetupReport AddHookLine(string folder, HookAddition addition)
    {
        return Add!(folder, addition);
    }
}
