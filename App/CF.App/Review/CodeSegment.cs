namespace CF.App.Review;

/// <summary>
/// A run of one line of code that has one colour: the name of the colour token, or none for plain code.
/// </summary>
internal readonly record struct CodeSegment(string Text, string? BrushName);
