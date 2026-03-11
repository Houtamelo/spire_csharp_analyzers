# Type Declaration Nodes

Roslyn 4.12.0 syntax nodes for all type declarations. All inherit from `TypeDeclarationSyntax` (except `EnumDeclarationSyntax`), which inherits from `BaseTypeDeclarationSyntax` -> `MemberDeclarationSyntax`.

## Common Base: TypeDeclarationSyntax

**Namespace:** `Microsoft.CodeAnalysis.CSharp.Syntax`

Shared properties across class, struct, interface, and record declarations:

- `Keyword` — the type keyword token (`class`, `struct`, `interface`, `record`)
- `Identifier` — the type name token
- `Modifiers` — `SyntaxTokenList` of access/modifier keywords
- `TypeParameterList` — `TypeParameterListSyntax` (generics)
- `BaseList` — `BaseListSyntax` (base types and interfaces)
- `ConstraintClauses` — `SyntaxList<TypeParameterConstraintClauseSyntax>`
- `Members` — `SyntaxList<MemberDeclarationSyntax>`
- `AttributeLists` — `SyntaxList<AttributeListSyntax>`

## ClassDeclarationSyntax

**SyntaxKind:** `SyntaxKind.ClassDeclaration`

```csharp
public sealed class MyService : IDisposable
{
    public void Dispose() { }
}
```

## StructDeclarationSyntax

**SyntaxKind:** `SyntaxKind.StructDeclaration`

```csharp
public readonly struct Vector2
{
    public readonly float X, Y;
}
```

## RecordDeclarationSyntax

**SyntaxKinds:** `SyntaxKind.RecordDeclaration`, `SyntaxKind.RecordStructDeclaration`

Additional property: `ClassOrStructKeyword` — distinguishes `record class` from `record struct`.
Additional property: `ParameterList` — positional parameters for primary constructor.

```csharp
// record class (SyntaxKind.RecordDeclaration)
public record Person(string Name, int Age);

// record struct (SyntaxKind.RecordStructDeclaration)
public readonly record struct Point(int X, int Y);
```

## InterfaceDeclarationSyntax

**SyntaxKind:** `SyntaxKind.InterfaceDeclaration`

```csharp
public interface IMovable
{
    void Move(Vector2 delta);
}
```

## EnumDeclarationSyntax

**Namespace:** `Microsoft.CodeAnalysis.CSharp.Syntax`
**SyntaxKind:** `SyntaxKind.EnumDeclaration`

Inherits from `BaseTypeDeclarationSyntax` (not `TypeDeclarationSyntax`). Has no `Members` property; uses `Members` of type `SeparatedSyntaxList<EnumMemberDeclarationSyntax>`.

```csharp
public enum Direction : byte { Up, Down, Left, Right }
```

**Key properties:**
- `EnumKeyword` — the `enum` token
- `Identifier`, `Modifiers`, `BaseList`, `AttributeLists` — inherited
- `Members` — `SeparatedSyntaxList<EnumMemberDeclarationSyntax>`

## TypeParameterSyntax

**Namespace:** `Microsoft.CodeAnalysis.CSharp.Syntax`
**SyntaxKind:** `SyntaxKind.TypeParameter`

Represents a single generic type parameter in a type parameter list.

**Key properties:**
- `Identifier` — the parameter name
- `VarianceKeyword` — `in`/`out` for covariance/contravariance (default if none)
- `AttributeLists` — attributes on the type parameter

```csharp
public interface IReadable<out T> where T : struct { }
// TypeParameterSyntax: Identifier = "T", VarianceKeyword = "out"
```

## BaseListSyntax

**Namespace:** `Microsoft.CodeAnalysis.CSharp.Syntax`
**SyntaxKind:** `SyntaxKind.BaseList`

Contains the list of base types and interfaces after the `:` in a type declaration.

**Key properties:**
- `ColonToken` — the `:` token
- `Types` — `SeparatedSyntaxList<BaseTypeSyntax>`

```csharp
public struct Velocity : IEquatable<Velocity>, IComparable<Velocity> { }
// BaseListSyntax.Types has two SimpleBaseTypeSyntax nodes
```

## Iterating All Type Declarations

To register for all struct-like type declarations:

```csharp
context.RegisterSyntaxNodeAction(AnalyzeType,
    SyntaxKind.StructDeclaration,
    SyntaxKind.RecordStructDeclaration);
```

To check the type kind from `TypeDeclarationSyntax`:

```csharp
private void AnalyzeType(SyntaxNodeAnalysisContext ctx)
{
    var typeDecl = (TypeDeclarationSyntax)ctx.Node;

    // Works for struct, record struct, class, interface, record class
    var symbol = ctx.SemanticModel.GetDeclaredSymbol(typeDecl);
    if (symbol is INamedTypeSymbol { IsValueType: true } namedType)
    {
        // This is a value type (struct or enum)
    }
}
```

## When to Use in an Analyzer

- Register `SyntaxNodeAction` for specific `SyntaxKind` values to target exact declaration types
- Use `TypeDeclarationSyntax` as the cast target when you want to handle struct + record struct uniformly
- Use `BaseTypeDeclarationSyntax` when you also need to handle enums
- Always prefer `SemanticModel.GetDeclaredSymbol()` to get the `INamedTypeSymbol` for reliable type analysis
