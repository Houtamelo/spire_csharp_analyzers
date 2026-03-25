# Plan 010: SPIRE001 — Array allocation creates default instances of [EnforceInitializationialized] struct

**Status**: Ready for implementation
**Goal**: Flag non-empty array allocations of structs marked with `[EnforceInitializationialized]`, since array allocation fills elements with `default(T)` bypassing any required initialization.

---

## Overview

**ID**: SPIRE001
**Title**: Array allocation creates default instances of [EnforceInitializationialized] struct
**Category**: Correctness
**Default severity**: Error
**Message format**: `Array allocation creates default instance(s) of '{0}', which is marked with [EnforceInitializationialized]`
**Enabled by default**: Yes

### What this rule does

Array allocation in C# always fills elements with `default(T)`. For value types, this means zeroed memory — no constructor is called, even if the struct defines a parameterless constructor (C# 10+). This rule detects when a non-empty array is allocated with an element type that is a struct marked with `[EnforceInitializationialized]`, since such structs require explicit initialization.

---

## The attribute: `[EnforceInitializationialized]`

A marker attribute applied to struct declarations. It declares that the struct must always be explicitly initialized — using `default` or any mechanism that produces zeroed-out instances is incorrect.

```csharp
namespace Spire.Analyzers;

[AttributeUsage(AttributeTargets.Struct, Inherited = false, AllowMultiple = false)]
public sealed class EnforceInitializationializedAttribute : Attribute { }
```

This attribute ships in the analyzer package itself (`src/Spire.Analyzers/`).

---

## What SPIRE001 Detects

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
| `new UnmarkedStruct[5]` | Struct without the attribute — no diagnostic |

### Out of scope for v1

| Code | Why excluded |
|------|-------------|
| `Array.CreateInstance(typeof(MarkedStruct), 5)` | Requires invocation analysis — future rule |
| `GC.AllocateArray<MarkedStruct>(5)` | Requires invocation analysis — future rule |
| `GC.AllocateUninitializedArray<MarkedStruct>(5)` | Same — future rule |
| `Enumerable.Repeat(default(MarkedStruct), 5)` | LINQ, impractical to detect |
| `new T[5]` where T is a generic type parameter | Cannot resolve to concrete struct at definition site |

---

## Implementation Notes

### Attribute/marker type

`EnforceInitializationializedAttribute` — new file `src/Spire.Analyzers/EnforceInitializationializedAttribute.cs`.

### Detection strategy

- **IOperation kind(s)**: `OperationKind.ArrayCreation`
- **Key checks**:
  1. Element type is a struct with `[EnforceInitializationialized]`
  2. No initializer with elements (i.e. `Initializer` is null or has zero `ElementValues`)
  3. No dimension is a compile-time constant zero
- **Use `CompilationStartAction`**: Yes — resolve `Spire.Analyzers.EnforceInitializationializedAttribute` once per compilation

### File list

| File | Purpose | Created by |
|------|---------|------------|
| `src/Spire.Analyzers/EnforceInitializationializedAttribute.cs` | The attribute definition | Lead |
| `src/Spire.Analyzers/Descriptors.cs` | Add `SPIRE001_ArrayOfEnforceInitializationializedStruct` | Lead |
| `tests/Spire.Analyzers.Tests/SPIRE001/SPIRE001Tests.cs` | Test runner | Lead |
| `tests/Spire.Analyzers.Tests/SPIRE001/cases/_shared.cs` | Shared preamble | Lead |
| `tests/Spire.Analyzers.Tests/SPIRE001/cases/*.cs` | Test case files | test-case-writer |
| `src/Spire.Analyzers/Analyzers/SPIRE001ArrayOfEnforceInitializationializedStructAnalyzer.cs` | The analyzer | Implementer |
| `docs/rules/SPIRE001.md` | Rule documentation | Lead |

---

## Implementation Order (TDD)

1. Create `EnforceInitializationializedAttribute.cs`
2. Add `SPIRE001` descriptor to `Descriptors.cs`
3. Scaffold test folder, `_shared.cs`, and test runner `SPIRE001Tests.cs`
4. Spawn test-case-writer agent to populate case files
5. Run `dotnet test` — detection tests FAIL, false-positive tests PASS
6. Spawn analyzer-implementer agent
7. Run `dotnet test` — ALL tests pass
8. Create `docs/rules/SPIRE001.md`
