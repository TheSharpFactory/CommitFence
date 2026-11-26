namespace CF.App.Rules.Page;

/// <summary>
/// The add button that ends a clause's flow of tokens. The flow keeps it on the last token's line, or in the keyword's column when
/// that line has no room for it (see TokenFlow).
/// </summary>
internal interface ITokenAdd
{
}
