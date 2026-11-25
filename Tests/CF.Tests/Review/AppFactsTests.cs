using CF.Core.Services;
using CF.Tests.Review.Support;
using Xunit;

namespace CF.Tests.Review;

/// <summary>What Settings shows that the body provides: the languages of this build with their files in the last commit, and the notes of the running version.</summary>
public sealed class AppFactsTests
{
    [Fact]
    public void LanguagesCountTheirFilesInTheLastCommitAndTheNotesAreThoseOfTheRunningVersion()
    {
        // the last commit holds three C# files among others; a language is told by its name, its endings and what its files number
        var folder = Path.Combine(TestPaths.Temp, "invoicing");
        var git = new FakeGit(folder);
        git.AddCommit("last", new Dictionary<string, string> { ["a.cs"] = "", ["src/b.CS"] = "", ["src/c.cs"] = "", ["notes.md"] = "" });
        git.Name("HEAD", "last");
        var service = new LanguageService(git, [new TestLanguage()]);

        var language = Assert.Single(service.Show(folder));
        Assert.Equal("C#", language.Name);
        Assert.Equal([".cs"], language.FileEndings);
        Assert.Equal(3, language.FileCount);

        // a folder that is no repository, and a repository with no commit, have no count to give
        Assert.Null(Assert.Single(new LanguageService(git, [new TestLanguage()]).Show(Path.Combine(TestPaths.Temp, "elsewhere"))).FileCount);
        Assert.Null(Assert.Single(new LanguageService(new FakeGit(folder), [new TestLanguage()]).Show(folder)).FileCount);

        // the section of a changelog for a version, with the date of its heading; none for a version it does not name
        const string changelog = "# Changelog\n\n## 1.2.3.0 — 2026-11-24\n\n### Changes\n\n- **One.**\n\n## 1.2.2.0 — 2026-11-20\n\n- Old.\n";
        var notes = VersionNotes.Find(changelog, "1.2.3.0");
        Assert.Equal("### Changes\n\n- **One.**", notes?.Markdown);
        Assert.Equal(new DateOnly(2026, 11, 24), notes?.Released);
        Assert.Null(VersionNotes.Find(changelog, "1.2.4.0"));

        // the program carries its own changelog: the notes of the version it runs are found, with their heading's date
        var running = new ProductInfoService().Notes;
        Assert.NotNull(running);
        Assert.NotNull(running.Released);
        Assert.False(string.IsNullOrWhiteSpace(running.Markdown));
    }
}
