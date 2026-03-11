# Member Declaration Nodes

Roslyn 4.12.0 syntax nodes for member declarations within types. All inherit from `MemberDeclarationSyntax`.

## FieldDeclarationSyntax

**Namespace:** `Microsoft.CodeAnalysis.CSharp.Syntax`
**SyntaxKind:** `SyntaxKind.FieldDeclaration`

**Key properties:**
- `Declaration` — `VariableDeclarationSyntax` containing type and variable declarators
- `Modifiers` — access, `readonly`, `static`, `volatile`, `const`, etc.
- `AttributeLists` — attributes on the field

Note: a single `FieldDeclarationSyntax` can declare multiple fields (`int x, y, z;`). Each is a `VariableDeclaratorSyntax` inside `Declaration.Variables`.

```csharp
public readonly int _value;

// FieldDeclarationSyntax:
//   Modifiers              = { "public", "readonly" }
//   Declaration.Type       = PredefinedTypeSyntax("int")
//   Declaration.Variables  = { VariableDeclaratorSyntax("_value") }
```

**Getting the field symbol:**
```csharp
foreach (var variable in fieldDecl.Declaration.Variables)
{
    var fieldSymbol = (IFieldSymbol)model.GetDeclaredSymbol(variable);
}
```

## PropertyDeclarationSyntax

**Namespace:** `Microsoft.CodeAnalysis.CSharp.Syntax`
**SyntaxKind:** `SyntaxKind.PropertyDeclaration`

**Key properties:**
- `Type` — the property type
- `Identifier` — the property name
- `Modifiers` — access, `static`, `readonly`, `virtual`, `override`, etc.
- `AccessorList` — `AccessorListSyntax` with `get`/`set`/`init` accessors
- `ExpressionBody` — arrow expression body (`=> expr`)
- `Initializer` — property initializer (`= value`)

```csharp
public int X { get; init; }
public int Length => Math.Sqrt(X * X + Y * Y);

// First: AccessorList has two AccessorDeclarationSyntax nodes (get, init)
// Second: ExpressionBody is an ArrowExpressionClauseSyntax
```

## MethodDeclarationSyntax

**Namespace:** `Microsoft.CodeAnalysis.CSharp.Syntax`
**SyntaxKind:** `SyntaxKind.MethodDeclaration`

**Key properties:**
- `ReturnType` — `TypeSyntax` (may be `RefTypeSyntax` for ref returns)
- `Identifier` — the method name
- `Modifiers` — includes `readonly` for readonly struct members
- `TypeParameterList` — generic type parameters
- `ParameterList` — `ParameterListSyntax`
- `ConstraintClauses` — generic constraints
- `Body` — `BlockSyntax` body (null for expression-bodied)
- `ExpressionBody` — `ArrowExpressionClauseSyntax` (null for block body)

```csharp
public readonly ref readonly Vector3 GetNormalized()
{
    return ref _normalized;
}

// MethodDeclarationSyntax:
//   Modifiers  = { "public", "readonly" }
//   ReturnType = RefTypeSyntax(ref readonly Vector3)
//   Identifier = "GetNormalized"
```

## ConstructorDeclarationSyntax

**Namespace:** `Microsoft.CodeAnalysis.CSharp.Syntax`
**SyntaxKind:** `SyntaxKind.ConstructorDeclaration`

**Key properties:**
- `Identifier` — the type name (constructors use the type name)
- `Modifiers` — access modifiers, `static`
- `ParameterList` — constructor parameters
- `Initializer` — `ConstructorInitializerSyntax` for `: this()` or `: base()`
- `Body` / `ExpressionBody` — constructor body

```csharp
public Vector3(float x, float y, float z) : this()
{
    X = x; Y = y; Z = z;
}
```

## OperatorDeclarationSyntax

**Namespace:** `Microsoft.CodeAnalysis.CSharp.Syntax`
**SyntaxKind:** `SyntaxKind.OperatorDeclaration`

**Key properties:**
- `ReturnType` — the return type
- `OperatorToken` — the operator symbol (`+`, `-`, `==`, etc.)
- `ParameterList` — operator parameters
- `Modifiers` — must include `public` and `static`

```csharp
public static Vector3 operator +(Vector3 a, Vector3 b)
    => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
```

## EventDeclarationSyntax

**Namespace:** `Microsoft.CodeAnalysis.CSharp.Syntax`
**SyntaxKind:** `SyntaxKind.EventDeclaration`

**Key properties:**
- `Type` — the delegate type
- `Identifier` — event name
- `AccessorList` — `add`/`remove` accessors

```csharp
public event EventHandler Changed
{
    add { _handler += value; }
    remove { _handler -= value; }
}
```

Note: field-like events (`public event Action OnFire;`) produce `EventFieldDeclarationSyntax`, not `EventDeclarationSyntax`.

## IndexerDeclarationSyntax

**Namespace:** `Microsoft.CodeAnalysis.CSharp.Syntax`
**SyntaxKind:** `SyntaxKind.IndexerDeclaration`

**Key properties:**
- `Type` — the return type
- `ParameterList` — `BracketedParameterListSyntax` (the indexer parameters)
- `AccessorList` — `get`/`set` accessors
- `ExpressionBody` — arrow expression body

```csharp
public ref readonly float this[int index] => ref _values[index];
```

## Struct-Analyzer Relevance

For struct analyzers, the most important member nodes are:

| Node | Why It Matters |
|------|---------------|
| `FieldDeclarationSyntax` | Detect mutable fields in structs that should be readonly |
| `PropertyDeclarationSyntax` | Detect auto-properties without `readonly` or `init` |
| `MethodDeclarationSyntax` | Detect non-readonly methods that cause defensive copies |
| `ConstructorDeclarationSyntax` | Verify proper initialization of all fields |

**Prefer the IOperation API** for analyzing method bodies. Use syntax nodes primarily for declaration-level analysis (modifiers, signatures, structure).
