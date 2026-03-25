# Plan 018: SPIRE005 — Activator.CreateInstance on [EnforceInitialization] struct

**Status**: Ready for implementation
**Goal**: Flag `Activator.CreateInstance` calls that produce default instances of `[EnforceInitialization]` structs.

## Overview

**ID**: SPIRE005
**Title**: Activator.CreateInstance on [EnforceInitialization] struct produces a default instance
**Category**: Correctness
**Default severity**: Error
**Message format**: `Activator.CreateInstance produces a default instance of struct '{0}' marked with [EnforceInitialization]`
**Enabled by default**: Yes

### What this rule does

`Activator.CreateInstance` produces default (zeroed) instances of value types when called
without constructor arguments. For structs marked with `[EnforceInitialization]`, this bypasses the
required initialization the attribute enforces.

The rule covers all `Activator.CreateInstance` overloads where the target type is statically
resolvable via `typeof(T)` or a generic type argument, and where no constructor arguments
are provided (or args are provably null/empty).

## What SPIRE005 Detects

### Flagged

| Code | Why |
|------|-----|
| `Activator.CreateInstance<T>()` | Generic overload, always produces default for structs |
| `Activator.CreateInstance(typeof(T))` | Type-only overload, no ctor args |
| `Activator.CreateInstance(typeof(T), true)` | nonPublic overload, still no ctor args |
| `Activator.CreateInstance(typeof(T), false)` | nonPublic overload, still no ctor args |
| `Activator.CreateInstance(typeof(T), (object[])null)` | params overload with null args |
| `Activator.CreateInstance(typeof(T), new object[0])` | params overload with empty args |
| `Activator.CreateInstance(typeof(T), Array.Empty<object>())` | params overload with empty args |
| `Activator.CreateInstance(typeof(T), new object[] { })` | params overload with empty initializer |
| `Activator.CreateInstance(typeof(T), null, null)` | args+activation overload with null args |
| `Activator.CreateInstance(typeof(T), new object[0], null)` | args+activation overload with empty args |
| `Activator.CreateInstance(typeof(T), flags, null, null, null)` | BindingFlags overload with null args |
| `Activator.CreateInstance(typeof(T), flags, null, new object[0], null)` | BindingFlags overload with empty args |
| `Activator.CreateInstance(typeof(T), flags, null, null, null, null)` | Full overload with null args |
| `Activator.CreateInstance(typeof(T), flags, null, new object[0], null, null)` | Full overload with empty args |

Where T is any `[EnforceInitialization]` struct with instance fields.

### NOT flagged

| Code | Why |
|------|-----|
| `Activator.CreateInstance<PlainStruct>()` | Not `[EnforceInitialization]` |
| `Activator.CreateInstance<EmptyEnforceInitialization>()` | Fieldless `[EnforceInitialization]` struct |
| `Activator.CreateInstance<int>()` | Built-in type, not `[EnforceInitialization]` |
| `Activator.CreateInstance<string>()` | Reference type |
| `Activator.CreateInstance<SomeClass>()` | Reference type |
| `Activator.CreateInstance(typeof(T), 42)` | Has constructor arguments |
| `Activator.CreateInstance(typeof(T), new object[] { 42 })` | Has constructor arguments |
| `Activator.CreateInstance(typeof(T), 42, "hello")` | Has constructor arguments |
| `Activator.CreateInstance(typeof(T), flags, null, new object[] { 42 }, null)` | Has constructor arguments |
| `Activator.CreateInstance(typeVariable)` | Type not statically resolvable |
| `Activator.CreateInstance(GetType())` | Type not statically resolvable |

### Out of scope

| Code | Why excluded |
|------|-------------|
| `Activator.CreateInstance("asm", "typeName")` | String-based, type not resolvable at compile time |
| `Activator.CreateInstance("asm", "typeName", ...)` | String-based overloads |
| `Activator.CreateInstanceFrom(...)` | File-based, type not resolvable at compile time |
| Generic type parameter: `Create<T>() where T : new()` | T unknown at definition site |

## Implementation Notes

### Attribute/marker type

None — reuses existing `EnforceInitializationAttribute`.

### Detection strategy

- **IOperation kind(s)**: `OperationKind.Invocation` (`IInvocationOperation`)
- **Key checks**:
  1. Method is `Activator.CreateInstance` (any overload)
  2. Resolve target struct type:
     - Generic overload: type argument `T` from `CreateInstance<T>()`
     - Type overloads: first argument must be `typeof(X)` expression (`ITypeOfOperation`)
       — if not a typeof, skip (type not statically resolvable)
  3. Resolved type is a struct with `[EnforceInitialization]` and has instance fields
  4. For overloads with `object[] args` parameter: args must be provably null or empty
     - Literal null
     - `new object[0]`, `new object[] { }` (IArrayCreationOperation with 0 length / empty initializer)
     - `Array.Empty<object>()` (known method)
     - If args is a variable or other expression → don't flag (can't determine emptiness)
- **Use `CompilationStartAction`**: Yes — resolve `System.Activator`, `EnforceInitializationAttribute`,
  and `System.Array` (for `Array.Empty`) once per compilation

### Overload discrimination

The analyzer doesn't need to identify which exact overload is called. Instead:

1. If `CreateInstance<T>()` (generic, no params) → always flag if T qualifies
2. If first param is `Type`:
   - If only 1 param (Type-only) → always flag if type qualifies
   - If 2 params and second is `bool` → always flag (nonPublic overload)
   - If any param named `args` (type `object[]`) exists → check if null/empty
   - If the params overload is called with actual varargs → NOT null/empty, don't flag

### Null/empty detection for args parameter

"Clearly null or empty" means:
- `null` literal
- `default` literal or `default(object[])`
- `new object[0]`
- `new object[] { }`
- `Array.Empty<object>()`

Anything else (variables, method returns, etc.) → assume non-empty, don't flag.

### File list

| File | Purpose | Created by |
|------|---------|------------|
| `src/Spire.Analyzers/Rules/SPIRE005ActivatorCreateInstanceOfEnforceInitializationStructAnalyzer.cs` | The analyzer | Implementer |
| `tests/Spire.Analyzers.Tests/SPIRE005/SPIRE005Tests.cs` | Test runner | Lead |
| `tests/Spire.Analyzers.Tests/SPIRE005/cases/_shared.cs` | Shared preamble | Lead |
| `tests/Spire.Analyzers.Tests/SPIRE005/cases/*.cs` | Test case files | test-case-writer |
| `docs/rules/SPIRE005.md` | Rule documentation | Lead |
