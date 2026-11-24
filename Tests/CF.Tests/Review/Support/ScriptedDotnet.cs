using System.Text.Json;
using CF.Core.Ports;

namespace CF.Tests.Review.Support;

/// <summary>
/// What the dotnet command answers for two projects, App (which uses Program.cs and refers to Lib) and Lib (which uses
/// Lib.cs): how they evaluate, that packages restore, and that Lib does not build. Nothing is started.
/// </summary>
internal static class ScriptedDotnet
{
    public const string BuildErrorOfLib = "Lib.cs(4,9): error CS1002: ; expected";

    public static ScriptedProcessRunner Create(bool appCannotBeEvaluated)
    {
        return new ScriptedProcessRunner()
            .Route(request => request.Arguments.Count == 1 && request.Arguments[0] == "--version", request => ScriptedProcessRunner.Output("11.0.100\n"))
            .Route(request => request.Arguments[0] == "restore", request => ScriptedProcessRunner.Output(string.Empty))
            .Route(request => request.Arguments.Contains("-t:Build"), request => ScriptedProcessRunner.Failure(1, BuildErrorOfLib))
            .Route(request => request.Arguments.Contains("-getProperty:TargetFramework"), request => AnswerFrameworks(request, appCannotBeEvaluated))
            .Route(request => request.Arguments.Contains("-getItem:Compile"), AnswerItems);
    }

    private static ProcessResult AnswerFrameworks(ProcessRequest request, bool appCannotBeEvaluated)
    {
        if(appCannotBeEvaluated && NameOf(request) == "App")
            return ScriptedProcessRunner.Failure(1, "error MSB4025: The project file could not be loaded.");

        return ScriptedProcessRunner.Output(JsonSerializer.Serialize(new { Properties = new { TargetFramework = "net11.0", TargetFrameworks = string.Empty } }));
    }

    private static ProcessResult AnswerItems(ProcessRequest request)
    {
        var name = NameOf(request);
        var folder = Path.GetDirectoryName(request.Arguments[1])!;
        var compile = new[] { new { FullPath = Path.Combine(folder, name == "App" ? "Program.cs" : "Lib.cs") } };
        var referencedProjects = name == "App" ? new[] { Path.GetFullPath(Path.Combine(folder, "..", "Lib", "Lib.csproj")) } : Array.Empty<string>();
        var references = referencedProjects.Select(path => new { FullPath = path }).ToArray();
        var assembly = new[] { new { FullPath = Path.Combine(folder, "obj", "Debug", "net11.0", name + ".dll") } };
        var answer = new
        {
            Items = new { Compile = compile, ProjectReference = references, IntermediateAssembly = assembly },
            Properties = new { DefineConstants = string.Empty, CustomAfterDirectoryBuildTargets = string.Empty }
        };

        return ScriptedProcessRunner.Output(JsonSerializer.Serialize(answer));
    }

    private static string NameOf(ProcessRequest request)
    {
        return Path.GetFileNameWithoutExtension(request.Arguments[1]);
    }
}
