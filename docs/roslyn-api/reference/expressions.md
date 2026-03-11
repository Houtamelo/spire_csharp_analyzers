# Expression Nodes

Roslyn 4.12.0 syntax nodes for expressions. All inherit from `ExpressionSyntax`. For analyzer work, prefer the **IOperation API** over syntax where possible -- it normalizes syntax variations and provides semantic information directly.

## InvocationExpressionSyntax

**SyntaxKind:** `SyntaxKind.InvocationExpression`
**IOperation:** `IInvocationOperation` (`OperationKind.Invocation`)

**Key properties:**
- `Expression` — the method being called (usually `MemberAccessExpressionSyntax` or `IdentifierNameSyntax`)
- `ArgumentList` — `ArgumentListSyntax` with the call arguments

```csharp
list.Add(item);
// Expression  = MemberAccessExpressionSyntax("list.Add")
// ArgumentList = (item)

// IInvocationOperation provides:
//   TargetMethod : IMethodSymbol — resolved method symbol
//   Instance     : IOperation    — the receiver (list)
//   Arguments    : ImmutableArray<IArgumentOperation>
```

## MemberAccessExpressionSyntax

**SyntaxKind:** `SyntaxKind.SimpleMemberAccessExpression`
**IOperation:** `IMemberReferenceOperation` (field, property, method, event)

**Key properties:**
- `Expression` — the left side (receiver)
- `Name` — the right side (member name, `SimpleNameSyntax`)
- `OperatorToken` — the `.` token

```csharp
vector.Length
// Expression = IdentifierNameSyntax("vector")
// Name       = IdentifierNameSyntax("Length")
```

## ObjectCreationExpressionSyntax

**SyntaxKind:** `SyntaxKind.ObjectCreationExpression`
**IOperation:** `IObjectCreationOperation` (`OperationKind.ObjectCreation`)

**Key properties:**
- `Type` — the type being constructed
- `ArgumentList` — constructor arguments
- `Initializer` — object/collection initializer

```csharp
new Vector3(1, 2, 3)
// Type         = IdentifierNameSyntax("Vector3")
// ArgumentList = (1, 2, 3)

// IObjectCreationOperation provides:
//   Constructor : IMethodSymbol — resolved constructor
//   Type        : ITypeSymbol   — the constructed type
//   Initializer : IObjectOrCollectionInitializerOperation
```

Note: `ImplicitObjectCreationExpressionSyntax` (`new(1, 2, 3)`) is a separate syntax kind but produces the same `IObjectCreationOperation`.

## DefaultExpressionSyntax

**SyntaxKind:** `SyntaxKind.DefaultExpression`
**IOperation:** `IDefaultValueOperation` (`OperationKind.DefaultValue`)

**Key properties:**
- `Type` — the type in `default(T)`

```csharp
default(Vector3)
// Type = IdentifierNameSyntax("Vector3")
```

Note: `default` without a type (`SyntaxKind.DefaultLiteralExpression`) also produces `IDefaultValueOperation`.

## AssignmentExpressionSyntax

**SyntaxKinds:** `SyntaxKind.SimpleAssignmentExpression`, `SyntaxKind.AddAssignmentExpression`, etc.
**IOperation:** `ISimpleAssignmentOperation` (`OperationKind.SimpleAssignment`), `ICompoundAssignmentOperation` (`OperationKind.CompoundAssignment`)

**Key properties:**
- `Left` — the assignment target
- `Right` — the value being assigned
- `OperatorToken` — `=`, `+=`, `-=`, etc.

```csharp
position.X = 10;
// Left  = MemberAccessExpressionSyntax("position.X")
// Right = LiteralExpressionSyntax(10)
```

## CastExpressionSyntax

**SyntaxKind:** `SyntaxKind.CastExpression`
**IOperation:** `IConversionOperation` (`OperationKind.Conversion`)

**Key properties:**
- `Type` — the target type
- `Expression` — the expression being cast

```csharp
(IEquatable<Vector3>)myStruct
// Type       = GenericNameSyntax("IEquatable<Vector3>")
// Expression = IdentifierNameSyntax("myStruct")

// IConversionOperation provides:
//   Operand    : IOperation
//   Type       : ITypeSymbol — result type
//   Conversion : Conversion  — boxing, unboxing, implicit, explicit, etc.
```

**Struct-relevant:** casting a struct to an interface causes boxing. Detect via `IConversionOperation` where `Conversion.IsBoxing == true`.

## BinaryExpressionSyntax

**SyntaxKinds:** `SyntaxKind.EqualsExpression`, `SyntaxKind.AddExpression`, etc.
**IOperation:** `IBinaryOperation` (`OperationKind.Binary`)

**Key properties:**
- `Left` — left operand
- `Right` — right operand
- `OperatorToken` — the operator

```csharp
a == b
// Left  = IdentifierNameSyntax("a")
// Right = IdentifierNameSyntax("b")
```

## IdentifierNameSyntax

**SyntaxKind:** `SyntaxKind.IdentifierName`
**IOperation:** varies — `ILocalReferenceOperation`, `IParameterReferenceOperation`, `IFieldReferenceOperation`, etc.

**Key properties:**
- `Identifier` — the name token

```csharp
myVariable
// Identifier = "myVariable"

// Use SemanticModel.GetSymbolInfo(identifierName) to resolve what it refers to.
// The IOperation kind depends on what the identifier resolves to:
//   Local variable  -> ILocalReferenceOperation  (OperationKind.LocalReference)
//   Parameter       -> IParameterReferenceOperation (OperationKind.ParameterReference)
//   Field           -> IFieldReferenceOperation  (OperationKind.FieldReference)
```

## ArrayCreationExpressionSyntax

**SyntaxKind:** `SyntaxKind.ArrayCreationExpression`
**IOperation:** `IArrayCreationOperation` (`OperationKind.ArrayCreation`)

**Key properties:**
- `Type` — `ArrayTypeSyntax` including rank specifiers
- `Initializer` — `InitializerExpressionSyntax` for array initializers

```csharp
new Vector3[10]
new[] { v1, v2, v3 }  // ImplicitArrayCreationExpressionSyntax
```

## IOperation Quick Reference for Expressions

| OperationKind | Interface | Typical Source |
|---------------|-----------|----------------|
| `Invocation` | `IInvocationOperation` | Method calls |
| `FieldReference` | `IFieldReferenceOperation` | Field access |
| `PropertyReference` | `IPropertyReferenceOperation` | Property access |
| `ObjectCreation` | `IObjectCreationOperation` | `new T(...)` |
| `Conversion` | `IConversionOperation` | Casts, implicit conversions, boxing |
| `SimpleAssignment` | `ISimpleAssignmentOperation` | `x = y` |
| `CompoundAssignment` | `ICompoundAssignmentOperation` | `x += y` |
| `LocalReference` | `ILocalReferenceOperation` | Local variable use |
| `ParameterReference` | `IParameterReferenceOperation` | Parameter use |
| `DefaultValue` | `IDefaultValueOperation` | `default` / `default(T)` |

## When to Use in an Analyzer

- **Boxing detection:** register for `OperationKind.Conversion`, check `IConversionOperation.Conversion.IsBoxing`
- **Method call analysis:** register for `OperationKind.Invocation`, inspect `TargetMethod` receiver type
- **Defensive copy detection:** register for `OperationKind.Invocation` / `OperationKind.PropertyReference`, check if receiver is a readonly field/in parameter of a non-readonly struct
- **Assignment tracking:** register for `OperationKind.SimpleAssignment` to detect field mutations
