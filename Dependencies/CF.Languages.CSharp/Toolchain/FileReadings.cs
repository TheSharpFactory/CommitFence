using CF.Core.Changes;
using CF.Core.Code;

namespace CF.Languages.CSharp.Toolchain;

/// <summary>
/// Finds, for each changed source file, every project and target framework of the plan that compiles it, with the
/// symbols the build defines there.
/// </summary>
/// <remarks>
/// The build's own evaluation is the source: nothing is guessed from the project file.
/// </remarks>
internal static class FileReadings
{
    public static IReadOnlyList<FileReading> Find(UnitGraph graph, BuildPlan plan, string treeFolder, IReadOnlyList<ChangedFile> changedFiles)
    {
        var readings = new List<FileReading>();
        var reviewed = graph.Units.Where(unit => plan.ReviewedKeys.Contains(unit.Key)).ToList();
        foreach(var file in changedFiles.Where(IsSourceFileToRead))
        {
            var fullPath = ToolchainPaths.Combine(treeFolder, file.Path);
            foreach(var unit in reviewed.Where(candidate => candidate.CompileFiles.Contains(fullPath)))
                readings.Add(new FileReading(file.Path, unit.Project.DisplayName, unit.Framework, unit.DefineConstants));
        }

        return readings;
    }

    private static bool IsSourceFileToRead(ChangedFile file)
    {
        return file.Status != ChangeStatus.Deleted && file.Path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase);
    }
}