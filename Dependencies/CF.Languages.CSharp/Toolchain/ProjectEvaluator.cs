using System.Text.Json;
using CF.Core.Ports;

namespace CF.Languages.CSharp.Toolchain;

/// <summary>
/// Asks the build's own evaluation, without compiling, which target frameworks a project has.
/// </summary>
/// <remarks>
/// For each, it asks which source files it compiles, which projects it references and where its compiler output goes
///.
/// </remarks>
internal sealed class ProjectEvaluator
{
    private const int _parallelEvaluations = 4;
    private const string _implicitSymbolsTarget = "AddImplicitDefineConstants";
    private const string _missingTargetError = "MSB4057";

    // the compiler's build task takes a semicolon, a comma or a space between two symbols
    private static readonly char[] _symbolSeparators = new[] { ';', ',', ' ' };

    private readonly DotnetTool _dotnet;

    public ProjectEvaluator(DotnetTool dotnet)
    {
        _dotnet = dotnet;
    }

    public IReadOnlyList<ProjectEvaluation> EvaluateAll(IReadOnlyList<ProjectFile> projects, string treeFolder)
    {
        return projects.AsParallel().AsOrdered().WithDegreeOfParallelism(_parallelEvaluations).Select(project => Evaluate(project, treeFolder)).ToList();
    }

    private ProjectEvaluation Evaluate(ProjectFile project, string treeFolder)
    {
        try
        {
            var units = ReadFrameworks(project, treeFolder).Select(framework => ReadUnit(project, framework, treeFolder)).ToList();
            return ProjectEvaluation.Succeeded(project, units);
        }
        catch(JsonException exception)
        {
            // Keep the reader's diagnostic for malformed JSON, including its original position.
            return ProjectEvaluation.Failed(project, (exception.InnerException as JsonException ?? exception).Message);
        }
        catch(InvalidOperationException exception)
        {
            return ProjectEvaluation.Failed(project, exception.Message);
        }
    }

    private List<string> ReadFrameworks(ProjectFile project, string treeFolder)
    {
        var answer = Ask(project, treeFolder, new List<string> { "-getProperty:TargetFramework", "-getProperty:TargetFrameworks" });
        var single = ReadProperty(answer, "TargetFramework");
        var several = ReadProperty(answer, "TargetFrameworks");
        var names = string.IsNullOrEmpty(single) ? several.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) : new[] { single };
        if(names.Length == 0)
            throw new InvalidOperationException("the project has no target framework");

        return names.ToList();
    }

    private ProjectUnit ReadUnit(ProjectFile project, string framework, string treeFolder)
    {
        var arguments = new List<string> { "-p:TargetFramework=" + framework, "-getItem:Compile", "-getItem:ProjectReference", "-getItem:IntermediateAssembly", "-getProperty:DefineConstants", "-getProperty:CustomAfterDirectoryBuildTargets" };
        var answer = AskWithImplicitSymbols(project, treeFolder, arguments);
        var items = answer.Items ?? throw new JsonException("the build's answer has no item groups");
        var compile = ReadFullPaths(items, "Compile").ToHashSet(ToolchainPaths.Comparer);
        var references = ReadFullPaths(items, "ProjectReference");
        var assembly = ReadFullPaths(items, "IntermediateAssembly").FirstOrDefault() ?? string.Empty;
        var symbols = ReadProperty(answer, "DefineConstants").Split(_symbolSeparators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct().ToList();

        return new ProjectUnit(project, framework, compile, references, assembly, symbols, ReadProperty(answer, "CustomAfterDirectoryBuildTargets"));
    }

    private static string ReadProperty(MsBuildAnswerDto answer, string name)
    {
        if(answer.Properties == null || !answer.Properties.TryGetValue(name, out var value))
            throw new JsonException($"the build's answer has no '{name}' property");

        return value ?? string.Empty;
    }

    private static List<string> ReadFullPaths(Dictionary<string, List<MsBuildItemDto>> items, string name)
    {
        if(!items.TryGetValue(name, out var list))
            return [];

        if(list == null || list.Any(item => string.IsNullOrEmpty(item?.FullPath)))
            throw new JsonException($"the build's '{name}' items must each have a full path");

        return list.Select(item => ToolchainPaths.Normalize(item.FullPath)).ToList();
    }

    /// <summary>
    /// The symbols a framework defines by itself (NET11_0_OR_GREATER and the like) are added by a target of the SDK,
    /// not at evaluation.
    /// </summary>
    /// <remarks>
    /// So that target is run too: it only sets properties and compiles nothing. Only a project the build says has no
    /// such target (one not written for the SDK) is asked again without it; any other failure, a time limit included,
    /// stays a failure, so that a list of symbols that is not the build's is never taken for it.
    /// </remarks>
    private MsBuildAnswerDto AskWithImplicitSymbols(ProjectFile project, string treeFolder, List<string> questions)
    {
        var withTarget = new List<string> { "-t:" + _implicitSymbolsTarget };
        withTarget.AddRange(questions);
        var result = Run(project, treeFolder, withTarget);
        if(HasNoSuchTarget(result))
            result = Run(project, treeFolder, questions);

        return ReadAnswer(result);
    }

    private MsBuildAnswerDto Ask(ProjectFile project, string treeFolder, List<string> questions)
    {
        return ReadAnswer(Run(project, treeFolder, questions));
    }

    private ProcessResult Run(ProjectFile project, string treeFolder, List<string> questions)
    {
        var arguments = new List<string> { "msbuild", project.Path, "-nologo" };
        arguments.AddRange(questions);

        return _dotnet.Run(treeFolder, arguments, DotnetTimeLimits.ProjectEvaluation);
    }

    private static bool HasNoSuchTarget(ProcessResult result)
    {
        return result.ExitCode != 0 && !result.TimedOut && (result.Output + result.Error).Contains(_missingTargetError, StringComparison.Ordinal);
    }

    private static MsBuildAnswerDto ReadAnswer(ProcessResult result)
    {
        if(result.ExitCode != 0)
            throw new InvalidOperationException("the build cannot evaluate the project: " + DotnetTool.SummarizeErrors(result));

        return JsonSerializer.Deserialize(result.Output, MsBuildAnswerJsonContext.Default.MsBuildAnswerDto)
            ?? throw new JsonException("the build's answer must be a JSON object");
    }
}