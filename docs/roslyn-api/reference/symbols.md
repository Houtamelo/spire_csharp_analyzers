# Semantic Model and Symbol API

Roslyn 4.12.0 semantic model and symbol types for analyzing type information, resolving references, and querying declarations. The symbol API provides the semantic view of code -- types, members, parameters -- independent of syntax.

## SemanticModel

**Namespace:** `Microsoft.CodeAnalysis`

The bridge between syntax and semantics. Obtained via `Compilation.GetSemanticModel(syntaxTree)` or from analysis contexts (`SyntaxNodeAnalysisContext.SemanticModel`).

**Key methods:**
- `GetDeclaredSymbol(node)` — get the symbol declared by a syntax node
- `GetSymbolInfo(expr)` — resolve what an expression refers to
- `GetTypeInfo(expr)` — get the type of an expression
- `GetOperation(node)` — get the IOperation for a syntax node
- `GetConstantValue(expr)` — evaluate compile-time constants

```csharp
// In an analyzer:
var model = context.SemanticModel;

// From a struct declaration, get the type symbol
var structDecl = (StructDeclarationSyntax)context.Node;
var typeSymbol = model.GetDeclaredSymbol(structDecl); // INamedTypeSymbol

// From an expression, get what it refers to
var symbolInfo = model.GetSymbolInfo(someExpression);
ISymbol symbol = symbolInfo.Symbol; // may be null if ambiguous

// From an expression, get its type
var typeInfo = model.GetTypeInfo(someExpression);
ITypeSymbol type = typeInfo.Type;           // the expression's type
ITypeSymbol converted = typeInfo.ConvertedType; // after implicit conversion
```

## INamedTypeSymbol

**Namespace:** `Microsoft.CodeAnalysis`

Represents a class, struct, interface, enum, or delegate type. The primary symbol type for type analysis.

**Key properties:**
- `Name` — type name (e.g., `"Vector3"`)
- `TypeKind` — `TypeKind.Struct`, `TypeKind.Class`, `TypeKind.Interface`, `TypeKind.Enum`
- `IsValueType` — `true` for structs and enums (inherited from `ITypeSymbol`)
- `IsReadOnly` — `true` for `readonly struct` (inherited from `ITypeSymbol`)
- `IsRefLikeType` — `true` for `ref struct` (inherited from `ITypeSymbol`)
- `IsRecord` — `true` for `record struct` and `record class` (inherited from `ITypeSymbol`)
- `IsGenericType` — whether the type has type parameters
- `Arity` — number of type parameters
- `TypeParameters` — `ImmutableArray<ITypeParameterSymbol>`
- `TypeArguments` — `ImmutableArray<ITypeSymbol>` (substituted)
- `Interfaces` — directly implemented interfaces
- `AllInterfaces` — all interfaces (recursive)
- `BaseType` — the base type (null for structs, `System.ValueType` is implicit)
- `ContainingType` — enclosing type (for nested types)
- `ContainingNamespace` — the namespace
- `MemberNames` — names of declared members
- `Constructors` — all constructors
- `InstanceConstructors` — non-static constructors
- `GetMembers()` — all members
- `GetMembers(name)` — members with a specific name

```csharp
if (typeSymbol is INamedTypeSymbol namedType)
{
    bool isStruct = namedType.TypeKind == TypeKind.Struct;
    bool isReadOnly = namedType.IsReadOnly;
    bool isRefLike = namedType.IsRefLikeType;

    foreach (var member in namedType.GetMembers())
    {
        // Iterate all fields, methods, properties, etc.
    }
}
```

## IFieldSymbol

**Namespace:** `Microsoft.CodeAnalysis`

Represents a field in a class, struct, or enum.

**Key properties:**
- `Name` — field name
- `Type` — `ITypeSymbol` of the field
- `IsReadOnly` — declared with `readonly`
- `IsConst` — declared with `const` (also true for enum members)
- `IsStatic` — declared with `static`
- `IsVolatile` — declared with `volatile`
- `RefKind` — `RefKind.None`, `RefKind.Ref`, `RefKind.RefReadOnly`
- `ContainingType` — the type that declares this field
- `IsImplicitlyDeclared` — true for compiler-generated backing fields

```csharp
// Check if all fields in a struct are readonly
foreach (var member in structSymbol.GetMembers())
{
    if (member is IFieldSymbol { IsReadOnly: false, IsConst: false, IsStatic: false } field)
    {
        // Mutable instance field found
    }
}
```

## IMethodSymbol

**Namespace:** `Microsoft.CodeAnalysis`

Represents a method, constructor, destructor, operator, or property/event accessor.

**Key properties:**
- `Name` — method name
- `MethodKind` — `MethodKind.Ordinary`, `PropertyGet`, `PropertySet`, `Constructor`, `Operator`, etc.
- `ReturnType` — `ITypeSymbol`
- `Parameters` — `ImmutableArray<IParameterSymbol>`
- `IsReadOnly` — `true` if the method is `readonly` (receiver is `ref readonly`)
- `IsStatic` — `true` if static
- `ReturnsByRef` — returns by `ref`
- `ReturnsByRefReadonly` — returns by `ref readonly`
- `RefKind` — `RefKind` of the return
- `ContainingType` — the declaring type
- `OverriddenMethod` — the method this overrides (if any)
- `IsImplicitlyDeclared` — true for compiler-generated methods

```csharp
// Check for non-readonly instance methods on a struct
if (method is { IsStatic: false, IsReadOnly: false, ContainingType.IsValueType: true })
{
    // This method may cause defensive copies when called on readonly receivers
}
```

## IPropertySymbol

**Namespace:** `Microsoft.CodeAnalysis`

**Key properties:**
- `Name` — property name
- `Type` — property type
- `IsReadOnly` — has only a getter (no setter)
- `IsWriteOnly` — has only a setter
- `GetMethod` — `IMethodSymbol` for the getter
- `SetMethod` — `IMethodSymbol` for the setter
- `IsIndexer` — true for indexers
- `RefKind` — `RefKind` of the property
- `ContainingType` — the declaring type

## IParameterSymbol

**Namespace:** `Microsoft.CodeAnalysis`

**Key properties:**
- `Name` — parameter name
- `Type` — `ITypeSymbol` of the parameter
- `RefKind` — `RefKind.None`, `Ref`, `Out`, `In`, `RefReadOnlyParameter`
- `IsParams` — declared with `params`
- `HasExplicitDefaultValue` — has a default value
- `ExplicitDefaultValue` — the default value (if any)
- `ContainingSymbol` — the method/property that owns this parameter

```csharp
// Check for large struct passed by value
if (param.RefKind == RefKind.None && param.Type.IsValueType)
{
    // Struct is copied -- might want 'in' or 'ref readonly'
}
```

## ILocalSymbol

**Namespace:** `Microsoft.CodeAnalysis`

**Key properties:**
- `Name` — variable name
- `Type` — `ITypeSymbol`
- `IsRef` — declared as `ref`
- `RefKind` — `RefKind.None`, `Ref`, `RefReadOnly`
- `IsConst` — declared as `const`
- `ContainingSymbol` — the enclosing method

## RefKind Enum Values

| Value | Meaning |
|-------|---------|
| `RefKind.None` | Pass by value |
| `RefKind.Ref` | `ref` parameter/return |
| `RefKind.Out` | `out` parameter |
| `RefKind.In` | `in` parameter (readonly ref) |
| `RefKind.RefReadOnlyParameter` | `ref readonly` parameter (C# 12+) |

## GetDeclaredSymbol vs GetSymbolInfo vs GetTypeInfo

| Method | Input | Returns | Use Case |
|--------|-------|---------|----------|
| `GetDeclaredSymbol` | Declaration node | The declared `ISymbol` | Get symbol for a struct/field/method declaration |
| `GetSymbolInfo` | Expression/name node | `SymbolInfo` with `.Symbol` | Resolve what a name/expression refers to |
| `GetTypeInfo` | Expression node | `TypeInfo` with `.Type`, `.ConvertedType` | Get the type of an expression |

## When to Use in an Analyzer

- Use `INamedTypeSymbol.IsValueType` + `IsReadOnly` to check struct characteristics (more reliable than syntax)
- Use `IFieldSymbol.IsReadOnly` to find mutable fields
- Use `IMethodSymbol.IsReadOnly` to detect methods that may cause defensive copies
- Use `IParameterSymbol.RefKind` to check for `in`/`ref readonly` parameter passing
- Use `ITypeSymbol.AllInterfaces` to check interface implementations
- Always resolve symbols via semantic model rather than relying on syntax names
