# Plan 019: SPIRE006 — Clear on array/span of [MustBeInit] struct

**Status**: Ready for implementation
**Goal**: Flag `Array.Clear` and `Span<T>.Clear()` calls that zero elements of `[MustBeInit]` structs.

## Overview

**ID**: SPIRE006
**Title**: Clearing array or span of [MustBeInit] struct produces default instances
**Category**: Correctness
**Default severity**: Error
**Message format**: `{0} zeros elements of struct '{1}' marked with [MustBeInit]`
**Enabled by default**: Yes

### What this rule does

`Array.Clear` and `Span<T>.Clear()` reset elements to `default(T)`. When T is a struct
marked with `[MustBeInit]`, this produces uninitialized instances — the same state that
`[MustBeInit]` exists to prevent.

## What SPIRE006 Detects

### Flagged

| Code | Why |
|------|-----|
| `Array.Clear(mustInitArray)` | Zeros all elements of [MustBeInit] array |
| `Array.Clear(mustInitArray, 0, n)` | Zeros range of [MustBeInit] array |
| `span.Clear()` where span is `Span<MustInitStruct>` | Zeros span elements |
| `mustInitArray.AsSpan().Clear()` | AsSpan returns Span<T>, then Clear zeros it |
| Same patterns with record struct, readonly struct, readonly ref struct | Still [MustBeInit] with fields |
| Multidimensional array: `Array.Clear(mustInit2D)` | Element type is still [MustBeInit] |

Where T is any `[MustBeInit]` struct with instance fields.

### NOT flagged

| Code | Why |
|------|-----|
| `Array.Clear(plainArray)` | Not [MustBeInit] |
| `Array.Clear(intArray)` | Built-in type, not [MustBeInit] |
| `plainSpan.Clear()` | Not [MustBeInit] |
| `Array.Clear(emptyMustInitArray)` | Fieldless [MustBeInit] struct (SPIRE002 territory) |
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

None — reuses existing `MustBeInitAttribute`.

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
     - Has `[MustBeInit]` attribute
     - Has at least one instance field
- **Use `CompilationStartAction`**: Yes — resolve `System.Array`, `System.Span<T>`,
  and `MustBeInitAttribute` once per compilation

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
| `src/Spire.Analyzers/Rules/SPIRE006ClearOfMustBeInitElementsAnalyzer.cs` | The analyzer | Implementer |
| `tests/Spire.Analyzers.Tests/SPIRE006/SPIRE006Tests.cs` | Test runner | Lead |
| `tests/Spire.Analyzers.Tests/SPIRE006/cases/_shared.cs` | Shared preamble | Lead |
| `tests/Spire.Analyzers.Tests/SPIRE006/cases/*.cs` | Test case files | test-case-writer |
| `docs/rules/SPIRE006.md` | Rule documentation | Lead |
