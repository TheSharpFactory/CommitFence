namespace CF.Languages.CSharp.Catalog;

/// <summary>
/// The names of the artifact types and properties, written once so the catalog and the reader cannot disagree.
/// </summary>
internal static class CSharpNames
{
    public const string IfStatement = "if-statement";
    public const string Method = "method";
    public const string Class = "class";
    public const string Usage = "usage";

    public const string Name = "name";
    public const string WrittenAccess = "writtenAccess";
    public const string Modifiers = "modifiers";
    public const string DeclaredAccess = "declaredAccess";
    public const string ContainingType = "containingType";
    public const string ContainingTypeName = "containingTypeName";
    public const string ContainingTypeWrittenAccess = "containingTypeWrittenAccess";
    public const string ContainingTypeDeclaredAccess = "containingTypeDeclaredAccess";
    public const string ParameterCount = "parameterCount";
    public const string Statements = "statements";
    public const string Lines = "lines";
    public const string FitsOnOneLine = "fitsOnOneLine";
    public const string IsOverride = "isOverride";
    public const string DocumentationPresent = "documentationPresent";
    public const string Attributes = "attributes";
    public const string IsPartial = "isPartial";
    public const string Target = "target";
    public const string TargetType = "targetType";
    public const string TargetAssembly = "targetAssembly";

    public const string StatementsInBody = "statementsInBody";
    public const string BodyFitsOnOneLine = "bodyFitsOnOneLine";
    public const string Braces = "braces";
    public const string IsElseIf = "isElseIf";
    public const string HasElse = "hasElse";

    public const string None = "none";
}