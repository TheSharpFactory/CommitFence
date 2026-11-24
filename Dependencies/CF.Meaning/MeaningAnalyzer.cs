using System.Collections.Concurrent;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace CF.Meaning;

/// <summary>
/// The analyzer of CommitFence that the team's own compiler loads during a review's build.
/// </summary>
/// <remarks>
/// It holds no rules and no settings: for the project being compiled it reports what every name refers to and what
/// every declaration is, each as a note in the compiler's own log, and closes the log with a count and a checksum so
/// that a log that was cut short is recognised. It writes no file.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MeaningAnalyzer : DiagnosticAnalyzer
{
    /// <summary>
    /// The rule id of every fact. The compiler's own log carries it; CommitFence reads it.
    /// </summary>
    public const string FactRuleId = "CFM001";

    /// <summary>
    /// The rule id of the closing entry.
    /// </summary>
    public const string ClosingRuleId = "CFM999";

    // Not configurable: the repository's settings can neither switch these notes off one by one nor raise them to errors.
    private static readonly DiagnosticDescriptor _fact = new DiagnosticDescriptor(
        FactRuleId,
        "CommitFence fact",
        "{0}",
        "CommitFence",
        DiagnosticSeverity.Info,
        true,
        customTags: new[] { WellKnownDiagnosticTags.NotConfigurable });

    private static readonly DiagnosticDescriptor _closing = new DiagnosticDescriptor(
        ClosingRuleId,
        "CommitFence facts complete",
        "{0}",
        "CommitFence",
        DiagnosticSeverity.Info,
        true,
        customTags: new[] { WellKnownDiagnosticTags.NotConfigurable, WellKnownDiagnosticTags.CompilationEnd });

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
    {
        get { return ImmutableArray.Create(_fact, _closing); }
    }

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
        context.RegisterCompilationStartAction(StartCompilation);
    }

    private static void StartCompilation(CompilationStartAnalysisContext start)
    {
        var facts = new ConcurrentBag<string>();
        var analyzedTrees = new ConcurrentBag<string>();

        start.RegisterSemanticModelAction(model => ReportFacts(model, facts, analyzedTrees));
        start.RegisterCompilationEndAction(end => ReportClosing(end, facts, analyzedTrees));
    }

    private static void ReportFacts(SemanticModelAnalysisContext context, ConcurrentBag<string> facts, ConcurrentBag<string> analyzedTrees)
    {
        analyzedTrees.Add(context.SemanticModel.SyntaxTree.FilePath);
        foreach(var fact in FactCollector.Collect(context.SemanticModel, context.CancellationToken))
        {
            facts.Add(fact);
            context.ReportDiagnostic(Diagnostic.Create(_fact, Location.None, fact));
        }
    }

    private static void ReportClosing(CompilationAnalysisContext context, ConcurrentBag<string> facts, ConcurrentBag<string> analyzedTrees)
    {
        var text = ClosingEntry.Create(facts, context.Compilation.AssemblyName ?? string.Empty, analyzedTrees.Count);
        context.ReportDiagnostic(Diagnostic.Create(_closing, Location.None, text));
    }
}