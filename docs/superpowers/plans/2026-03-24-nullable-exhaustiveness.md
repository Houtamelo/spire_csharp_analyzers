# Nullable Exhaustiveness Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Require `null` arm in exhaustiveness switches when the switched-on type is nullable, and support nullable value type unions (currently ignored by SPIRE009).

**Architecture:** Two independent analyzers need changes: SPIRE015 (enum exhaustiveness in `Spire.Analyzers`) and SPIRE009 (discriminated union exhaustiveness in `Spire.SourceGenerators`). Both get null-coverage tracking alongside their existing variant/member coverage. The CS8509 suppressor also needs updating.

**Tech Stack:** Roslyn IOperation API, `NullableAnnotation`, `Nullable<T>` unwrapping

---

## Nullability Rules

When a switch subject type is nullable, the analyzer must require a `null` arm in addition to all variant/member arms.

**A type is nullable when:**
- **Value types** (structs, enums): wrapped in `Nullable<T>` (i.e., `T?`)
- **Reference types** (record unions): `NullableAnnotation.Annotated` (explicit `T?` in `#nullable enable`) OR `NullableAnnotation.None` (oblivious — `#nullable disable` context, where reference types are implicitly nullable)
- **NOT nullable when:** reference type with `NullableAnnotation.NotAnnotated` (explicit `T` in `#nullable enable`)

**A null arm is any of:**
- Explicit `null` constant pattern (`case null:`, `null =>`)
- `null or X` binary pattern (null on either side of `or`)
- Wildcard: `_`, `var x`, `default:` (these cover everything including null)

**Diagnostic message:** Append `'null'` to the existing missing list. Example: `"Switch on 'Color' does not handle member(s): Green, null"`

No new diagnostic IDs. SPIRE009 and SPIRE015 messages naturally accommodate "null" in the missing list.

---

## File Structure

### Modified Files

| File | Responsibility |
|------|---------------|
| `src/Spire.Analyzers/Rules/SPIRE015ExhaustiveEnumSwitchAnalyzer.cs` | Add `isNullable` tracking, null coverage detection, include "null" in missing members |
| `src/Spire.SourceGenerators/Analyzers/ExhaustivenessAnalyzer.cs` | Unwrap `Nullable<T>`, detect nullable ref types, pass `isNullable` to coverage/reporting |
| `src/Spire.SourceGenerators/Analyzers/PatternAnalyzer.cs` | Add `CoversNull` to `SwitchCoverage`, detect null patterns in `CollectVariants` |
| `src/Spire.SourceGenerators/Analyzers/UnionTypeInfo.cs` | Add `TryCreateWithNullableUnwrap` method |
| `src/Spire.SourceGenerators/Analyzers/CS8509Suppressor.cs` | Use nullable-aware `TryCreate`, gate suppression on null coverage |

### Modified Test Files

| File | Change |
|------|--------|
| `tests/Spire.Analyzers.Tests/SPIRE015/cases/NoReport_SwitchStatement_NullableEnum_AllMembersCovered.cs` | Add `case null:` arm (was passing without it; now null is required) |
| `tests/Spire.SourceGenerators.Tests/Exhaustiveness/cases/Pass_RecordFullCoverage.cs` | Add `#nullable enable` (makes ref type non-nullable, so null not required) |
| `tests/Spire.SourceGenerators.Tests/Exhaustiveness/cases/Pass_RecordGenericFull.cs` | Add `#nullable enable` |
| `tests/Spire.SourceGenerators.Tests/Exhaustiveness/cases/Pass_Suppressor_RecordAllCovered.cs` | Add `#nullable enable` |
| `tests/Spire.SourceGenerators.Tests/Exhaustiveness/cases/Pass_Record_SwitchStmt_Full.cs` | Add `#nullable enable` |

### New Test Files

**SPIRE015 (enum):**

| File | Type | Description |
|------|------|-------------|
| `Detect_SwitchStatement_NullableEnum_MissingNull.cs` | should_fail | All enum members covered, no null, no default |
| `Detect_SwitchExpression_NullableEnum_MissingNull.cs` | should_fail | All enum members covered, no null, no discard |
| `NoReport_SwitchStatement_NullableEnum_ExplicitNull.cs` | should_pass | All members + explicit `case null:` |
| `NoReport_SwitchExpression_NullableEnum_ExplicitNull.cs` | should_pass | All members + explicit `null =>` arm |
| `NoReport_SwitchExpression_NullableEnum_NullOrPattern.cs` | should_pass | All members, last arm is `null or Color.Blue =>` |
| `Detect_SwitchStatement_NullableEnum_MembersAndNullMissing.cs` | should_fail | Nullable enum, some members + null missing |

**SPIRE009 (union) — nullable struct:**

| File | Type | Description |
|------|------|-------------|
| `NullableStruct_MissingVariantAndNull.cs` | should_fail | `Shape?`, one variant + null missing |
| `NullableStruct_MissingNull.cs` | should_fail | `Shape?`, all variants covered, null missing |
| `NullableStruct_AllMissing.cs` | should_fail | `Shape?`, no arms at all |
| `Pass_NullableStruct_AllCoveredWithNull.cs` | should_pass | `Shape?`, all variants + `null =>` |
| `Pass_NullableStruct_WildcardCoversNull.cs` | should_pass | `Shape?`, all variants + `_ =>` |
| `Pass_NullableStruct_VarCoversNull.cs` | should_pass | `Shape?`, all variants + `var x =>` |
| `NullableStruct_SwitchStmt_MissingNull.cs` | should_fail | Switch statement on `Shape?`, all variants, no null/default |
| `NullableStruct_NotNullDoesNotCoverNull.cs` | should_fail | `Shape?`, `not null` arm covers variants but not null |
| `Pass_NullableStruct_SwitchStmt_DefaultCoversNull.cs` | should_pass | Switch statement on `Shape?`, all variants + `default:` |

**SPIRE009 (union) — nullable reference types:**

| File | Type | Description |
|------|------|-------------|
| `NullableRecord_MissingNull.cs` | should_fail | `#nullable enable`, `Option<int>?`, all variants, no null |
| `Pass_NullableRecord_WithNull.cs` | should_pass | `#nullable enable`, `Option<int>?`, all variants + null |
| `Pass_NonNullableRecord_NoNullNeeded.cs` | should_pass | `#nullable enable`, `Option<int>` (not nullable), no null arm needed |
| `ObliviousRecord_MissingNull.cs` | should_fail | No `#nullable`, `Result<T,E>`, no null arm (oblivious = nullable) |
| `Pass_ObliviousRecord_WithNull.cs` | should_pass | No `#nullable`, `Result<T,E>`, all variants + null |

**CS8509 Suppressor:**

| File | Type | Description |
|------|------|-------------|
| `Pass_Suppressor_NullableStruct_AllWithNull.cs` | should_pass | Suppressor fires when all variants + null covered (no CS8509) |

---

## Task 1: SPIRE015 — Null Coverage for Nullable Enums

**Files:**
- Modify: `src/Spire.Analyzers/Rules/SPIRE015ExhaustiveEnumSwitchAnalyzer.cs`
- Modify: `tests/Spire.Analyzers.Tests/SPIRE015/cases/NoReport_SwitchStatement_NullableEnum_AllMembersCovered.cs`
- Create: 6 new test files in `tests/Spire.Analyzers.Tests/SPIRE015/cases/`

### Tests

- [ ] **Step 1: Update existing test — add null arm**

In `NoReport_SwitchStatement_NullableEnum_AllMembersCovered.cs`, add `case null: break;` after the Color.Blue case. This test was passing without null; now it needs null to remain should_pass.

```csharp
//@ should_pass
// Ensure that SPIRE015 is NOT triggered when switching on Color? covers all three members AND null.
public class NoReport_SwitchStatement_NullableEnum_AllMembersCovered
{
    public void Method(Color? value)
    {
        switch (value)
        {
            case Color.Red:
                break;
            case Color.Green:
                break;
            case Color.Blue:
                break;
            case null:
                break;
        }
    }
}
```

- [ ] **Step 2: Write should_fail tests for missing null**

Create `Detect_SwitchStatement_NullableEnum_MissingNull.cs`:

```csharp
//@ should_fail
// All enum members covered on Color? but no null arm and no default — SPIRE015
public class Detect_SwitchStatement_NullableEnum_MissingNull
{
    public void Method(Color? value)
    {
        switch (value) //~ ERROR
        {
            case Color.Red:
                break;
            case Color.Green:
                break;
            case Color.Blue:
                break;
        }
    }
}
```

Create `Detect_SwitchExpression_NullableEnum_MissingNull.cs`:

```csharp
//@ should_fail
// All enum members covered on Color? but no null arm and no discard — SPIRE015
public class Detect_SwitchExpression_NullableEnum_MissingNull
{
    public string Method(Color? value)
    {
        return value switch //~ ERROR
        {
            Color.Red => "red",
            Color.Green => "green",
            Color.Blue => "blue",
        };
    }
}
```

Create `Detect_SwitchStatement_NullableEnum_MembersAndNullMissing.cs`:

```csharp
//@ should_fail
// Color? switch missing both Green member and null — SPIRE015
public class Detect_SwitchStatement_NullableEnum_MembersAndNullMissing
{
    public void Method(Color? value)
    {
        switch (value) //~ ERROR
        {
            case Color.Red:
                break;
            case Color.Blue:
                break;
        }
    }
}
```

- [ ] **Step 3: Write should_pass tests for proper null coverage**

Create `NoReport_SwitchStatement_NullableEnum_ExplicitNull.cs`:

```csharp
//@ should_pass
// All enum members + explicit null arm on Color? — no diagnostic
public class NoReport_SwitchStatement_NullableEnum_ExplicitNull
{
    public void Method(Color? value)
    {
        switch (value)
        {
            case null:
                break;
            case Color.Red:
                break;
            case Color.Green:
                break;
            case Color.Blue:
                break;
        }
    }
}
```

Create `NoReport_SwitchExpression_NullableEnum_ExplicitNull.cs`:

```csharp
//@ should_pass
// All enum members + explicit null arm on Color? — no diagnostic
public class NoReport_SwitchExpression_NullableEnum_ExplicitNull
{
    public string Method(Color? value)
    {
        return value switch
        {
            null => "none",
            Color.Red => "red",
            Color.Green => "green",
            Color.Blue => "blue",
        };
    }
}
```

Create `NoReport_SwitchExpression_NullableEnum_NullOrPattern.cs`:

```csharp
//@ should_pass
// All enum members covered, Blue and null combined via or-pattern — no diagnostic
public class NoReport_SwitchExpression_NullableEnum_NullOrPattern
{
    public string Method(Color? value)
    {
        return value switch
        {
            Color.Red => "red",
            Color.Green => "green",
            null or Color.Blue => "blue-or-null",
        };
    }
}
```

- [ ] **Step 4: Run tests — verify detection tests fail, pass tests pass**

```bash
dotnet test tests/Spire.Analyzers.Tests/ --filter "FullyQualifiedName~SPIRE015"
```

Expected: new `Detect_*` tests FAIL (no null tracking yet), new `NoReport_*` tests PASS (no diagnostic emitted).

### Implementation

- [ ] **Step 5: Implement null coverage tracking in SPIRE015**

In `SPIRE015ExhaustiveEnumSwitchAnalyzer.cs`:

**5a.** Change `GetEnumType` to also return whether the type was nullable:

```csharp
private static (INamedTypeSymbol? enumType, bool isNullable) GetEnumTypeAndNullability(ITypeSymbol? type)
{
    if (type is not INamedTypeSymbol named)
        return (null, false);

    if (named.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T
        && named.TypeArguments.Length == 1)
    {
        return (named.TypeArguments[0] as INamedTypeSymbol, true);
    }

    return (named, false);
}
```

**5b.** Add a helper to check if a pattern covers null:

```csharp
private static bool PatternCoversNull(IPatternOperation pattern)
{
    switch (pattern)
    {
        case IDiscardPatternOperation:
            return true;

        case IDeclarationPatternOperation decl:
            if (decl.Syntax is Microsoft.CodeAnalysis.CSharp.Syntax.VarPatternSyntax)
                return true;
            return decl.MatchesNull;

        case IConstantPatternOperation c:
            return c.Value.ConstantValue is { HasValue: true, Value: null };

        case IBinaryPatternOperation { OperatorKind: BinaryOperatorKind.Or } binary:
            return PatternCoversNull(binary.LeftPattern) || PatternCoversNull(binary.RightPattern);

        default:
            return false;
    }
}
```

**5c.** Add null coverage tracking to `CollectCoverageFromSwitchStatement`. Add a `ref bool coversNull` parameter. Set it when:
- `IDefaultCaseClauseOperation` encountered (default covers null)
- `ISingleValueCaseClauseOperation` with `ConstantValue.Value == null` (no guard)
- `IPatternCaseClauseOperation` where `PatternCoversNull` returns true (no guard)

**5d.** Add null coverage tracking to `CollectCoverageFromSwitchExpression`. Add a `ref bool coversNull` parameter. Set it when:
- An arm's pattern satisfies `PatternCoversNull` (no guard)

**5e.** In `AnalyzeSwitch`, use `GetEnumTypeAndNullability` instead of `GetEnumType`. After coverage collection, if `isNullable && !coversNull`, append `"null"` to `missingGroups`.

- [ ] **Step 6: Run tests — verify all SPIRE015 tests pass**

```bash
dotnet test tests/Spire.Analyzers.Tests/ --filter "FullyQualifiedName~SPIRE015"
```

Expected: ALL pass.

- [ ] **Step 7: Commit**

```bash
git add src/Spire.Analyzers/Rules/SPIRE015ExhaustiveEnumSwitchAnalyzer.cs \
        tests/Spire.Analyzers.Tests/SPIRE015/cases/
git commit -m "feat(SPIRE015): require null arm for nullable enum switches"
```

---

## Task 2: SPIRE009 — Nullable Value Type Union Support

**Files:**
- Modify: `src/Spire.SourceGenerators/Analyzers/UnionTypeInfo.cs`
- Modify: `src/Spire.SourceGenerators/Analyzers/PatternAnalyzer.cs`
- Modify: `src/Spire.SourceGenerators/Analyzers/ExhaustivenessAnalyzer.cs`
- Create: 8 new test files in `tests/Spire.SourceGenerators.Tests/Exhaustiveness/cases/`

### Tests

- [ ] **Step 1: Write should_fail tests for nullable struct unions**

Create `NullableStruct_MissingVariantAndNull.cs`:

```csharp
//@ should_fail
// Shape? switch missing Square variant and null
using Houtamelo.Spire;
namespace TestNs
{
    [DiscriminatedUnion]
    partial struct Shape
    {
        [Variant] public static partial Shape Circle(double radius);
        [Variant] public static partial Shape Square(int sideLength);
    }

    class Consumer
    {
        int Test(Shape? s) => s switch //~ ERROR
        {
            (Shape.Kind.Circle, double r) => 1,
        };
    }
}
```

Create `NullableStruct_MissingNull.cs`:

```csharp
//@ should_fail
// Shape? switch covers all variants but not null
using Houtamelo.Spire;
namespace TestNs
{
    [DiscriminatedUnion]
    partial struct Shape
    {
        [Variant] public static partial Shape Circle(double radius);
        [Variant] public static partial Shape Square(int sideLength);
    }

    class Consumer
    {
        int Test(Shape? s) => s switch //~ ERROR
        {
            (Shape.Kind.Circle, double r) => 1,
            (Shape.Kind.Square, int x) => 2,
        };
    }
}
```

Create `NullableStruct_AllMissing.cs`:

```csharp
//@ should_fail
// Shape? switch with only a non-null constant — everything missing
using Houtamelo.Spire;
namespace TestNs
{
    [DiscriminatedUnion]
    partial struct Shape
    {
        [Variant] public static partial Shape Circle(double radius);
        [Variant] public static partial Shape Square(int sideLength);
    }

    class Consumer
    {
        int Test(Shape? s) => s switch //~ ERROR
        {
            null => 0,
        };
    }
}
```

Create `NullableStruct_SwitchStmt_MissingNull.cs`:

```csharp
//@ should_fail
// Switch statement on Shape? covers all variants but not null and no default
using Houtamelo.Spire;
namespace TestNs
{
    [DiscriminatedUnion]
    partial struct Shape
    {
        [Variant] public static partial Shape Circle(double radius);
        [Variant] public static partial Shape Square(int sideLength);
    }

    class Consumer
    {
        void Test(Shape? s)
        {
            switch (s) //~ ERROR
            {
                case (Shape.Kind.Circle, double r):
                    break;
                case (Shape.Kind.Square, int x):
                    break;
            }
        }
    }
}
```

Create `NullableStruct_NotNullDoesNotCoverNull.cs`:

```csharp
//@ should_fail
// Shape? with not-null arm covering all variants but null is NOT covered
using Houtamelo.Spire;
namespace TestNs
{
    [DiscriminatedUnion]
    partial struct Shape
    {
        [Variant] public static partial Shape Circle(double radius);
        [Variant] public static partial Shape Square(int sideLength);
    }

    class Consumer
    {
        int Test(Shape? s) => s switch //~ ERROR
        {
            not null and (Shape.Kind.Circle, double r) => 1,
            not null and (Shape.Kind.Square, int x) => 2,
        };
    }
}
```

- [ ] **Step 2: Write should_pass tests for nullable struct unions**

Create `Pass_NullableStruct_AllCoveredWithNull.cs`:

```csharp
//@ should_pass
// Shape? with all variants + explicit null arm — no diagnostic
using Houtamelo.Spire;
namespace TestNs
{
    [DiscriminatedUnion]
    partial struct Shape
    {
        [Variant] public static partial Shape Circle(double radius);
        [Variant] public static partial Shape Square(int sideLength);
    }

    class Consumer
    {
        int Test(Shape? s) => s switch
        {
            null => 0,
            (Shape.Kind.Circle, double r) => 1,
            (Shape.Kind.Square, int x) => 2,
        };
    }
}
```

Create `Pass_NullableStruct_WildcardCoversNull.cs`:

```csharp
//@ should_pass
// Shape? with all variants + discard covers null — no diagnostic
using Houtamelo.Spire;
namespace TestNs
{
    [DiscriminatedUnion]
    partial struct Shape
    {
        [Variant] public static partial Shape Circle(double radius);
        [Variant] public static partial Shape Square(int sideLength);
    }

    class Consumer
    {
        int Test(Shape? s) => s switch
        {
            (Shape.Kind.Circle, double r) => 1,
            (Shape.Kind.Square, int x) => 2,
            _ => 0,
        };
    }
}
```

Create `Pass_NullableStruct_SwitchStmt_DefaultCoversNull.cs`:

```csharp
//@ should_pass
// Switch statement on Shape? with all variants + default covers null — no diagnostic
using Houtamelo.Spire;
namespace TestNs
{
    [DiscriminatedUnion]
    partial struct Shape
    {
        [Variant] public static partial Shape Circle(double radius);
        [Variant] public static partial Shape Square(int sideLength);
    }

    class Consumer
    {
        void Test(Shape? s)
        {
            switch (s)
            {
                case (Shape.Kind.Circle, double r):
                    break;
                case (Shape.Kind.Square, int x):
                    break;
                default:
                    break;
            }
        }
    }
}
```

Create `Pass_NullableStruct_VarCoversNull.cs`:

```csharp
//@ should_pass
// Shape? with all variants + var covers null — no diagnostic
using Houtamelo.Spire;
namespace TestNs
{
    [DiscriminatedUnion]
    partial struct Shape
    {
        [Variant] public static partial Shape Circle(double radius);
        [Variant] public static partial Shape Square(int sideLength);
    }

    class Consumer
    {
        int Test(Shape? s) => s switch
        {
            (Shape.Kind.Circle, double r) => 1,
            (Shape.Kind.Square, int x) => 2,
            var other => 0,
        };
    }
}
```

- [ ] **Step 3: Run tests — verify detection tests fail, pass tests pass**

```bash
dotnet test tests/Spire.SourceGenerators.Tests/ --filter "FullyQualifiedName~Exhaustiveness"
```

Expected: new `NullableStruct_*` should_fail tests FAIL (no nullable support yet), new `Pass_NullableStruct_*` tests PASS (no diagnostic emitted since analyzer ignores nullable types entirely).

### Implementation

- [ ] **Step 4: Add `TryCreateWithNullableUnwrap` to `UnionTypeInfo`**

```csharp
/// Unwraps Nullable<T> and checks reference type nullability.
/// Returns the union info (or null) and whether the type is nullable.
public static UnionTypeInfo? TryCreateWithNullableUnwrap(
    ITypeSymbol type, INamedTypeSymbol duAttr, out bool isNullable)
{
    isNullable = false;

    // Unwrap Nullable<T> for value types
    if (type is INamedTypeSymbol named
        && named.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T
        && named.TypeArguments.Length == 1)
    {
        type = named.TypeArguments[0];
        isNullable = true;
    }

    var info = TryCreate(type, duAttr);
    if (info is null)
        return null;

    // Reference types: nullable unless explicitly NotAnnotated
    if (!isNullable && !type.IsValueType)
    {
        isNullable = type.NullableAnnotation != NullableAnnotation.NotAnnotated;
    }

    return info;
}
```

- [ ] **Step 5: Add `CoversNull` to `SwitchCoverage` and null detection to `PatternAnalyzer`**

In `PatternAnalyzer.cs`:

**5a.** Add `CoversNull` property to `SwitchCoverage`:

```csharp
public bool CoversNull { get; set; }
```

**5b.** Add a static helper method:

```csharp
internal static bool IsNullPattern(IPatternOperation pattern)
{
    switch (pattern)
    {
        case IDiscardPatternOperation:
            return true;

        case IDeclarationPatternOperation decl:
            if (decl.Syntax is Microsoft.CodeAnalysis.CSharp.Syntax.VarPatternSyntax)
                return true;
            return decl.MatchesNull;

        case IConstantPatternOperation c:
            return c.Value.ConstantValue is { HasValue: true, Value: null };

        case IBinaryPatternOperation { OperatorKind: BinaryOperatorKind.Or } binary:
            return IsNullPattern(binary.LeftPattern) || IsNullPattern(binary.RightPattern);

        default:
            return false;
    }
}
```

**5c.** In `AnalyzeExpression`: after `CollectVariants`, also check `IsNullPattern(arm.Pattern)`. If true and no guard, set `coverage.CoversNull = true`. Note: `HasWildcard` already implies null coverage, but track explicitly for clarity.

**5d.** In `AnalyzeStatement`: for `IDefaultCaseClauseOperation`, set `coverage.CoversNull = true`. For `IPatternCaseClauseOperation`, check `IsNullPattern(patternClause.Pattern)` (no guard) → set `coverage.CoversNull = true`.

- [ ] **Step 6: Update `ExhaustivenessAnalyzer` to use nullable-aware TryCreate**

In `ExhaustivenessAnalyzer.cs`:

**6a.** In `AnalyzeSwitchExpression` and `AnalyzeSwitchStatement`, replace `UnionTypeInfo.TryCreate(subjectType, duAttr)` with `UnionTypeInfo.TryCreateWithNullableUnwrap(subjectType, duAttr, out var isNullable)`.

**6b.** Pass `isNullable` to `ReportDiagnostics`.

**6c.** In `ReportDiagnostics`, after computing `missing`:

```csharp
bool needsNull = isNullable && !coverage.CoversNull && !coverage.HasWildcard;

if (missing.IsEmpty && !needsNull)
    return;

var missingParts = missing.Select(v => $"'{v}'").ToList();
if (needsNull)
    missingParts.Add("'null'");
var missingStr = string.Join(", ", missingParts);
```

Update the properties dictionary similarly — append "null" to `MissingVariants` when needed.

Update the wildcard early-return: `if (coverage.HasWildcard) return;` — this stays because wildcard covers both variants and null.

- [ ] **Step 7: Run tests — verify all pass**

```bash
dotnet test tests/Spire.SourceGenerators.Tests/ --filter "FullyQualifiedName~Exhaustiveness"
```

Expected: ALL pass.

- [ ] **Step 8: Commit**

```bash
git add src/Spire.SourceGenerators/Analyzers/ \
        tests/Spire.SourceGenerators.Tests/Exhaustiveness/cases/NullableStruct_* \
        tests/Spire.SourceGenerators.Tests/Exhaustiveness/cases/Pass_NullableStruct_*
git commit -m "feat(SPIRE009): support nullable struct unions with null requirement"
```

---

## Task 3: SPIRE009 — Nullable Reference Type Unions

**Files:**
- Modify: 4 existing test files (add `#nullable enable`)
- Create: 5 new test files in `tests/Spire.SourceGenerators.Tests/Exhaustiveness/cases/`

### Tests

- [ ] **Step 1: Update existing record should_pass tests**

Add `#nullable enable` as the SECOND line (after the `//@ should_pass` header) to these files. This puts the switch in nullable-enabled context where `Option<int>` / `Result<T,E>` are explicitly non-nullable, so no null arm is needed:

- `Pass_RecordFullCoverage.cs`
- `Pass_RecordGenericFull.cs`
- `Pass_Suppressor_RecordAllCovered.cs`
- `Pass_Record_SwitchStmt_Full.cs`

Example change (first 3 lines of each file):

```csharp
//@ should_pass
#nullable enable
// All record variants covered
```

- [ ] **Step 2: Write should_fail tests for nullable reference unions**

Create `NullableRecord_MissingNull.cs`:

```csharp
//@ should_fail
#nullable enable
// Option<int>? with all variants but missing null
using Houtamelo.Spire;
namespace TestNs
{
    [DiscriminatedUnion]
    public partial record Option<T>
    {
        public partial record Some(T Value) : Option<T>;
        public partial record None() : Option<T>;
    }
    class C
    {
        int Test(Option<int>? o) => o switch //~ ERROR
        {
            Option<int>.Some { Value: var v } => v,
            Option<int>.None => 0,
        };
    }
}
```

Create `ObliviousRecord_MissingNull.cs`:

```csharp
//@ should_fail
// No #nullable — oblivious context, reference type is implicitly nullable
using Houtamelo.Spire;
namespace TestNs
{
    [DiscriminatedUnion]
    public partial record Option<T>
    {
        public partial record Some(T Value) : Option<T>;
        public partial record None() : Option<T>;
    }
    class C
    {
        int Test(Option<int> o) => o switch //~ ERROR
        {
            Option<int>.Some { Value: var v } => v,
            Option<int>.None => 0,
        };
    }
}
```

- [ ] **Step 3: Write should_pass tests for nullable reference unions**

Create `Pass_NullableRecord_WithNull.cs`:

```csharp
//@ should_pass
#nullable enable
// Option<int>? with all variants + null arm — no diagnostic
using Houtamelo.Spire;
namespace TestNs
{
    [DiscriminatedUnion]
    public partial record Option<T>
    {
        public partial record Some(T Value) : Option<T>;
        public partial record None() : Option<T>;
    }
    class C
    {
        int Test(Option<int>? o) => o switch
        {
            null => -1,
            Option<int>.Some { Value: var v } => v,
            Option<int>.None => 0,
        };
    }
}
```

Create `Pass_NonNullableRecord_NoNullNeeded.cs`:

```csharp
//@ should_pass
#nullable enable
// Option<int> (non-nullable in #nullable enable) — no null arm needed
using Houtamelo.Spire;
namespace TestNs
{
    [DiscriminatedUnion]
    public partial record Option<T>
    {
        public partial record Some(T Value) : Option<T>;
        public partial record None() : Option<T>;
    }
    class C
    {
        int Test(Option<int> o) => o switch
        {
            Option<int>.Some { Value: var v } => v,
            Option<int>.None => 0,
        };
    }
}
```

Create `Pass_ObliviousRecord_WithNull.cs`:

```csharp
//@ should_pass
// No #nullable — oblivious context, but null arm present — no diagnostic
using Houtamelo.Spire;
namespace TestNs
{
    [DiscriminatedUnion]
    public partial record Option<T>
    {
        public partial record Some(T Value) : Option<T>;
        public partial record None() : Option<T>;
    }
    class C
    {
        int Test(Option<int> o) => o switch
        {
            null => -1,
            Option<int>.Some { Value: var v } => v,
            Option<int>.None => 0,
        };
    }
}
```

- [ ] **Step 4: Run tests — verify expected pass/fail**

```bash
dotnet test tests/Spire.SourceGenerators.Tests/ --filter "FullyQualifiedName~Exhaustiveness"
```

Expected: new should_fail tests FAIL (Task 2 implementation handles struct unions but reference type nullability isn't wired in yet — unless the `TryCreateWithNullableUnwrap` from Task 2 already handles reference types, in which case they may already pass). Existing updated tests should still PASS.

- [ ] **Step 5: Verify — if Task 2 implementation already handles reference types, all tests should pass now. If not, wire up the reference type nullable detection in `TryCreateWithNullableUnwrap`.**

The `TryCreateWithNullableUnwrap` from Task 2 already includes the reference type nullable detection (`type.NullableAnnotation != NullableAnnotation.NotAnnotated`). So all tests should already pass after Task 2's implementation. Verify and fix any issues.

- [ ] **Step 6: Run full test suite**

```bash
dotnet test tests/Spire.SourceGenerators.Tests/ --filter "FullyQualifiedName~Exhaustiveness"
```

Expected: ALL pass.

- [ ] **Step 7: Commit**

```bash
git add tests/Spire.SourceGenerators.Tests/Exhaustiveness/cases/
git commit -m "feat(SPIRE009): require null arm for nullable reference type unions"
```

---

## Task 4: CS8509 Suppressor Update

**Files:**
- Modify: `src/Spire.SourceGenerators/Analyzers/CS8509Suppressor.cs`
- Create: 1 new test file in `tests/Spire.SourceGenerators.Tests/Exhaustiveness/cases/`

- [ ] **Step 1: Write suppressor test**

Create `Pass_Suppressor_NullableStruct_AllWithNull.cs`:

```csharp
//@ should_pass
// All struct variants + null covered on Shape? — CS8509 suppressed, no SPIRE009
using Houtamelo.Spire;
namespace TestNs
{
    [DiscriminatedUnion]
    partial struct Shape
    {
        [Variant] public static partial Shape Circle(double radius);
        [Variant] public static partial Shape Square(int sideLength);
    }

    class Consumer
    {
        int Test(Shape? s) => s switch
        {
            null => 0,
            (Shape.Kind.Circle, double r) => 1,
            (Shape.Kind.Square, int x) => 2,
        };
    }
}
```

- [ ] **Step 2: Update CS8509Suppressor to handle nullable types**

In `CS8509Suppressor.cs`:

Replace `UnionTypeInfo.TryCreate(subjectType, duAttr)` with `UnionTypeInfo.TryCreateWithNullableUnwrap(subjectType, duAttr, out var isNullable)`.

Update suppression condition:

```csharp
bool allVariantsCovered = missing.IsEmpty || coverage.HasWildcard;
bool nullSatisfied = !isNullable || coverage.CoversNull || coverage.HasWildcard;

if (allVariantsCovered && nullSatisfied)
{
    context.ReportSuppression(
        Suppression.Create(Descriptor, diagnostic));
}
```

- [ ] **Step 3: Run all exhaustiveness tests**

```bash
dotnet test tests/Spire.SourceGenerators.Tests/ --filter "FullyQualifiedName~Exhaustiveness"
```

Expected: ALL pass.

- [ ] **Step 4: Run full test suite**

```bash
dotnet test
```

Expected: ALL pass across all test projects.

- [ ] **Step 5: Commit**

```bash
git add src/Spire.SourceGenerators/Analyzers/CS8509Suppressor.cs \
        tests/Spire.SourceGenerators.Tests/Exhaustiveness/cases/Pass_Suppressor_NullableStruct_AllWithNull.cs
git commit -m "fix(suppressor): account for nullable null coverage in CS8509 suppression"
```

---

## Implementation Notes

### Pattern matching on nullable struct unions

C# pattern matching on nullable value types automatically unwraps `Nullable<T>`. When switching on `Shape? s`, patterns like `(Shape.Kind.Circle, double r)` compile and work — the compiler inserts the null check implicitly. A `null` pattern handles the null case, and all other patterns match against the underlying `Shape`. This is standard C# behavior since C# 7.0; no special handling is needed in test cases.

In Roslyn's IOperation tree, the switch subject type is `Nullable<Shape>`, but `IRecursivePatternOperation.MatchedType` for the deconstruction pattern will be the unwrapped `Shape`. The analyzer unwraps `Nullable<T>` before looking up `[DiscriminatedUnion]`, so `UnionTypeInfo.TryCreate` receives the underlying `Shape` type.

### `#nullable enable` in generator test cases

The `GeneratorTestHelper.RunGenerator` creates compilations with `NullableContextOptions.Disable` (default). Test cases use `#nullable enable` source directives to control nullable context per-file. This works because Roslyn processes per-file directives regardless of project-level settings.

### Existing should_fail tests for record unions

These tests (e.g., `Record_MissingVariant.cs`, `Class_MissingVariant.cs`) are in nullable-oblivious context. Under the new rules, they would also be missing a null arm. Since the diagnostic still fires on the same line (just with "null" added to the missing list), and the test framework only checks line numbers (not messages), these tests continue to pass without modification.
