# 026 — Variant Field Access Safety Analyzer

**Goal:** Warn/error when accessing a discriminated union struct's variant field without proper tag guard or from the wrong variant context.

**Architecture:** DiagnosticAnalyzer registers on `OperationKind.FieldReference`. For each field access on a `[DiscriminatedUnion]` struct, determines the variant context (switch arm, if/is pattern, tag comparison) and reports if the access is unsafe.

**Design spec:** `plans/022_discriminated_unions_design.md` lines 384-409, 499-511

---

## Diagnostics

| ID | Severity | Scenario | Message |
|---|---|---|---|
| SPIRE013 | Error | Accessing another variant's field inside a guarded context | "'{0}' belongs to variant '{1}', not '{2}'" |
| SPIRE014 | Warning | Accessing any variant field without a tag guard | "Accessing variant field '{0}' without a tag guard" |

## Scope

**Struct unions only** — record/class unions use type-based access (fields live on the variant class, so accessing the wrong one is already a compile error).

**What is a "variant field"?** Any field on the struct that has `[EditorBrowsable(EditorBrowsableState.Never)]` attribute. The `tag` field and the `Kind` enum are NOT variant fields.

**Field → variant mapping:** The generated field names follow patterns:
- Single-field variant: `{variantCamelCase}_{paramName}` (e.g., `circle_radius`)
- Multi-field tuple: `{variantCamelCase}` (e.g., `rectangle`)
- Region 2 ref fields: `{variantCamelCase}_{paramName}` (e.g., `click_target`)
- Region 3 boxed slots: `_obj_{n}` — shared, no single variant owner

The variant name is recoverable from the field name prefix. But a more reliable approach: the generator knows which variant owns which field. We can encode this in the generated code via a naming convention, OR the analyzer can match field names against the Kind enum members.

**Approach for field→variant mapping:**
1. Get the `Kind` enum members (variant names)
2. For each variant name, camelCase it
3. A field belongs to variant V if its name starts with `{camelCase(V)}_` or equals `{camelCase(V)}`
4. Fields starting with `_obj_` are shared (Region 3) — skip checking, always allow

## Detection Algorithm

### Roslyn API (verified via Sherlock)

- `IFieldReferenceOperation`: `.Field` (IFieldSymbol), `.Instance` (IOperation, from IMemberReferenceOperation)
- `IIsPatternOperation`: `.Value` (IOperation), `.Pattern` (IPatternOperation)

### 1. Detect variant field access

Register on `OperationKind.FieldReference`:
1. Get `IFieldReferenceOperation`
2. Check `.Field.ContainingType` has `[DiscriminatedUnion]` and is a value type
3. Check the field has `[EditorBrowsable(EditorBrowsableState.Never)]` attribute
4. Determine which variant owns this field (field name → variant mapping)

### 2. Determine variant context

Walk up the operation tree to find the enclosing guard. **Keep walking past non-guard operations until reaching a guard or method/lambda scope.** Do NOT stop at the first `if` — nested ifs where only the outer one has a guard must be handled.

**Context A: Inside a switch arm/case**
- Walk up to find enclosing `ISwitchExpressionArmOperation` or `ISwitchCaseOperation`
- Extract the guarded variant from the arm's pattern
- Need `PatternAnalyzer.CollectVariants` to be `internal` (currently private) — expose it or create a new `GetVariantFromPattern(IPatternOperation, UnionTypeInfo) → HashSet<string>` entry point

**Context B: Inside `if (shape.tag == Shape.Kind.Circle)` body**
- Walk up to find enclosing `IConditionalOperation` (if statement)
- Check if the condition is a binary comparison: `shape.tag == Kind.X`
- Extract variant name from the Kind constant
- Check if field access is in the WhenTrue branch (not WhenFalse)
- **`else` branches are NOT considered guards** — field access in `else` gets SPIRE014

**Context C: Inside `if (shape is (Kind.Circle, ...))` body**
- Walk up to find enclosing `IConditionalOperation`
- Check if condition is `IIsPatternOperation` with a pattern matching the union
- Extract variant from the pattern using PatternAnalyzer

**Context D: No guard**
- If no enclosing guard found at any level → SPIRE014 warning

**Property pattern field references:**
Property patterns like `{ circle_radius: var r }` MAY trigger `IFieldReferenceOperation` for the member access. The implementer MUST verify this. If it does fire, the field access is inherently safe (it's inside the pattern itself — the guard IS the pattern). Detect this by checking if the field reference is inside an `IPropertySubpatternOperation` and skip it.

### 3. Report

- If inside guard for variant V, accessing variant W's field (W ≠ V) → SPIRE013 error
- If inside guard for variant V, accessing variant V's field → OK
- If no guard → SPIRE014 warning
- If field is `_obj_*` (shared Region 3 slot) → skip (can't determine variant)
- If field access is inside a property pattern subpattern → skip (inherently guarded)

### Out of scope: BoxedFields / BoxedTuple

These strategies use private fields (`_f0`, `_f1`, `_payload`) without `[EditorBrowsable(Never)]`. Users can't access them from outside the struct — the compiler rejects it. The analyzer's `EditorBrowsable` filter naturally excludes them.

## File Structure

```
src/Spire.SourceGenerators/
    Analyzers/
        FieldAccessSafetyAnalyzer.cs    -- DiagnosticAnalyzer
        VariantFieldMap.cs              -- Maps field names to variant names
    AnalyzerDescriptors.cs              -- Add SPIRE013/SPIRE014

tests/Spire.SourceGenerators.Tests/
    FieldAccess/
        FieldAccessTests.cs             -- Test runner
        cases/
            (test cases)
```

## Tasks

### Task 1: Infrastructure + field→variant mapping

- Add SPIRE013/SPIRE014 to AnalyzerDescriptors.cs
- Create VariantFieldMap — given a union type, maps field names to variant names
- Create FieldAccessSafetyAnalyzer placeholder
- Create test runner + one should_pass case
- Update AnalyzerReleases.Unshipped.md

### Task 2: No-guard warning (SPIRE014)

The simplest case — no enclosing guard at all. Implement field detection + report warning.

Test cases:
- `Warn_NoGuard.cs` — should_fail: `shape.circle_radius` outside any guard
- `Pass_InsideSwitchArm.cs` — should_pass: access inside matching switch arm
- `Pass_PropertyPattern.cs` — should_pass: `{ tag: Kind.Circle, circle_radius: var r }` — property patterns are OK (inline access)

### Task 3: Wrong variant error (SPIRE013)

Detect enclosing guard context and check field ownership.

Test cases:
- `Error_WrongVariantInSwitch.cs` — should_fail: `shape.rect` inside `case (Kind.Circle, ...)`
- `Error_WrongVariantInIf.cs` — should_fail: `shape.square_sideLength` inside `if (shape.tag == Kind.Circle)`
- `Error_WrongVariantInIsPattern.cs` — should_fail: field access in `if (shape is (Kind.Circle, ...))`
- `Pass_CorrectVariantInSwitch.cs` — should_pass: `shape.circle_radius` inside `case (Kind.Circle, ...)`
- `Pass_CorrectVariantInIf.cs` — should_pass: `shape.circle_radius` inside `if (shape.tag == Kind.Circle)`

### Task 4: Edge cases

- `Pass_TagFieldAccess.cs` — already exists from Task 1
- `Pass_NestedIf.cs` — should_pass: field access inside nested if where OUTER if has tag guard
- `Pass_SwitchStatement.cs` — should_pass: correct field access in switch statement (not expression)
- `Pass_OrPatternGuard.cs` — should_pass: `case (Kind.Circle, _) or (Kind.Square, _):` — field for either variant is OK
- `Warn_ElseBranch.cs` — should_fail: field access in `else` branch of tag guard (not considered guarded)
- `Pass_BoxedFieldsLayout.cs` — should_pass: BoxedFields union (no EditorBrowsable fields, analyzer doesn't fire)

## Execution order

Task 1 → Task 2 → Task 3 → Task 4 (sequential)

## Verification

```bash
dotnet test tests/Spire.SourceGenerators.Tests/
dotnet test  # full solution
```
