using CF.Core.Code;
using CF.Core.Ports;

namespace CF.Languages.CSharp.Toolchain;

/// <summary>
/// Finds projects whose build might see the change, plus the projects those builds require.
/// </summary>
internal sealed class ProjectReach
{
    private readonly Dictionary<string, ProjectFile> _projects;
    private readonly ILookup<string, ProjectFile> _dependents;
    private readonly HashSet<string> _selected = new HashSet<string>(ToolchainPaths.Comparer);

    public ProjectReach(IFileSystem files, ToolchainRequest request, IReadOnlyList<ProjectFile> projects)
    {
        var reader = new ProjectSketch(files, request.TreeFolder);
        var changes = new HashSet<string>(ToolchainPaths.Comparer);
        foreach(var file in request.ChangedFiles)
        {
            changes.Add(ToolchainPaths.Combine(request.TreeFolder, file.Path));
            if(file.PreviousPath != null)
                changes.Add(ToolchainPaths.Combine(request.TreeFolder, file.PreviousPath));
        }

        var sketches = projects.Select(project => reader.Read(project, changes)).ToList();
        _projects = projects.ToDictionary(project => project.Path, ToolchainPaths.Comparer);
        _dependents = sketches.SelectMany(sketch => sketch.References.Select(path => (Path: path, sketch.Project)))
            .ToLookup(reference => reference.Path, reference => reference.Project, ToolchainPaths.Comparer);
        Include(sketches.Where(sketch => sketch.Reachable).Select(sketch => sketch.Project.Path));
    }

    public void Include(IEnumerable<string> paths)
    {
        var pending = new Queue<string>(paths);
        while(pending.TryDequeue(out var path))
        {
            if(!_projects.ContainsKey(path) || !_selected.Add(path))
                continue;

            foreach(var dependent in _dependents[path])
                pending.Enqueue(dependent.Path);
        }
    }

    public List<ProjectFile> Pending(IReadOnlySet<string> evaluated)
    {
        return _projects.Values.Where(project => _selected.Contains(project.Path) && !evaluated.Contains(project.Path)).ToList();
    }
}
