# 024 — Exhaustiveness Analyzer: Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development or superpowers:executing-plans. Checkboxes for tracking.

**Goal:** Implement exhaustiveness checking for discriminated union `switch` expressions/statements, plus CS8509 suppressor.

**Architecture:** DiagnosticAnalyzer in `Spire.SourceGenerators` detects incomplete switch coverage on `[DiscriminatedUnion]` types. Two detection paths: struct unions (Kind enum coverage) and record/class unions (sealed subtype coverage). DiagnosticSuppressor for CS8509 when all variants are explicitly covered.

**Tech Stack:** Roslyn 5.0.0 IOperation API, DiagnosticAnalyzer, DiagnosticSuppressor, netstandard2.0

**Design spec:** `plans/022_discriminated_unions_design.md` lines 354-418

---

## Diagnostics

| ID | Severity | Scenario | Message |
|---|---|---|---|
| SPIRE009 | Error | Missing variant(s), no wildcard covers them | "Switch on '{0}' does not handle variant(s): {1}" |
| SPIRE010 | Warning | Wildcard `_` covers missing variants | "Switch on '{0}' uses wildcard instead of exhaustive variant matching. Missing: {1}" |
| CS8509 | Suppressed | All variants explicitly covered | Suppress compiler warning |

Rules:
- `when` guard does NOT count as full coverage for that variant
- Discard/wildcard arm covers remaining variants → Warning (SPIRE010), not Error
- All variants explicitly covered → suppress CS8509, no SPIRE diagnostic
- All variants + redundant discard → suppress CS8509, no SPIRE diagnostic
- Only checked on `switch` expressions and `switch` statements where subject is a `[DiscriminatedUnion]` type
- NOT checked on `if`/`is` patterns (those are partial matching)

## Detection Algorithm

### 1. Identify switch on discriminated union
- `RegisterOperationAction` on `OperationKind.SwitchExpression` and `OperationKind.Switch`
- Get switch subject type (`ISwitchExpressionOperation.Value.Type` / `ISwitchOperation.Value.Type`)
- Check type has `[DiscriminatedUnion]` attribute (via `GetAttributes()`)

### 2. Extract variant names
- **Struct path**: Find nested `Kind` enum → enum members = variant names
- **Record/class path**: Find sealed nested types inheriting from the union → type names = variant names

### 3. Analyze pattern coverage

**Struct path** (Deconstruct-based):
- Positional pattern `(Shape.Kind.Circle, ...)`: `IRecursivePatternOperation.DeconstructionSubpatterns[0]` is an `IConstantPatternOperation` whose `.Value` matches a Kind enum member value. `Shape.Kind.Circle` is the nested enum member — the analyzer compares the constant value against the Kind enum member ordinals.
- Property pattern `{ tag: Shape.Kind.Circle }`: `IRecursivePatternOperation.PropertySubpatterns` contains a property match on `tag` with an `IConstantPatternOperation` matching a Kind enum value
- Discard `_` / `var x`: `IDiscardPatternOperation` or `IDeclarationPatternOperation` with `var` type → covers all remaining
- Default case in switch statement: `IDefaultCaseClauseOperation` → covers all remaining (wildcard equivalent)

**Record/class path** (type-based):
- Type pattern `Option<int>.Some`: `ITypePatternOperation.MatchedType` matches a sealed variant type
- Declaration pattern `Option<int>.Some varName`: `IDeclarationPatternOperation.MatchedType` matches variant type
- Recursive pattern `Option<int>.Some { ... }`: `IRecursivePatternOperation.MatchedType` matches variant type
- Use `OriginalDefinition` comparison for generic unions (e.g., `Option<int>.Some` vs `Option<T>.Some`)
- Discard `_` / `var x` / `default:`: covers all remaining

**Both paths — Roslyn pattern types to handle**:
- `IConstantPatternOperation` — matches constant (Kind enum values for struct path)
- `IDiscardPatternOperation` — wildcard `_`
- `IRecursivePatternOperation` — positional `(a, b)` or property `{ x: val }` patterns
- `IDeclarationPatternOperation` — `Type varName`
- `ITypePatternOperation` — `Type` (without binding)
- `IBinaryPatternOperation` — `or`/`and` patterns (`.OperatorKind`, `.LeftPattern`, `.RightPattern`)
  - `or`: each branch analyzed independently, union of covered variants
  - `and`: the type-narrowing branch determines the variant (e.g., `SomeVariant and { Prop: > 5 }` → covers SomeVariant)
- `INegatedPatternOperation` — `not X`: does NOT contribute to variant coverage
- `IRelationalPatternOperation` — `> 5`, `< 10`: does NOT contribute to variant coverage
- `when` guard on arm → variant NOT fully covered (tracked separately)
- Guarded variant + wildcard: wildcard covers the guarded variant (since guard means not fully covered)
- `var x` pattern acts as catch-all (like discard)

### 4. Report

```
coveredVariants = set of variants fully covered (no when guard)
guardedVariants = set of variants covered but with when guard
hasWildcard = true if any arm is a discard/wildcard
missingVariants = allVariants - coveredVariants

if missingVariants is empty:
    → suppress CS8509, no SPIRE diagnostic
elif hasWildcard:
    → SPIRE010 warning (wildcard covers missing)
    → suppress CS8509
else:
    → SPIRE009 error (missing variants)
```

---

## File Structure

```
src/Spire.SourceGenerators/
    Analyzers/
        ExhaustivenessAnalyzer.cs       -- DiagnosticAnalyzer for SPIRE009/SPIRE010
        CS8509Suppressor.cs             -- DiagnosticSuppressor
        UnionTypeInfo.cs                -- Shared: detects union types, extracts variants
        PatternAnalyzer.cs              -- Shared: analyzes switch patterns for coverage
    AnalyzerDescriptors.cs              -- SPIRE009/SPIRE010 descriptors (separate from Diagnostics.cs)

tests/Spire.SourceGenerators.Tests/
    GeneratorAnalyzerTestBase.cs        -- Runs generator + analyzer, file-based cases
    Exhaustiveness/
        ExhaustivenessTests.cs          -- Test runner
        cases/
            Struct_MissingVariant.cs
            Struct_WildcardWarning.cs
            Struct_AllCovered.cs
            Struct_WhenGuard.cs
            Record_MissingVariant.cs
            Record_AllCovered.cs
            Record_WildcardWarning.cs
            Class_MissingVariant.cs
            Class_AllCovered.cs
            Pass_FullCoverage.cs
            Pass_WithRedundantDiscard.cs
            ...
```

---

## Tasks

### Task 1: Infrastructure — descriptors, union type detection, test base

**Files:**
- Create: `src/Spire.SourceGenerators/AnalyzerDescriptors.cs`
- Create: `src/Spire.SourceGenerators/Analyzers/UnionTypeInfo.cs`
- Create: `tests/Spire.SourceGenerators.Tests/GeneratorAnalyzerTestBase.cs`
- Modify: `src/Spire.SourceGenerators/AnalyzerReleases.Unshipped.md`

**AnalyzerDescriptors.cs**: SPIRE009 + SPIRE010 descriptors. Separate from `Diagnostics.cs` (which has generator-internal diagnostics).

**UnionTypeInfo**: Given a type symbol, determines if it's a discriminated union and extracts variant info:
```csharp
internal sealed class UnionTypeInfo
{
    public bool IsStructUnion { get; }
    public bool IsRecordOrClassUnion { get; }
    public ImmutableArray<string> VariantNames { get; }

    public static UnionTypeInfo? TryCreate(ITypeSymbol type, INamedTypeSymbol duAttributeType)
    {
        // Check [DiscriminatedUnion] attribute
        // Struct: find nested Kind enum → extract member names
        // Record/class: find sealed nested types inheriting from type
    }
}
```

**GeneratorAnalyzerTestBase**: Runs generator THEN analyzer on the combined compilation. Uses same file format as `GeneratorDiagnosticTestBase` (`//@ should_fail` + `//~ ERROR` markers):
```csharp
// 1. Parse case file (same LoadAndParse as GeneratorDiagnosticTestBase)
// 2. Strip comments from source
// 3. Run generator: GeneratorTestHelper.RunGenerator(source, out outputCompilation, out genDiags)
// 4. Create CompilationWithAnalyzers:
//    var analyzersAndSuppressors = ImmutableArray.Create<DiagnosticAnalyzer>(
//        new ExhaustivenessAnalyzer(),
//        new CS8509Suppressor());
//    var withAnalyzers = outputCompilation.WithAnalyzers(analyzersAndSuppressors);
// 5. Get analyzer diagnostics:
//    var analyzerDiags = await withAnalyzers.GetAnalyzerDiagnosticsAsync();
// 6. Filter to SPIRE009/SPIRE010 (by ID prefix "SPIRE00" or "SPIRE01")
// 7. Match diagnostic lines against expected lines from markers
```

Abstract property `DiagnosticIdPrefix` (e.g., `"SPIRE009"` or `"SPIRE01"`) controls which diagnostics are checked. The test base provides the analyzer instances via abstract method `GetAnalyzers()`.

### Task 2: Struct exhaustiveness — Kind-based pattern analysis

**Files:**
- Create: `src/Spire.SourceGenerators/Analyzers/PatternAnalyzer.cs`
- Create: `src/Spire.SourceGenerators/Analyzers/ExhaustivenessAnalyzer.cs`
- Create: `tests/Spire.SourceGenerators.Tests/Exhaustiveness/ExhaustivenessTests.cs`
- Create: test case files under `Exhaustiveness/cases/`

**PatternAnalyzer**: Walks switch arm/case patterns and determines which variants are covered. Two entry points for expressions and statements:
```csharp
internal static class PatternAnalyzer
{
    // Switch expressions: ISwitchExpressionOperation.Arms → ISwitchExpressionArmOperation.Pattern + Guard
    public static SwitchCoverage AnalyzeExpression(
        ISwitchExpressionOperation switchOp,
        UnionTypeInfo unionInfo) { ... }

    // Switch statements: ISwitchOperation.Cases → ISwitchCaseOperation.Clauses
    //   Each clause is IPatternCaseClauseOperation (has .Pattern + .Guard)
    //   or IDefaultCaseClauseOperation (wildcard equivalent)
    public static SwitchCoverage AnalyzeStatement(
        ISwitchOperation switchOp,
        UnionTypeInfo unionInfo) { ... }

    // Shared: recursively walk a pattern to extract covered variant name(s)
    private static void AnalyzePattern(
        IPatternOperation pattern,
        UnionTypeInfo unionInfo,
        HashSet<string> coveredVariants) { ... }
}

internal sealed class SwitchCoverage
{
    public ImmutableHashSet<string> CoveredVariants { get; }
    public ImmutableHashSet<string> GuardedVariants { get; }
    public bool HasWildcard { get; }
    public ImmutableArray<string> MissingVariants { get; }
}
```

**ExhaustivenessAnalyzer**:
- `RegisterCompilationStartAction`: resolve `DiscriminatedUnionAttribute` type
- `RegisterOperationAction(OperationKind.SwitchExpression)`: call `PatternAnalyzer.AnalyzeExpression`, report SPIRE009/SPIRE010
- `RegisterOperationAction(OperationKind.Switch)`: call `PatternAnalyzer.AnalyzeStatement`, report SPIRE009/SPIRE010

**Test cases** (struct path):
- `Struct_MissingVariant.cs` — should_fail: switch missing one variant, no wildcard
- `Struct_AllMissing.cs` — should_fail: empty switch expression
- `Struct_WildcardWarning.cs` — should_fail: `_` covers missing variants (SPIRE010 warning)
- `Struct_WhenGuard.cs` — should_fail: all variants present but one has `when` guard
- `Struct_PropertyPattern.cs` — should_fail: property pattern `{ tag: ... }` with missing variant
- `Struct_SwitchStmt_Missing.cs` — should_fail: switch STATEMENT missing variant
- `Struct_OrPattern.cs` — should_fail: `or` pattern covers some but not all
- `Pass_StructFullCoverage.cs` — should_pass: all variants explicitly covered
- `Pass_StructWithDiscard.cs` — should_pass: all variants + redundant discard
- `Pass_StructSwitchStatement.cs` — should_pass: switch statement fully covered
- `Pass_StructVarPattern.cs` — should_pass: `var x` acts as catch-all

### Task 3: Record/class exhaustiveness — type-based pattern analysis

**Files:**
- Modify: `src/Spire.SourceGenerators/Analyzers/PatternAnalyzer.cs`
- Create: additional test case files

**Record/class pattern detection**:
- `IDeclarationPatternOperation.MatchedType`: check matches a sealed variant type
- `ITypePatternOperation.MatchedType`: check is a variant type
- `IRecursivePatternOperation.MatchedType`: check matched type is a variant
- `IDiscardPatternOperation`: wildcard
- Use `OriginalDefinition` comparison for generic unions (e.g., `Option<int>.Some` vs `Option<T>.Some`)

**Test cases** (record/class path):
- `Record_MissingVariant.cs` — should_fail
- `Record_WildcardWarning.cs` — should_fail (SPIRE010)
- `Record_WhenGuard.cs` — should_fail
- `Record_Generic_Missing.cs` — should_fail: `Option<int>` switch missing variant
- `Class_MissingVariant.cs` — should_fail
- `Class_WildcardWarning.cs` — should_fail
- `Class_SwitchStmt_Missing.cs` — should_fail: switch statement
- `Pass_RecordFullCoverage.cs` — should_pass
- `Pass_RecordGenericFull.cs` — should_pass: `Option<int>` all variants covered
- `Pass_ClassFullCoverage.cs` — should_pass

### Task 4: CS8509 DiagnosticSuppressor

**Files:**
- Create: `src/Spire.SourceGenerators/Analyzers/CS8509Suppressor.cs`
- Create: test cases for suppression

**DiagnosticSuppressor**:
```csharp
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class CS8509Suppressor : DiagnosticSuppressor
{
    private static readonly SuppressionDescriptor Descriptor = new(
        id: "SPIRE_SUP001",
        suppressedDiagnosticId: "CS8509",
        justification: "All variants of discriminated union are handled");

    public override ImmutableArray<SuppressionDescriptor> SupportedSuppressions =>
        ImmutableArray.Create(Descriptor);

    public override void ReportSuppressions(SuppressionAnalysisContext context)
    {
        foreach (var diagnostic in context.ReportedDiagnostics)
        {
            // Find the switch expression at the diagnostic location via syntax tree
            // Check if subject is a [DiscriminatedUnion] type
            // Reuse PatternAnalyzer + UnionTypeInfo to check coverage
            // If all variants covered → context.ReportSuppression(Suppression.Create(Descriptor, diagnostic))
        }
    }
}
```

**Suppressor test mechanism**: The `GeneratorAnalyzerTestBase` needs a suppressor-aware mode. The test pipeline:
1. Run generator → get output compilation
2. Run `CompilationWithAnalyzers` including BOTH the ExhaustivenessAnalyzer AND the CS8509Suppressor
3. For should_pass suppressor tests: verify CS8509 is NOT in the final diagnostics (it was suppressed)
4. For should_fail suppressor tests: verify CS8509 IS still present (suppressor didn't fire)

**Test cases** (suppressor):
- `Suppressor_AllCovered.cs` — should_pass: all variants covered, CS8509 suppressed (switch compiles without warning)
- `Suppressor_AllCoveredPlusDiscard.cs` — should_pass: all variants + redundant discard, CS8509 suppressed
- `Suppressor_RecordAllCovered.cs` — should_pass: record union all variants covered
- Note: should_fail suppressor tests are implicitly covered by the SPIRE009 tests (CS8509 fires when not all variants covered)

### Task 5: Edge cases + final verification

Additional test cases:
- `Pass_NullableUnion.cs` — should_pass: `Shape?` switch, analyzer does NOT fire (nullable wrapper not a union)
- `Pass_UnionInTuple.cs` — should_pass: `(shape, flag) switch { ... }`, NOT checked per design spec
- `Struct_OrPattern.cs` — should_fail: `or` pattern covers some but not all
- `Pass_EmptyUnion.cs` — should_pass: union with no variants, no switch to check
- Switch returning value (expression) vs void (statement) — both paths tested in Tasks 2-3

---

## Execution order

Task 1 → Task 2 → Task 3 → Task 4 → Task 5 (all sequential)

## Verification

```bash
dotnet test tests/Spire.SourceGenerators.Tests/ --filter "ExhaustivenessTests"
dotnet test tests/Spire.SourceGenerators.Tests/
```
