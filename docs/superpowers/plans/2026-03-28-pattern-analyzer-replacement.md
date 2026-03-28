# Replace PatternAnalyzer with ExhaustivenessChecker

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Delete `PatternAnalyzer` and wire `ExhaustivenessChecker` (from `Houtamelo.Spire.PatternAnalysis`) into `ExhaustivenessAnalyzer`, `CS8509Suppressor`, and `FieldAccessSafetyAnalyzer`.

**Architecture:** `ExhaustivenessChecker.Check(Compilation, ISwitchOperation)` replaces `PatternAnalyzer.AnalyzeExpression/Statement()`. Struct DUs use `DUTupleDomain`/`DUPropertyPatternDomain` (Kind enum). Record DUs use `EnforceExhaustiveDomain` (type hierarchy). `FieldAccessSafetyAnalyzer` gets a new `CollectVariants` method on `ExhaustivenessChecker`.

**Tech Stack:** C# / netstandard2.0 / Roslyn 5.0.0

**Spec:** `docs/superpowers/specs/2026-03-25-recursive-pattern-exhaustiveness-design.md`

**Existing tests that must keep passing:**
- 58 exhaustiveness test cases in `tests/Houtamelo.Spire.SourceGenerators.Tests/Exhaustiveness/cases/`
- 739 analyzer tests in `tests/Houtamelo.Spire.Analyzers.Tests/`
- 425 source generator tests in `tests/Houtamelo.Spire.SourceGenerators.Tests/`
- 305 pattern analysis tests in `tests/Houtamelo.Spire.PatternAnalysis.Tests/`

---

## File Map

### Modified files

| File | Change |
|------|--------|
| `src/Houtamelo.Spire.Analyzers/Houtamelo.Spire.Analyzers.csproj` | Add ProjectReference to `Houtamelo.Spire.PatternAnalysis` |
| `src/Houtamelo.Spire.Analyzers/SourceGenerators/Analyzers/ExhaustivenessAnalyzer.cs` | Replace `PatternAnalyzer` calls with `ExhaustivenessChecker.Check()` |
| `src/Houtamelo.Spire.Analyzers/SourceGenerators/Analyzers/CS8509Suppressor.cs` | Replace `PatternAnalyzer` calls with `ExhaustivenessChecker.Check()` |
| `src/Houtamelo.Spire.Analyzers/SourceGenerators/Analyzers/FieldAccessSafetyAnalyzer.cs` | Replace `PatternAnalyzer.CollectVariants()` with new API |
| `src/Houtamelo.Spire.PatternAnalysis/DomainResolver.cs` | Wire struct DU detection → DUTupleDomain/DUPropertyPatternDomain |
| `src/Houtamelo.Spire.PatternAnalysis/Algorithm/PatternMatrix.cs` | Handle DU patterns: Kind enum constants, variant field decomposition, style detection (deconstruct vs property), cross-style conversion |
| `src/Houtamelo.Spire.PatternAnalysis/ExhaustivenessChecker.cs` | Add `CollectVariants()` static method for FieldAccessSafetyAnalyzer |

### Deleted files

| File | Reason |
|------|--------|
| `src/Houtamelo.Spire.Analyzers/SourceGenerators/Analyzers/PatternAnalyzer.cs` | Replaced by ExhaustivenessChecker |

### Not modified

| File | Reason |
|------|--------|
| `UnionTypeInfo.cs` | Still used by FieldAccessSafetyAnalyzer, TypeSafetyAnalyzer, and ExhaustivenessAnalyzer for DU detection |
| `AnalyzerDescriptors.cs` | SPIRE009 descriptor unchanged |
| `VariantFieldMap.cs` | Used only by FieldAccessSafetyAnalyzer, independent of PatternAnalyzer |

---

## Task 1: Add project reference

**Files:**
- Modify: `src/Houtamelo.Spire.Analyzers/Houtamelo.Spire.Analyzers.csproj`

- [ ] **Step 1: Add ProjectReference**

Add to the `<ItemGroup>` with package references:
```xml
<ProjectReference Include="..\Houtamelo.Spire.PatternAnalysis\Houtamelo.Spire.PatternAnalysis.csproj" PrivateAssets="all" />
```

`PrivateAssets="all"` prevents the dependency from leaking to consumers — analyzers are self-contained.

- [ ] **Step 2: Verify build**

Run: `dotnet build`
Expected: Success.

- [ ] **Step 3: Commit**

```
feat(analyzers): add Houtamelo.Spire.PatternAnalysis project reference
```

---

## Task 2: Wire struct DU detection in DomainResolver

**Files:**
- Modify: `src/Houtamelo.Spire.PatternAnalysis/DomainResolver.cs`
- Modify: `src/Houtamelo.Spire.PatternAnalysis/Algorithm/PatternMatrix.cs`

The `DomainResolver` currently falls back to `PropertyPatternDomain` for `[DiscriminatedUnion]` types. Struct DUs need proper domain creation.

- [ ] **Step 1: Implement struct DU detection in DomainResolver.ResolveInner()**

When a type has `[DiscriminatedUnion]` and is a value type:
1. Find nested `Kind` enum via `type.GetTypeMembers("Kind").FirstOrDefault(t => t.TypeKind == TypeKind.Enum)`
2. If found → struct DU. DON'T create the domain yet — domain style depends on pattern analysis (deconstruct vs property). Return a `StructuralDomain` placeholder that `PatternMatrix.Build()` will replace.

Actually, the domain must be created at resolve time, but the STYLE (tuple vs property) depends on the patterns. Solution:

**DomainResolver detects the DU and creates the KIND enum domain.** `PatternMatrix.Build()` detects the pattern style and structures the matrix accordingly (multi-column for deconstruct, property-based for property patterns). The DomainResolver provides the Kind enum info; the matrix builder decides the column structure.

Add a new method to DomainResolver:
```csharp
/// Checks if a type is a Spire struct DU. Returns the Kind enum type if found.
public INamedTypeSymbol? TryGetStructDUKindEnum(ITypeSymbol type)
{
    if (!type.IsValueType || _discriminatedUnionAttr is null)
        return null;
    if (!HasAttribute(type, _discriminatedUnionAttr))
        return null;
    return type.GetTypeMembers("Kind")
        .FirstOrDefault(t => t.TypeKind == TypeKind.Enum);
}
```

For record DUs (reference types with `[DiscriminatedUnion]`):
```csharp
/// Checks if a type is a Spire record DU. Returns variant types if found.
public ImmutableArray<INamedTypeSymbol>? TryGetRecordDUVariants(ITypeSymbol type)
{
    if (type.IsValueType || _discriminatedUnionAttr is null)
        return null;
    if (!HasAttribute(type, _discriminatedUnionAttr))
        return null;
    var variants = type.GetTypeMembers()
        .Where(nested => nested.IsSealed &&
            SymbolEqualityComparer.Default.Equals(
                nested.BaseType?.OriginalDefinition,
                type.OriginalDefinition))
        .ToImmutableArray();
    return variants.Length > 0 ? variants : null;
}
```

Update `ResolveInner()`:
- Struct DU → `EnumDomain.Universe(kindEnum)` as the root domain (the Kind enum IS the exhaustiveness domain for a struct DU — each variant = one Kind member). The matrix builder will expand into multi-column if deconstruct patterns are present.

Wait — this isn't right either. A struct DU switched directly is switched on the struct itself, not the Kind enum. The matrix builder needs to know "this is a struct DU" so it can decompose patterns into Kind + fields.

**Revised approach**: DomainResolver returns an `EnumDomain` wrapping the Kind enum when it detects a struct DU. The `PatternMatrix.Build()` detects that the subject is a struct DU (via `DomainResolver.TryGetStructDUKindEnum()`) and builds a multi-column matrix where column 0 = Kind enum and remaining columns = variant fields.

- [ ] **Step 2: Update PatternMatrix.Build() for struct DUs**

In `BuildCore()`, add detection before tuple/property checks:

```
if (resolver.TryGetStructDUKindEnum(subjectType) is INamedTypeSymbol kindEnum)
    return BuildStructDUMatrix(subjectType, kindEnum, patterns, resolver);
```

`BuildStructDUMatrix()`:
1. Scan arms top-to-bottom for pattern style:
   - `IRecursivePatternOperation` with `DeconstructionSubpatterns` → deconstruct style
   - `IRecursivePatternOperation` with `PropertySubpatterns` containing `kind` member → property style
   - Default → property style
2. Build column structure based on style
3. For each arm, convert pattern to row (with cross-style conversion if needed)

- [ ] **Step 3: Implement deconstruct-style DU matrix construction**

Columns: [Kind (EnumDomain), field0 (domain), field1 (domain), ...]

For deconstruct patterns `(Kind.Circle, double r)`:
- Element[0] → Kind constant → EnumDomain constraint
- Element[1..] → field patterns → resolve domain per field type

For property patterns in deconstruct mode (cross-style conversion):
- Find `kind` property subpattern → Kind constant
- Find field property subpatterns → map by field name to positional slot

For wildcards/discards → all columns wildcard.

- [ ] **Step 4: Implement property-style DU matrix construction**

Columns: [kind (EnumDomain), prop0 (domain), prop1 (domain), ...]
Only include properties mentioned across all arms.

For property patterns `{ kind: Kind.Circle, radius: var r }`:
- `kind` subpattern → Kind constant → EnumDomain constraint
- Other subpatterns → map to property columns

For deconstruct patterns in property mode (cross-style conversion):
- Element[0] → Kind constant → map to kind column
- Element[1..] → map by position to field properties (need variant metadata for field names)

- [ ] **Step 5: Write tests verifying struct DU domain resolution**

Add integration tests in `tests/Houtamelo.Spire.PatternAnalysis.Tests/Integration/Union/`:
- `UnionTests.cs` — test runner
- `cases/_shared.cs` — define a struct DU manually (with `[DiscriminatedUnion]` attribute as raw source)
- Deconstruct pattern cases (exhaustive + not exhaustive)
- Property pattern cases (exhaustive + not exhaustive)

- [ ] **Step 6: Run tests — verify all pass**

Run: `dotnet test`
Expected: All 305+ PatternAnalysis tests pass.

- [ ] **Step 7: Commit**

```
feat(pattern-analysis): wire struct DU detection into DomainResolver and PatternMatrix
```

---

## Task 3: Wire record DU detection

**Files:**
- Modify: `src/Houtamelo.Spire.PatternAnalysis/DomainResolver.cs`
- Modify: `src/Houtamelo.Spire.PatternAnalysis/Algorithm/PatternMatrix.cs`

- [ ] **Step 1: Update DomainResolver for record DUs**

Record DUs are reference types with `[DiscriminatedUnion]` and sealed nested variant types. They match via type patterns — same as `[EnforceExhaustiveness]` hierarchies.

In `ResolveInner()`, when type has `[DiscriminatedUnion]` and is NOT a value type:
- Find sealed nested variant types (same logic as `UnionTypeInfo.TryCreateRecord()`)
- Create `EnforceExhaustiveDomain` directly from the known variants (no assembly walk needed — variants are nested types)

- [ ] **Step 2: Add record DU integration tests**

In `Integration/Union/cases/`:
- `RecordDeconstruct.cs` — record DU matched via type patterns (exhaustive)
- `RecordMissing.cs` — missing one variant (not exhaustive)

- [ ] **Step 3: Run tests — verify all pass**

- [ ] **Step 4: Commit**

```
feat(pattern-analysis): wire record DU detection into DomainResolver
```

---

## Task 4: Add CollectVariants to ExhaustivenessChecker

**Files:**
- Modify: `src/Houtamelo.Spire.PatternAnalysis/ExhaustivenessChecker.cs`

`FieldAccessSafetyAnalyzer` needs to know which variants a pattern arm guards. This is a simpler query than full exhaustiveness — just "which variant names does this pattern match?"

- [ ] **Step 1: Add CollectVariants method**

```csharp
/// Extracts variant names from a pattern on a discriminated union.
/// Used by FieldAccessSafetyAnalyzer for guard detection.
/// Returns true if the pattern is a wildcard (matches all variants).
public static bool CollectVariants(
    IPatternOperation pattern,
    INamedTypeSymbol? kindEnumType,
    ImmutableArray<INamedTypeSymbol> variantTypes,
    bool isStructUnion,
    List<string> variants)
```

This replicates the essential logic of the old `PatternAnalyzer.CollectVariants()`:
- Struct unions: extract Kind enum constant from deconstruct/property patterns → variant name
- Record unions: extract matched type → compare against variant types → variant name
- Binary or patterns: collect from both branches
- Wildcards/discards: return true

The implementation can largely be extracted from `PatternMatrix.ConvertPattern()` logic but returns variant names instead of domain constraints.

- [ ] **Step 2: Run full test suite — verify no regressions**

- [ ] **Step 3: Commit**

```
feat(pattern-analysis): add CollectVariants to ExhaustivenessChecker for FieldAccessSafetyAnalyzer
```

---

## Task 5: Rewire ExhaustivenessAnalyzer

**Files:**
- Modify: `src/Houtamelo.Spire.Analyzers/SourceGenerators/Analyzers/ExhaustivenessAnalyzer.cs`

- [ ] **Step 1: Replace PatternAnalyzer calls**

Current:
```csharp
var coverage = PatternAnalyzer.AnalyzeExpression(switchOp, unionInfo);
ReportDiagnostics(ctx, subjectType, unionInfo, coverage, isNullable, switchOp.Syntax.GetLocation());
```

New:
```csharp
var result = ExhaustivenessChecker.Check(ctx.Compilation, switchOp);
ReportDiagnostics(ctx, subjectType, unionInfo, result, switchOp.Syntax.GetLocation());
```

- [ ] **Step 2: Update ReportDiagnostics to use ExhaustivenessResult**

Current logic: checks `SwitchCoverage.HasWildcard`, `GetMissingVariants()`, nullable null coverage.

New logic:
```csharp
private static void ReportDiagnostics(
    OperationAnalysisContext ctx,
    ITypeSymbol subjectType,
    UnionTypeInfo unionInfo,
    ExhaustivenessResult result,
    Location location)
{
    if (result.MissingCases.IsEmpty)
        return;

    // Format missing cases for the diagnostic message.
    // Extract variant names from MissingCase slot constraints.
    var missingNames = ExtractMissingNames(result, unionInfo);
    if (missingNames.Count == 0)
        return;

    var missingStr = string.Join(", ", missingNames.Select(n => $"'{n}'"));

    var properties = ImmutableDictionary.CreateBuilder<string, string?>();
    properties.Add("MissingVariants", string.Join(",", missingNames));

    ctx.ReportDiagnostic(Diagnostic.Create(
        AnalyzerDescriptors.SPIRE009_SwitchNotExhaustive,
        location, properties.ToImmutable(),
        subjectType.Name, missingStr));
}
```

`ExtractMissingNames()` converts `MissingCase` domain constraints back to variant names:
- `EnumDomain` (Kind enum) → extract field symbol names
- `EnforceExhaustiveDomain` (record variants) → extract type names
- `NullableDomain` with null remaining → "null"

- [ ] **Step 3: Run exhaustiveness tests**

Run: `dotnet test tests/Houtamelo.Spire.SourceGenerators.Tests/ --filter "FullyQualifiedName~Exhaustiveness"`
Expected: All 58 exhaustiveness cases pass.

- [ ] **Step 4: Commit**

```
refactor(analyzers): rewire ExhaustivenessAnalyzer to use ExhaustivenessChecker
```

---

## Task 6: Rewire CS8509Suppressor

**Files:**
- Modify: `src/Houtamelo.Spire.Analyzers/SourceGenerators/Analyzers/CS8509Suppressor.cs`

- [ ] **Step 1: Replace PatternAnalyzer call**

Current:
```csharp
var coverage = PatternAnalyzer.AnalyzeExpression(switchOp, unionInfo);
var missing = coverage.GetMissingVariants(unionInfo.VariantNames);
bool allVariantsCovered = missing.IsEmpty || coverage.HasWildcard;
bool nullSatisfied = !isNullable || coverage.CoversNull || coverage.HasWildcard;
```

New:
```csharp
var result = ExhaustivenessChecker.Check(context.Compilation, switchOp);
if (result.MissingCases.IsEmpty)
{
    context.ReportSuppression(Suppression.Create(Descriptor, diagnostic));
}
```

Much simpler — `ExhaustivenessChecker` already handles nullable, wildcards, and variant coverage internally.

- [ ] **Step 2: Run suppressor tests**

Run: `dotnet test tests/Houtamelo.Spire.SourceGenerators.Tests/ --filter "FullyQualifiedName~Suppressor"`
Expected: All suppressor cases pass.

- [ ] **Step 3: Commit**

```
refactor(analyzers): rewire CS8509Suppressor to use ExhaustivenessChecker
```

---

## Task 7: Rewire FieldAccessSafetyAnalyzer

**Files:**
- Modify: `src/Houtamelo.Spire.Analyzers/SourceGenerators/Analyzers/FieldAccessSafetyAnalyzer.cs`

- [ ] **Step 1: Replace PatternAnalyzer.CollectVariants() calls**

Three call sites (lines ~203, 213, 247). Replace each:

```csharp
// Before:
PatternAnalyzer.CollectVariants(arm.Pattern, info, variants);

// After:
ExhaustivenessChecker.CollectVariants(
    arm.Pattern, info.KindEnumType, info.VariantTypes, info.IsStructUnion, variants);
```

- [ ] **Step 2: Run field access safety tests**

Run: `dotnet test tests/Houtamelo.Spire.SourceGenerators.Tests/ --filter "FullyQualifiedName~FieldAccess"`
Expected: All SPIRE013/SPIRE014 cases pass.

- [ ] **Step 3: Commit**

```
refactor(analyzers): rewire FieldAccessSafetyAnalyzer to use ExhaustivenessChecker.CollectVariants
```

---

## Task 8: Delete PatternAnalyzer

**Files:**
- Delete: `src/Houtamelo.Spire.Analyzers/SourceGenerators/Analyzers/PatternAnalyzer.cs`

- [ ] **Step 1: Delete the file**

- [ ] **Step 2: Verify build**

Run: `dotnet build`
Expected: Success with no references to `PatternAnalyzer` or `SwitchCoverage`.

- [ ] **Step 3: Run ALL tests**

Run: `dotnet test`
Expected: ALL tests pass (1,164+ from analyzer + generator + pattern analysis projects).

- [ ] **Step 4: Commit**

```
refactor(analyzers): delete PatternAnalyzer — fully replaced by ExhaustivenessChecker
```

---

## Execution Order

```
Task 1 (project reference)
  └─ Task 2 (struct DU wiring) ← most complex
       └─ Task 3 (record DU wiring)
            └─ Task 4 (CollectVariants method)
                 ├─ Task 5 (rewire ExhaustivenessAnalyzer)
                 ├─ Task 6 (rewire CS8509Suppressor)
                 └─ Task 7 (rewire FieldAccessSafetyAnalyzer)
                      └─ Task 8 (delete PatternAnalyzer)
```

Tasks 5, 6, 7 are independent of each other but all depend on Tasks 1-4. Task 8 depends on all others.
