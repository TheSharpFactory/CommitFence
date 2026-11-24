using CF.Core.Ports;

namespace CF.Languages.CSharp.Toolchain;

/// <summary>
/// The two things of ours the build needs, kept in the product's data folder and never in a reviewed repository.
/// </summary>
/// <remarks>
/// They are a small MSBuild file handed to the build from outside (CustomAfterDirectoryBuildTargets), and an empty
/// folder used as the only package source, so that a restore cannot reach the network. The file is rewritten only when
/// its text differs, because a changed file makes every project compile again.
/// </remarks>
internal sealed class ToolchainTargets
{
    /// <summary>
    /// The text of the file.
    /// </summary>
    /// <remarks>
    /// It is made of the file the repository names in the same slot, the log path of each compile, the rules a second
    /// build lets through, and the meaning analyzer, each named by a property the review passes with each project. A
    /// last target runs after the compile, also when the build skipped it, and writes the manifest: what the compiler
    /// task is given, one entry to a line, so that the key of a kept answer is made of what the compiler read and not of
    /// a guess. A line is a tag, a bar and a value: a file (its full path), a property (its text), whether the compiler's
    /// log is still the one the review asked for, a mark that no key can be made (a rule set file can include files, and a
    /// response file can name files, that the build does not list), or, last, the end of the manifest.
    /// </remarks>
    internal const string TargetsText = """
        <Project>
          <!-- Handed to the build of CommitFence's private copy from outside. Nothing in the reviewed repository is edited. -->
          <!-- The file the repository itself names in this slot is imported first, under the condition the SDK itself uses, so that taking the slot does not change what is compiled. -->
          <Import Project="$(CommitFenceRepositoryTargets)" Condition="'$(CommitFenceRepositoryTargets)' != ''" />
          <PropertyGroup Condition="'$(CommitFenceLog)' != ''">
            <ErrorLog>$(CommitFenceLog),version=2.1</ErrorLog>
          </PropertyGroup>
          <!-- The rules a second build lets through are appended to the project's own NoWarn, never put in its place. -->
          <PropertyGroup Condition="'$(CommitFenceLetThrough)' != ''">
            <NoWarn>$(NoWarn);$(CommitFenceLetThrough)</NoWarn>
          </PropertyGroup>
          <ItemGroup Condition="'$(CommitFenceMeaning)' != ''">
            <Analyzer Include="$(CommitFenceMeaning)" />
          </ItemGroup>
          <!-- Runs after the compile also when the compile was skipped because its outputs are up to date. -->
          <Target Name="CommitFenceManifest" AfterTargets="CoreCompile" Condition="'$(CommitFenceManifest)' != ''">
            <ItemGroup>
              <_CommitFenceLine Include="errorlog|$([System.String]::Equals('$(ErrorLog)', '$(CommitFenceLog),version=2.1'))" />
              <_CommitFenceLine Include="@(Compile->'source|%(FullPath)')" />
              <_CommitFenceLine Include="@(ReferencePathWithRefAssemblies->'reference|%(FullPath)')" />
              <_CommitFenceLine Include="@(ReferencePathWithRefAssemblies->'prop|referenceof:%(FullPath)=aliases:%(Aliases),embed:%(EmbedInteropTypes)')" />
              <_CommitFenceLine Include="@(Analyzer->'analyzer|%(FullPath)')" />
              <_CommitFenceLine Include="@(AdditionalFiles->'additional|%(FullPath)')" />
              <_CommitFenceLine Include="@(EditorConfigFiles->'editorconfig|%(FullPath)')" />
              <_CommitFenceLine Include="@(EmbeddedFiles->'embeddedfile|%(FullPath)')" />
              <_CommitFenceLine Include="@(EmbeddedResource->'resource|%(FullPath)')" />
              <_CommitFenceLine Include="@(LinkResource->'linkresource|%(FullPath)')" />
              <_CommitFenceLine Include="@(AddModules->'module|%(FullPath)')" />
              <_CommitFenceLine Include="@(CompiledLicenseFile->'license|%(FullPath)')" />
              <_CommitFenceLine Include="@(CustomAdditionalCompileInputs->'custominput|%(FullPath)')" />
              <_CommitFenceLine Include="file|$([MSBuild]::NormalizePath('$(MSBuildProjectDirectory)', '$(ApplicationIcon)'))" Condition="'$(ApplicationIcon)' != ''" />
              <_CommitFenceLine Include="file|$([MSBuild]::NormalizePath('$(MSBuildProjectDirectory)', '$(KeyOriginatorFile)'))" Condition="'$(KeyOriginatorFile)' != ''" />
              <_CommitFenceLine Include="file|$([MSBuild]::NormalizePath('$(MSBuildProjectDirectory)', '$(Win32Resource)'))" Condition="'$(Win32Resource)' != ''" />
              <_CommitFenceLine Include="file|$([MSBuild]::NormalizePath('$(MSBuildProjectDirectory)', '$(Win32Manifest)'))" Condition="'$(Win32Manifest)' != ''" />
              <_CommitFenceLine Include="nokey|ruleset" Condition="'$(ResolvedCodeAnalysisRuleSet)' != ''" />
              <_CommitFenceLine Include="file|$([MSBuild]::NormalizePath('$(MSBuildProjectDirectory)', '$(AppConfigForCompiler)'))" Condition="'$(AppConfigForCompiler)' != ''" />
              <_CommitFenceLine Include="nokey|responsefile" Condition="'$(CompilerResponseFile)' != ''" />
              <_CommitFenceLine Include="file|$([MSBuild]::NormalizePath('$(MSBuildProjectDirectory)', '$(SourceLink)'))" Condition="'$(SourceLink)' != ''" />
              <_CommitFenceLine Include="prop|DefineConstants=$(DefineConstants.Replace(';', ','))" />
              <_CommitFenceLine Include="prop|NoWarn=$(NoWarn.Replace(';', ','))" />
              <_CommitFenceLine Include="prop|WarningsAsErrors=$(WarningsAsErrors.Replace(';', ','))" />
              <_CommitFenceLine Include="prop|WarningsNotAsErrors=$(WarningsNotAsErrors.Replace(';', ','))" />
              <_CommitFenceLine Include="prop|TreatWarningsAsErrors=$(TreatWarningsAsErrors)" />
              <_CommitFenceLine Include="prop|WarningLevel=$(WarningLevel)" />
              <_CommitFenceLine Include="prop|Nullable=$(Nullable)" />
              <_CommitFenceLine Include="prop|LangVersion=$(LangVersion)" />
              <_CommitFenceLine Include="prop|Features=$(Features.Replace(';', ','))" />
              <_CommitFenceLine Include="prop|AnalysisLevel=$(AnalysisLevel)" />
              <_CommitFenceLine Include="prop|AnalysisMode=$(AnalysisMode)" />
              <_CommitFenceLine Include="prop|EnforceCodeStyleInBuild=$(EnforceCodeStyleInBuild)" />
              <_CommitFenceLine Include="prop|GenerateDocumentationFile=$(GenerateDocumentationFile)" />
              <_CommitFenceLine Include="prop|AllowUnsafeBlocks=$(AllowUnsafeBlocks)" />
              <_CommitFenceLine Include="prop|CheckForOverflowUnderflow=$(CheckForOverflowUnderflow)" />
              <_CommitFenceLine Include="prop|Optimize=$(Optimize)" />
              <_CommitFenceLine Include="prop|Deterministic=$(Deterministic)" />
              <_CommitFenceLine Include="prop|OutputType=$(OutputType)" />
              <_CommitFenceLine Include="prop|AssemblyName=$(AssemblyName)" />
              <_CommitFenceLine Include="prop|RootNamespace=$(RootNamespace)" />
              <_CommitFenceLine Include="prop|TargetFramework=$(TargetFramework)" />
              <_CommitFenceLine Include="prop|ChecksumAlgorithm=$(ChecksumAlgorithm)" />
              <_CommitFenceLine Include="prop|DebugType=$(DebugType)" />
              <_CommitFenceLine Include="prop|PlatformTarget=$(PlatformTarget)" />
              <_CommitFenceLine Include="prop|Prefer32Bit=$(Prefer32Bit)" />
              <_CommitFenceLine Include="prop|StartupObject=$(StartupObject)" />
              <_CommitFenceLine Include="prop|PathMap=$(PathMap.Replace(';', ','))" />
              <_CommitFenceLine Include="prop|InterceptorsNamespaces=$(InterceptorsNamespaces.Replace(';', ','))" />
              <_CommitFenceLine Include="prop|ProduceOnlyReferenceAssembly=$(ProduceOnlyReferenceAssembly)" />
              <_CommitFenceLine Include="prop|RunAnalyzers=$(RunAnalyzers)" />
              <_CommitFenceLine Include="prop|PublicSign=$(PublicSign)" />
              <_CommitFenceLine Include="prop|DelaySign=$(DelaySign)" />
              <_CommitFenceLine Include="prop|EmbedAllSources=$(EmbedAllSources)" />
              <_CommitFenceLine Include="prop|ModuleAssemblyName=$(ModuleAssemblyName)" />
              <_CommitFenceLine Include="prop|CodePage=$(CodePage)" />
              <_CommitFenceLine Include="prop|AdditionalLibPaths=$(AdditionalLibPaths)" />
              <_CommitFenceLine Include="prop|BaseAddress=$(BaseAddress)" />
              <_CommitFenceLine Include="prop|DisableSdkPath=$(DisableSdkPath)" />
              <_CommitFenceLine Include="prop|ErrorEndLocation=$(ErrorEndLocation)" />
              <_CommitFenceLine Include="prop|ErrorReport=$(ErrorReport)" />
              <_CommitFenceLine Include="prop|GenerateFullPaths=$(GenerateFullPaths)" />
              <_CommitFenceLine Include="prop|HighEntropyVA=$(HighEntropyVA)" />
              <_CommitFenceLine Include="prop|InterceptorsPreviewNamespaces=$(InterceptorsPreviewNamespaces)" />
              <_CommitFenceLine Include="prop|FileAlignment=$(FileAlignment)" />
              <_CommitFenceLine Include="prop|CompilerGeneratedFilesOutputPath=$(CompilerGeneratedFilesOutputPath)" />
              <_CommitFenceLine Include="prop|Instrument=$(Instrument)" />
              <_CommitFenceLine Include="prop|KeyContainerName=$(KeyContainerName)" />
              <_CommitFenceLine Include="prop|NoCompilerStandardLib=$(NoCompilerStandardLib)" />
              <_CommitFenceLine Include="prop|PreferredUILang=$(PreferredUILang)" />
              <_CommitFenceLine Include="prop|ReportAnalyzer=$(ReportAnalyzer)" />
              <_CommitFenceLine Include="prop|ReportIVTs=$(ReportIVTs)" />
              <_CommitFenceLine Include="prop|RuntimeMetadataVersion=$(RuntimeMetadataVersion)" />
              <_CommitFenceLine Include="prop|SubsystemVersion=$(SubsystemVersion)" />
              <_CommitFenceLine Include="prop|_SkipAnalyzers=$(_SkipAnalyzers)" />
              <_CommitFenceLine Include="prop|SkipCompilerExecution=$(SkipCompilerExecution)" />
              <_CommitFenceLine Include="prop|DebugSymbols=$(DebugSymbols)" />
              <_CommitFenceLine Include="prop|CscEnvironment=$(CscEnvironment)" />
              <_CommitFenceLine Include="prop|CscToolPath=$(CscToolPath)" />
              <_CommitFenceLine Include="prop|CscToolExe=$(CscToolExe)" />
              <_CommitFenceLine Include="end|manifest" />
            </ItemGroup>
            <WriteLinesToFile File="$(CommitFenceManifest)" Lines="@(_CommitFenceLine)" Overwrite="true" />
          </Target>
        </Project>

        """;

    private readonly IFileSystem _fileSystem;
    private readonly IDataFolder _dataFolder;

    public ToolchainTargets(IFileSystem fileSystem, IDataFolder dataFolder)
    {
        _fileSystem = fileSystem;
        _dataFolder = dataFolder;
    }

    /// <summary>
    /// Makes sure the file exists with the right text and returns its path.
    /// </summary>
    public string EnsureTargetsFile()
    {
        var folder = Path.Combine(_dataFolder.RootFolder, "csharp");
        _fileSystem.CreateFolder(folder);
        var path = ToolchainPaths.Combine(folder, "commitfence.targets");
        if(!_fileSystem.FileExists(path) || _fileSystem.ReadAllText(path) != TargetsText)
            _fileSystem.WriteAllText(path, TargetsText);

        return path;
    }

    /// <summary>
    /// Makes sure the empty package source exists and returns its path.
    /// </summary>
    public string EnsureEmptyPackageSource()
    {
        var path = ToolchainPaths.Combine(Path.Combine(_dataFolder.RootFolder, "csharp"), "no-network-packages");
        _fileSystem.CreateFolder(path);

        return path;
    }
}