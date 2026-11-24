using System.Text.Json;
using CF.Core.Ports;

namespace CF.Tests.Toolchain.Support;

/// <summary>
/// One target framework of a project of the fake tree: the files it compiles and the projects it references.
/// </summary>
internal sealed class FakeUnit
{
    public FakeUnit(string framework, IReadOnlyList<string> compiles, IReadOnlyList<string> references, IReadOnlyList<string>? defines = null, string? intermediateAssembly = null)
    {
        Framework = framework;
        Compiles = compiles;
        References = references;
        Defines = defines ?? new List<string>();
        IntermediateAssembly = intermediateAssembly;
    }

    public string Framework { get; }

    /// <summary>
    /// Paths of the source files, relative to the tree.
    /// </summary>
    public IReadOnlyList<string> Compiles { get; }

    /// <summary>
    /// Paths of the referenced project files, relative to the tree.
    /// </summary>
    public IReadOnlyList<string> References { get; }

    public IReadOnlyList<string> Defines { get; }

    /// <summary>
    /// The path of the compiler's output in obj, relative to the tree or absolute; the default is inside the tree.
    /// </summary>
    public string? IntermediateAssembly { get; }
}

/// <summary>
/// A project of the fake tree, as the build would describe it.
/// </summary>
internal sealed class FakeProject
{
    public FakeProject(string path, params FakeUnit[] units)
    {
        Path = path;
        Units = units;
    }

    public string Path { get; }

    public IReadOnlyList<FakeUnit> Units { get; }

    public string Name
    {
        get { return System.IO.Path.GetFileNameWithoutExtension(Path); }
    }
}

/// <summary>
/// One request to compile a unit, as the review made it.
/// </summary>
internal sealed record BuildCall(string Project, string Framework, string LogPath, IReadOnlyList<string> LetThrough);

/// <summary>
/// What the fake build answers: an exit code, and the text of the compiler's log when it writes one.
/// </summary>
internal sealed record BuildAnswer(int ExitCode, string? Log, bool TimedOut = false, string Output = "");

/// <summary>
/// A stand-in for the dotnet command: it answers the questions the review asks about the tree from a description of the
/// projects, and plays the builds from a script, writing the compiler's log where the review asked for it.
/// </summary>
internal sealed class FakeDotnet : IProcessRunner
{
    private readonly string _tree;
    private readonly List<FakeProject> _projects;
    private readonly Lock _syncRoot = new Lock();
    private readonly List<ProcessRequest> _requests = new List<ProcessRequest>();

    public FakeDotnet(string tree, params FakeProject[] projects)
    {
        _tree = tree;
        _projects = projects.ToList();
    }

    /// <summary>
    /// The text between two symbols in DefineConstants; the compiler's task takes a semicolon, a comma or a space.
    /// </summary>
    public string SymbolSeparator { get; set; } = ";";

    public ProcessResult VersionAnswer { get; set; } = new ProcessResult(0, "11.0.100\n", string.Empty);

    /// <summary>
    /// What a restore answers for a project (its file name without extension); a restore succeeds by default.
    /// </summary>
    public Func<string, ProcessResult> RestoreAnswer { get; set; } = _ => new ProcessResult(0, string.Empty, string.Empty);

    /// <summary>
    /// What a build answers; by default, and when the script answers null, a build succeeds with a log that holds only the closing entry of the analyzer.
    /// </summary>
    public Func<BuildCall, BuildAnswer?>? BuildAnswerFor { get; set; }

    /// <summary>
    /// Describes the project another way from now on: the same project file, other frameworks, sources or references.
    /// </summary>
    public void Replace(FakeProject project)
    {
        _projects.RemoveAll(candidate => candidate.Path == project.Path);
        _projects.Add(project);
    }

    /// <summary>
    /// The lines the manifest of a build holds besides the sources, the references and the properties the fake writes
    /// itself, such as the files given to an analyzer; each is a tag, a bar and a value, as the real target writes them.
    /// </summary>
    public Func<BuildCall, IReadOnlyList<string>> ManifestAdditions { get; set; } = _ => new List<string>();

    /// <summary>
    /// Whether the compiler of a build wrote its log to the path the review asked for.
    /// </summary>
    public Func<BuildCall, bool> LogIsAsked { get; set; } = _ => true;

    /// <summary>
    /// Whether the manifest of a build has its closing line: a manifest cut short does not.
    /// </summary>
    public Func<BuildCall, bool> ManifestIsClosed { get; set; } = _ => true;

    public IReadOnlyList<ProcessRequest> Requests
    {
        get
        {
            lock(_syncRoot)
                return _requests.ToList();
        }
    }

    public List<BuildCall> Builds { get; } = new List<BuildCall>();

    public ProcessResult Run(ProcessRequest request)
    {
        lock(_syncRoot)
            _requests.Add(request);

        var arguments = request.Arguments;
        if(arguments.Count > 0 && arguments[0] == "--version")
            return VersionAnswer;

        if(arguments.Count > 1 && arguments[0] == "restore")
            return RestoreAnswer(System.IO.Path.GetFileNameWithoutExtension(arguments[1]));

        if(arguments.Count > 1 && arguments[0] == "msbuild")
            return RunMsBuild(arguments);

        return new ProcessResult(1, string.Empty, "error: the fake dotnet does not know " + string.Join(" ", arguments));
    }

    private ProcessResult RunMsBuild(IReadOnlyList<string> arguments)
    {
        var project = _projects.FirstOrDefault(candidate => FullPath(candidate.Path) == arguments[1].Replace('\\', '/'));
        if(project == null)
            return new ProcessResult(1, string.Empty, "error MSB1009: Project file does not exist.");

        var framework = FindValue(arguments, "-p:TargetFramework=");
        if(arguments.Contains("-t:Build"))
            return Build(project, framework ?? string.Empty, arguments);

        if(arguments.Contains("-getItem:Compile"))
            return new ProcessResult(0, DescribeUnit(project.Units.First(unit => unit.Framework == framework)), string.Empty);

        return new ProcessResult(0, DescribeFrameworks(project), string.Empty);
    }

    private ProcessResult Build(FakeProject project, string framework, IReadOnlyList<string> arguments)
    {
        var logPath = FindValue(arguments, "-p:CommitFenceLog=") ?? string.Empty;
        var letThrough = (FindValue(arguments, "-p:CommitFenceLetThrough=") ?? string.Empty).Split("%3B", StringSplitOptions.RemoveEmptyEntries).ToList();
        var call = new BuildCall(project.Name, framework, logPath, letThrough);
        lock(_syncRoot)
            Builds.Add(call);

        var answer = BuildAnswerFor?.Invoke(call) ?? new BuildAnswer(0, SarifLogText.CleanLog(project.Name, project.Units.First(unit => unit.Framework == framework).Compiles.Count));
        if(answer.Log != null)
            File.WriteAllText(logPath, answer.Log);

        if(answer.TimedOut)
            return new ProcessResult(-2, string.Empty, "dotnet did not finish within 1200 seconds and was stopped.", true);

        if(answer.ExitCode == 0)
            File.WriteAllLines(FindValue(arguments, "-p:CommitFenceManifest=") ?? string.Empty, DescribeManifest(project, framework, call));

        return new ProcessResult(answer.ExitCode, answer.Output, string.Empty);
    }

    /// <summary>
    /// What the real target writes after the compile, also when the compile was skipped.
    /// </summary>
    private List<string> DescribeManifest(FakeProject project, string framework, BuildCall call)
    {
        var unit = project.Units.First(candidate => candidate.Framework == framework);
        var lines = new List<string> { "errorlog|" + (LogIsAsked(call) ? "True" : "False"), "prop|TargetFramework=" + framework };
        lines.AddRange(unit.Compiles.Select(path => "source|" + FullPath(path)));
        foreach(var reference in unit.References)
        {
            var referenced = _projects.First(candidate => FullPath(candidate.Path) == FullPath(reference)).Units[0];
            lines.Add("reference|" + FullPath(referenced.IntermediateAssembly ?? "obj/Debug/" + referenced.Framework + "/fake.dll"));
        }

        lines.AddRange(ManifestAdditions(call));
        if(ManifestIsClosed(call))
            lines.Add("end|manifest");

        return lines;
    }

    private string FullPath(string relative)
    {
        return System.IO.Path.GetFullPath(System.IO.Path.Combine(_tree, relative)).Replace('\\', '/');
    }

    private static string DescribeFrameworks(FakeProject project)
    {
        var frameworks = project.Units.Select(unit => unit.Framework).ToList();
        var single = frameworks.Count == 1 ? frameworks[0] : string.Empty;
        return JsonSerializer.Serialize(new { Properties = new { TargetFramework = single, TargetFrameworks = string.Join(";", frameworks) } });
    }

    private string DescribeUnit(FakeUnit unit)
    {
        var assembly = unit.IntermediateAssembly ?? "obj/Debug/" + unit.Framework + "/fake.dll";
        var items = new
        {
            Compile = unit.Compiles.Select(path => new { FullPath = FullPath(path) }).ToList(),
            ProjectReference = unit.References.Select(path => new { FullPath = FullPath(path) }).ToList(),
            IntermediateAssembly = new[] { new { FullPath = FullPath(assembly) } }
        };

        return JsonSerializer.Serialize(new { Items = items, Properties = new { DefineConstants = string.Join(SymbolSeparator, unit.Defines), CustomAfterDirectoryBuildTargets = string.Empty } });
    }

    private static string? FindValue(IReadOnlyList<string> arguments, string prefix)
    {
        var argument = arguments.FirstOrDefault(candidate => candidate.StartsWith(prefix, StringComparison.Ordinal));
        return argument?.Substring(prefix.Length).Replace("%25", "%", StringComparison.Ordinal);
    }
}
