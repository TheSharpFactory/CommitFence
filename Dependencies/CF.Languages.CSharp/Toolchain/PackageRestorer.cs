using CF.Core.Ports;

namespace CF.Languages.CSharp.Toolchain;

/// <summary>
/// Restores the packages of the projects to build using only what is already on the machine.
/// </summary>
/// <remarks>
/// The only package source is an empty folder (the sources of the build and the additional sources of a project are
/// both replaced), so NuGet finds a package only in the global packages folder; the audit, which asks the network for
/// known vulnerabilities, is off. A package that is not there fails the restore and the review says so.
/// </remarks>
internal sealed class PackageRestorer
{
    private static readonly string[] _packageErrorCodes = { "NU1100", "NU1101", "NU1102", "NU1103", "NU1301" };

    private readonly DotnetTool _dotnet;
    private readonly string _emptyPackageSource;

    public PackageRestorer(DotnetTool dotnet, string emptyPackageSource)
    {
        _dotnet = dotnet;
        _emptyPackageSource = emptyPackageSource;
    }

    /// <summary>
    /// Restores every project of the plan, starting from the ones no other project of the plan references.
    /// </summary>
    /// <returns>
    /// For each project that could not be restored, a plain reason.
    /// </returns>
    /// <remarks>
    /// When a restore from a project fails, each project it reaches is restored on its own, so that only the projects
    /// that really fail are named.
    /// </remarks>
    public Dictionary<string, string> Restore(BuildPlan plan, UnitGraph graph, string treeFolder)
    {
        var failures = new Dictionary<string, string>(ToolchainPaths.Comparer);
        var restored = new HashSet<string>(ToolchainPaths.Comparer);
        var projects = plan.Order.Select(unit => unit.Project.Path).Distinct(ToolchainPaths.Comparer).ToList();
        var referenced = projects.SelectMany(graph.ReferencedProjectsOf).ToHashSet(ToolchainPaths.Comparer);
        foreach(var root in projects.Where(project => !referenced.Contains(project)))
            RestoreRoot(root, projects, graph, treeFolder, restored, failures);

        return failures;
    }

    private void RestoreRoot(string root, List<string> projects, UnitGraph graph, string treeFolder, HashSet<string> restored, Dictionary<string, string> failures)
    {
        var members = ClosureOf(root, graph).Where(path => projects.Contains(path, ToolchainPaths.Comparer)).ToList();
        var rootResult = Run(root, false, treeFolder);
        if(rootResult.ExitCode == 0)
        {
            restored.UnionWith(members);
            return;
        }

        if(rootResult.TimedOut)
        {
            FailAll(members, rootResult, restored, failures);
            return;
        }

        foreach(var project in members.Where(path => !restored.Contains(path) && !failures.ContainsKey(path)))
        {
            var result = Run(project, true, treeFolder);
            if(result.ExitCode == 0)
                restored.Add(project);
            else
                failures[project] = DescribeFailure(result);
        }
    }

    private static void FailAll(List<string> members, ProcessResult timedOut, HashSet<string> restored, Dictionary<string, string> failures)
    {
        foreach(var project in members.Where(path => !restored.Contains(path) && !failures.ContainsKey(path)))
            failures[project] = DescribeFailure(timedOut);
    }

    private ProcessResult Run(string project, bool alone, string treeFolder)
    {
        var arguments = new List<string> { "restore", project, "-nologo", "-v:q", "-p:RestoreSources=" + _emptyPackageSource, "-p:RestoreAdditionalProjectSources=", "-p:NuGetAudit=false" };
        if(alone)
            arguments.Add("--no-dependencies");

        return _dotnet.Run(treeFolder, arguments, DotnetTimeLimits.PackageRestore);
    }

    private static string DescribeFailure(ProcessResult result)
    {
        if(result.TimedOut)
            return "restoring its packages did not finish (" + DotnetTool.SummarizeErrors(result) + "), so it was not built. Run dotnet restore on the solution, then try again.";

        var problems = ListPackageProblems(result);
        if(problems.Count > 0)
            return "packages it needs are not on this machine, and a review never uses the network (" + string.Join("; ", problems) + "). Run dotnet restore on the solution while online, then try again.";

        return "its packages could not be restored (" + DotnetTool.SummarizeErrors(result) + "). Run dotnet restore on the solution, then try again.";
    }

    private static List<string> ListPackageProblems(ProcessResult result)
    {
        var lines = (result.Output + "\n" + result.Error).Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var problems = new List<string>();
        foreach(var line in lines)
        {
            var start = line.IndexOf("error NU", StringComparison.Ordinal);
            if(start < 0)
                continue;

            var text = line.Substring(start + "error ".Length);
            var tail = text.IndexOf(" No packages exist", StringComparison.Ordinal);
            problems.Add(tail > 0 ? text.Substring(0, tail) : text);
        }

        return problems.Where(problem => _packageErrorCodes.Any(code => problem.StartsWith(code, StringComparison.Ordinal))).Distinct().Take(3).ToList();
    }

    private static HashSet<string> ClosureOf(string root, UnitGraph graph)
    {
        var found = new HashSet<string>(ToolchainPaths.Comparer) { root };
        var pending = new Stack<string>();
        pending.Push(root);
        while(pending.Count > 0)
        {
            foreach(var reference in graph.ReferencedProjectsOf(pending.Pop()))
            {
                if(found.Add(reference))
                    pending.Push(reference);
            }
        }

        return found;
    }
}