namespace CF.Core.Setup;

/// <summary>An action a face can offer beside a fix.</summary>
/// <param name="Kind">What to do with the value.</param>
/// <param name="Label">The words to show.</param>
/// <param name="Value">A file path, text to copy, or a web address.</param>
/// <param name="Line">The 1-based line to open, where known.</param>
public sealed record DoctorAction(DoctorActionKind Kind, string Label, string Value, int? Line = null);
