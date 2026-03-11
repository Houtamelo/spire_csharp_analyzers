# Statement Nodes

Roslyn 4.12.0 syntax nodes for statements. All inherit from `StatementSyntax`. For analyzer work inside method bodies, prefer the **IOperation API** which provides a normalized, semantic view.

## LocalDeclarationStatementSyntax

**SyntaxKind:** `SyntaxKind.LocalDeclarationStatement`
**IOperation:** `IVariableDeclarationGroupOperation` (`OperationKind.VariableDeclarationGroup`)

**Key properties:**
- `Declaration` — `VariableDeclarationSyntax` with the type and declarators
- `Modifiers` — can contain `const`, `using`, `ref`, `readonly`
- `UsingKeyword` — for `using var` declarations
- `AwaitKeyword` — for `await using var` declarations

```csharp
var position = new Vector3(1, 2, 3);

// LocalDeclarationStatementSyntax:
//   Declaration.Type      = IdentifierNameSyntax("var")
//   Declaration.Variables = { VariableDeclaratorSyntax("position", initializer) }
```

**Getting the local symbol:**
```csharp
var declarator = localDecl.Declaration.Variables[0];
var localSymbol = (ILocalSymbol)model.GetDeclaredSymbol(declarator);
// localSymbol.Type -> INamedTypeSymbol for Vector3
// localSymbol.IsRef -> true if declared as ref
// localSymbol.RefKind -> RefKind.None, RefKind.Ref, RefKind.RefReadOnly
```

**IOperation hierarchy:**
```
IVariableDeclarationGroupOperation (OperationKind.VariableDeclarationGroup)
  └─ IVariableDeclarationOperation (OperationKind.VariableDeclaration)
       └─ IVariableDeclaratorOperation (OperationKind.VariableDeclarator)
            └─ IVariableInitializerOperation (initializer value)
```

## ReturnStatementSyntax

**SyntaxKind:** `SyntaxKind.ReturnStatement`
**IOperation:** `IReturnOperation` (`OperationKind.Return`)

**Key properties:**
- `Expression` — the return value expression (null for `return;`)
- `ReturnKeyword` — the `return` token

```csharp
return new Vector3(x, y, z);

// IReturnOperation provides:
//   ReturnedValue : IOperation — the returned expression
```

## ExpressionStatementSyntax

**SyntaxKind:** `SyntaxKind.ExpressionStatement`
**IOperation:** `IExpressionStatementOperation` (`OperationKind.ExpressionStatement`)

Wraps an expression used as a statement.

**Key properties:**
- `Expression` — the expression (usually invocation or assignment)

```csharp
list.Add(item);

// ExpressionStatementSyntax:
//   Expression = InvocationExpressionSyntax

// IExpressionStatementOperation:
//   Operation = IInvocationOperation
```

## ForEachStatementSyntax

**SyntaxKind:** `SyntaxKind.ForEachStatement`
**IOperation:** `IForEachLoopOperation` (`OperationKind.Loop`)

**Key properties:**
- `Type` — the declared element type
- `Identifier` — the loop variable name
- `Expression` — the collection being iterated
- `Statement` — the loop body

```csharp
foreach (var item in collection)
{
    Process(item);
}

// IForEachLoopOperation provides:
//   Collection    : IOperation — the collection
//   LoopControlVariable : IOperation — loop variable reference
//   Body          : IOperation — the loop body
//   Info.ElementType : ITypeSymbol — the element type
```

**Struct-relevant:** iterating over a collection of structs via interface (`IEnumerable<T>`) can cause boxing of the enumerator. Also, the loop variable is a copy -- mutations won't affect the original collection.

## UsingStatementSyntax

**SyntaxKind:** `SyntaxKind.UsingStatement`
**IOperation:** `IUsingOperation` (`OperationKind.Using`)

**Key properties:**
- `Declaration` — `VariableDeclarationSyntax` (for `using var x = ...`)
- `Expression` — expression form (for `using (expr)`)
- `Statement` — the using body

```csharp
using (var stream = File.OpenRead(path))
{
    // ...
}
```

**Struct-relevant:** `ref struct` types with `IDisposable` pattern work with `using` statements. A non-readonly struct in a `using` gets a defensive copy when `Dispose()` is called.

## BlockSyntax

**SyntaxKind:** `SyntaxKind.Block`
**IOperation:** `IBlockOperation` (`OperationKind.Block`)

**Key properties:**
- `Statements` — `SyntaxList<StatementSyntax>`
- `OpenBraceToken`, `CloseBraceToken`

```csharp
{
    var x = 1;
    Console.WriteLine(x);
}

// IBlockOperation provides:
//   Operations : ImmutableArray<IOperation> — all operations in the block
//   Locals     : ImmutableArray<ILocalSymbol> — locals declared in the block
```

## IOperation Quick Reference for Statements

| OperationKind | Interface | Typical Source |
|---------------|-----------|----------------|
| `VariableDeclarationGroup` | `IVariableDeclarationGroupOperation` | Local variable declarations |
| `VariableDeclaration` | `IVariableDeclarationOperation` | Type + declarators group |
| `VariableDeclarator` | `IVariableDeclaratorOperation` | Individual variable + initializer |
| `Return` | `IReturnOperation` | `return expr;` |
| `ExpressionStatement` | `IExpressionStatementOperation` | Expression used as statement |
| `Loop` | `IForEachLoopOperation` / `IForLoopOperation` | `foreach` / `for` |
| `Using` | `IUsingOperation` | `using` statement |
| `Block` | `IBlockOperation` | `{ ... }` blocks |

## When to Use in an Analyzer

- **Local variable analysis:** use `IVariableDeclaratorOperation` to inspect initializers and types
- **Foreach over structs:** register for `OperationKind.Loop`, check if the collection's enumerator type is a struct, check if it gets boxed
- **Using + struct disposal:** register for `OperationKind.Using`, check for defensive copies on `Dispose()`
- **Method body traversal:** use `IBlockOperation.Operations` to walk all operations in a method body
- Prefer registering for specific `OperationKind` values rather than walking syntax trees manually
