# SPIRE015 — Exhaustive Enum Switch

**Date**: 2026-03-21
**Status**: Approved
**Rule ID**: SPIRE015
**Severity**: Error
**Category**: Correctness

## Summary

Enforces exhaustive handling of all named members in switch statements and switch expressions when the switched enum type is marked `[EnforceExhaustiveness]`. Reports a single diagnostic per switch listing all unhandled members. Includes a code fix that adds missing case arms.

## Attribute

`EnforceExhaustivenessAttribute` inherits `EnforceInitializationAttribute`. Defined in `src/Spire.Core/EnforceExhaustivenessAttribute.cs`.

`EnforceInitializationAttribute` currently has `[AttributeUsage(AttributeTargets.Struct | AttributeTargets.Class)]`. Must be updated to include `AttributeTargets.Enum` so that `[EnforceExhaustiveness]` (and `[EnforceInitialization]` directly) can be applied to enums.

`EnforceExhaustivenessAttribute` declares `[AttributeUsage(AttributeTargets.Enum)]` — it only makes sense on enums, even though the base class allows structs/classes.

Consequence of inheritance: all existing SPIRE001-008 rules that check for `[EnforceInitialization]` will also fire on `[EnforceExhaustiveness]` enums (where applicable — e.g., SPIRE003 flags `default(MyEnum)`). This is intentional.

### Utility Refactor

New static method in `Spire.Analyzers.Utils`:

```csharp
/// <summary>
/// Returns true if <paramref name="type"/> has an attribute whose class is
/// or inherits from <paramref name="attributeType"/>.
/// </summary>
public static bool HasOrInheritsAttribute(ITypeSymbol type, INamedTypeSymbol attributeType)
```

Visibility is `public` — this is a cross-project utility in `Spire.Analyzers.Utils`, used by both `Spire.Analyzers` and potentially `Spire.SourceGenerators`.

For each attribute on `type`, walks the attribute's class base type chain checking for a match against `attributeType`. This is NOT a check on the symbol's own type hierarchy — it walks the *attribute class* hierarchy.

SPIRE001-008 analyzers refactored to use this instead of `SymbolEqualityComparer.Equals` on attribute classes.

## Trigger

Any switch statement (`ISwitchOperation`) or switch expression (`ISwitchExpressionOperation`) where the switched value's type is an enum marked with `[EnforceExhaustiveness]`.

**Nullable unwrap**: If the switched value's type is `Nullable<T>`, unwrap to get `T` before checking for the attribute. This means `switch (nullableEnumVal)` triggers the rule if the underlying enum has `[EnforceExhaustiveness]`. The null case is not the analyzer's concern.

## Detection Algorithm

1. Resolve all named members of the enum type via `INamedTypeSymbol.GetMembers()` filtered to `IFieldSymbol` with `HasConstantValue`.
2. Build coverage groups: members sharing the same underlying value form one group. Covering any member in a group covers all of them.
3. Walk the switch arms/cases, extracting covered enum values from patterns.
4. Compute uncovered members. Report one diagnostic if any are missing.
5. Populate diagnostic properties: `"MissingMembers"` key with comma-separated member names. The code fix reads this.

### Pattern Analysis — Switch Statements

Switch statements use `ISwitchOperation` → `ISwitchCaseOperation` → `ICaseClauseOperation`. Each case clause is one of:

- `ISingleValueCaseClauseOperation` — traditional `case MyEnum.A:` label. Extract the constant value from `Value`.
- `IPatternCaseClauseOperation` — pattern-based case (C# 9+). Extract values recursively from the `Pattern` (see pattern table below).
- `IDefaultCaseClauseOperation` — `default:`. No coverage.

A single `ISwitchCaseOperation` can have multiple clauses (fallthrough: `case A: case B: /* body */`). All clauses contribute coverage.

### Pattern Analysis — Switch Expressions

Switch expressions use `ISwitchExpressionOperation` → `ISwitchExpressionArmOperation` → `IPatternOperation`. Extract values recursively from the arm's `Pattern`.

### Pattern Extraction (recursive)

| Pattern Type | Coverage |
|---------|----------|
| `IConstantPatternOperation` | Covers the constant's enum value |
| `IBinaryPatternOperation` (Or) | Recurse both sides, union results |
| `IBinaryPatternOperation` (And) | Recurse both sides, intersect results |
| `INegatedPatternOperation` | Covers all members except those in the inner pattern |
| `IDiscardPatternOperation` | No coverage |
| `IDeclarationPatternOperation` (`var x`) | No coverage |
| `IRelationalPatternOperation` | No coverage |
| Numeric literal constant (not an enum member) | No coverage |

### When Guards

Arms/cases with a `when` guard do NOT count as coverage (MVP). A `case MyEnum.A when condition:` does not cover A.

This means any switch that uses `when` guards on all arms for a given member will report that member as uncovered. This is an accepted trade-off — future semantic analysis will address this.

### Flags Enum Coverage

For enums marked `[Flags]`, when a case handles value V, all named members M where `(M & V) == M` are covered. This means:

- `case Flags._3:` where `_3 = _1 | _2` covers `_3`, `_1`, `_2`, and any zero-valued member
- Zero-valued members (`None = 0`) are covered by any enum constant case (0 is a bitwise subset of all values). Catch-all arms (`default`/`_`/`var`) do NOT cover zero-valued members.
- Individual members do NOT cover composites — handling `_1` and `_2` separately does not cover `_3`
- Enums with only zero-valued members: any single case covers all of them

Note: this is intentional design-level coverage semantics, not runtime matching semantics. The user explicitly requested this behavior.

### Alias Handling

Members with identical underlying values form a single coverage unit. If `A = 0` and `B = 0`, covering either covers both. The diagnostic message uses the first declared member name for each uncovered group.

## Diagnostic

- **ID**: `SPIRE015`
- **Message**: `"Switch on '{0}' does not handle member(s): {1}"`
- **Location**: The `switch` keyword token
- **Severity**: Error
- **Category**: Correctness
- **One diagnostic per switch**, listing all missing members comma-separated
- **Properties**: `ImmutableDictionary` with key `"MissingMembers"` → comma-separated member names (code fix reads this)

## Code Fix

`CodeFixProvider` in `src/Spire.CodeFixes/` — new file `AddMissingEnumArmsCodeFix.cs` (separate from existing `AddMissingArmsCodeFix.cs` which handles SPIRE009 union variants).

- **Fixable diagnostic**: `SPIRE015`
- **Title**: "Add missing enum cases"
- Reads `diagnostic.Properties["MissingMembers"]` to get missing member names
- For switch statements: inserts `case EnumName.Member: break;` for each missing member before the `default` arm (or at the end if no default). Uses syntax tree (`SwitchStatementSyntax`).
- For switch expressions: inserts `EnumName.Member => throw new System.NotImplementedException(),` for each missing member before the discard arm (or at the end). Uses syntax tree (`SwitchExpressionSyntax`).

## What Is NOT Flagged

- Switch on enum without `[EnforceExhaustiveness]`
- Switch where all named members are covered (even if `default` is also present)
- Switch on non-enum types
- Cast away from enum: `switch ((int)val)` — no longer enum type
- Empty enum (no named members)

## Edge Cases

| Case | Behavior |
|------|----------|
| Empty enum | No diagnostic |
| Nullable enum `MyEnum?` | Unwrap `Nullable<T>`, check enum member coverage |
| Generic type parameter | Out of scope — need concrete type at the switch site |
| Nested enum | Supported — attribute on the enum type regardless of nesting |
| `when` guard on case | Does not count as coverage (MVP) |
| Switch with only `default` | Reports all members as missing |
| All members covered + `default` present | No diagnostic |
| Partial coverage + `default` | Reports uncovered members |
| `goto case` / `goto default` | Ignored for coverage (control flow, not pattern matching) |
| All `[Flags]` members zero-valued | Any single case covers all of them |
| Cross-assembly enum with `[EnforceExhaustiveness]` | Works — `GetTypeByMetadataName` resolves referenced assemblies |

## Implementation Notes

### Registration

```csharp
context.RegisterCompilationStartAction(compilationCtx =>
{
    var enforceType = compilationCtx.Compilation.GetTypeByMetadataName(
        "Spire.Analyzers.EnforceExhaustivenessAttribute");
    if (enforceType is null) return;

    compilationCtx.RegisterOperationAction(ctx => AnalyzeSwitch(ctx, enforceType),
        OperationKind.Switch, OperationKind.SwitchExpression);
});
```
