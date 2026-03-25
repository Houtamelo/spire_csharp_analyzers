# Plan 016: SPIRE003 — default(T) where T is [EnforceInitialization]

**Status**: Ready for implementation
**Goal**: Flag all uses of `default(T)` or `default` literal where T is a `[EnforceInitialization]` struct with instance fields.

---

## Overview

**ID**: SPIRE003
**Title**: default(T) where T is a [EnforceInitialization] struct produces an uninitialized instance
**Category**: Correctness
**Default severity**: Error
**Message format**: `default value of struct '{0}' marked with [EnforceInitialization] bypasses required initialization`
**Enabled by default**: Yes

### What this rule does

Structs marked with `[EnforceInitialization]` require explicit initialization — using `default(T)` or the `default` literal produces a zeroed-out instance, defeating the purpose of the attribute. This rule flags every expression where a `[EnforceInitialization]` struct is produced via `default`, including explicit `default(T)`, implicit `default` literals in assignments, returns, arguments, ternaries, and optional parameter defaults.

---

## What SPIRE003 Detects

### Flagged

| Code | Why |
|------|-----|
| `default(Config)` | Explicit default expression with [EnforceInitialization] type |
| `Config c = default;` | Default literal assigned to [EnforceInitialization] variable |
| `return default;` (return type is [EnforceInitialization]) | Default literal returned as [EnforceInitialization] type |
| `void Foo(Config c = default)` — the `default` in the parameter | Default literal in optional parameter of [EnforceInitialization] type |
| `Foo(default)` where parameter is [EnforceInitialization] | Default literal passed as argument to [EnforceInitialization] parameter |
| `condition ? validConfig : default` — the `default` | Default literal branch in conditional expression |
| `config ?? default` — the `default` (for `Config?`) | Default literal in null-coalescing with [EnforceInitialization] target |
| `Config c; c = default;` | Default literal in assignment to [EnforceInitialization] variable |
| `Config[] arr = { default, default };` | Default literal in collection/array initializer of [EnforceInitialization] element type |
| `(Config a, Config b) = (default, default);` — each `default` | Default in tuple deconstruction targeting [EnforceInitialization] |

### NOT flagged

| Code | Why |
|------|-----|
| `default(PlainStruct)` | PlainStruct is not [EnforceInitialization] |
| `int x = default;` | Built-in types are never [EnforceInitialization] |
| `c == default` | Equality comparison, not creation |
| `c != default` | Inequality comparison, not creation |
| `default(EmptyEnforceInitializationStruct)` | Fieldless [EnforceInitialization] type — default is the only value |
| `EqualityComparer<Config>.Default` | Property access, not default expression |
| `default(T)` in generic method without concrete [EnforceInitialization] type | Generic type parameter — concrete type unknown at definition site |

### Out of scope

| Code | Why excluded |
|------|-------------|
| `new Config()` | Parameterless constructor on structs — separate rule (SPIRE004+) |
| `Activator.CreateInstance<Config>()` | Runtime creation — separate rule if needed |
| `FormatterServices.GetUninitializedObject(typeof(Config))` | Reflection — separate rule if needed |

---

## Implementation Notes

### Attribute/marker type

None — reuses existing `EnforceInitializationAttribute` from `src/Spire.Analyzers/EnforceInitializationAttribute.cs`.

### Detection strategy

- **IOperation kind(s)**: `OperationKind.DefaultValue` — covers both `default(T)` and `default` literal
- **Key checks**:
  1. Get the type of the `IDefaultValueOperation` — this is the type being defaulted
  2. Check if the type is a struct marked with `[EnforceInitialization]`
  3. Check if the type has at least one instance field (skip fieldless types — those are SPIRE002's concern)
  4. Check the operation is NOT inside an equality/inequality comparison (`IOperation.Parent` is `IBinaryOperation` with `BinaryOperatorKind.Equals` or `BinaryOperatorKind.NotEquals`)
  5. Report diagnostic at the `default` keyword location
- **Use `CompilationStartAction`**: Yes — resolve `EnforceInitializationAttribute` via `GetTypeByMetadataName("Spire.Analyzers.EnforceInitializationAttribute")` once per compilation

### Comparison exclusion logic

Walk up to the parent operation. If the parent is `IBinaryOperation` with operator kind `Equals` or `NotEquals`, skip. This covers `x == default` and `x != default`. Also check for `IIsPatternOperation` if patterns are relevant (e.g., `x is default` — but `default` isn't a valid constant pattern for structs, so likely not needed).

### File list

| File | Purpose | Created by |
|------|---------|------------|
| `src/Spire.Analyzers/Rules/SPIRE003DefaultOfEnforceInitializationStructAnalyzer.cs` | The analyzer | Implementer |
| `tests/Spire.Analyzers.Tests/SPIRE003/SPIRE003Tests.cs` | Test runner | Lead |
| `tests/Spire.Analyzers.Tests/SPIRE003/cases/_shared.cs` | Shared preamble | Lead |
| `tests/Spire.Analyzers.Tests/SPIRE003/cases/*.cs` | Test case files | test-case-writer |
| `docs/rules/SPIRE003.md` | Rule documentation | Lead |
