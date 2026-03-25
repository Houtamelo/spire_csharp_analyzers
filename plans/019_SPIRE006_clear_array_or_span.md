# Plan 019: SPIRE006 — Clear on array/span of [EnforceInitialization] struct

**Status**: Ready for implementation
**Goal**: Flag `Array.Clear` and `Span<T>.Clear()` calls that zero elements of `[EnforceInitialization]` structs.

## Overview

**ID**: SPIRE006
**Title**: Clearing array or span of [EnforceInitialization] struct produces default instances
**Category**: Correctness
**Default severity**: Error
**Message format**: `{0} zeros elements of struct '{1}' marked with [EnforceInitialization]`
**Enabled by default**: Yes

### What this rule does

`Array.Clear` and `Span<T>.Clear()` reset elements to `default(T)`. When T is a struct
marked with `[EnforceInitialization]`, this produces uninitialized instances — the same state that
`[EnforceInitialization]` exists to prevent.

## What SPIRE006 Detects

### Flagged

| Code | Why |
|------|-----|
| `Array.Clear(enforceInitializationArray)` | Zeros all elements of [EnforceInitialization] array |
| `Array.Clear(enforceInitializationArray, 0, n)` | Zeros range of [EnforceInitialization] array |
| `span.Clear()` where span is `Span<EnforceInitializationStruct>` | Zeros span elements |
| `enforceInitializationArray.AsSpan().Clear()` | AsSpan returns Span<T>, then Clear zeros it |
| Same patterns with record struct, readonly struct, readonly ref struct | Still [EnforceInitialization] with fields |
| Multidimensional array: `Array.Clear(enforceInitialization2D)` | Element type is still [EnforceInitialization] |

Where T is any `[EnforceInitialization]` struct with instance fields.

### NOT flagged

| Code | Why |
|------|-----|
| `Array.Clear(plainArray)` | Not [EnforceInitialization] |
| `Array.Clear(intArray)` | Built-in type, not [EnforceInitialization] |
| `plainSpan.Clear()` | Not [EnforceInitialization] |
| `Array.Clear(emptyEnforceInitializationArray)` | Fieldless [EnforceInitialization] struct (SPIRE002 territory) |
| `Array.Clear(arrayTypedVar)` where variable is typed `Array` | Can't resolve element type |
| Generic `Span<T>.Clear()` where T is type parameter | Can't resolve at definition site |
| `Array.Clear(classArray)` | Reference type, not a struct |

### Out of scope

| Code | Why excluded |
|------|-------------|
| `List<T>.Clear()` | Removes elements, doesn't zero in-place |
| `Dictionary<K,V>.Clear()` | Same — removes entries |
| `ReadOnlySpan<T>` | No Clear() method |
| `MemoryMarshal` operations | Niche unsafe code |

## Implementation Notes

### Attribute/marker type

None — reuses existing `EnforceInitializationAttribute`.

### Detection strategy

- **IOperation kind(s)**: `OperationKind.Invocation` (`IInvocationOperation`)
- **Key checks**:
  1. **Array.Clear path**:
     - Method is `Array.Clear` (any overload: 1-param or 3-param)
     - First argument's type is `IArrayTypeSymbol` — extract `ElementType`
     - If first arg is typed as `Array` (non-generic), skip (can't resolve element type)
  2. **Span<T>.Clear() path**:
     - Method is `Span<T>.Clear()` (instance method, no params)
     - Extract T from `method.ContainingType.TypeArguments[0]`
  3. **Common checks** (both paths):
     - Resolved element type is a named struct type
     - Has `[EnforceInitialization]` attribute
     - Has at least one instance field
- **Use `CompilationStartAction`**: Yes — resolve `System.Array`, `System.Span<T>`,
  and `EnforceInitializationAttribute` once per compilation

### Element type resolution

For `Array.Clear`:
- `T[]` → `IArrayTypeSymbol.ElementType` is T
- `T[,]` → same, `IArrayTypeSymbol.ElementType` is T
- `Array` (non-generic) → skip, can't determine element type

For `Span<T>.Clear()`:
- `Span<T>` → `INamedTypeSymbol.TypeArguments[0]` is T
- The receiver could be any expression returning `Span<T>` (variable, method call, property)
  — we only care about the type, not the source

### File list

| File | Purpose | Created by |
|------|---------|------------|
| `src/Spire.Analyzers/Rules/SPIRE006ClearOfEnforceInitializationElementsAnalyzer.cs` | The analyzer | Implementer |
| `tests/Spire.Analyzers.Tests/SPIRE006/SPIRE006Tests.cs` | Test runner | Lead |
| `tests/Spire.Analyzers.Tests/SPIRE006/cases/_shared.cs` | Shared preamble | Lead |
| `tests/Spire.Analyzers.Tests/SPIRE006/cases/*.cs` | Test case files | test-case-writer |
| `docs/rules/SPIRE006.md` | Rule documentation | Lead |
