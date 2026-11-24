namespace CF.Languages.CSharp.EditorConfig;

/// <summary>
/// One dotnet_diagnostic severity line of an .editorconfig: the section it is in, the rule, the value without its
/// comment, the comments that may name the rule, and the line.
/// </summary>
internal sealed record EditorConfigSetting(string Section, string Id, string Value, string? TrailingComment, string? CommentAbove, int Line);
