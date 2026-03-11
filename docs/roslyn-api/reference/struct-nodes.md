# Struct-Specific Syntax Nodes

Roslyn 4.12.0 syntax nodes and tokens relevant to struct declarations and struct-related modifiers.

## StructDeclarationSyntax

**Namespace:** `Microsoft.CodeAnalysis.CSharp.Syntax`
**SyntaxKind:** `SyntaxKind.StructDeclaration`

The primary node for `struct` type declarations. Inherits from `TypeDeclarationSyntax`.

**Key properties:**
- `Keyword` — the `struct` token
- `Identifier` — the struct's name token
- `Modifiers` — `SyntaxTokenList` (readonly, partial, public, etc.)
- `TypeParameterList` — generic type parameters, if any
- `BaseList` — implemented interfaces
- `ConstraintClauses` — generic constraints
- `Members` — `SyntaxList<MemberDeclarationSyntax>` of all member declarations

```csharp
// Source code:
public readonly partial struct Vector3 : IEquatable<Vector3>
{
    public float X { get; }
}

// Produces a StructDeclarationSyntax where:
//   Keyword        = "struct"
//   Identifier     = "Vector3"
//   Modifiers      = { "public", "readonly", "partial" }
//   BaseList.Types = { SimpleBaseTypeSyntax("IEquatable<Vector3>") }
//   Members        = { PropertyDeclarationSyntax }
```

## RecordDeclarationSyntax (record struct)

**Namespace:** `Microsoft.CodeAnalysis.CSharp.Syntax`
**SyntaxKinds:** `SyntaxKind.RecordDeclaration`, `SyntaxKind.RecordStructDeclaration`

Covers both `record class` and `record struct`. Distinguish via `ClassOrStructKeyword`.

**Key properties:**
- `Keyword` — the `record` token
- `ClassOrStructKeyword` — `struct` or `class` (or default for plain `record`)
- `Identifier`, `Modifiers`, `BaseList`, `Members` — same as TypeDeclarationSyntax
- `ParameterList` — positional parameters (primary constructor)

```csharp
// Source code:
public readonly record struct Point(int X, int Y);

// Produces a RecordDeclarationSyntax where:
//   Kind                 = SyntaxKind.RecordStructDeclaration
//   Keyword              = "record"
//   ClassOrStructKeyword = "struct"
//   Modifiers            = { "public", "readonly" }
//   ParameterList        = (int X, int Y)
```

**How to filter for record structs only:**
```csharp
if (recordDecl.IsKind(SyntaxKind.RecordStructDeclaration))
{
    // This is a record struct
}
```

## RefTypeSyntax

**Namespace:** `Microsoft.CodeAnalysis.CSharp.Syntax`
**SyntaxKind:** `SyntaxKind.RefType`

Wraps a type with `ref` or `ref readonly` semantics.

**Key properties:**
- `RefKeyword` — the `ref` token
- `ReadOnlyKeyword` — the `readonly` token (default if not present)
- `Type` — the inner `TypeSyntax`

```csharp
// Source code:
ref readonly int GetValue() => ref _value;

// The return type is a RefTypeSyntax where:
//   RefKeyword      = "ref"
//   ReadOnlyKeyword = "readonly"
//   Type            = PredefinedTypeSyntax("int")
```

## ParameterSyntax with in/ref readonly modifiers

**Namespace:** `Microsoft.CodeAnalysis.CSharp.Syntax`

**Key properties:**
- `Modifiers` — contains `in`, `ref`, `out`, `params`, `this`, `scoped`
- `Type` — the parameter type (may be `RefTypeSyntax` for `ref readonly`)
- `Identifier` — the parameter name

```csharp
// Source code:
void Process(in Vector3 position, ref readonly Matrix4x4 transform)

// First parameter:
//   Modifiers = { "in" }
//   Type      = IdentifierNameSyntax("Vector3")

// Second parameter:
//   Modifiers = { "ref", "readonly" }
//   Type      = IdentifierNameSyntax("Matrix4x4")
```

**Checking for `in` parameter:**
```csharp
bool hasIn = parameter.Modifiers.Any(SyntaxKind.InKeyword);
```

## Struct-Related Modifier Tokens

| Token | SyntaxKind | Usage |
|-------|-----------|-------|
| `readonly` | `SyntaxKind.ReadOnlyKeyword` | On struct decl, field, method, property |
| `ref` | `SyntaxKind.RefKeyword` | On struct decl (`ref struct`), parameters, returns |
| `in` | `SyntaxKind.InKeyword` | On parameters (pass by readonly ref) |
| `partial` | `SyntaxKind.PartialKeyword` | On struct decl (partial struct) |

```csharp
// Checking if a struct is readonly:
bool isReadOnly = structDecl.Modifiers.Any(SyntaxKind.ReadOnlyKeyword);

// Checking if a struct is a ref struct:
bool isRefStruct = structDecl.Modifiers.Any(SyntaxKind.RefKeyword);
```

## When to Use in an Analyzer

- **Detecting non-readonly structs** — register for `SyntaxKind.StructDeclaration`, check `Modifiers` for `ReadOnlyKeyword`
- **Analyzing ref structs** — check for `RefKeyword` in modifiers; ref structs have special constraints
- **Parameter passing analysis** — check `ParameterSyntax.Modifiers` for `in`/`ref` to detect value-type copying
- **Record struct detection** — register for `SyntaxKind.RecordStructDeclaration` or check `ClassOrStructKeyword`
- Prefer using the **IOperation API** or **semantic model** (`ITypeSymbol.IsReadOnly`, `ITypeSymbol.IsRefLikeType`) over syntax checks when possible, as they handle all declaration forms uniformly
