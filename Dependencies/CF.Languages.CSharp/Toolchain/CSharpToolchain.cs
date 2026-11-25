using CF.Core.Changes;
using CF.Core.Code;
using CF.Core.Findings;
using CF.Core.Ports;
using CF.Languages.CSharp.Meaning;

namespace CF.Languages.CSharp.Toolchain;

/// <summary>
/// Checks C# with the team's own compiler, run through the build on the private copy.
/// </summary>
/// <remarks>
/// It finds the projects, asks the build which of them compile the changed files, compiles those and everything that
/// depends on them in dependency order, and reads the compiler's logs, with what our analyzer wrote into them about the
/// meaning of the code. It answers the same question for the tree before a push, from kept answers or by building it.
/// It writes nothing into the reviewed tree except build outputs.
/// </remarks>
internal sealed class CSharpToolchain : IToolchain
{
    private const string _languageId = "csharp";

    private readonly IFileSystem _fileSystem;
    private readonly ToolchainTargets _targets;
    private readonly DotnetTool _dotnet;
    private readonly ProjectFinder _finder;
    private readonly SdkCheck _sdkCheck;
    private readonly ProjectEvaluator _evaluator;
    private readonly MeaningAnalyzerFile _analyzer;
    private readonly KeptAnswers _kept;
    private readonly IDataFolder _dataFolder;

    public CSharpToolchain(IProcessRunner processRunner, IFileSystem fileSystem, IDataFolder dataFolder)
    {
        _fileSystem = fileSystem;
        _dataFolder = dataFolder;
        _targets = new ToolchainTargets(fileSystem, dataFolder);
        _dotnet = new DotnetTool(processRunner);
        _finder = new ProjectFinder(fileSystem);
        _sdkCheck = new SdkCheck(_dotnet);
        _evaluator = new ProjectEvaluator(_dotnet);
        _analyzer = new MeaningAnalyzerFile(fileSystem);
        _kept = new KeptAnswers(fileSystem, dataFolder);
    }

    public IReadOnlyList<ChangedFile> FilesOfConcern(string treeFolder, IReadOnlyList<ChangedFile> files)
    {
        var projectFolders = _finder.Find(treeFolder).Select(project => ToolchainPaths.FolderOf(project.Path)).ToList();
        return files.Where(file => IsConcern(treeFolder, file.Path, projectFolders) || (file.PreviousPath != null && IsConcern(treeFolder, file.PreviousPath, projectFolders))).ToList();
    }

    /// <summary>
    /// A push can reach any project, so a project that cannot be evaluated always counts. A whole review judges the
    /// projects under its target only: one outside it that cannot be evaluated counts when a project under review
    /// needs it, and the build walk says so there.
    /// </summary>
    private static bool IsUnderReview(ProjectFile project, ToolchainRequest request)
    {
        if(!request.IsWholeReview)
            return true;

        return request.ChangedFiles.Any(file => string.Equals(ToolchainPaths.Combine(request.TreeFolder, file.Path), project.Path, ToolchainPaths.Comparison));
    }

    public bool IsProjectFile(string path)
    {
        return ChangeSelector.IsProjectFile(path);
    }

    private static bool IsConcern(string treeFolder, string path, IReadOnlyList<string> projectFolders)
    {
        return ChangeSelector.IsConcern(ToolchainPaths.Combine(treeFolder, path), projectFolders);
    }

    public ToolchainResult Run(ToolchainRequest request)
    {
        var report = new ToolchainReport();
        try
        {
            RunInto(WithRealTree(request), report);
        }
        catch(Exception exception) when(exception is IOException || exception is UnauthorizedAccessException)
        {
            // caught here and not only at the review's own boundary, because the report already holds what the builds
            // before the failure found, and that must not be lost
            report.AddCoverage("C# build", CoverageStatus.Failed, "the build could not be run: " + exception.Message);
        }

        return report.ToResult();
    }

    /// <summary>
    /// The request with the tree under the name the build will see it by: what the compiler writes into its logs is
    /// compared with this folder, and a folder reached through a symbolic link is another text for the same place.
    /// </summary>
    private ToolchainRequest WithRealTree(ToolchainRequest request)
    {
        return new ToolchainRequest(request.Tree, _fileSystem.ResolveRealPath(request.TreeFolder), request.LogFolder, request.ChangedFiles, request.IsWholeReview);
    }

    private void RunInto(ToolchainRequest request, ToolchainReport report)
    {
        var projects = _finder.Find(request.TreeFolder);
        if(projects.Count == 0)
        {
            report.AddNote("No C# project was found in this tree, so nothing was compiled.");
            ReportNotCovered(SelectPlan(new UnitGraph(new List<ProjectEvaluation>()), request), report);
            return;
        }

        var sdk = _sdkCheck.Check(request.TreeFolder);
        if(sdk.Problem != null)
        {
            report.AddCoverage("C# build", CoverageStatus.Failed, sdk.Problem);
            return;
        }

        if(!_analyzer.Exists())
        {
            report.AddCoverage("C# build", CoverageStatus.Failed, _analyzer.DescribeMissing());
            return;
        }

        var graph = new UnitGraph(_evaluator.EvaluateAll(projects, request.TreeFolder));
        report.SetTreeAssemblies(graph.AssemblyNames());
        foreach(var evaluation in graph.Evaluations.Where(candidate => candidate.Failure != null && IsUnderReview(candidate.Project, request)))
            report.AddCoverage(evaluation.Project.DisplayName, CoverageStatus.Failed, evaluation.Failure ?? string.Empty);

        var plan = SelectPlan(graph, request);
        ReportNotCovered(plan, report);

        if(plan.Order.Count == 0)
            return;

        report.AddReadings(FileReadings.Find(graph, plan, request.TreeFolder, request.ChangedFiles));
        Compile(request.TreeFolder, request.LogFolder, request.IsWholeReview, sdk.Version, graph, plan, report);
        KeepAnswers(request.Tree, request.TreeFolder, sdk.Version, report);
        RemoveEvidenceOfGoneProjects(request.TreeFolder, graph);
    }

    /// <summary>
    /// Removes what was kept for projects that no longer exist, when every project of the tree could be read: one that
    /// could not be read may still be there.
    /// </summary>
    private void RemoveEvidenceOfGoneProjects(string treeFolder, UnitGraph graph)
    {
        if(graph.Evaluations.Any(evaluation => evaluation.Failure != null))
            return;

        try
        {
            new KeptEvidence(_fileSystem, _dataFolder, treeFolder).RemoveExcept(graph.Units.Select(unit => unit.PartKeyIn(treeFolder)));
        }
        catch(Exception exception) when(exception is IOException || exception is UnauthorizedAccessException)
        {
            // only a cleaning: the next review tries again
        }
    }

    private static BuildPlan SelectPlan(UnitGraph graph, ToolchainRequest request)
    {
        return request.IsWholeReview
            ? ChangeSelector.SelectForWholeReview(graph, request.TreeFolder, request.ChangedFiles)
            : ChangeSelector.SelectForChange(graph, request.TreeFolder, request.ChangedFiles);
    }

    /// <summary>
    /// The answers for another tree than the one under review, for the parts asked for.
    /// </summary>
    /// <remarks>
    /// Answers kept under the same key are used; otherwise the parts are built in the private copy, which already holds
    /// that tree. A tree that cannot be built gives answers with gaps.
    /// </remarks>
    public MeaningAnswers AnswerFor(MeaningRequest requested)
    {
        var request = new MeaningRequest(requested.Tree, _fileSystem.ResolveRealPath(requested.TreeFolder), requested.LogFolder, requested.Parts);
        var sdk = _sdkCheck.Check(request.TreeFolder);
        if(sdk.Problem != null)
            return MeaningAnswers.Missing(_languageId, sdk.Problem);

        if(!_analyzer.Exists())
            return MeaningAnswers.Missing(_languageId, _analyzer.DescribeMissing());

        var kept = _kept.Find(MeaningKey.Create(request.Tree, sdk.Version, _analyzer.Hash()), request.Parts);
        if(kept != null)
            return MeaningAnswerBuilder.Build(kept, new List<string>(), new List<string>());

        var report = new ToolchainReport();
        BuildParts(request, sdk.Version, report);

        return report.ToAnswers();
    }

    private void BuildParts(MeaningRequest request, string sdkVersion, ToolchainReport report)
    {
        var projects = _finder.Find(request.TreeFolder);
        if(projects.Count == 0)
            return;

        var graph = new UnitGraph(_evaluator.EvaluateAll(projects, request.TreeFolder));
        foreach(var evaluation in graph.Evaluations.Where(candidate => candidate.Failure != null))
            report.AddCoverage(evaluation.Project.DisplayName, CoverageStatus.Failed, evaluation.Failure ?? string.Empty);

        var plan = ChangeSelector.SelectParts(graph, request.TreeFolder, request.Parts);
        if(plan.Order.Count == 0)
            return;

        Compile(request.TreeFolder, request.LogFolder, false, sdkVersion, graph, plan, report);
        KeepAnswers(request.Tree, request.TreeFolder, sdkVersion, report);
    }

    /// <summary>
    /// Keeps complete answers under the key of the tree, so that the next push, whose tree before is this one, needs no
    /// second build.
    /// </summary>
    /// <remarks>
    /// Keeping is only a saving: a file that cannot be written never fails a review.
    /// </remarks>
    private void KeepAnswers(string tree, string treeFolder, string sdkVersion, ToolchainReport report)
    {
        if(!report.ToAnswers().IsComplete || report.Facts.Count == 0)
            return;

        try
        {
            _kept.Save(MeaningKey.Create(tree, sdkVersion, _analyzer.Hash()), treeFolder, report.Facts);
        }
        catch(Exception exception) when(exception is IOException || exception is UnauthorizedAccessException)
        {
            report.AddNote("The answers for this tree could not be kept for the next review: " + exception.Message);
        }
    }

    private static void ReportNotCovered(BuildPlan plan, ToolchainReport report)
    {
        foreach(var path in plan.NotCovered)
            report.AddCoverage(path, CoverageStatus.Failed, "not covered: no project compiles this file, so the build did not check it");
    }

    private void Compile(string treeFolder, string logFolder, bool isWholeReview, string sdkVersion, UnitGraph graph, BuildPlan plan, ToolchainReport report)
    {
        var targetsPath = _targets.EnsureTargetsFile();
        var restorer = new PackageRestorer(_dotnet, _targets.EnsureEmptyPackageSource());
        var restoreFailures = restorer.Restore(plan, graph, treeFolder);
        var evidenceKey = new EvidenceKey(_fileSystem, treeFolder, sdkVersion, _analyzer.Hash());
        var builder = new UnitBuilder(_fileSystem, _dotnet, treeFolder, logFolder, targetsPath, _analyzer.Path, evidenceKey, new KeptEvidence(_fileSystem, _dataFolder, treeFolder), isWholeReview);
        try
        {
            new BuildWalker(builder, graph).Walk(plan, restoreFailures, report);
        }
        finally
        {
            report.RanBuild = builder.RanBuild;
        }
    }
}