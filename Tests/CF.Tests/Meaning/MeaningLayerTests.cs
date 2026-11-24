using CF.Core.Code;
using CF.Core.Findings;
using CF.Core.Review;
using CF.Languages.CSharp.Meaning;
using CF.Tests.Meaning.Support;
using Xunit;

namespace CF.Tests.Meaning;

/// <summary>
/// The layer directions: what counts as a dependency, which of them are new in a push, and what the review says when it
/// cannot place one.
/// </summary>
public sealed class MeaningLayerTests
{
    private const string _secret = """
        using System;

        namespace Shop.Infrastructure;

        public class Secret
        {
            public int Fn()
            {
                return 3;
            }
        }

        """;

    private const string _factory = """
        using System;
        using Shop.Infrastructure;

        namespace Shop.Factory;

        public static class Source
        {
            public static Secret Make()
            {
                return new Secret();
            }
        }

        """;

    private const string _serviceBefore = """
        using System;
        using Shop.Factory;
        using Shop.Infrastructure;

        namespace Shop.Reports;

        public class Service
        {
            private static readonly object _a = new Secret(),
                _b = 1;

            public static event Func<int>? E1 = Source.Make().Fn,
                E2 = null;

            public static object P1 { get; } = new Secret();

            public int Count { get; } = 1;
        }

        """;

    private const string _serviceAfter = """
        using System;
        using Shop.Factory;
        using Shop.Infrastructure;

        namespace Shop.Reports;

        // a comment that moves every line below it
        // and another
        public class Service
        {
            private static readonly object _a = new Secret(),
                _b = new Secret(); // new

            public static event Func<int>? E1 = Source.Make().Fn,
                E2 = Source.Make().Fn; // new

            public static object P1 { get; } = new Secret();

            public int Count { get; } = 2;

            public object Added()
            {
                return new Secret(); // new
            }
        }

        """;

    private const string _forms = """
        using System;
        using Shop.Domain;
        using Shop.Factory;
        using Shop.Infrastructure;

        namespace Shop.Reports;

        public class Forms
        {
            public void ImplicitFrom()
            {
                Source.Accept(5); // L
            }

            public int Extension()
            {
                return 3.Twice(); // L
            }

            public void GenericInferred()
            {
                Source.Wrap(Source.Make); // L
            }

            public void TargetNew()
            {
                Source.Accept(new()); // L
            }

            public void Allowed()
            {
                Source.Take(new()); // control
            }
        }

        """;

    private static readonly IReadOnlyDictionary<string, string[]> _namespaceModules = new Dictionary<string, string[]>
    {
        ["infrastructure"] = new[] { "namespace:Shop.Infrastructure" },
        ["factory"] = new[] { "namespace:Shop.Factory" },
        ["reports"] = new[] { "namespace:Shop.Reports" }
    };

    private static MeaningUse Use(string usedFile)
    {
        return new MeaningUse("src/Shop.Consumer/Consumer.cs", "Shop.Consumer.Consumer.Go", "Shop.Consumer", "Shop.Consumer", "Shop.Shared.Forbidden.Secret.Value", "Shop.Shared.Forbidden.Secret", "Shop.Shared.Forbidden", "Shop.Shared", usedFile, new List<SourcePoint> { new SourcePoint(9, 20) });
    }

    private static MeaningAnswers Answers(MeaningUse use)
    {
        return new MeaningAnswers("csharp", new List<MeaningDeclaration>(), new List<MeaningUse> { use }, new List<string> { "src/Shop.Consumer/Shop.Consumer.csproj|net11.0" }, new List<string>(), new List<string> { "Shop.Consumer", "Shop.Shared" });
    }

    private static List<int?> LinesOf(IEnumerable<Finding> findings)
    {
        return findings.Select(finding => finding.Line).Order().ToList();
    }

    private static List<int?> MarkedLines(string text, string marker)
    {
        var lines = text.Split('\n');
        return Enumerable.Range(0, lines.Length).Where(index => lines[index].EndsWith(marker, StringComparison.Ordinal)).Select(index => (int?)(index + 1)).ToList();
    }

    [Fact]
    public void OnlyNewMemberDependenciesSurviveBaselineComparison()
    {
        var folder = Path.Combine(TestPaths.Temp, "cf-meaning-baseline");
        var before = MeaningCompiler.Analyze("Shop", folder, new Dictionary<string, string> { ["Infrastructure/Secret.cs"] = _secret, ["Factory/Source.cs"] = _factory, ["Reports/Service.cs"] = _serviceBefore });
        var after = MeaningCompiler.Analyze("Shop", folder, new Dictionary<string, string> { ["Infrastructure/Secret.cs"] = _secret, ["Factory/Source.cs"] = _factory, ["Reports/Service.cs"] = _serviceAfter });
        var harness = new MeaningHarness();
        harness.Before.Before = MeaningCompiler.Answers(before);
        var settings = MeaningSettings.Layers(_namespaceModules, "factory -> infrastructure", "reports -> factory");

        var result = harness.Layers(settings, MeaningCompiler.Answers(after));

        // the second field, the second event and the new method; the old uses, the property and the edited initializer are not new
        Assert.All(result.Findings, finding => Assert.Equal("Reports/Service.cs", finding.File));
        Assert.Equal(MarkedLines(_serviceAfter, "// new"), LinesOf(result.Findings));
        Assert.Equal(ExitCode.Blocked, result.ExitCode);
    }

    [Fact]
    public void LayerRuleFindsDependenciesWithoutATypeName()
    {
        var sources = new Dictionary<string, string>
        {
            ["Infrastructure/Rich.cs"] = """
                namespace Shop.Infrastructure;

                public class Rich
                {
                    public static implicit operator Rich(int value)
                    {
                        return new Rich();
                    }
                }

                public static class Extensions
                {
                    public static int Twice(this int value)
                    {
                        return value * 2;
                    }
                }
                """,
            ["Domain/Money.cs"] = "namespace Shop.Domain;\n\npublic class Money\n{\n}\n",
            ["Factory/Source.cs"] = """
                using System;
                using System.Collections.Generic;
                using Shop.Domain;
                using Shop.Infrastructure;

                namespace Shop.Factory;

                public static class Source
                {
                    public static Rich Make()
                    {
                        return new Rich();
                    }

                    public static void Accept(Rich value)
                    {
                    }

                    public static void Take(Money value)
                    {
                    }

                    public static List<T> Wrap<T>(Func<T> maker)
                    {
                        return new List<T> { maker() };
                    }
                }
                """,
            ["Reports/Forms.cs"] = _forms
        };
        var analysis = MeaningCompiler.Analyze("Shop", Path.Combine(TestPaths.Temp, "cf-meaning-forms"), sources);
        var modules = new Dictionary<string, string[]>(_namespaceModules) { ["domain"] = new[] { "namespace:Shop.Domain" } };
        var settings = MeaningSettings.Layers(modules, "infrastructure -> domain", "factory -> infrastructure", "factory -> domain", "reports -> domain", "reports -> factory");

        var result = new MeaningHarness().Layers(settings, MeaningCompiler.Answers(analysis), true);

        // one finding for each member that depends on the forbidden layer without ever writing its name, and none for the allowed one
        Assert.All(result.Findings, finding => Assert.Equal("ARCH001", finding.RuleId));
        Assert.All(result.Findings, finding => Assert.Equal("Reports/Forms.cs", finding.File));
        Assert.Equal(MarkedLines(_forms, "// L"), LinesOf(result.Findings));
        Assert.Empty(result.Skipped);
    }

    [Fact]
    public void LinkedDeclarationsResolveInBothReferencingAssemblies()
    {
        var first = Units("Shop.A", "Shop.CA", "src/Shop.CA/Consumer.cs");
        var second = Units("Shop.B", "Shop.CB", "src/Shop.CB/Consumer.cs");

        var answers = MeaningAnswerBuilder.Build(new[] { first, second }, new List<string>(), new List<string> { "Shop.A", "Shop.B", "Shop.CA", "Shop.CB" });

        // the same file is compiled into two assemblies: a use of the type in either of them is placed in that file
        Assert.Equal(2, answers.Declarations.Count);
        Assert.Equal(2, answers.Uses.Count);
        Assert.All(answers.Uses, use => Assert.Equal("src/Shared/Linked.cs", use.UsedFile));
    }

    private static UnitFacts Units(string assembly, string consumerAssembly, string consumerFile)
    {
        var declaration = new MeaningDeclaration("src/Shared/Linked.cs", new SourcePoint(3, 1), "class", "Shared.Linked", assembly, "public", "public");
        var use = new MeaningUse(consumerFile, "Consumer.Run", "Shop.Consumer", consumerAssembly, "Shared.Linked.Value", "Shared.Linked", "Shared", assembly, string.Empty, new List<SourcePoint> { new SourcePoint(7, 16) });

        return new UnitFacts(consumerAssembly + "|net11.0", new List<string>(), string.Empty, new List<MeaningDeclaration> { declaration }, new List<MeaningUse> { use });
    }

    [Fact]
    public void LayerReviewOnlyJudgesDependenciesItCanPlace()
    {
        var byFolder = MeaningSettings.Layers(
            new Dictionary<string, string[]> { ["consumer"] = new[] { "project:Shop.Consumer" }, ["forbidden"] = new[] { "folder:src/Shop.Shared/Forbidden/**" }, ["allowed"] = new[] { "folder:src/Shop.Shared/Allowed/**" } },
            "consumer -> allowed");
        var byNamespace = MeaningSettings.Layers(
            new Dictionary<string, string[]> { ["consumer"] = new[] { "project:Shop.Consumer" }, ["forbidden"] = new[] { "namespace:Shop.Shared.Forbidden" }, ["allowed"] = new[] { "namespace:Shop.Shared.Allowed" } },
            "consumer -> allowed");

        // the file of the type is unknown (its project was not compiled): the folder cannot say where the type is
        var unplaced = new MeaningHarness().Layers(byFolder, Answers(Use(string.Empty)), true);

        Assert.Equal(ExitCode.Incomplete, unplaced.ExitCode);
        Assert.Empty(unplaced.Findings);
        var reason = Assert.Single(unplaced.Skipped);
        Assert.Contains("ARCH001", reason, StringComparison.Ordinal);
        Assert.Contains("src/Shop.Consumer/Consumer.cs", reason, StringComparison.Ordinal);
        Assert.Contains("Shop.Consumer.Consumer.Go", reason, StringComparison.Ordinal);
        Assert.Contains("Shop.Shared.Forbidden.Secret", reason, StringComparison.Ordinal);
        Assert.Contains("Shop.Shared", reason, StringComparison.Ordinal);

        // the namespace alone places the same use, and it is a breach
        var byName = new MeaningHarness().Layers(byNamespace, Answers(Use(string.Empty)), true);

        Assert.Equal(ExitCode.Blocked, byName.ExitCode);
        Assert.Equal("src/Shop.Consumer/Consumer.cs", Assert.Single(byName.Findings).File);
        Assert.Empty(byName.Skipped);

        // with the file of the type known, the folder places it too
        var placed = new MeaningHarness().Layers(byFolder, Answers(Use("src/Shop.Shared/Forbidden/Secret.cs")), true);

        Assert.Equal(ExitCode.Blocked, placed.ExitCode);
        Assert.Equal(9, Assert.Single(placed.Findings).Line);
        Assert.Empty(placed.Skipped);
    }

    [Fact]
    public void FirstPushTreatsEveryViolationAsNew()
    {
        var settings = MeaningSettings.Layers(
            new Dictionary<string, string[]> { ["consumer"] = new[] { "project:Shop.Consumer" }, ["forbidden"] = new[] { "namespace:Shop.Shared.Forbidden" } },
            "forbidden -> consumer");
        var harness = new MeaningHarness();

        var first = harness.Layers(settings, Answers(Use("src/Shop.Shared/Forbidden/Secret.cs")), true);

        Assert.Equal(ExitCode.Blocked, first.ExitCode);
        Assert.Equal(9, Assert.Single(first.Findings).Line);
        Assert.All(first.Coverage, entry => Assert.Equal(CoverageStatus.Reviewed, entry.Status));
        Assert.Empty(first.Skipped);
        Assert.Empty(harness.Before.Requests);
        Assert.Empty(harness.Snapshots.Moves);

        // the same use in a push whose tree before has it already is old, and that tree is built
        var later = new MeaningHarness();
        later.Before.Before = Answers(Use("src/Shop.Shared/Forbidden/Secret.cs"));
        var second = later.Layers(settings, Answers(Use("src/Shop.Shared/Forbidden/Secret.cs")));

        Assert.Empty(second.Findings);
        Assert.Single(later.Before.Requests);
    }
}
