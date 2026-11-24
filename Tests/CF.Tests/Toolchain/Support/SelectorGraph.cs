using CF.Core.Changes;
using CF.Languages.CSharp.Toolchain;

namespace CF.Tests.Toolchain.Support;

/// <summary>
/// A graph of units held in memory, as the evaluation of a tree would give it, for the questions that need no build.
/// </summary>
/// <remarks>
/// Library (net9.0) is needed by Consumer (net9.0 and net11.0), which is needed by Tool (net11.0). Other (net11.0) stands
/// alone and shares Shared/Common.cs with Library.
/// </remarks>
internal sealed class SelectorGraph
{
    public SelectorGraph(string treeFolder)
    {
        Tree = ToolchainPaths.Normalize(treeFolder);
        Graph = new UnitGraph(new List<ProjectEvaluation>
        {
            Evaluate("Library", new[] { Unit("Library", "net9.0", new[] { "Library/Greeter.cs", "Shared/Common.cs" }, Array.Empty<string>()) }),
            Evaluate("Consumer", new[] { Unit("Consumer", "net9.0", new[] { "Consumer/Use.cs" }, new[] { "Library" }), Unit("Consumer", "net11.0", new[] { "Consumer/Use.cs" }, new[] { "Library" }) }),
            Evaluate("Tool", new[] { Unit("Tool", "net11.0", new[] { "Tool/Runner.cs" }, new[] { "Consumer" }) }),
            Evaluate("Other", new[] { Unit("Other", "net11.0", new[] { "Other/Lonely.cs", "Shared/Common.cs" }, Array.Empty<string>()) })
        });
        ProjectFolders = new[] { "Library", "Consumer", "Tool", "Other" }.Select(name => ToolchainPaths.Combine(Tree, name)).ToList();
    }

    public string Tree { get; }

    public UnitGraph Graph { get; }

    public IReadOnlyList<string> ProjectFolders { get; }

    public static ChangedFile Change(string text)
    {
        var kind = text.Substring(0, 2);
        var path = text.Substring(2);
        var arrow = path.IndexOf("->", StringComparison.Ordinal);
        if(arrow >= 0)
            return new ChangedFile(path.Substring(arrow + 2), null, ChangeStatus.Renamed, new List<LineRange>(), path.Substring(0, arrow));

        return new ChangedFile(path, path.EndsWith(".cs", StringComparison.Ordinal) ? "csharp" : null, kind == "D:" ? ChangeStatus.Deleted : ChangeStatus.Modified, new List<LineRange>());
    }

    public BuildPlan Select(bool isWholeReview, IEnumerable<string> changes)
    {
        var files = changes.Select(Change).ToList();
        return isWholeReview ? ChangeSelector.SelectForWholeReview(Graph, Tree, files) : ChangeSelector.SelectForChange(Graph, Tree, files);
    }

    private ProjectEvaluation Evaluate(string name, IEnumerable<ProjectUnit> units)
    {
        return ProjectEvaluation.Succeeded(new ProjectFile(ToolchainPaths.Combine(Tree, name + "/" + name + ".csproj"), name), units.ToList());
    }

    private ProjectUnit Unit(string project, string framework, string[] sources, string[] references)
    {
        var file = new ProjectFile(ToolchainPaths.Combine(Tree, project + "/" + project + ".csproj"), project);
        var compiled = sources.Select(source => ToolchainPaths.Combine(Tree, source)).ToHashSet();
        var referenced = references.Select(name => ToolchainPaths.Combine(Tree, name + "/" + name + ".csproj")).ToList();

        return new ProjectUnit(file, framework, compiled, referenced, ToolchainPaths.Combine(Tree, project + "/obj/fake.dll"), new List<string>(), string.Empty);
    }
}
