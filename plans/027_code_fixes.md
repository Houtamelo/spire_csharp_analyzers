# 027 — Code Fixes for Discriminated Union Analyzers

**Goal:** Implement 3 CodeFixProviders that auto-fix SPIRE009, SPIRE010, and SPIRE011 diagnostics.

**Architecture:** CodeFixProviders in a NEW `Spire.CodeFixes` project (separate from `Spire.SourceGenerators` to avoid `EnforceExtendedAnalyzerRules` conflict with Workspaces dependency). The code fixes project references `Spire.SourceGenerators` for shared types (`UnionTypeInfo`, `VariantFieldMap`). Both DLLs ship in the same NuGet package.

**Design spec:** `plans/022_discriminated_unions_design.md` lines 422-478

---

## Blockers from review (addressed)

1. **EnforceExtendedAnalyzerRules conflict** → Separate `Spire.CodeFixes` project without the flag
2. **No diagnostic properties** → Update analyzers to emit `Diagnostic.Properties` with structured data (missing variant names, expected types)
3. **Variant field info** → Code fix re-analyzes at fix time via `UnionTypeInfo` + factory method lookup. Needs `[InternalsVisibleTo]` from `Spire.SourceGenerators` to `Spire.CodeFixes`
4. **Switch statements** → Expressions only for v1. Document limitation.

## Code Fixes

### 1. AddMissingArmsCodeFix (SPIRE009)

Trigger: SPIRE009 "Switch does not handle variant(s)..."
Scope: Switch expressions only (v1)

Reads `Diagnostic.Properties["MissingVariants"]` (comma-separated names).
For each missing variant, looks up factory method params to get field types/names.
Generates: `(Kind.Variant, Type1 name1, ...) => throw new NotImplementedException()`

### 2. FixFieldTypeCodeFix (SPIRE011)

Trigger: SPIRE011 "Variant field type mismatch"

Reads `Diagnostic.Properties["ExpectedType"]` and `Properties["FieldIndex"]`.
Replaces the wrong type in the declaration pattern with the correct type.

### 3. ExpandWildcardCodeFix (SPIRE010)

Trigger: SPIRE010 "Switch uses wildcard instead of exhaustive matching"
Scope: Switch expressions only (v1)

Reads `Diagnostic.Properties["MissingVariants"]`.
Replaces `_` arm with explicit arms for each missing variant, copying the wildcard's expression body.

## File Structure

```
src/Spire.CodeFixes/
    Spire.CodeFixes.csproj              -- NO EnforceExtendedAnalyzerRules
    AddMissingArmsCodeFix.cs
    FixFieldTypeCodeFix.cs
    ExpandWildcardCodeFix.cs

src/Spire.SourceGenerators/
    (add [InternalsVisibleTo("Spire.CodeFixes")])
    Analyzers/ExhaustivenessAnalyzer.cs  -- add Diagnostic.Properties
    Analyzers/TypeSafetyAnalyzer.cs      -- add Diagnostic.Properties
```

## Tasks

### Task 1: Project scaffold + analyzer properties

- Create `src/Spire.CodeFixes/Spire.CodeFixes.csproj` (netstandard2.0, CSharp.Workspaces 5.0.0, references Spire.SourceGenerators)
- Add `[InternalsVisibleTo("Spire.CodeFixes")]` to Spire.SourceGenerators
- Update solution file
- Update ExhaustivenessAnalyzer to emit `Properties["MissingVariants"]` on SPIRE009/SPIRE010
- Update TypeSafetyAnalyzer to emit `Properties["ExpectedType"]` + `Properties["FieldIndex"]` on SPIRE011
- Verify existing tests still pass

### Task 2: AddMissingArmsCodeFix

- Implement for SPIRE009 (switch expressions only)
- Re-analyze union type at fix time to get variant field info
- Generate correct arm syntax with Kind constant + typed params

### Task 3: FixFieldTypeCodeFix

- Implement for SPIRE011
- Replace type token in declaration pattern

### Task 4: ExpandWildcardCodeFix

- Implement for SPIRE010 (switch expressions only)
- Replace wildcard arm with explicit variant arms
- Copy wildcard's expression body

### Task 5: Tests + verification

Code fix tests need a custom harness: run generator → apply fix → verify fixed source.

## Execution order

Task 1 → Task 2 → Task 3 → Task 4 → Task 5 (sequential)

## Verification

```bash
dotnet build
dotnet test
```
