# Plan 005: First Rule — RequireInitialization Attribute + SAS001

**Status**: Abandoned
**Goal**: Ship the first version of Spire.Analyzers with one attribute and one rule.

---

## Overview

### The attribute: `[RequireInitialization]`

A marker attribute applied to struct declarations. It declares that the struct must always be explicitly initialized — using `default` or any mechanism that produces zeroed-out instances is incorrect.

```csharp
namespace Spire.Analyzers;

[AttributeUsage(AttributeTargets.Struct, Inherited = false, AllowMultiple = false)]
public sealed class RequireInitializationAttribute : Attribute { }
```

This attribute ships in the analyzer package itself (inside `src/Spire.Analyzers/`). Consumers reference the package and both the attribute and analyzer are available.

### The rule: SAS001 — Non-empty array creates default instances of `[RequireInitialization]` struct

**ID**: `SAS001`
**Title**: Array allocation creates default instances of a struct marked with `[RequireInitialization]`
**Category**: Correctness
**Default severity**: Error
**Message format**: `Array allocation creates {0} default instance(s) of '{1}', which is marked with [RequireInitialization]`
**Enabled by default**: Yes

---

## What SAS001 Detects

### Flagged (array elements are default-initialized)

| Code | Why |
|------|-----|
| `new MarkedStruct[5]` | 5 default instances created |
| `new MarkedStruct[n]` | Unknown count, but likely non-zero — flag conservatively |
| `new MarkedStruct[3, 4]` | 12 default instances (multidimensional) |
| `stackalloc MarkedStruct[5]` | 5 default instances on the stack |

### NOT flagged (no default-initialized elements)

| Code | Why |
|------|-----|
| `new MarkedStruct[0]` | Zero-length — no instances created |
| `new MarkedStruct[0, 5]` | Any dimension is zero — total is 0 |
| `new MarkedStruct[] { a, b }` | All elements explicitly initialized |
| `new MarkedStruct[2] { a, b }` | Sized + fully initialized (compiler enforces all elements) |
| `new[] { a, b }` | Implicitly-typed, always has initializer |
| `new MarkedStruct[5][]` | Jagged outer — elements are `null` (reference type `MarkedStruct[]`), not `default(MarkedStruct)` |
| `default(MarkedStruct[])` | Produces `null`, not an array |
| `Array.Empty<MarkedStruct>()` | Empty array, zero elements |
| `MarkedStruct[] arr = []` | Collection expression, empty |
| `MarkedStruct[] arr = [a, b]` | Collection expression, explicitly initialized |
| `stackalloc MarkedStruct[] { a, b }` | Stackalloc with initializer |

### Out of scope for v1

| Code | Why excluded |
|------|-------------|
| `Array.CreateInstance(typeof(MarkedStruct), 5)` | Requires invocation analysis — add in a future rule |
| `GC.AllocateArray<MarkedStruct>(5)` | Requires invocation analysis — add in a future rule |
| `GC.AllocateUninitializedArray<MarkedStruct>(5)` | Same — future rule |
| `Enumerable.Repeat(default(MarkedStruct), 5)` | LINQ, impractical to detect |
| `new T[5]` where T is a generic type parameter | Cannot resolve to concrete struct at definition site |

---

## Implementation Spec

### File list

| File | Purpose | Created by |
|------|---------|------------|
| `src/Spire.Analyzers/RequireInitializationAttribute.cs` | The attribute definition | Lead |
| `src/Spire.Analyzers/Descriptors.cs` | Central descriptor registry (add `SAS001`) | Lead |
| `tests/Spire.Analyzers.Tests/SAS001/SAS001Tests.cs` | Test runner | Lead |
| `tests/Spire.Analyzers.Tests/SAS001/cases/_shared.cs` | Shared preamble | Lead |
| `tests/Spire.Analyzers.Tests/SAS001/cases/*.cs` | Test case files | Lead |
| `src/Spire.Analyzers/Analyzers/SAS001ArrayOfNonDefaultableStructAnalyzer.cs` | The analyzer | Implementer |
| `docs/rules/SAS001.md` | Rule documentation | Lead |

### Attribute implementation

```csharp
// src/Spire.Analyzers/RequireInitializationAttribute.cs
using System;

namespace Spire.Analyzers;

/// <summary>
/// Marks a struct as requiring explicit initialization.
/// Creating default instances (e.g. via <c>new T[n]</c>) will produce analyzer error SAS001.
/// </summary>
[AttributeUsage(AttributeTargets.Struct, Inherited = false, AllowMultiple = false)]
public sealed class RequireInitializationAttribute : Attribute { }
```

### Descriptor

```csharp
// In Descriptors.cs
public static readonly DiagnosticDescriptor SAS001_ArrayOfNonDefaultableStruct = new(
    id: "SAS001",
    title: "Array allocation creates default instances of [RequireInitialization] struct",
    messageFormat: "Array allocation creates default instance(s) of '{0}', which is marked with [RequireInitialization]",
    category: "Correctness",
    defaultSeverity: DiagnosticSeverity.Error,
    isEnabledByDefault: true,
    description: "Allocating a non-empty array of a struct marked with [RequireInitialization] creates default (zeroed) instances, bypassing any required initialization logic.",
    helpLinkUri: "https://github.com/TODO/docs/rules/SAS001.md"
);
```

### Analyzer implementation

Use `CompilationStartAction` to resolve the attribute type once, then register `OperationAction` for `OperationKind.ArrayCreation`.

```csharp
// src/Spire.Analyzers/Analyzers/SAS001ArrayOfNonDefaultableStructAnalyzer.cs
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Spire.Analyzers.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class SAS001ArrayOfNonDefaultableStructAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(Descriptors.SAS001_ArrayOfNonDefaultableStruct);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(compilationContext =>
        {
            // Resolve attribute type once per compilation
            var attributeType = compilationContext.Compilation
                .GetTypeByMetadataName("Spire.Analyzers.RequireInitializationAttribute");

            if (attributeType is null)
                return; // Attribute not referenced — nothing to analyze

            compilationContext.RegisterOperationAction(
                ctx => AnalyzeArrayCreation(ctx, attributeType),
                OperationKind.ArrayCreation);
        });
    }

    private static void AnalyzeArrayCreation(
        OperationAnalysisContext context,
        INamedTypeSymbol attributeType)
    {
        var operation = (IArrayCreationOperation)context.Operation;

        // Skip if there's an initializer with elements (explicitly initialized)
        if (operation.Initializer is { ElementValues.Length: > 0 })
            return;

        // Get the array's element type
        if (operation.Type is not IArrayTypeSymbol arrayType)
            return;

        var elementType = arrayType.ElementType;

        // Must be a struct (not enum, not type parameter)
        if (elementType.TypeKind != TypeKind.Struct)
            return;

        // Must have [RequireInitialization]
        if (!HasAttribute(elementType, attributeType))
            return;

        // Check if any dimension is definitely zero — if so, skip
        if (IsDefinitelyZeroLength(operation))
            return;

        // Report diagnostic
        context.ReportDiagnostic(Diagnostic.Create(
            Descriptors.SAS001_ArrayOfNonDefaultableStruct,
            operation.Syntax.GetLocation(),
            elementType.Name));
    }

    private static bool HasAttribute(ITypeSymbol type, INamedTypeSymbol attributeType)
    {
        foreach (var attr in type.GetAttributes())
        {
            if (SymbolEqualityComparer.Default.Equals(attr.AttributeClass, attributeType))
                return true;
        }
        return false;
    }

    private static bool IsDefinitelyZeroLength(IArrayCreationOperation operation)
    {
        foreach (var dimSize in operation.DimensionSizes)
        {
            if (dimSize.ConstantValue.HasValue &&
                dimSize.ConstantValue.Value is int intVal &&
                intVal == 0)
            {
                return true; // Any dimension is zero → total elements = 0
            }
        }
        return false;
    }
}
```

#### Key design decisions in the analyzer

1. **`OperationKind.ArrayCreation`** — Covers `new T[n]`, `new T[n,m]`, and `stackalloc T[n]` through the IOperation model. Higher-level than syntax, gives us `DimensionSizes` and `Initializer` with semantic info already resolved.

2. **`CompilationStartAction` + `GetTypeByMetadataName`** — Resolves the attribute type once per compilation. If the attribute isn't referenced, the analyzer short-circuits entirely (zero overhead for projects that don't use it).

3. **`SymbolEqualityComparer.Default`** — Standard pattern for comparing Roslyn symbols. Handles cross-assembly resolution correctly.

4. **Conservative flagging for non-constant sizes** — `new MarkedStruct[n]` is flagged even if `n` could be zero at runtime. Only skip when size is a **compile-time constant zero**. This matches user intent: if you write `new T[n]`, you expect `n` to be non-zero.

5. **`EnableConcurrentExecution()`** — Required for performance. The analyzer is stateless per-invocation (all state is captured in the closure from `CompilationStartAction`).

6. **`ConfigureGeneratedCodeAnalysis(None)`** — Don't analyze generated code (e.g., source generators, designers).

#### What this implementation does NOT handle (out of scope for v1)

- `Array.CreateInstance(typeof(MarkedStruct), n)` — would need `OperationKind.Invocation`
- `GC.AllocateArray<MarkedStruct>(n)` — same
- Generic `new T[n]` where `T` is a type parameter — would need type parameter constraint analysis
- `Enumerable.Repeat(default(MarkedStruct), n)` — impractical

---

## Test Plan

### Test structure: `tests/Spire.Analyzers.Tests/SAS001/`

Tests use file-based cases (per plan 009). Each case is a `.cs` file in `SAS001/cases/`.

#### Shared preamble: `cases/_shared.cs`

```csharp
using System;
using Spire.Analyzers;

[RequireInitialization]
public struct MarkedStruct
{
    public int Value;
    public MarkedStruct(int value) { Value = value; }
}

public struct UnmarkedStruct
{
    public int Value;
}
```

#### Cases that SHOULD trigger SAS001 (detection — use `{|SAS001:code|}` markup)

| Case file | Code | Why |
|-----------|------|-----|
| `NonEmptyArray.cs` | `new MarkedStruct[5]` | 5 default instances created |
| `VariableSizedArray.cs` | `new MarkedStruct[n]` | Non-constant size, flag conservatively |
| `MultidimensionalArray.cs` | `new MarkedStruct[3, 4]` | 12 default instances |
| `ConstantNonZeroSize.cs` | `new MarkedStruct[Size]` (Size=10) | Constant non-zero |
| `Stackalloc.cs` | `stackalloc MarkedStruct[5]` | 5 default instances on stack |

#### Cases that should NOT trigger SAS001 (false-positive — no markup)

| Case file | Code | Why |
|-----------|------|-----|
| `ZeroLengthArray.cs` | `new MarkedStruct[0]` | Zero-length, no instances |
| `ConstantZeroSize.cs` | `new MarkedStruct[Size]` (Size=0) | Constant zero |
| `ArrayWithInitializer.cs` | `new MarkedStruct[] { a, b }` | All elements explicitly initialized |
| `SizedArrayWithInitializer.cs` | `new MarkedStruct[2] { a, b }` | Sized + fully initialized |
| `ImplicitlyTypedArray.cs` | `new[] { a }` | Always has initializer |
| `MultidimZeroDimension.cs` | `new MarkedStruct[0, 5]` | Any dimension zero → total 0 |
| `JaggedArray.cs` | `new MarkedStruct[5][]` | Elements are null refs, not default structs |
| `UnmarkedStruct.cs` | `new UnmarkedStruct[5]` | No attribute |
| `DefaultArrayRef.cs` | `MarkedStruct[] arr = default` | Produces null, not an array |
| `ArrayEmpty.cs` | `Array.Empty<MarkedStruct>()` | No elements |
| `EmptyCollectionExpression.cs` | `MarkedStruct[] arr = []` | Empty |
| `NonEmptyCollectionExpression.cs` | `MarkedStruct[] arr = [a, b]` | Explicitly initialized |
| `StackallocWithInitializer.cs` | `stackalloc MarkedStruct[] { a }` | Initializer present |

#### Test runner: `SAS001Tests.cs`

Uses `[Theory]` + `[InlineData("CaseName")]` with `TestCaseLoader.LoadCase("SAS001", caseName)`.
Two methods: `ShouldReportSAS001` (detection) and `ShouldNotReportSAS001` (false-positive).

---

## Implementation Order (TDD — per plan 009)

1. Create `RequireInitializationAttribute.cs`
2. Add `SAS001` descriptor to `Descriptors.cs`
3. Create test folder `SAS001/`, shared preamble `cases/_shared.cs`, case files, and test runner `SAS001Tests.cs`
4. Run `dotnet test` — detection tests FAIL (no analyzer yet), false-positive tests PASS
5. Spawn implementer to create `SAS001ArrayOfNonDefaultableStructAnalyzer.cs`
6. Run `dotnet test` — ALL tests pass
7. Create `docs/rules/SAS001.md`

---

## docs/rules/SAS001.md Template

```markdown
# SAS001: Array allocation creates default instances of [RequireInitialization] struct

| Property    | Value           |
|-------------|-----------------|
| **ID**      | SAS001          |
| **Category**| Correctness     |
| **Severity**| Error           |
| **Enabled** | Yes             |

## Description

Allocating a non-empty array of a struct marked with `[RequireInitialization]` creates
default (zeroed) instances, bypassing any required initialization logic.

Array allocation in C# always fills elements with `default(T)`. For value types, this
means zeroed memory — no constructor is called, even if the struct defines a parameterless
constructor (C# 10+).

## Examples

### Violating code

\```csharp
[RequireInitialization]
public struct UserId
{
    public int Id;
    public UserId(int id) { Id = id; }
}

var users = new UserId[10]; // SAS001: creates 10 zeroed UserId instances
\```

### Compliant code

\```csharp
// Option 1: Use an explicitly initialized array
var users = new UserId[] { new UserId(1), new UserId(2) };

// Option 2: Use a collection expression
UserId[] users = [new UserId(1), new UserId(2)];

// Option 3: Zero-length array (no default instances)
var users = Array.Empty<UserId>();
\```

## When to Suppress

Suppress this warning if you intentionally need a buffer of default-initialized structs
and will fill them before use (e.g., pooled arrays, interop buffers).

\```csharp
#pragma warning disable SAS001
var buffer = new UserId[capacity];
#pragma warning restore SAS001
\```
```
