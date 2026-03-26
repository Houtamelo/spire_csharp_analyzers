# Common Patterns for Analyzer Implementations

Reusable Roslyn 4.12.0 patterns for struct-focused analyzers in the Spire project.

## Registering a SyntaxNodeAction for Struct Declarations

```csharp
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MyStructAnalyzer : DiagnosticAnalyzer
{
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterSyntaxNodeAction(
            AnalyzeStructDeclaration,
            SyntaxKind.StructDeclaration,
            SyntaxKind.RecordStructDeclaration);
    }

    private static void AnalyzeStructDeclaration(SyntaxNodeAnalysisContext context)
    {
        var typeDecl = (TypeDeclarationSyntax)context.Node;
        // ...
    }
}
```

## Getting INamedTypeSymbol from a StructDeclarationSyntax

```csharp
private static void AnalyzeStruct(SyntaxNodeAnalysisContext context)
{
    var structDecl = (TypeDeclarationSyntax)context.Node;

    if (context.SemanticModel.GetDeclaredSymbol(structDecl, context.CancellationToken)
        is not INamedTypeSymbol typeSymbol)
    {
        return;
    }

    // typeSymbol is now the semantic representation of the struct
    // typeSymbol.IsValueType == true
    // typeSymbol.TypeKind == TypeKind.Struct
}
```

## Checking if a Struct is Readonly

**Via syntax (single declaration only):**
```csharp
bool isReadOnly = structDecl.Modifiers.Any(SyntaxKind.ReadOnlyKeyword);
```

**Via semantic model (handles partial types, recommended):**
```csharp
bool isReadOnly = typeSymbol.IsReadOnly;
```

## Walking All Members of a Struct

```csharp
// Get all members including inherited (for structs, no inherited members exist beyond System.ValueType)
foreach (ISymbol member in typeSymbol.GetMembers())
{
    switch (member)
    {
        case IFieldSymbol field when !field.IsImplicitlyDeclared:
            // User-declared field (excludes backing fields for auto-properties)
            break;
        case IMethodSymbol method when method.MethodKind == MethodKind.Ordinary:
            // Regular method (excludes constructors, accessors, operators)
            break;
        case IPropertySymbol property:
            // Property or indexer
            break;
    }
}
```

## Detecting Defensive Copies

A defensive copy occurs when a non-readonly member is invoked on a readonly receiver of a non-readonly struct. The compiler copies the struct to avoid mutating the original.

**Using IOperation API (preferred):**
```csharp
context.RegisterOperationAction(AnalyzeInvocation, OperationKind.Invocation);
context.RegisterOperationAction(AnalyzePropertyRef, OperationKind.PropertyReference);

private static void AnalyzeInvocation(OperationAnalysisContext context)
{
    var invocation = (IInvocationOperation)context.Operation;

    if (invocation.TargetMethod.IsStatic)
        return;

    // Check: is the method non-readonly on a value type?
    IMethodSymbol method = invocation.TargetMethod;
    if (method.ContainingType.IsValueType && !method.IsReadOnly
        && !method.ContainingType.IsReadOnly)
    {
        // Check: is the receiver a readonly context?
        if (IsReadOnlyReceiver(invocation.Instance))
        {
            // Defensive copy will be created
            context.ReportDiagnostic(...);
        }
    }
}

private static bool IsReadOnlyReceiver(IOperation? instance)
{
    if (instance is null) return false;

    return instance switch
    {
        // readonly field of a struct type
        IFieldReferenceOperation { Field.IsReadOnly: true, Field.Type.IsValueType: true } => true,

        // 'in' parameter
        IParameterReferenceOperation { Parameter.RefKind: RefKind.In } => true,

        // ref readonly local
        ILocalReferenceOperation { Local.RefKind: RefKind.RefReadOnly } => true,

        // 'this' in a readonly method of a struct
        IInstanceReferenceOperation when IsInReadOnlyContext(instance) => true,

        _ => false,
    };
}
```

## Checking if a Type Implements an Interface

```csharp
// Check by interface symbol (exact match)
bool implementsIDisposable = typeSymbol.AllInterfaces
    .Any(i => i.OriginalDefinition.SpecialType == SpecialType.System_IDisposable);

// Check by metadata name (for custom interfaces)
bool implementsTarget = typeSymbol.AllInterfaces
    .Any(i => i.OriginalDefinition.ToDisplayString() == "MyNamespace.IMyInterface");

// Using GetTypeByMetadataName (preferred -- resolve once in CompilationStart)
var targetInterface = compilation.GetTypeByMetadataName("MyNamespace.IMyInterface");
if (targetInterface is not null)
{
    bool implements = typeSymbol.AllInterfaces
        .Any(i => SymbolEqualityComparer.Default.Equals(i.OriginalDefinition, targetInterface));
}
```

## Using CompilationStartAction for Type Caching

Use `CompilationStartAction` to resolve well-known types once per compilation, then pass them to inner actions. This avoids repeated `GetTypeByMetadataName` calls.

```csharp
public override void Initialize(AnalysisContext context)
{
    context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
    context.EnableConcurrentExecution();

    context.RegisterCompilationStartAction(compilationContext =>
    {
        var compilation = compilationContext.Compilation;

        // Resolve types once
        var requireInitAttr = compilation.GetTypeByMetadataName(
            "Houtamelo.Spire.Analyzers.RequireInitializationAttribute");

        if (requireInitAttr is null)
            return; // Attribute not referenced -- nothing to analyze

        // Register inner actions that use the cached type
        compilationContext.RegisterSyntaxNodeAction(
            ctx => AnalyzeStruct(ctx, requireInitAttr),
            SyntaxKind.StructDeclaration,
            SyntaxKind.RecordStructDeclaration);
    });
}

private static void AnalyzeStruct(
    SyntaxNodeAnalysisContext context,
    INamedTypeSymbol requireInitAttr)
{
    var typeDecl = (TypeDeclarationSyntax)context.Node;
    var typeSymbol = context.SemanticModel.GetDeclaredSymbol(typeDecl, context.CancellationToken);
    if (typeSymbol is null) return;

    // Check if the type has the attribute
    bool hasAttr = typeSymbol.GetAttributes()
        .Any(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, requireInitAttr));
}
```

## Using the IOperation API for Analysis

The IOperation API provides a language-neutral, semantically-rich tree of operations. It normalizes different syntax forms into uniform operations.

**Registering for operations:**
```csharp
// Register directly on AnalysisContext
context.RegisterOperationAction(AnalyzeConversion, OperationKind.Conversion);

// Or within CompilationStartAction
compilationContext.RegisterOperationAction(AnalyzeConversion, OperationKind.Conversion);
```

**Boxing detection example:**
```csharp
private static void AnalyzeConversion(OperationAnalysisContext context)
{
    var conversion = (IConversionOperation)context.Operation;

    if (conversion.Operand.Type is not { IsValueType: true } sourceType)
        return;

    // Check for boxing (value type -> reference type)
    if (conversion.Type is { IsReferenceType: true }
        && conversion.OperatorMethod is null) // not a user-defined conversion
    {
        // Boxing detected -- report diagnostic
        context.ReportDiagnostic(
            Diagnostic.Create(BoxingDescriptor, conversion.Syntax.GetLocation(),
                sourceType.Name));
    }
}
```

**Walking the operation tree:**
```csharp
// Get all operations within a method body
private static void AnalyzeMethodBody(OperationBlockAnalysisContext context)
{
    foreach (var block in context.OperationBlocks)
    {
        foreach (var operation in block.DescendantsAndSelf())
        {
            if (operation is IInvocationOperation invocation)
            {
                // Process each method call in the body
            }
        }
    }
}
```

## Key OperationKind Values for Struct Analyzers

| OperationKind | Interface | What to Detect |
|---------------|-----------|----------------|
| `Invocation` | `IInvocationOperation` | Method calls causing defensive copies |
| `PropertyReference` | `IPropertyReferenceOperation` | Property access causing defensive copies |
| `FieldReference` | `IFieldReferenceOperation` | Readonly field access patterns |
| `Conversion` | `IConversionOperation` | Boxing of value types |
| `ObjectCreation` | `IObjectCreationOperation` | Struct instantiation patterns |
| `SimpleAssignment` | `ISimpleAssignmentOperation` | Field/property mutations |
| `LocalReference` | `ILocalReferenceOperation` | Local variable usage |
| `ParameterReference` | `IParameterReferenceOperation` | Parameter usage (in/ref analysis) |

## Reporting a Diagnostic

```csharp
// Define the descriptor (in Descriptors.cs)
public static readonly DiagnosticDescriptor SPIRE001 = new(
    id: "SPIRE001",
    title: "Struct should be readonly",
    messageFormat: "Struct '{0}' has no mutable state and should be declared readonly",
    category: "Performance",
    defaultSeverity: DiagnosticSeverity.Warning,
    isEnabledByDefault: true);

// Report it
context.ReportDiagnostic(
    Diagnostic.Create(
        Descriptors.SPIRE001,
        structDecl.Identifier.GetLocation(),
        typeSymbol.Name));
```

## Checking Attributes on a Symbol

```csharp
// Using a cached attribute type (from CompilationStart)
bool hasAttribute = symbol.GetAttributes()
    .Any(a => SymbolEqualityComparer.Default.Equals(
        a.AttributeClass, cachedAttributeType));

// Reading attribute constructor arguments
var attr = symbol.GetAttributes()
    .FirstOrDefault(a => SymbolEqualityComparer.Default.Equals(
        a.AttributeClass, cachedAttributeType));

if (attr is not null && attr.ConstructorArguments.Length > 0)
{
    var firstArg = attr.ConstructorArguments[0];
    if (firstArg.Value is string s) { /* use s */ }
}
```
