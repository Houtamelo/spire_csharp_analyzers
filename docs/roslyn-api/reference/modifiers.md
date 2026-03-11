# Modifier Tokens and How to Check Them

Roslyn 4.12.0 guide to working with modifiers at both the syntax level (tokens) and semantic level (symbol properties).

## Modifier Tokens on Syntax Nodes

All declaration syntax nodes have a `Modifiers` property of type `SyntaxTokenList`. Check for specific modifiers using `Any(SyntaxKind)`:

```csharp
var decl = (TypeDeclarationSyntax)context.Node;

bool isPublic   = decl.Modifiers.Any(SyntaxKind.PublicKeyword);
bool isReadOnly = decl.Modifiers.Any(SyntaxKind.ReadOnlyKeyword);
bool isStatic   = decl.Modifiers.Any(SyntaxKind.StaticKeyword);
bool isPartial  = decl.Modifiers.Any(SyntaxKind.PartialKeyword);
bool isRef      = decl.Modifiers.Any(SyntaxKind.RefKeyword);
```

## SyntaxKind.ReadOnlyKeyword

Applies to: struct declarations, field declarations, method declarations, property accessors, local variables.

**On a struct:**
```csharp
// Source:
public readonly struct Vector3 { }

// Check:
structDecl.Modifiers.Any(SyntaxKind.ReadOnlyKeyword) // true
```

**On a method (readonly member):**
```csharp
// Source:
public readonly float GetLength() => ...;

// Check:
methodDecl.Modifiers.Any(SyntaxKind.ReadOnlyKeyword) // true
```

**On a field:**
```csharp
// Source:
private readonly int _value;

// Check:
fieldDecl.Modifiers.Any(SyntaxKind.ReadOnlyKeyword) // true
```

**Semantic equivalent:**
```csharp
INamedTypeSymbol type = ...;
type.IsReadOnly         // true for readonly struct

IFieldSymbol field = ...;
field.IsReadOnly        // true for readonly field

IMethodSymbol method = ...;
method.IsReadOnly       // true for readonly method member
```

## SyntaxKind.RefKeyword

Applies to: struct declarations (`ref struct`), parameters, return types, local variables.

**On a struct:**
```csharp
// Source:
public ref struct SpanWrapper { }

// Check:
structDecl.Modifiers.Any(SyntaxKind.RefKeyword) // true
```

**On a parameter:**
```csharp
// Source:
void Set(ref Vector3 target) { }

// Check:
parameterSyntax.Modifiers.Any(SyntaxKind.RefKeyword) // true
```

**On a return type (uses RefTypeSyntax, not modifiers):**
```csharp
// Source:
ref readonly int GetRef() => ref _value;

// Check via syntax:
methodDecl.ReturnType is RefTypeSyntax refType
    && refType.RefKeyword.IsKind(SyntaxKind.RefKeyword)

// Check via symbol:
methodSymbol.ReturnsByRef          // true for ref return
methodSymbol.ReturnsByRefReadonly  // true for ref readonly return
```

**Semantic equivalent:**
```csharp
ITypeSymbol type = ...;
type.IsRefLikeType      // true for ref struct

IParameterSymbol param = ...;
param.RefKind           // RefKind.Ref, RefKind.In, RefKind.Out, etc.
```

## SyntaxKind.InKeyword

Applies to: parameters (pass by readonly reference), type parameter variance.

**On a parameter:**
```csharp
// Source:
void Process(in Vector3 position) { }

// Check:
parameterSyntax.Modifiers.Any(SyntaxKind.InKeyword) // true
```

**Semantic equivalent:**
```csharp
IParameterSymbol param = ...;
param.RefKind == RefKind.In  // true for 'in' parameter
```

**Struct-relevant:** `in` parameters receive a readonly reference. If the struct is not `readonly`, calling a non-readonly method on an `in` parameter creates a defensive copy.

## SyntaxKind.StaticKeyword

Applies to: classes, methods, fields, properties, constructors, operators.

```csharp
// Source:
public static readonly Vector3 Zero = new(0, 0, 0);

// Check:
fieldDecl.Modifiers.Any(SyntaxKind.StaticKeyword) // true
```

## SyntaxKind.PartialKeyword

Applies to: classes, structs, interfaces, methods.

```csharp
// Source:
public partial struct GameState { }

// Check:
structDecl.Modifiers.Any(SyntaxKind.PartialKeyword) // true
```

**Note:** partial types may have modifiers split across declarations. The semantic model unifies them -- always prefer `INamedTypeSymbol` properties for definitive answers.

## Checking Multiple Modifiers

```csharp
// Check if a struct is both readonly and ref:
bool isReadOnlyRefStruct =
    structDecl.Modifiers.Any(SyntaxKind.ReadOnlyKeyword) &&
    structDecl.Modifiers.Any(SyntaxKind.RefKeyword);

// Equivalent semantic check (preferred):
if (typeSymbol is { IsReadOnly: true, IsRefLikeType: true })
{
    // readonly ref struct
}
```

## Iterating All Modifiers

```csharp
foreach (var modifier in decl.Modifiers)
{
    switch (modifier.Kind())
    {
        case SyntaxKind.PublicKeyword:
        case SyntaxKind.PrivateKeyword:
        case SyntaxKind.InternalKeyword:
        case SyntaxKind.ProtectedKeyword:
            // Access modifier
            break;
        case SyntaxKind.ReadOnlyKeyword:
        case SyntaxKind.StaticKeyword:
        case SyntaxKind.RefKeyword:
            // Structural modifier
            break;
    }
}
```

## Syntax vs Semantic: When to Use Which

| Check | Syntax (Modifiers) | Semantic (Symbol) |
|-------|--------------------|--------------------|
| Is struct readonly? | `Modifiers.Any(ReadOnlyKeyword)` | `ITypeSymbol.IsReadOnly` |
| Is ref struct? | `Modifiers.Any(RefKeyword)` | `ITypeSymbol.IsRefLikeType` |
| Is field readonly? | `Modifiers.Any(ReadOnlyKeyword)` | `IFieldSymbol.IsReadOnly` |
| Is method readonly? | `Modifiers.Any(ReadOnlyKeyword)` | `IMethodSymbol.IsReadOnly` |
| Is parameter `in`? | `Modifiers.Any(InKeyword)` | `IParameterSymbol.RefKind == RefKind.In` |

**General rule:** Use **semantic checks** (symbol properties) whenever possible. They handle:
- Partial types (modifiers split across files)
- Inherited properties
- Metadata-only types (no syntax available)
- Implicit modifiers (e.g., enum members are implicitly `const`)

Use **syntax checks** only when you need to report diagnostics at the exact token location or when semantic info is unavailable.
