using CF.Tests.Meaning.Support;
using Xunit;

namespace CF.Tests.Meaning;

/// <summary>
/// The rules that need to know what the code refers to, judged over code compiled in memory with the analyzer of CommitFence.
/// </summary>
public sealed class MeaningRuleTests
{
    private const string _clock = """
        using System;
        using static System.DateTime;

        namespace Shop.Domain;

        public class Clock
        {
            public System.DateTime Direct()
            {
                return System.DateTime.Now; // hit
            }

            public DateTime Plain()
            {
                return DateTime.Now; // hit
            }

            public DateTime Static()
            {
                return Now; // hit
            }

            public int Chained()
            {
                return System.DateTime.Now.Year; // hit
            }
        }

        """;

    private const string _lookalike = """
        namespace Shop.Own;

        public class DateTime
        {
            public static int Now
            {
                get { return 1; }
            }
        }

        public class User
        {
            public int Use()
            {
                return DateTime.Now; // miss
            }
        }

        """;

    private const string _fileUse = """
        using System.IO;

        namespace Shop.Placed;

        public class Reader
        {
            public string Read(string path)
            {
                return File.ReadAllText(path); // hit
            }
        }

        """;

    private const string _partialPublicA = """
        namespace Shop.Domain;

        public partial class Wide
        {
        }

        """;

    private const string _partialPublicB = """
        namespace Shop.Domain;

        partial class Wide
        {
            protected void Extend()
            {
            }
        }

        """;

    private const string _partialInternalA = """
        namespace Shop.Domain;

        internal partial class Hidden
        {
        }

        """;

    private const string _partialInternalB = """
        namespace Shop.Domain;

        partial class Hidden
        {
            internal void Quiet() // hit
            {
            }
        }

        """;

    private const string _keysBase = """
        namespace Shop.Domain;

        public class KeysBase
        {
            protected virtual void OnKeyDown(int key)
            {
            }

            internal virtual void Measure()
            {
            }
        }

        """;

    private const string _keys = """
        namespace Shop.Domain;

        internal class Keys : KeysBase
        {
            protected override void OnKeyDown(int key)
            {
            }

            internal override void Measure()
            {
            }

            protected void Helper() // hit
            {
            }

            public void Open()
            {
            }

            private void Close()
            {
            }
        }

        """;

    private static List<string> HitsOf(string path, string text, string marker, bool withColumn)
    {
        var lines = text.Split('\n');
        var hits = new List<string>();
        for(var index = 0; index < lines.Length; index++)
        {
            if(lines[index].EndsWith("// hit", StringComparison.Ordinal))
                hits.Add(path + ":" + (index + 1) + (withColumn ? ":" + (lines[index].IndexOf(marker, StringComparison.Ordinal) + 1) : string.Empty));
        }

        return hits;
    }

    [Fact]
    public void UsageRulesMatchResolvedMembersAndTypesWithinScope()
    {
        var sources = new Dictionary<string, string>
        {
            ["Domain/Clock.cs"] = _clock,
            ["Domain/Own.cs"] = _lookalike,
            ["Core/Reader.cs"] = _fileUse,
            ["Other/Reader.cs"] = _fileUse.Replace("Shop.Placed", "Shop.Other", StringComparison.Ordinal)
        };
        var analysis = MeaningCompiler.Analyze("Shop", Path.Combine(TestPaths.Temp, "cf-meaning-usage"), sources);

        var findings = new MeaningHarness().RuleFindings(MeaningSettings.Rules(MeaningSettings.ClockRule, MeaningSettings.FileApiRule), analysis);

        // each use of Now is one finding, at the member and not at the type before it; the type of the same name that is the team's own is not the clock
        var clock = findings.Where(finding => finding.RuleId == "TEAM012").Select(finding => $"{finding.File}:{finding.Line}:{finding.Column}").Order(StringComparer.Ordinal).ToList();
        Assert.Equal(HitsOf("Domain/Clock.cs", _clock, "Now", true).Order(StringComparer.Ordinal), clock);

        // the file API is forbidden inside the path of the rule and nowhere else
        var fileApi = findings.Where(finding => finding.RuleId == "TEAM013").Select(finding => $"{finding.File}:{finding.Line}").Distinct().ToList();
        Assert.Equal(HitsOf("Core/Reader.cs", _fileUse, "File", false), fileApi);
    }

    [Fact]
    public void MethodRuleUsesCombinedPartialAccessibilityAndRespectsOverrides()
    {
        var sources = new Dictionary<string, string>
        {
            ["Domain/PartA.cs"] = _partialPublicA,
            ["Domain/PartB.cs"] = _partialPublicB,
            ["Domain/HiddenA.cs"] = _partialInternalA,
            ["Domain/HiddenB.cs"] = _partialInternalB,
            ["Domain/KeysBase.cs"] = _keysBase,
            ["Domain/Keys.cs"] = _keys
        };
        var analysis = MeaningCompiler.Analyze("Shop", Path.Combine(TestPaths.Temp, "cf-meaning-methods"), sources);

        var findings = new MeaningHarness().RuleFindings(MeaningSettings.Rules(MeaningSettings.MethodAccessRule), analysis);

        // read from one file alone, the second part of Wide looks internal and its method protected; only the whole type is public
        var expected = HitsOf("Domain/HiddenB.cs", _partialInternalB, string.Empty, false).Concat(HitsOf("Domain/Keys.cs", _keys, string.Empty, false)).Order(StringComparer.Ordinal);
        Assert.Equal(expected, findings.Select(finding => $"{finding.File}:{finding.Line}").Order(StringComparer.Ordinal));
        Assert.All(findings, finding => Assert.Equal("TEAM011", finding.RuleId));
    }
}
