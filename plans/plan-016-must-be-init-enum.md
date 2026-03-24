# Plan 016: SPIRE016 — Invalid value of [MustBeInit] enum

**Status**: Ready for implementation
**Goal**: Flag operations that produce enum values not corresponding to named members when the enum is marked `[MustBeInit]`.

## Overview

**ID**: SPIRE016
**Title**: Operation may produce invalid value of [MustBeInit] enum
**Category**: Correctness
**Default severity**: Error
**Message format**: `{0} may produce a value of enum '{1}' marked with [MustBeInit] that is not a named member`
**Enabled by default**: Yes

### What this rule does

C# enums can hold any integer value, including ones that don't correspond to named members. When `[MustBeInit]` is applied to an enum, it signals that only named members are valid values. This rule flags operations that may produce unnamed/invalid enum values.

Key semantic: whether `default` (value 0) is valid depends on whether the enum has a zero-valued named member. If it does, default produces a valid variant and is not flagged.

## What SPIRE016 Detects

### Flagged

| Code | Why |
|------|-----|
| `default(NoZeroEnum)` | No zero-valued member — default produces unnamed value |
| `default` literal targeting `NoZeroEnum` | Same as above — return, assignment, argument, ternary |
| `(MarkedEnum)42` where 42 is not a named member | Known invalid cast value |
| `(MarkedEnum)variable` where value unknown | Cannot verify value is a named member |
| `(MarkedEnum)(someInt + 1)` non-constant expression | Cannot verify at compile time |
| `Unsafe.SkipInit<MarkedEnum>(out var e)` | Garbage data — always invalid regardless of zero member |
| `new MarkedEnum[n]` where no zero member | Array elements are default(0), which is unnamed |
| `Array.Clear` on `MarkedEnum[]` where no zero member | Resets to default(0) |
| `Activator.CreateInstance<MarkedEnum>()` where no zero member | Produces default(0) |

### NOT flagged

| Code | Why |
|------|-----|
| `default(WithZeroEnum)` | Zero-valued member exists — default is a valid variant |
| `default` literal targeting `WithZeroEnum` | Same as above |
| `MarkedEnum.SomeMember` | Direct named member access — always valid |
| `(MarkedEnum)1` where 1 is a named member | Known valid cast value |
| `default(PlainEnum)` | Enum not marked `[MustBeInit]` |
| `(PlainEnum)999` | Enum not marked `[MustBeInit]` |
| `someMarkedEnum == default` | Equality comparison — detection pattern, not creation |
| `new WithZeroEnum[n]` | Zero member exists, array elements are valid |
| `Enum.Parse<MarkedEnum>("Active")` | String-based resolution — runtime validated |
| `Enum.TryParse` | Returns false for invalid values — self-validating |
| `Enum.IsDefined` check before cast | Already guarded |

### Out of scope

| Code | Why excluded |
|------|-------------|
| `[Flags]` composite values like `Read \| Write` | Composite flag values are intentionally unnamed — `[MustBeInit]` + `[Flags]` interaction needs separate design |
| Generic `T` where T could be a `[MustBeInit]` enum | Requires flow analysis through generic constraints — too complex for v1 |
| `Enum.ToObject(typeof(MarkedEnum), value)` | Rare runtime API — low priority |
| Serialization/deserialization | External data can produce any value — out of analyzer scope |

## Implementation Notes

### Attribute/marker type (if needed)

None — reuses existing `[MustBeInit]` attribute from `Spire.Core`.

### Detection strategy

- **CompilationStartAction**: Resolve `Spire.MustBeInitAttribute`, `System.Runtime.CompilerServices.Unsafe`, `System.Runtime.CompilerServices.RuntimeHelpers`. Cache the set of zero-valued-member enums.
- **IOperation kinds**:
  - `OperationKind.DefaultValue` — `default(T)` and `default` literal
  - `OperationKind.Conversion` — integer-to-enum casts
  - `OperationKind.Invocation` — `Unsafe.SkipInit`, `RuntimeHelpers.GetUninitializedObject`, `Activator.CreateInstance`, `Array.Clear`, etc.
  - `OperationKind.ArrayCreation` — `new MarkedEnum[n]`
- **Key checks**:
  1. Is the target type an enum with `[MustBeInit]`?
  2. Does the enum have a zero-valued named member? (precompute per enum type)
  3. For casts: is the source value a compile-time constant matching a named member?
  4. For unsafe ops: always flag regardless of zero member
- **Use `CompilationStartAction`**: Yes — resolve attribute type, build enum metadata cache

### File list

| File | Purpose | Created by |
|------|---------|------------|
| `src/Spire.Analyzers/Rules/SPIRE016InvalidMustBeInitEnumValueAnalyzer.cs` | The analyzer | Implementer |
| `tests/Spire.Analyzers.Tests/SPIRE016/SPIRE016Tests.cs` | Test runner | Lead |
| `tests/Spire.Analyzers.Tests/SPIRE016/cases/_shared.cs` | Shared preamble | Lead |
| `tests/Spire.Analyzers.Tests/SPIRE016/cases/*.cs` | Test case files | test-case-writer |
| `docs/rules/SPIRE016.md` | Rule documentation | Lead |
