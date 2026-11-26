using System.Text;
using CF.Adapters.Git;
using CF.Adapters.Machine;
using CF.Adapters.Storage;
using CF.Core.Findings;
using CF.Core.Fixes;
using CF.Core.Ports;
using CF.Core.Services;
using CF.Languages.CSharp;
using CF.Tests.Review.Support;
using Xunit;

namespace CF.Tests.Review;

public sealed class FixServiceTests
{
    private const string _source = "src/Sample.cs";
    private const string _sibling = "src/Sibling.cs";
    private const string _project = "src/Sample.csproj";

    [Fact]
    public void ApplyRefusesOneChangedByteAndWritesExactBytesOnAMatch()
    {
        using var sandbox = new GitSandbox();
        var repository = sandbox.CreateRepository("write");
        var (_, service, location) = Services(sandbox, repository);
        var file = Path.Combine(repository.Folder, "Sample.cs");
        var before = WithMark("class Sample { }\r\n");
        var after = WithMark("class Sample\r\n{\r\n}\r\n");
        var fix = new Fix(location, "Sample.cs", "IDE0055", before, after, "patch");
        var changed = before.ToArray();
        changed[^1] = (byte)' ';

        File.WriteAllBytes(file, changed);
        Assert.Equal("The file changed after the review. Review again to fix this one.", service.ApplyFix(fix).Reason);
        Assert.Equal(changed, File.ReadAllBytes(file));

        File.WriteAllBytes(file, before);
        Assert.True(service.ApplyFix(fix).Succeeded);
        Assert.Equal(after, File.ReadAllBytes(file));
        Assert.Single(Directory.EnumerateFiles(repository.Folder));
    }

    [Fact]
    public void ALineEndingOnlyFixIsDescribedAndWritesTheExactBytes()
    {
        using var sandbox = new GitSandbox();
        var repository = sandbox.CreateRepository("line-endings");
        repository.Write(".gitattributes", "*.cs text eol=crlf\n");
        var before = "class Sample { }\r\n"u8.ToArray();
        var file = Path.Combine(repository.Folder, _source);
        repository.Write(_source, "class Sample { }\r\n");
        var commit = repository.Commit("add source with CRLF checkout");
        var (store, service, location) = Services(sandbox, repository, new FormatterRunner("line endings", sandbox.Runner));
        var tree = repository.Git("rev-parse", commit + "^{tree}");

        var fix = service.SuggestFix(new FixRequest(location, tree, Finding("IDE0055"))).Fix;

        Assert.NotNull(fix);
        Assert.Empty(fix.Patch);
        Assert.Equal("The fix changes only the line endings of this file (CRLF to LF).", fix.ByteChanges);
        Assert.Equal(before, fix.Before.ToArray());
        Assert.Equal(before, File.ReadAllBytes(Path.Combine(store.MoveTo(location, tree), _source)));
        Assert.True(service.ApplyFix(fix).Succeeded);
        Assert.Equal("class Sample { }\n"u8.ToArray(), fix.After.ToArray());
        Assert.Equal(fix.After.ToArray(), File.ReadAllBytes(file));
    }

    [Fact]
    public void LinesTheFormatterAddsKeepTheFileLineEnding()
    {
        using var sandbox = new GitSandbox();
        var repository = sandbox.CreateRepository("added-lines");
        repository.Write(_source, "class Sample { }\n");
        var commit = repository.Commit("add source with LF");
        var (_, service, location) = Services(sandbox, repository, new FormatterRunner("added lines", sandbox.Runner));
        var tree = repository.Git("rev-parse", commit + "^{tree}");

        var fix = service.SuggestFix(new FixRequest(location, tree, Finding("IDE0011"))).Fix;

        Assert.NotNull(fix);
        Assert.Equal("class Sample\n{\n}\n"u8.ToArray(), fix.After.ToArray());
        Assert.Empty(fix.ByteChanges);
    }

    [Theory]
    [InlineData("success")]
    [InlineData("error")]
    [InlineData("timeout")]
    [InlineData("second file")]
    [InlineData("new file")]
    public void SuggestionRestoresTheReviewedTree(string outcome)
    {
        using var sandbox = new GitSandbox();
        var repository = sandbox.CreateRepository("preview");
        repository.Write(_source, "reviewed source\n");
        repository.Write(_sibling, "reviewed sibling\n");
        var commit = repository.Commit("original source");
        var (store, service, location) = Services(sandbox, repository, new FormatterRunner(outcome, sandbox.Runner));
        var tree = repository.Git("rev-parse", commit + "^{tree}");
        var folder = store.MoveTo(location, tree);

        var answer = service.SuggestFix(new FixRequest(location, tree, Finding("IDE0011")));

        Assert.Equal(outcome == "success", answer.Fix != null);
        Assert.Equal(outcome != "success", answer.Reason != null);
        Assert.Equal("reviewed source\n", File.ReadAllText(Path.Combine(folder, _source)));
        Assert.Equal("reviewed sibling\n", File.ReadAllText(Path.Combine(folder, _sibling)));
        Assert.Empty(store.ReadChangedFiles(location));
        Assert.Empty(store.ReadUntrackedFiles(location));
    }

    [Fact]
    public void SdkFixesBothSuggestionsInOneFileAndLeavesTheSiblingAlone()
    {
        using var sandbox = new GitSandbox();
        var repository = sandbox.CreateRepository("sdk");
        repository.Write(_project, ReviewSources.ProjectFile);
        repository.Write(".editorconfig", "root = true\n[*.cs]\ncharset = utf-8-bom\nend_of_line = crlf\ncsharp_prefer_braces = true:suggestion\ndotnet_diagnostic.IDE0011.severity = suggestion\n");
        var source = """
            namespace Demo;
            public static class Sample
            {
                public static int Count(int value)
                {
                    if (value < 0)
                        value++;
                    if (value > 0)
                        value--;
                    return value;
                }
            }
            """.ReplaceLineEndings("\r\n");
        File.WriteAllBytes(Path.Combine(repository.Folder, _source), WithMark(source));
        repository.Write(_sibling, source.Replace("class Sample", "class Sibling", StringComparison.Ordinal));
        var commit = repository.Commit("add two brace suggestions");
        var (store, service, location) = Services(sandbox, repository);
        var tree = repository.Git("rev-parse", commit + "^{tree}");
        var folder = store.MoveTo(location, tree);
        var packages = Directory.CreateDirectory(Path.Combine(sandbox.Root, "empty-feed")).FullName;
        var restore = sandbox.Run("dotnet", folder, ["restore", _project, "--source", packages, "--disable-build-servers"]);
        Assert.True(restore.ExitCode == 0, restore.Output + restore.Error);
        var build = sandbox.Run("dotnet", folder, ["build", _project, "--no-restore", "--disable-build-servers"]);
        Assert.True(build.ExitCode == 0, build.Output + build.Error);

        var answer = service.SuggestFix(new FixRequest(location, tree, Finding("IDE0011")));

        Assert.True(answer.Fix != null, answer.Reason);
        Assert.Equal(2, answer.Fix!.Patch.Split('\n').Count(line => line.TrimEnd('\r') == "+        {"));
        Assert.DoesNotContain("Sibling.cs", answer.Fix.Patch, StringComparison.Ordinal);
        Assert.Equal(WithMark(source), answer.Fix.Before.ToArray());
        var fixedBytes = answer.Fix.After.ToArray();
        Assert.True(fixedBytes.AsSpan().StartsWith(Encoding.UTF8.Preamble));
        Assert.DoesNotContain('\n', Encoding.UTF8.GetString(fixedBytes).Replace("\r\n", string.Empty, StringComparison.Ordinal));
        Assert.Equal(source.Replace("class Sample", "class Sibling", StringComparison.Ordinal), File.ReadAllText(Path.Combine(folder, _sibling)));
        Assert.Equal(new FixAnswer(), service.SuggestFix(new FixRequest(location, tree, Finding("IDE1006"))));
    }

    private static Finding Finding(string id)
    {
        return new Finding(id, FindingSource.EditorConfig, Severity.Warn, "csharp", "Sample", "net11.0", _source, 6, 9, "Use braces", null, null)
        {
            ProjectFile = _project,
            BuildFramework = "net11.0"
        };
    }

    private static byte[] WithMark(string text)
    {
        return [.. Encoding.UTF8.Preamble, .. Encoding.UTF8.GetBytes(text)];
    }

    private static (GitSnapshotStore Store, FixService Service, RepositoryLocation Location) Services(GitSandbox sandbox, SandboxRepository repository, IProcessRunner? formatter = null)
    {
        var data = new FolderOfTest(sandbox.DataFolder);
        var files = new SystemFileSystem();
        var store = new GitSnapshotStore(sandbox.Runner, data);
        var service = new FixService([new CSharpLanguage(formatter ?? sandbox.Runner, files, data)], store, new FileReviewLock(data), files);
        var location = new GitCommandLine(sandbox.Runner, data).FindRepository(repository.Folder)!;
        return (store, service, location);
    }

    private sealed class FormatterRunner(string outcome, IProcessRunner git) : IProcessRunner
    {
        public ProcessResult Run(ProcessRequest request)
        {
            if(request.FileName != "dotnet")
                return git.Run(request);

            var folder = request.WorkingFolder!;
            var text = outcome switch
            {
                "line endings" => "class Sample { }\n",
                "added lines" => "class Sample\n{\r\n}\n",
                _ => "formatted source\n"
            };
            File.WriteAllText(Path.Combine(folder, _source), text);
            if(outcome == "second file")
                File.WriteAllText(Path.Combine(folder, _sibling), "formatted sibling\n");

            if(outcome == "new file")
                File.WriteAllText(Path.Combine(folder, "extra.txt"), "created during format\n");

            return outcome switch
            {
                "error" => new ProcessResult(1, string.Empty, "format failed"),
                "timeout" => new ProcessResult(-2, string.Empty, "format timed out", timedOut: true),
                _ => new ProcessResult(0, string.Empty, string.Empty)
            };
        }
    }
}
