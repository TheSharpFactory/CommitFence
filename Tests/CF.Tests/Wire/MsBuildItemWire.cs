using System.Text.Json.Serialization;

namespace CF.Tests.Wire;

/// <summary>One item of an MSBuild answer, with MSBuild's own key name.</summary>
internal sealed record MsBuildItemWire([property: JsonPropertyName("FullPath")] string FullPath);