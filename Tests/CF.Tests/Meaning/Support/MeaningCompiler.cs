using System.Collections.Immutable;
using CF.Core.Code;
using CF.Languages.CSharp.Meaning;
using CF.Meaning;
using CF.Tests.Toolchain.Support;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace CF.Tests.Meaning.Support;

/// <summary>
/// One project compiled in memory with the analyzer of CommitFence: the facts it wrote and its closing entry, as a build of
/// the team's compiler would put them into its log.
/// </summary>
internal sealed class MeaningAnalysis
{
    public MeaningAnalysis(string assembly, string treeFolder, IReadOnlyDictionary<string, string> sources, Compilation compilation, IReadOnlyList<string> facts, string closing)
    {
        Assembly = assembly;
        TreeFolder = treeFolder;
        Sources = sources;
        Compilation = compilation;
        Facts = facts;
        Closing = closing;
    }

    public string Assembly { get; }

    public string TreeFolder { get; }

    /// <summary>
    /// The text of each source file, by its path relative to the tree.
    /// </summary>
    public IReadOnlyDictionary<string, string> Sources { get; }

    public Compilation Compilation { get; }

    public IReadOnlyList<string> Facts { get; }

    public string Closing { get; }

    public string PartKey
    {
        get { return Assembly + "/" + Assembly + ".csproj|net11.0"; }
    }

    /// <summary>
    /// What the build's log of this project holds: every fact and the closing entry, or only the facts when the closing
    /// entry is left out.
    /// </summary>
    public string LogText(bool withClosing)
    {
        var log = new SarifLogText();
        foreach(var fact in Facts)
            log.Result("CFM001", "note", fact, null, 0, 0);

        return (withClosing ? log.Result("CFM999", "note", Closing, null, 0, 0) : log).ToString();
    }
}

/// <summary>
/// Compiles sources in memory and runs the analyzer of CommitFence over them, the way the team's compiler does in a build.
/// </summary>
internal static class MeaningCompiler
{
    private static readonly IReadOnlyList<MetadataReference> _runtime = FindRuntimeReferences();

    /// <summary>
    /// Compiles the sources of one project, which may use the projects given as references. The code must compile: an error
    /// is a mistake of the test.
    /// </summary>
    public static MeaningAnalysis Analyze(string assembly, string treeFolder, IReadOnlyDictionary<string, string> sources, params MeaningAnalysis[] references)
    {
        var trees = sources.Select(source => CSharpSyntaxTree.ParseText(source.Value, new CSharpParseOptions(LanguageVersion.Latest), Path.Combine(treeFolder, source.Key))).ToList();
        var metadata = _runtime.Concat(references.Select(reference => reference.Compilation.ToMetadataReference())).ToList();
        var options = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable);
        var compilation = CSharpCompilation.Create(assembly, trees, metadata, options);
        var errors = compilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).Select(diagnostic => diagnostic.ToString()).ToList();
        if(errors.Count > 0)
            throw new InvalidOperationException("the fixture does not compile: " + string.Join("; ", errors));

        var analyzers = ImmutableArray.Create<DiagnosticAnalyzer>(new MeaningAnalyzer());
        var diagnostics = compilation.WithAnalyzers(analyzers).GetAnalyzerDiagnosticsAsync().GetAwaiter().GetResult();
        var facts = diagnostics.Where(diagnostic => diagnostic.Id == MeaningAnalyzer.FactRuleId).Select(diagnostic => diagnostic.GetMessage()).ToList();
        var closing = diagnostics.Single(diagnostic => diagnostic.Id == MeaningAnalyzer.ClosingRuleId).GetMessage();

        return new MeaningAnalysis(assembly, treeFolder, sources, compilation, facts, closing);
    }

    /// <summary>
    /// The answers the toolchain would give for these projects: what its reader makes of each log, put together.
    /// </summary>
    public static MeaningAnswers Answers(params MeaningAnalysis[] analyses)
    {
        var units = analyses.Select(analysis => new MeaningFactReader(analysis.TreeFolder).Read(analysis.Facts, new[] { analysis.Closing }, analysis.PartKey, analysis.Sources.Count)).ToList();
        return MeaningAnswerBuilder.Build(units, new List<string>(), analyses.Select(analysis => analysis.Assembly).ToList());
    }

    private static List<MetadataReference> FindRuntimeReferences()
    {
        var platform = (string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!;
        return platform.Split(Path.PathSeparator).Where(path => Path.GetFileName(path).StartsWith("System.", StringComparison.Ordinal) || Path.GetFileName(path) == "mscorlib.dll" || Path.GetFileName(path) == "netstandard.dll").Select(path => (MetadataReference)MetadataReference.CreateFromFile(path)).ToList();
    }
}
