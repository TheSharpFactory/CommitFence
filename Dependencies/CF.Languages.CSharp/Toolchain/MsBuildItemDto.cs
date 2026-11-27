namespace CF.Languages.CSharp.Toolchain;

/// <summary>The absolute path of a source file, project reference or assembly returned by MSBuild.</summary>
internal sealed record MsBuildItemDto(string FullPath);