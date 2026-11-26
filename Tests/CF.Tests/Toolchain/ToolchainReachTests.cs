using CF.Core.Changes;
using CF.Core.Findings;
using CF.Core.Review;
using CF.Tests.Toolchain.Support;
using Xunit;

namespace CF.Tests.Toolchain;

/// <summary>
/// The changed files and the project sketches go through a real review and the installed SDK.
/// </summary>
public sealed class ToolchainReachTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void UnrelatedProjectIsSkippedOnlyIfItsXmlCanBeRead(bool malformed)
    {
        using(var folder = new TestFolder())
        {
            var tree = Path.Combine(folder.Path, "tree");
            WriteProject(tree, "Good/Good.csproj");
            TreeFiles.Write(tree, "Good/Good.cs", TreeFiles.SourceText);
            if(malformed)
                TreeFiles.Write(tree, "Broken/Broken.csproj", "<Project>");
            else
                WriteProject(tree, "Broken/Broken.csproj", """<Import Project="missing/does-not-exist.props" />""");

            var review = Review(folder.Path, tree, ReviewHarness.Changed("Good/Good.cs"));

            AssertReviewed(review, "Good");
            if(malformed)
            {
                Assert.Equal(ExitCode.Incomplete, review.ExitCode);
                Assert.Contains(review.Coverage, entry => entry.What == "Broken" && entry.Status == CoverageStatus.Failed);
                Assert.DoesNotContain("were not loaded", review.Summary, StringComparison.Ordinal);
            }
            else
            {
                Assert.Equal(ExitCode.Passed, review.ExitCode);
                Assert.DoesNotContain(review.Coverage, entry => entry.What == "Broken");
                Assert.Contains("1 projects the change cannot reach were not loaded.", review.Summary, StringComparison.Ordinal);
            }
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RedirectedDirectoryBuildTargetsRetainTheOwner(bool imported)
    {
        using(var folder = new TestFolder())
        {
            var tree = Path.Combine(folder.Path, "tree");
            const string redirect = "<PropertyGroup><_DirectoryBuildTargetsFile>Repo.Build.targets</_DirectoryBuildTargetsFile></PropertyGroup>";
            if(imported)
                TreeFiles.Write(tree, "build/redirect.props", $"<Project>{redirect}</Project>");

            WriteProject(tree, "App/App.csproj", imported ? """<Import Project="../build/redirect.props" />""" : redirect);
            TreeFiles.Write(tree, "Repo.Build.targets", """<Project><ItemGroup><Compile Include="../Shared/Changed.cs" /></ItemGroup></Project>""");
            TreeFiles.Write(tree, "Shared/Changed.cs", TreeFiles.SourceText);

            var review = Review(folder.Path, tree, ReviewHarness.Changed("Shared/Changed.cs"));

            Assert.Equal(ExitCode.Passed, review.ExitCode);
            AssertReviewed(review, "App");
        }
    }

    [Fact]
    public void SiblingProjectReferencesKeepEvaluationWithinTheChangedProduct()
    {
        var projects = new List<FakeProject>();
        var changedProduct = new List<string>();
        for(var index = 1; index <= 4; index++)
        {
            var product = $"Product{index}";
            var core = $"{product}/P{index}Core/P{index}Core";
            projects.Add(new FakeProject(core + ".csproj", new FakeUnit("net11.0", new List<string> { core + ".cs" }, new List<string>())));
            var libraries = new List<string>();
            for(var number = 1; number <= 8; number++)
            {
                var library = $"{product}/P{index}Lib{number}/P{index}Lib{number}";
                libraries.Add(library + ".csproj");
                projects.Add(new FakeProject(library + ".csproj", new FakeUnit("net11.0", new List<string> { library + ".cs" }, new List<string> { core + ".csproj" })));
            }

            var app = $"{product}/P{index}App/P{index}App";
            projects.Add(new FakeProject(app + ".csproj", new FakeUnit("net11.0", new List<string> { app + ".cs" }, libraries)));
            if(index == 1)
                changedProduct.AddRange(projects.Select(project => project.Name));
        }

        using(var fixture = new FakeBuildFixture(TreeFiles.NoRules, projects.ToArray()))
        {
            var review = fixture.Review(ReviewHarness.Changed("Product1/P1Lib3/P1Lib3.cs"));

            var evaluated = fixture.Dotnet.Requests
                .Where(request => request.Arguments.Contains("-getProperty:TargetFrameworks", StringComparer.Ordinal))
                .Select(request => Path.GetFileNameWithoutExtension(request.Arguments[1]))
                .Order(StringComparer.Ordinal);
            Assert.Equal(changedProduct.Order(StringComparer.Ordinal), evaluated);
            Assert.Contains("30 projects the change cannot reach were not loaded.", review.Summary, StringComparison.Ordinal);
            Assert.Equal(ExitCode.Passed, review.ExitCode);
            AssertReviewed(review, "P1Lib3");
            AssertReviewed(review, "P1App");
        }
    }

    [Theory]
    [InlineData("vbproj")]
    [InlineData("fsproj")]
    [InlineData("proj")]
    public void NonCSharpProjectReferenceKeepsItsDependentInTheReview(string extension)
    {
        var bridge = "Bridge/Bridge." + extension;
        var app = new FakeProject("App/App.csproj", new FakeUnit("net11.0", ["App/Use.cs"], [bridge]));
        var core = new FakeProject("Core/Core.csproj", new FakeUnit("net11.0", ["Core/Api.cs"], []));
        using var fixture = new FakeBuildFixture(TreeFiles.NoRules, app, core);
        TreeFiles.Write(fixture.Tree, bridge, """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup><TargetFramework>net11.0</TargetFramework></PropertyGroup>
              <ItemGroup><ProjectReference Include="../Core/Core.csproj" /></ItemGroup>
            </Project>
            """);

        fixture.Review(ReviewHarness.Changed("Core/Api.cs"));

        Assert.Contains(fixture.Dotnet.Requests, request => request.Arguments.Contains("-getProperty:TargetFrameworks", StringComparer.Ordinal)
            && Path.GetFileName(request.Arguments[1]) == "App.csproj");
    }

    [Fact]
    public void ImportWildcardRetainsBothOwnersOfTheChangedSource()
    {
        using(var folder = new TestFolder())
        {
            var tree = Path.Combine(folder.Path, "tree");
            WriteProject(tree, "First/First.csproj", """<Import Project="../build/common.props" />""");
            WriteProject(tree, "Second/Second.csproj", """<Import Project="../build/common.props" />""");
            TreeFiles.Write(tree, "build/common.props", """<Project><Import Project="items/shared.props" /></Project>""");
            TreeFiles.Write(tree, "build/items/shared.props", """<Project><ItemGroup><Compile Include="../shared/**/*.cs" /></ItemGroup></Project>""");
            TreeFiles.Write(tree, "shared/sub/Common.cs", TreeFiles.SourceText);

            var review = Review(folder.Path, tree, ReviewHarness.Changed("shared/sub/Common.cs"));

            AssertReviewed(review, "First");
            AssertReviewed(review, "Second");
        }
    }

    [Fact]
    public void PropertyInReferenceRetainsTheDependent()
    {
        using(var folder = new TestFolder())
        {
            var tree = Path.Combine(folder.Path, "tree");
            WriteProject(tree, "Library/Library.csproj");
            TreeFiles.Write(tree, "Library/Source.cs", TreeFiles.SourceText);
            WriteProject(tree, "Consumer/Consumer.csproj", """
                <PropertyGroup><LibraryProject>../Library/Library.csproj</LibraryProject></PropertyGroup>
                <ItemGroup><ProjectReference Include="$(LibraryProject)" /></ItemGroup>
                """);
            TreeFiles.Write(tree, "Consumer/Use.cs", "namespace Consumer;\n\npublic class Use : N.C\n{\n}\n");

            var review = Review(folder.Path, tree, ReviewHarness.Changed("Library/Source.cs"));

            AssertReviewed(review, "Library");
            AssertReviewed(review, "Consumer");
        }
    }

    [Fact]
    public void DirectoryBuildPropsChangeRetainsTheProjectsUnderIt()
    {
        using(var folder = new TestFolder())
        {
            var tree = Path.Combine(folder.Path, "tree");
            WriteProject(tree, "Product/First/First.csproj");
            WriteProject(tree, "Product/Second/Second.csproj");
            TreeFiles.Write(tree, "Product/First/Source.cs", TreeFiles.SourceText);
            TreeFiles.Write(tree, "Product/Second/Source.cs", TreeFiles.SourceText);
            TreeFiles.Write(tree, "Product/Directory.Build.props", """
                <Project><PropertyGroup><DefineConstants>IN_PRODUCT</DefineConstants></PropertyGroup></Project>
                """);

            var review = Review(folder.Path, tree, ReviewHarness.Changed("Product/Directory.Build.props"));

            AssertReviewed(review, "First");
            AssertReviewed(review, "Second");
        }
    }

    [Fact]
    public void LinkOutsideTheProjectRetainsItsOwner()
    {
        using(var folder = new TestFolder())
        {
            var tree = Path.Combine(folder.Path, "tree");
            WriteProject(tree, "Library/Library.csproj", """
                <ItemGroup><Compile Include="Local.cs;../Shared/Linked.cs" Link="Linked.cs" /></ItemGroup>
                <PropertyGroup><EnableDefaultCompileItems>false</EnableDefaultCompileItems></PropertyGroup>
                """);
            TreeFiles.Write(tree, "Library/Local.cs", "namespace N;\n\npublic class Local\n{\n}\n");
            TreeFiles.Write(tree, "Shared/Linked.cs", TreeFiles.SourceText);

            var review = Review(folder.Path, tree, ReviewHarness.Changed("Shared/Linked.cs"));

            AssertReviewed(review, "Library");
        }
    }

    private static void WriteProject(string tree, string path, string contents = "")
    {
        TreeFiles.Write(tree, path, $"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup><TargetFramework>net11.0</TargetFramework></PropertyGroup>
              {contents}
            </Project>
            """);
    }

    private static ReviewResult Review(string root, string tree, ChangedFile change)
    {
        TreeFiles.Write(tree, "commitfence.json", TreeFiles.NoRules);
        var runner = new IsolatedProcessRunner(Path.Combine(root, "home"), Path.Combine(root, "packages"));
        var harness = ReviewHarness.ForCSharp(runner, Path.Combine(root, "data"), new FolderSnapshots(tree), new EmptyTreeGit());
        return harness.Review(ChangeSet.ForChanges("the change", "tree-before", "tree-after", new List<ChangedFile> { change }, new List<string>(), string.Empty));
    }

    private static void AssertReviewed(ReviewResult review, string project)
    {
        var entry = Assert.Single(review.Coverage, candidate => candidate.What == project + " (net11.0)");
        Assert.Equal(CoverageStatus.Reviewed, entry.Status);
    }
}
