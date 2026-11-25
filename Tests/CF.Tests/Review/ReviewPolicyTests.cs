using CF.Core.Changes;
using CF.Core.Code;
using CF.Core.Findings;
using CF.Core.Review;
using CF.Languages.CSharp.Reader;
using CF.Tests.Review.Support;
using CF.Tests.Rules.Support;
using Xunit;

namespace CF.Tests.Review;

/// <summary>What exemptions and strictness do after the review has found and placed a breach.</summary>
public sealed class ReviewPolicyTests
{
    private const string _path = "Core.cs";
    private const string _allow = "// commitfence:allow TEAM010: generated code";
    private const string _bad = "if(x > 0)\n    {\n        return 1;\n    }";

    [Theory]
    [InlineData(_allow, _allow, _bad, true, false)]
    [InlineData(_allow, "// commitfence:allow TEAM010:", _bad, false, true)]
    [InlineData(_allow, "// commitfence:allow TEAM010 no colon", _bad, false, true)]
    [InlineData("// commitfence:allow TEAM010", "// commitfence:allow TEAM010", _bad, false, false)]
    [InlineData("", "", "if(x > 0) { return \"commitfence:allow TEAM010: generated code\".Length; }", false, false)]
    [InlineData("", "", "if(x > 0) { return 1; } " + _allow, true, false)]
    [InlineData("", "x++; " + _allow, _bad, false, false)]
    public void RangeUsesActualCommentsAndRequiresAReasonWhereTouched(string beforeLine, string afterLine, string badIf, bool exempted, bool missingReason)
    {
        var settings = ReviewSources.Settings(ReviewSources.Team010);
        var before = WithLine(beforeLine, "if(x > 0)\n        return 1;");
        var after = WithLine(afterLine, badIf);
        using var harness = new ReviewHarness();
        AddChanges(harness, settings, before, after);

        var result = harness.Service().ReviewChanges(harness.RepositoryFolder, ReviewScope.ForRange("old", "new"), null);
        var command = harness.Run("review", "--base", "old", "--head", "new");

        Assert.Equal(exempted ? ExitCode.Passed : ExitCode.Blocked, command.ExitCode);
        Assert.Equal(command.ExitCode, result.ExitCode);
        Assert.Equal(!exempted, result.Findings.Any(finding => finding.RuleId == "TEAM010"));
        Assert.Equal(missingReason, result.Findings.Any(finding => finding.RuleId == "ALLOW001"));
        if(missingReason)
        {
            var invalid = Assert.Single(result.Findings, finding => finding.RuleId == "ALLOW001");
            Assert.Equal(ReviewSources.LineOf(after, "commitfence:allow"), invalid.Line);
            Assert.Equal("An exemption needs a reason", invalid.Message);
        }

        if(!exempted)
        {
            Assert.Empty(result.ExemptedFindings);
            return;
        }

        var entry = Assert.Single(result.ExemptedFindings);
        Assert.Equal("TEAM010", entry.Finding.RuleId);
        Assert.Equal("generated code", entry.Reason);
        Assert.Equal(ReviewSources.LineOf(after, "commitfence:allow"), entry.CommentLine);
        Assert.Contains("1 finding set aside by an allow comment.", command.Output, StringComparison.Ordinal);
        using var fixture = new RulesFixture(settings);
        fixture.Write(_path, after);
        Assert.Equal(ExitCode.Passed, fixture.Commands.Run("review", "--files", _path).ExitCode);
    }

    [Theory]
    [InlineData("strict", "warning", false, false, ExitCode.Blocked, Severity.Block)]
    [InlineData("quiet", "suggestion", false, false, ExitCode.Passed, null)]
    [InlineData("quiet", "suggestion", true, false, ExitCode.Blocked, null)]
    [InlineData("strict", "warning", false, true, ExitCode.Blocked, Severity.Block)]
    [InlineData("strict", null, false, false, ExitCode.Passed, Severity.Warn)]
    [InlineData("quiet", "error", false, false, ExitCode.Blocked, Severity.Block)]
    public void RangeMapsOnlyFileRulesAndKeepsCompileErrors(string preset, string? word, bool teamRule, bool compileError, ExitCode code, Severity? fileEffect)
    {
        var settings = $"{{ \"strictness\": \"{preset}\", \"rules\": [" + (teamRule ? ReviewSources.Team010 : "") + "] }";
        var after = WithLine(compileError ? "// commitfence:allow CS1002: still a compile error" : "", _bad.Replace("x > 0", "x != 0", StringComparison.Ordinal));
        var line = ReviewSources.LineOf(after, "if(x != 0)");
        var reach = ArtifactReach.OfSpan(new SourceSpan(line, 1, line, 9));
        var findings = new List<Finding>
        {
            new Finding("CA1000", FindingSource.EditorConfig, word == "error" ? Severity.Block : Severity.Warn, "csharp", "Demo", "net11.0", _path, line, 1, "File diagnostic", null, reach) { FileSeverity = word }
        };
        if(compileError)
            findings.Add(new Finding("CS1002", FindingSource.Compiler, Severity.Block, "csharp", "Demo", "net11.0", _path, line, 1, "Compile error", null, null));

        var toolchain = new FakeToolchain { Answer = _ => new ToolchainResult(findings, [], [], FakeToolchain.CompleteAnswers(), []) };
        using var harness = new ReviewHarness { Languages = new List<ILanguage> { new TestLanguage(toolchain) } };
        AddChanges(harness, settings, ReviewSources.Class("Core", ReviewSources.SignOk), after);

        var result = harness.Service().ReviewChanges(harness.RepositoryFolder, ReviewScope.ForRange("old", "new"), null);
        Assert.Equal(code, result.ExitCode);
        Assert.Equal(fileEffect, result.Findings.SingleOrDefault(finding => finding.RuleId == "CA1000")?.Severity);
        Assert.Equal(teamRule ? Severity.Block : (Severity?)null, result.Findings.SingleOrDefault(finding => finding.RuleId == "TEAM010")?.Severity);
        Assert.Equal(compileError, result.Findings.Any(finding => finding.IsCompileError));
    }

    [Theory]
    [InlineData("\n// commitfence:allow TEAM010: generated", 2, "TEAM010", ExemptionForm.AllowComment, "generated", 1)]
    [InlineData("/* commitfence:allow TEAM010: generated */\n", 1, "TEAM010", ExemptionForm.AllowComment, "generated", 1)]
    [InlineData("// commitfence:allow TEAM010", 1, "TEAM010", ExemptionForm.AllowComment, null, 1)]
    [InlineData("\n#pragma warning disable CA1000, CA1001 // generated", 2, "CA1001", ExemptionForm.Pragma, "generated", 2)]
    [InlineData("#pragma warning disable CS0168", 1, "CS0168", ExemptionForm.Pragma, null, 1)]
    [InlineData("#pragma warning disable // generated", 1, null, ExemptionForm.Pragma, "generated", 1)]
    [InlineData("#pragma warning disable", 1, null, ExemptionForm.Pragma, null, 1)]
    [InlineData("/*/", 0, null, ExemptionForm.AllowComment, null, 0)]
    [InlineData("[System.Diagnostics.CodeAnalysis.SuppressMessage(\"Style\", \"IDE0001:Title\", Justification = @\"Generated\")]\nclass Core { }", 1, "IDE0001", ExemptionForm.SuppressMessage, "Generated", 1)]
    [InlineData("\n[SuppressMessage(\"Style\", \"IDE0001\")]\nclass Core { }", 2, "IDE0001", ExemptionForm.SuppressMessage, null, 1)]
    public void CSharpListsEachExemptionWithItsFormAndReason(string source, int line, string? id, ExemptionForm form, string? reason, int count)
    {
        var exemptions = CSharpExemptions.Read(source);

        Assert.Equal(count, exemptions.Count);
        if(count == 0)
            return;

        var exemption = Assert.Single(exemptions, item => item.RuleId == id);
        Assert.Equal((line, form, reason), (exemption.Line, exemption.Form, exemption.Reason));
        Assert.Equal(id ?? "all warnings", exemption.Target);
    }

    private static string WithLine(string additionalLine, string statement)
    {
        return ReviewSources.Class("Core", "public int Sign(int x)\n{\n    " + additionalLine + "\n    " + statement + "\n\n    return 0;\n}");
    }

    private static void AddChanges(ReviewHarness harness, string settings, string before, string after)
    {
        harness.Fake.AddCommit("old", new Dictionary<string, string> { ["commitfence.json"] = settings, [_path] = before });
        harness.Fake.AddCommit("new", new Dictionary<string, string> { ["commitfence.json"] = settings, [_path] = after }, "old");
    }
}
