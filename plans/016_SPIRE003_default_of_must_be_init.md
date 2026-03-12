# Plan 016: SPIRE003 — default(T) where T is [MustBeInit]

**Status**: Ready for implementation
**Goal**: Flag all uses of `default(T)` or `default` literal where T is a `[MustBeInit]` struct with instance fields.

---

## Overview

**ID**: SPIRE003
**Title**: default(T) where T is a [MustBeInit] struct produces an uninitialized instance
**Category**: Correctness
**Default severity**: Error
**Message format**: `default value of struct '{0}' marked with [MustBeInit] bypasses required initialization`
**Enabled by default**: Yes

### What this rule does

Structs marked with `[MustBeInit]` require explicit initialization — using `default(T)` or the `default` literal produces a zeroed-out instance, defeating the purpose of the attribute. This rule flags every expression where a `[MustBeInit]` struct is produced via `default`, including explicit `default(T)`, implicit `default` literals in assignments, returns, arguments, ternaries, and optional parameter defaults.

---

## What SPIRE003 Detects

### Flagged

| Code | Why |
|------|-----|
| `default(Config)` | Explicit default expression with [MustBeInit] type |
| `Config c = default;` | Default literal assigned to [MustBeInit] variable |
| `return default;` (return type is [MustBeInit]) | Default literal returned as [MustBeInit] type |
| `void Foo(Config c = default)` — the `default` in the parameter | Default literal in optional parameter of [MustBeInit] type |
| `Foo(default)` where parameter is [MustBeInit] | Default literal passed as argument to [MustBeInit] parameter |
| `condition ? validConfig : default` — the `default` | Default literal branch in conditional expression |
| `config ?? default` — the `default` (for `Config?`) | Default literal in null-coalescing with [MustBeInit] target |
| `Config c; c = default;` | Default literal in assignment to [MustBeInit] variable |
| `Config[] arr = { default, default };` | Default literal in collection/array initializer of [MustBeInit] element type |
| `(Config a, Config b) = (default, default);` — each `default` | Default in tuple deconstruction targeting [MustBeInit] |

### NOT flagged

| Code | Why |
|------|-----|
| `default(PlainStruct)` | PlainStruct is not [MustBeInit] |
| `int x = default;` | Built-in types are never [MustBeInit] |
| `c == default` | Equality comparison, not creation |
| `c != default` | Inequality comparison, not creation |
| `default(EmptyMustInitStruct)` | Fieldless [MustBeInit] type — default is the only value |
| `EqualityComparer<Config>.Default` | Property access, not default expression |
| `default(T)` in generic method without concrete [MustBeInit] type | Generic type parameter — concrete type unknown at definition site |

### Out of scope

| Code | Why excluded |
|------|-------------|
| `new Config()` | Parameterless constructor on structs — separate rule (SPIRE004+) |
| `Activator.CreateInstance<Config>()` | Runtime creation — separate rule if needed |
| `FormatterServices.GetUninitializedObject(typeof(Config))` | Reflection — separate rule if needed |

---

## Implementation Notes

### Attribute/marker type

None — reuses existing `MustBeInitAttribute` from `src/Spire.Analyzers/MustBeInitAttribute.cs`.

### Detection strategy

- **IOperation kind(s)**: `OperationKind.DefaultValue` — covers both `default(T)` and `default` literal
- **Key checks**:
  1. Get the type of the `IDefaultValueOperation` — this is the type being defaulted
  2. Check if the type is a struct marked with `[MustBeInit]`
  3. Check if the type has at least one instance field (skip fieldless types — those are SPIRE002's concern)
  4. Check the operation is NOT inside an equality/inequality comparison (`IOperation.Parent` is `IBinaryOperation` with `BinaryOperatorKind.Equals` or `BinaryOperatorKind.NotEquals`)
  5. Report diagnostic at the `default` keyword location
- **Use `CompilationStartAction`**: Yes — resolve `MustBeInitAttribute` via `GetTypeByMetadataName("Spire.Analyzers.MustBeInitAttribute")` once per compilation

### Comparison exclusion logic

Walk up to the parent operation. If the parent is `IBinaryOperation` with operator kind `Equals` or `NotEquals`, skip. This covers `x == default` and `x != default`. Also check for `IIsPatternOperation` if patterns are relevant (e.g., `x is default` — but `default` isn't a valid constant pattern for structs, so likely not needed).

### File list

| File | Purpose | Created by |
|------|---------|------------|
| `src/Spire.Analyzers/Rules/SPIRE003DefaultOfMustBeInitStructAnalyzer.cs` | The analyzer | Implementer |
| `tests/Spire.Analyzers.Tests/SPIRE003/SPIRE003Tests.cs` | Test runner | Lead |
| `tests/Spire.Analyzers.Tests/SPIRE003/cases/_shared.cs` | Shared preamble | Lead |
| `tests/Spire.Analyzers.Tests/SPIRE003/cases/*.cs` | Test case files | test-case-writer |
| `docs/rules/SPIRE003.md` | Rule documentation | Lead |
