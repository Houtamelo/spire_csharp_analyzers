# Discriminated Union Utilities Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add IsVariant properties, IDiscriminatedUnion interface + OfKind LINQ extension, and init-settable properties to all discriminated union emitters.

**Architecture:** Three changes across the generator: (1) Spire.Core gets the interface and LINQ type, (2) all 7 emitters gain IsVariant properties, (3) all 5 struct emitters change backing fields to `{ get; init; }`, public properties to `{ get; init; }`, kind from field to non-auto property, and implement `IDiscriminatedUnion<Kind>`. The generator conditionally emits `init` (C# 9+) or `readonly` (C# <9) based on whether `IsExternalInit` is available in the compilation.

**Tech Stack:** C#, Roslyn source generators, netstandard2.0 (Spire.Core + generators), net10.0 (tests)

**Spec:** `docs/superpowers/specs/2026-03-22-discriminated-union-utilities-design.md`

---

### Task 1: Add IDiscriminatedUnion and SpireLINQ to Spire.Core

**Files:**
- Create: `src/Spire.Core/IDiscriminatedUnion.cs`
- Create: `src/Spire.Core/SpireLINQ.cs`

- [ ] **Step 1: Create the interface file**

```csharp
// src/Spire.Core/IDiscriminatedUnion.cs
using System;

namespace Spire;

public interface IDiscriminatedUnion<TEnum> where TEnum : Enum
{
    TEnum kind { get; }
}
```

- [ ] **Step 2: Create the LINQ extension file**

```csharp
// src/Spire.Core/SpireLINQ.cs
using System;
using System.Collections.Generic;

namespace Spire;

public static class SpireLINQ
{
    public static IEnumerable<TDU> OfKind<TDU, TEnum>(
        this IEnumerable<TDU> source, TEnum kind)
        where TDU : IDiscriminatedUnion<TEnum>
        where TEnum : Enum
    {
        foreach (var item in source)
        {
            if (EqualityComparer<TEnum>.Default.Equals(item.kind, kind))
                yield return item;
        }
    }
}
```

- [ ] **Step 3: Build Spire.Core to verify**

Run: `dotnet build src/Spire.Core/Spire.Core.csproj`
Expected: Build succeeded, 0 errors

- [ ] **Step 4: Commit**

```bash
git add src/Spire.Core/IDiscriminatedUnion.cs src/Spire.Core/SpireLINQ.cs
git commit -m "feat(core): add IDiscriminatedUnion interface and OfKind LINQ extension"
```

---

### Task 2: Add HasInitProperties flag to UnionDeclaration model

The generator needs to know at emit time whether C# 9+ `init` is available in the consuming compilation. This check happens at the generator driver level (where `Compilation` is accessible) and flows into the model. `IDiscriminatedUnion` does not need a guard — Spire.Core is always present (emitters already emit `[global::Spire.EnforceInitialization]` unconditionally).

**Files:**
- Modify: `src/Spire.SourceGenerators/Model/UnionDeclaration.cs` — add `HasInitProperties` boolean field
- Modify: `src/Spire.SourceGenerators/DiscriminatedUnionGenerator.cs` — compute flag via `GetTypeByMetadataName`
- Modify: `src/Spire.SourceGenerators/BenchmarkUnionGenerator.cs` — same (if it uses the same model)

- [ ] **Step 1: Add flag to UnionDeclaration**

Add one field to `UnionDeclaration`:
```csharp
/// Whether System.Runtime.CompilerServices.IsExternalInit is available (C# 9+ init support).
public bool HasInitProperties;
```

- [ ] **Step 2: Compute flag in the generator driver**

In `DiscriminatedUnionGenerator` (and `BenchmarkUnionGenerator` if applicable), after parsing the union, check:
```csharp
var hasInit = comp.GetTypeByMetadataName("System.Runtime.CompilerServices.IsExternalInit") != null;
```

Pass into the `UnionDeclaration`.

- [ ] **Step 3: Build to verify**

Run: `dotnet build src/Spire.SourceGenerators/Spire.SourceGenerators.csproj`

- [ ] **Step 4: Commit**

```bash
git add src/Spire.SourceGenerators/Model/UnionDeclaration.cs src/Spire.SourceGenerators/DiscriminatedUnionGenerator.cs
git commit -m "feat(model): add HasInitProperties flag to UnionDeclaration"
```

---

### Task 3: Update AdditiveEmitter (reference implementation)

This is the most common struct strategy. Implement all changes here first as the reference pattern for the other emitters.

**Files:**
- Modify: `src/Spire.SourceGenerators/Emit/AdditiveEmitter.cs`

Changes required in `AdditiveEmitter.Emit()` and helper methods. The `Emit` method signature must accept the new flags (or the full `UnionDeclaration` already contains them).

**a) Kind field → non-auto property:**
Currently (line 52): `sb.AppendLine("public readonly Kind kind;");`
Replace: emit a private/internal readonly backing field + public getter-only property.

**b) Backing fields → `{ get; init; }` (conditional):**
Currently (lines 55-60): `sb.AppendLine($"internal readonly {slotType} _s{slot.Index};");`
When `HasInitProperties`: `sb.AppendLine($"internal {slotType} _s{slot.Index} {{ get; init; }}");`
When not: keep `internal readonly {slotType} _s{slot.Index};`

**c) Public properties → `{ get; init; }` (conditional):**
Currently (lines 237-241): expression-bodied read-only properties.
When `HasInitProperties`: emit `{ get; init; }` with getter delegating to backing slot and init setter writing to it.
When not: keep expression-bodied read-only.

**d) Add IsVariant properties:**
After properties, emit one `Is{VariantName}` per variant (always, not conditional):
`sb.AppendLine($"public bool Is{variant.Name} => this.kind == Kind.{variant.Name};");`

**e) Add IDiscriminatedUnion interface (unconditional (Spire.Core always present)):**
On the type declaration line, append `: global::Spire.IDiscriminatedUnion<Kind>` (unconditional — Spire.Core always present).

**f) Constructor updates:**
Currently (line 183): `sb.AppendLine("this.kind = kind;");`
Update to write to the kind backing field.
Currently (line 185): `sb.AppendLine($"this._s{slot.Index} = s{slot.Index};");`
When `HasInitProperties`: write to the init property (which works in constructors).
When not: keep direct field assignment.

- [ ] **Step 1: Modify kind emission** — change from `public readonly Kind kind;` to private backing field + public non-auto getter property
- [ ] **Step 2: Modify backing field emission** — conditional `{ get; init; }` vs `readonly` based on `HasInitProperties`
- [ ] **Step 3: Modify public property emission** — conditional `{ get; init; }` with delegation vs expression-bodied
- [ ] **Step 4: Add IsVariant property emission** — one `Is{VariantName}` per variant, always emitted
- [ ] **Step 5: Add IDiscriminatedUnion interface** — unconditional (Spire.Core always present)
- [ ] **Step 6: Update constructor** — adjust to work with new backing storage
- [ ] **Step 7: Build to verify**

Run: `dotnet build src/Spire.SourceGenerators/Spire.SourceGenerators.csproj`
Expected: Build succeeded

- [ ] **Step 8: Commit**

```bash
git add src/Spire.SourceGenerators/Emit/AdditiveEmitter.cs
git commit -m "feat(emitter): update AdditiveEmitter with utilities (IsVariant, init props, IDiscriminatedUnion)"
```

---

### Task 4: Update OverlapEmitter

**Files:**
- Modify: `src/Spire.SourceGenerators/Emit/OverlapEmitter.cs`

Same pattern as Task 3 with Overlap-specific differences:
- Kind field (line 46): uses `[FieldOffset(0)]`. Change to: private backing field with `[FieldOffset(0)]` + public non-auto getter property.
- Backing fields: already have `[FieldOffset(N)]`. When `HasInitProperties`: change to `{ get; init; }` auto-properties with `[field: FieldOffset(N)]`. When not: keep `readonly` fields with `[FieldOffset(N)]`.
- Public properties: conditional `{ get; init; }`.
- IsVariant properties: same pattern (always emitted).
- IDiscriminatedUnion: unconditional (Spire.Core always present).

- [ ] **Step 1: Modify kind emission** — keep `[FieldOffset(0)]` on backing field, add public getter property
- [ ] **Step 2: Modify backing fields** — conditional `[field: FieldOffset(N)]` on `{ get; init; }` vs `[FieldOffset(N)]` on `readonly` fields
- [ ] **Step 3: Modify public properties** — conditional `{ get; init; }`
- [ ] **Step 4: Add IsVariant properties**
- [ ] **Step 5: Add IDiscriminatedUnion interface** — unconditional (Spire.Core always present)
- [ ] **Step 6: Update constructor**
- [ ] **Step 7: Build to verify**

Run: `dotnet build src/Spire.SourceGenerators/Spire.SourceGenerators.csproj`

- [ ] **Step 8: Commit**

```bash
git add src/Spire.SourceGenerators/Emit/OverlapEmitter.cs
git commit -m "feat(emitter): update OverlapEmitter with utilities"
```

---

### Task 5: Update BoxedFieldsEmitter

**Files:**
- Modify: `src/Spire.SourceGenerators/Emit/BoxedFieldsEmitter.cs`

Same pattern as Task 3. Backing fields are `object? _f{i}`. Follow the same conditional `{ get; init; }` vs `readonly` pattern.

- [ ] **Step 1: Modify kind emission** — private backing field + public getter property
- [ ] **Step 2: Modify backing fields** — conditional `{ get; init; }`
- [ ] **Step 3: Modify public properties** — conditional `{ get; init; }` with delegation
- [ ] **Step 4: Add IsVariant properties**
- [ ] **Step 5: Add IDiscriminatedUnion interface** — unconditional (Spire.Core always present)
- [ ] **Step 6: Update constructor**
- [ ] **Step 7: Build to verify**
- [ ] **Step 8: Commit**

```bash
git add src/Spire.SourceGenerators/Emit/BoxedFieldsEmitter.cs
git commit -m "feat(emitter): update BoxedFieldsEmitter with utilities"
```

---

### Task 6: Update BoxedTupleEmitter

**Files:**
- Modify: `src/Spire.SourceGenerators/Emit/BoxedTupleEmitter.cs`

Backing storage is `object? _payload`. Same conditional `{ get; init; }` pattern.

Public properties use `kind switch` for casting. Init setters:
- **Single-field variants:** `init => this._payload = value;` (direct box)
- **Multi-field variants:** init setter must unbox the `ValueTuple`, replace the target item, and rebox. Example for a 2-field variant where field index 0 is being set:
  ```csharp
  init { var t = ((T0, T1))this._payload!; t.Item1 = value; this._payload = t; }
  ```
  This is boxing-heavy but correct — BoxedTuple is already a convenience-over-performance strategy.

- [ ] **Step 1: Modify kind emission** — private backing field + public getter property
- [ ] **Step 2: Modify backing payload** — conditional `{ get; init; }`
- [ ] **Step 3: Modify public properties** — conditional `{ get; init; }` with strategy-specific init setters (single-field: direct write, multi-field: unbox-modify-rebox)
- [ ] **Step 4: Add IsVariant properties**
- [ ] **Step 5: Add IDiscriminatedUnion interface** — unconditional (Spire.Core always present)
- [ ] **Step 6: Update constructor**
- [ ] **Step 7: Build to verify**
- [ ] **Step 8: Commit**

```bash
git add src/Spire.SourceGenerators/Emit/BoxedTupleEmitter.cs
git commit -m "feat(emitter): update BoxedTupleEmitter with utilities"
```

---

### Task 7: Update UnsafeOverlapEmitter

**Files:**
- Modify: `src/Spire.SourceGenerators/Emit/UnsafeOverlapEmitter.cs`

Most complex strategy. Two storage types:
- **Buffer fields** (`_data` / `_data{i}`): unmanaged types via `Unsafe.ReadUnaligned`/`Unsafe.WriteUnaligned`. Init setters use `Unsafe.WriteUnaligned` at the correct offset.
- **Dedup slots** (`_s{i}`): same as Additive — conditional `{ get; init; }`.

Kind field (line 52): change to private backing field + public getter property (keep kind as a separate field, not in the buffer).

- [ ] **Step 1: Modify kind emission** — private backing field + public getter property
- [ ] **Step 2: Modify dedup slot emission** — conditional `{ get; init; }`
- [ ] **Step 3: Modify public properties** — conditional `{ get; init; }` with init setters using `Unsafe.WriteUnaligned` for buffer fields, delegation for slot fields
- [ ] **Step 4: Add IsVariant properties**
- [ ] **Step 5: Add IDiscriminatedUnion interface** — unconditional (Spire.Core always present)
- [ ] **Step 6: Update constructor**
- [ ] **Step 7: Build to verify**
- [ ] **Step 8: Commit**

```bash
git add src/Spire.SourceGenerators/Emit/UnsafeOverlapEmitter.cs
git commit -m "feat(emitter): update UnsafeOverlapEmitter with utilities"
```

---

### Task 8: Update RecordEmitter and ClassEmitter (IsVariant only)

**Files:**
- Modify: `src/Spire.SourceGenerators/Emit/RecordEmitter.cs`
- Modify: `src/Spire.SourceGenerators/Emit/ClassEmitter.cs`

Record/class emitters only get IsVariant properties. No backing field changes, no init setters, no IDiscriminatedUnion. `{VariantName}` refers to nested types declared inside the base type — `this is {VariantName}` resolves correctly because the property is emitted in the base type's partial declaration.

For each, after the variant declarations, emit:
```csharp
public bool Is{VariantName} => this is {VariantName};
```

- [ ] **Step 1: Add IsVariant to RecordEmitter** — emit in the base abstract record, after variant re-declarations
- [ ] **Step 2: Add IsVariant to ClassEmitter** — same pattern
- [ ] **Step 3: Build to verify**
- [ ] **Step 4: Commit**

```bash
git add src/Spire.SourceGenerators/Emit/RecordEmitter.cs src/Spire.SourceGenerators/Emit/ClassEmitter.cs
git commit -m "feat(emitter): add IsVariant properties to Record and Class emitters"
```

---

### Task 9: Update snapshot tests

**Files:**
- Modify: all ~61 `output.cs` files in `tests/Spire.SourceGenerators.Tests/cases/`

The snapshot tests compare generated output against expected `output.cs` files. After emitter changes, the generated output will differ.

Note: JSON emitter outputs (`*_Stj.g.cs`, `*_Nsj.g.cs`) should be unaffected — JSON emitters reference `value.kind` (access syntax unchanged) and don't re-emit the kind declaration. Verify they pass without changes.

**Snapshot update strategy:** The test framework's `GeneratorSnapshotTestBase` throws `XunitException` with the actual generated output in the message on mismatch. For 61 files, a script-based approach is needed:

1. Write a small script or one-off program that runs each input.cs through the generator and writes the output to the corresponding output.cs file.
2. Alternatively, modify the test base temporarily to write actual output to disk when a mismatch is detected (update mode), run once, then revert.

- [ ] **Step 1: Run snapshot tests to see failures**

Run: `dotnet test tests/Spire.SourceGenerators.Tests/ --filter "FullyQualifiedName~Snapshot"`
Expected: Many failures (struct emitter + record/class outputs changed)

- [ ] **Step 2: Update all output.cs files** — use one of the approaches above to regenerate expected outputs from current emitter code.

- [ ] **Step 3: Run snapshot tests again**

Run: `dotnet test tests/Spire.SourceGenerators.Tests/ --filter "FullyQualifiedName~Snapshot"`
Expected: All pass

- [ ] **Step 4: Commit**

```bash
git add tests/Spire.SourceGenerators.Tests/cases/
git commit -m "test: update snapshot outputs for DU utility changes"
```

---

### Task 10: Update behavioral tests and coupled analyzers

**Files:**
- Verify: tests in `tests/Spire.BehavioralTests/Tests/`
- Verify/Modify: `src/Spire.SourceGenerators/Analyzers/TypeSafetyAnalyzer.cs`
- Verify/Modify: `src/Spire.SourceGenerators/Analyzers/FieldAccessSafetyAnalyzer.cs`
- Verify/Modify: `src/Spire.SourceGenerators/Analyzers/PatternAnalyzer.cs`
- Verify: `src/Spire.SourceGenerators/Analyzers/VariantFieldMap.cs`
- Verify: `src/Spire.SourceGenerators/Emit/DeconstructEmitter.cs` (if it exists as standalone — emits `this.kind` references which work identically for properties)

Since `kind` changes from field to property, analyzers that check `IFieldReferenceOperation` for `kind` will no longer match. They should already check `IPropertyReferenceOperation` too — verify both paths are covered. The `IFieldReferenceOperation` path becomes dead code but is harmless.

Behavioral tests reference `shape.kind` — property access syntax is identical to field access, so no compilation changes expected. However, behavioral tests may also need recompilation since the generated code changed.

- [ ] **Step 1: Check behavioral tests compile and pass**

Run: `dotnet test tests/Spire.BehavioralTests/`
Expected: If they fail, identify what changed and fix.

- [ ] **Step 2: Check all coupled analyzer files that reference `kind` by name**

Files to verify: `TypeSafetyAnalyzer.cs`, `FieldAccessSafetyAnalyzer.cs`, `PatternAnalyzer.cs`, `VariantFieldMap.cs`. Update `IFieldReferenceOperation` checks to `IPropertyReferenceOperation` if needed, or confirm both paths are already covered.

- [ ] **Step 3: Run all generator-coupled analyzer tests**

Run: `dotnet test tests/Spire.SourceGenerators.Tests/ --filter "FullyQualifiedName~Exhaustiveness or FullyQualifiedName~TypeSafety or FullyQualifiedName~FieldAccess"`

- [ ] **Step 4: Commit if changes were needed**

```bash
git add src/Spire.SourceGenerators/Analyzers/ tests/Spire.BehavioralTests/
git commit -m "fix: update analyzers and behavioral tests for kind field→property change"
```

---

### Task 11: Full verification

- [ ] **Step 1: Full build (including benchmarks)**

Run: `dotnet build` and `dotnet build benchmarks/Spire.Benchmarks/`
Expected: 0 errors. Benchmark types use the same emitters via `[BenchmarkUnion]`, so they will get IsVariant properties and init setters too. Verify they compile.

- [ ] **Step 2: Full test run**

Run: `dotnet test`
Expected: All tests pass across all test projects

- [ ] **Step 3: Verify init fallback works**

Manually verify: when `IsExternalInit` is not in the compilation, emitters fall back to `readonly` fields and expression-bodied properties. Check the `HasInitProperties` flag branching in at least one emitter.

- [ ] **Step 4: Final commit if any fixups needed**
