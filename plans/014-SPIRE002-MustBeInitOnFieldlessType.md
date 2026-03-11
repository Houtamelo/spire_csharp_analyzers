# Plan 014: SPIRE002 — [MustBeInit] on fieldless type has no effect

**Status**: Ready for implementation
**Goal**: Warn when `[MustBeInit]` is applied to a type that has no instance fields, since the attribute serves no purpose.

---

## Overview

**ID**: SPIRE002
**Title**: [MustBeInit] on fieldless type has no effect
**Category**: Correctness
**Default severity**: Warning
**Message format**: `[MustBeInit] on type '{0}' has no effect because it has no instance fields`
**Enabled by default**: Yes

### What this rule does

Detects when the `[MustBeInit]` attribute is applied to a struct that has no instance fields. Since a fieldless struct has only one possible value (the default), it is always considered "initialized" — the attribute does nothing. This helps users avoid false confidence that the attribute is providing protection.

**Key C# semantics**: Auto-properties (`int Value { get; set; }`) generate compiler-synthesized backing fields, which ARE visible as `IFieldSymbol` in `GetMembers()`. Non-auto properties (computed properties like `int Value => 42`) do NOT generate fields.

---

## What SPIRE002 Detects

### Flagged

| Code | Why |
|------|-----|
| `[MustBeInit] struct Empty { }` | No members at all — zero fields |
| `[MustBeInit] record struct Empty;` | Empty record struct — zero fields |
| `[MustBeInit] struct S { int Value => 42; }` | Non-auto property — no backing field |
| `[MustBeInit] struct S { static int X; }` | Only static field — no instance fields |
| `[MustBeInit] struct S { const int X = 1; }` | Only constants — no instance fields |
| `[MustBeInit] struct S { void M() {} }` | Only methods — no fields |

### NOT flagged

| Code | Why |
|------|-----|
| `[MustBeInit] struct S { int X; }` | Has explicit instance field |
| `[MustBeInit] struct S { int X { get; set; } }` | Auto-property generates backing field |
| `[MustBeInit] record struct S(int X);` | Positional param generates backing field |
| `struct S { }` | No `[MustBeInit]` attribute — not our concern |

### Out of scope

| Code | Why excluded |
|------|-------------|
| `[MustBeInit] class C { }` | Attribute targets structs only (`AttributeTargets.Struct`) |

---

## Implementation Notes

### Attribute/marker type

None — reuses existing `MustBeInitAttribute`.

### Detection strategy

- **Registration**: `RegisterSymbolAction(SymbolKind.NamedType)`
- **Key checks**:
  1. Type has `[MustBeInit]` attribute
  2. Type has zero instance fields (`GetMembers().OfType<IFieldSymbol>().Any(f => !f.IsStatic)` is false)
- **Use `CompilationStartAction`**: Yes — resolve `MustBeInitAttribute` once per compilation
- **Diagnostic location**: The attribute application syntax (`AttributeData.ApplicationSyntaxReference`)

### File list

| File | Purpose | Created by |
|------|---------|------------|
| `src/Spire.Analyzers/Rules/SPIRE002MustBeInitOnFieldlessTypeAnalyzer.cs` | The analyzer | Implementer |
| `tests/Spire.Analyzers.Tests/SPIRE002/SPIRE002Tests.cs` | Test runner | Lead |
| `tests/Spire.Analyzers.Tests/SPIRE002/cases/_shared.cs` | Shared preamble | Lead |
| `tests/Spire.Analyzers.Tests/SPIRE002/cases/*.cs` | Test case files | test-case-writer |
| `docs/rules/SPIRE002.md` | Rule documentation | Lead |
