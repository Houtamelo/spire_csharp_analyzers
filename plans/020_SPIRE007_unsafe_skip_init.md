# Plan 020: SPIRE007 — Unsafe.SkipInit on [MustBeInit] struct

**Status**: Ready for implementation
**Goal**: Flag `Unsafe.SkipInit<T>(out T)` when T is a `[MustBeInit]` struct with fields.

## Overview

**ID**: SPIRE007
**Title**: Unsafe.SkipInit on [MustBeInit] struct leaves it uninitialized
**Category**: Correctness
**Default severity**: Error
**Message format**: `Unsafe.SkipInit leaves struct '{0}' marked with [MustBeInit] completely uninitialized`
**Enabled by default**: Yes

### What this rule does

`Unsafe.SkipInit<T>(out T)` in `System.Runtime.CompilerServices` bypasses zero-initialization
entirely. The output variable contains whatever was in memory. For `[MustBeInit]` structs this
is worse than `default` — the instance is garbage.

## What SPIRE007 Detects

### Flagged

| Code | Why |
|------|-----|
| `Unsafe.SkipInit(out MustInitStruct s)` | Leaves [MustBeInit] struct uninitialized |
| Same with record struct, readonly struct | Still [MustBeInit] with fields |
| Any expression context (loop, lambda, async, nested type) | Context irrelevant |

### NOT flagged

| Code | Why |
|------|-----|
| `Unsafe.SkipInit(out PlainStruct s)` | Not [MustBeInit] |
| `Unsafe.SkipInit(out int x)` | Built-in type |
| `Unsafe.SkipInit(out EmptyMustInitStruct s)` | Fieldless [MustBeInit] |
| `Unsafe.SkipInit<T>(out T x)` in generic method | T is unresolved type param |
| `Unsafe.SkipInit(out string s)` | Reference type |

### Out of scope

| Code | Why excluded |
|------|-------------|
| Other `Unsafe.*` methods | Different semantics |

## Implementation Notes

### Attribute/marker type

None — reuses existing `MustBeInitAttribute`.

### Detection strategy

- **IOperation kind(s)**: `OperationKind.Invocation` (`IInvocationOperation`)
- **Key checks**:
  1. Method is `Unsafe.SkipInit` (containing type = `System.Runtime.CompilerServices.Unsafe`, name = `SkipInit`)
  2. Method has exactly 1 type argument
  3. Extract T from `method.TypeArguments[0]`
  4. T is `INamedTypeSymbol` with `TypeKind.Struct`
  5. Has `[MustBeInit]` attribute
  6. Has at least one instance field
- **Use `CompilationStartAction`**: Yes — resolve `System.Runtime.CompilerServices.Unsafe`
  and `MustBeInitAttribute` once per compilation

### File list

| File | Purpose | Created by |
|------|---------|------------|
| `src/Spire.Analyzers/Rules/SPIRE007UnsafeSkipInitOfMustBeInitStructAnalyzer.cs` | The analyzer | Implementer |
| `tests/Spire.Analyzers.Tests/SPIRE007/SPIRE007Tests.cs` | Test runner | Lead |
| `tests/Spire.Analyzers.Tests/SPIRE007/cases/_shared.cs` | Shared preamble | Lead |
| `tests/Spire.Analyzers.Tests/SPIRE007/cases/*.cs` | Test case files | test-case-writer |
| `docs/rules/SPIRE007.md` | Rule documentation | Lead |
