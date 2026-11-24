using CF.Core.Code;

namespace CF.Languages.CSharp.Catalog;

/// <summary>
/// The C# artifact types and their properties, each with its definition. Only the artifact types that the current rules
/// need are here; the rest of the catalog comes with its rules.
/// </summary>
internal sealed class CSharpArtifactCatalog : IArtifactCatalog
{
    private static readonly string[] _noValues = Array.Empty<string>();
    private static readonly string[] _declaredAccesses = { "public", "internal", "protected", "private", "protected internal", "private protected" };
    private static readonly string[] _writtenAccesses = { "none", "public", "internal", "protected", "private", "protected internal", "private protected" };
    private static readonly string[] _containingDeclaredAccesses = { "none", "public", "internal", "protected", "private", "protected internal", "private protected" };
    private static readonly string[] _containingTypeKinds = { "none", "class", "struct", "record", "interface" };

    public CSharpArtifactCatalog()
    {
        ArtifactTypes = new List<ArtifactType>
        {
            CreateIfStatement(),
            CreateMethod(),
            CreateClass(),
            CreateUsage()
        };
    }

    public IReadOnlyList<ArtifactType> ArtifactTypes { get; }

    public ArtifactType? FindArtifactType(string name)
    {
        return ArtifactTypes.FirstOrDefault(artifactType => artifactType.Name == name);
    }

    private static ArtifactType CreateIfStatement()
    {
        var properties = new List<PropertyDescriptor>
        {
            Syntax(CSharpNames.StatementsInBody, PropertyValueType.Number, "How many statements the body holds, braces or not. The body is the branch taken when the condition holds; an else branch is not part of it. A body without braces holds one statement.", _noValues),
            Syntax(CSharpNames.BodyFitsOnOneLine, PropertyValueType.Boolean, "Whether what is inside the body, from the first token of its first statement to the last token of its last one, sits on one line. The braces and comments are not counted. An empty body fits.", _noValues),
            Syntax(CSharpNames.FitsOnOneLine, PropertyValueType.Boolean, "Whether the whole if statement, from the if keyword to the end of its last branch, sits on one line.", _noValues),
            Syntax(CSharpNames.Braces, PropertyValueType.Boolean, "Whether the body is written inside braces.", _noValues),
            Syntax(CSharpNames.IsElseIf, PropertyValueType.Boolean, "Whether this if statement is the one after an else.", _noValues),
            Syntax(CSharpNames.HasElse, PropertyValueType.Boolean, "Whether an else follows the body.", _noValues),
            Syntax(CSharpNames.Lines, PropertyValueType.Number, "How many lines the if statement takes, from the if keyword to its end.", _noValues)
        };

        return new ArtifactType(CSharpNames.IfStatement, "An if statement. Each else if is an if statement of its own, and it is found where the if keyword after the else is.", properties);
    }

    private static ArtifactType CreateMethod()
    {
        var properties = new List<PropertyDescriptor>
        {
            Syntax(CSharpNames.Name, PropertyValueType.Text, "The name of the method.", _noValues),
            Syntax(CSharpNames.WrittenAccess, PropertyValueType.Text, "The access keyword written in the file, or none.", _writtenAccesses),
            Syntax(CSharpNames.Modifiers, PropertyValueType.Text, "Every modifier keyword written, in the order written, separated by a space.", _noValues),
            Syntax(CSharpNames.IsOverride, PropertyValueType.Boolean, "Whether the method is written with the override keyword.", _noValues),
            Meaning(CSharpNames.DeclaredAccess, PropertyValueType.Text, "The access the method really has, as the compiler says: the language's default applied when none is written.", _declaredAccesses),
            Syntax(CSharpNames.ContainingType, PropertyValueType.Text, "The kind of type the method is immediately declared in, or none. A record struct is a record.", _containingTypeKinds),
            Syntax(CSharpNames.ContainingTypeName, PropertyValueType.Text, "The name of that type, or empty.", _noValues),
            Syntax(CSharpNames.ContainingTypeWrittenAccess, PropertyValueType.Text, "The access keyword written on that type, or none.", _writtenAccesses),
            Meaning(CSharpNames.ContainingTypeDeclaredAccess, PropertyValueType.Text, "The declared access of that type, as the compiler says (every part of a partial type taken together), or none.", _containingDeclaredAccesses),
            Syntax(CSharpNames.ParameterCount, PropertyValueType.Number, "How many parameters the method has.", _noValues),
            Syntax(CSharpNames.Statements, PropertyValueType.Number, "How many statements are directly in the body; an expression body counts one, a method without a body none.", _noValues),
            Syntax(CSharpNames.Lines, PropertyValueType.Number, "How many lines the method takes, attributes included, documentation comment not.", _noValues),
            Syntax(CSharpNames.FitsOnOneLine, PropertyValueType.Boolean, "Whether the whole method sits on one line.", _noValues),
            Syntax(CSharpNames.DocumentationPresent, PropertyValueType.Boolean, "Whether a documentation comment comes right before the method.", _noValues),
            Syntax(CSharpNames.Attributes, PropertyValueType.Text, "The names of the attributes, as written, separated by a comma and a space.", _noValues)
        };

        return new ArtifactType(CSharpNames.Method, "A method of a class, struct, record or interface. Constructors, local functions, lambdas and accessors are not methods.", properties);
    }

    private static ArtifactType CreateClass()
    {
        var properties = new List<PropertyDescriptor>
        {
            Syntax(CSharpNames.Name, PropertyValueType.Text, "The name of the class.", _noValues),
            Syntax(CSharpNames.WrittenAccess, PropertyValueType.Text, "The access keyword written in the file, or none.", _writtenAccesses),
            Syntax(CSharpNames.Modifiers, PropertyValueType.Text, "Every modifier keyword written, in the order written, separated by a space.", _noValues),
            Meaning(CSharpNames.DeclaredAccess, PropertyValueType.Text, "The access the class really has, as the compiler says: the language's default applied when none is written, and every part of a partial class taken together.", _declaredAccesses),
            Syntax(CSharpNames.ContainingType, PropertyValueType.Text, "The kind of type the class is immediately declared in, or none for a class that is not nested.", _containingTypeKinds),
            Syntax(CSharpNames.IsPartial, PropertyValueType.Boolean, "Whether this part of the class is written partial.", _noValues),
            Syntax(CSharpNames.Lines, PropertyValueType.Number, "How many lines the class takes, attributes included, documentation comment not.", _noValues),
            Syntax(CSharpNames.FitsOnOneLine, PropertyValueType.Boolean, "Whether the whole class sits on one line.", _noValues),
            Syntax(CSharpNames.DocumentationPresent, PropertyValueType.Boolean, "Whether a documentation comment comes right before the class.", _noValues),
            Syntax(CSharpNames.Attributes, PropertyValueType.Text, "The names of the attributes, as written, separated by a comma and a space.", _noValues)
        };

        return new ArtifactType(CSharpNames.Class, "A class. Structs, records and interfaces are not classes.", properties);
    }

    private static ArtifactType CreateUsage()
    {
        var properties = new List<PropertyDescriptor>
        {
            Meaning(CSharpNames.Target, PropertyValueType.Text, "What the name refers to, as the compiler resolved it: a member written as Type.Member with the type's full name and no parameters, such as System.DateTime.Now, or a type's full name. A constructor counts as its type.", _noValues),
            Meaning(CSharpNames.TargetType, PropertyValueType.Text, "The full name of the type that is used, or that holds the member that is used.", _noValues),
            Meaning(CSharpNames.TargetAssembly, PropertyValueType.Text, "The name of the assembly that type comes from.", _noValues)
        };

        return new ArtifactType(CSharpNames.Usage, "A place where code refers to a type or to a member of a type, as the compiler resolved the name. Only known from the build.", properties, true);
    }

    private static PropertyDescriptor Syntax(string name, PropertyValueType valueType, string description, string[] allowedValues)
    {
        return new PropertyDescriptor(name, PropertyKind.Syntax, valueType, description, allowedValues);
    }

    private static PropertyDescriptor Meaning(string name, PropertyValueType valueType, string description, string[] allowedValues)
    {
        return new PropertyDescriptor(name, PropertyKind.Meaning, valueType, description, allowedValues);
    }
}