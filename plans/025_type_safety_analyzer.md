# 025 — Type Safety Analyzer: Implementation Plan

**Goal:** Detect field type mismatches and field count errors in discriminated union pattern matching.

**Architecture:** DiagnosticAnalyzer in `Spire.SourceGenerators` inspects deconstruction patterns on `[DiscriminatedUnion]` struct types. Uses `IRecursivePatternOperation.DeconstructSymbol` to get the Deconstruct method signature, then compares parameter types against pattern types. Struct unions only — record/class unions use native C# type checking.

**Tech Stack:** Roslyn 5.0.0 IOperation API, DiagnosticAnalyzer, netstandard2.0

**Design spec:** `plans/022_discriminated_unions_design.md` lines 369-382

---

## Diagnostics

| ID | Severity | Scenario | Message |
|---|---|---|---|
| SPIRE011 | Error | Field type mismatch | "Variant '{0}' field {1} is '{2}', not '{3}'" |
| SPIRE012 | Error | Field count mismatch | "Variant '{0}' has {1} field(s), not {2}" |

Rules:
- Exact type matching — no implicit conversions (`double` matches `double` only)
- Untyped discard `_` (`IDiscardPatternOperation`) — always OK, no type to check
- Typed discard `double _` (`IDeclarationPatternOperation` with MatchedType) — type IS checked
- Only applies to struct union Deconstruct patterns (record/class get native C# checking)
- Checked in any pattern context (switch, if/is, etc.) — not limited to switch

## Detection Algorithm

### Roslyn API (verified via Sherlock)

- `IRecursivePatternOperation.DeconstructSymbol` → `ISymbol` (the Deconstruct method)
- `IRecursivePatternOperation.DeconstructionSubpatterns` → `ImmutableArray<IPatternOperation>`
- Each subpattern is either:
  - `IDiscardPatternOperation` — no properties, always OK
  - `IDeclarationPatternOperation` — `.MatchedType` is the declared type (even for `double _`)
  - `IConstantPatternOperation` — `.Value` has type info
  - `IRecursivePatternOperation` — nested pattern (has `.MatchedType`)

### Algorithm

1. Register on `OperationKind.RecursivePattern` (NOT switch — catch patterns everywhere)
2. Check if the pattern's `DeconstructSymbol` belongs to a `[DiscriminatedUnion]` struct type
3. Get the Deconstruct method parameters (skip first `out Kind` param)
4. Compare subpattern count vs parameter count (field count check)
5. For each subpattern at index i:
   - Skip `IDiscardPatternOperation` (untyped)
   - For `IDeclarationPatternOperation`: compare `.MatchedType` against `param[i+1].Type`
   - For `IConstantPatternOperation`: compare `.Value.Type` against `param[i+1].Type`
   - Use `SymbolEqualityComparer.Default` — exact match only

### Identifying the variant

The first deconstruction subpattern (index 0) is the Kind constant. Subpatterns 1..N are the field values. So:
- Field count = `DeconstructionSubpatterns.Length - 1`
- Expected field count = `DeconstructMethod.Parameters.Length - 1` (minus the Kind param)
- Field type at index i = `DeconstructMethod.Parameters[i + 1].Type`

To get the variant name: resolve the Kind constant from subpattern[0] (same as PatternAnalyzer does).

## Scope

Struct unions only. The analyzer fires on `IRecursivePatternOperation` where `DeconstructSymbol` is a method on a `[DiscriminatedUnion]` struct.

Does NOT fire on:
- Record/class union patterns (C# handles natively)
- Property patterns `{ tag: Kind.Circle }` (no field type checking needed — property types are already enforced by C#)
- Non-union deconstruction patterns

## File Structure

```
src/Spire.SourceGenerators/
    Analyzers/
        TypeSafetyAnalyzer.cs       -- DiagnosticAnalyzer for SPIRE011/SPIRE012
    AnalyzerDescriptors.cs          -- Add SPIRE011/SPIRE012

tests/Spire.SourceGenerators.Tests/
    TypeSafety/
        TypeSafetyTests.cs          -- Test runner
        cases/
            TypeMismatch_SingleField.cs
            TypeMismatch_TypedDiscard.cs
            FieldCount_TooMany.cs
            FieldCount_TooFew.cs
            Pass_CorrectTypes.cs
            Pass_UntypedDiscard.cs
            Pass_MixedDiscardAndTyped.cs
            ...
```

## Tasks

### Task 1: Descriptors + TypeSafetyAnalyzer + test infrastructure

- Add SPIRE011/SPIRE012 to `AnalyzerDescriptors.cs`
- Create `TypeSafetyAnalyzer.cs` — register on `OperationKind.RecursivePattern`
- Create `TypeSafety/TypeSafetyTests.cs` test runner (inherits GeneratorAnalyzerTestBase)
- Update `AnalyzerReleases.Unshipped.md`

### Task 2: Implement type checking + test cases

- Implement the detection logic in TypeSafetyAnalyzer
- Create ~10 test cases covering all scenarios from design spec

Test cases:

**should_fail:**
- `TypeMismatch_SingleField.cs` — `(Kind.Circle, string bad)` when field is double
- `TypeMismatch_TypedDiscard.cs` — `(Kind.Circle, string _)` — typed discard with wrong type
- `TypeMismatch_MultiField.cs` — type error in 2nd field of multi-field variant
- `FieldCount_TooMany.cs` — `(Kind.Circle, _, _)` when Circle has 1 field
- `FieldCount_TooFew.cs` — `(Kind.Rectangle, var w)` when Rectangle has 2 fields

**should_pass:**
- `Pass_CorrectTypes.cs` — all types match exactly
- `Pass_UntypedDiscard.cs` — `(Kind.Circle, _)` — untyped discard always OK
- `Pass_TypedDiscardCorrect.cs` — `(Kind.Circle, double _)` — typed discard with correct type
- `Pass_ObjectDeconstruct.cs` — shared-arity `(Kind.Circle, object? val)` — object? matches object?
- `Pass_VarBinding.cs` — `(Kind.Circle, var r)` — var infers type from Deconstruct param
- `Pass_BoxedTupleLayout.cs` — BoxedTuple union (all object?) — no false positives
- `Pass_FieldlessInMixedUnion.cs` — `(Kind.Eof, _)` in union with both fieldless and non-fieldless variants
- `Pass_IfIsPattern.cs` — `if (s is (Shape.Kind.Circle, double r))` — works outside switch

**Implementation notes:**
- Roslyn handles Deconstruct overload resolution — `DeconstructSymbol` reflects the resolved overload
- For shared-arity `object?` Deconstruct, `var x` infers `object?` which trivially passes
- For BoxedTuple, all params are `object?` — type checking is trivially true
- Fieldless variants in shared-arity Deconstruct: `_` matches `object?` (which is null at runtime)

## Execution order

Task 1 → Task 2 (sequential)

## Verification

```bash
dotnet test tests/Spire.SourceGenerators.Tests/
```
