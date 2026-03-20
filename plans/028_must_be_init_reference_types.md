# Extend [MustBeInit] to Reference Types — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Extend `[MustBeInit]` attribute and analyzers SPIRE001/002/003/006 to cover classes and record classes, treating `null` as uninitialized for reference types.

**Architecture:** Change `AttributeTargets` from `Struct` to `Struct | Class`. Remove `TypeKind.Struct` guards in affected analyzers. Add nullable annotation awareness — skip diagnostics when element/type is nullable-annotated (`T?`), flag otherwise. Struct behavior unchanged.

**Tech Stack:** Roslyn 5.0.0 IOperation API, `ITypeSymbol.NullableAnnotation`, `IArrayTypeSymbol.ElementNullableAnnotation`

---

## Semantics

For reference types marked with `[MustBeInit]`:
- `null == uninitialized`
- In `#nullable enable` context: flag `T`, skip `T?`
- In `#nullable disable` context (annotation == `None`): always flag

## Rules affected

| Rule | Current | Change |
|------|---------|--------|
| Attribute | `AttributeTargets.Struct` | `Struct \| Class` |
| SPIRE001 | Array/stackalloc of struct | + reference types, nullable check |
| SPIRE002 | Fieldless struct warning | + classes/records |
| SPIRE003 | `default(T)` of struct | + reference types, nullable check |
| SPIRE006 | Clear of struct elements | + reference types, nullable check |
| SPIRE004/005/007/008 | Struct-only | **No change** |

## Nullable annotation logic

```
if (type.IsReferenceType && annotation == NullableAnnotation.Annotated)
    → skip (user explicitly allows null via T?)
else
    → flag (struct, or non-nullable/oblivious reference type)
```

API sources per context:
- Array creation (`new T[n]`): `IArrayTypeSymbol.ElementNullableAnnotation`
- Type arguments (`GC.AllocateArray<T>`, `ArrayPool<T>.Rent`, etc.): `ITypeSymbol.NullableAnnotation`
- `typeof(T)` in `Array.CreateInstance`: always `None` — always flag
- `default(T)`: `IDefaultValueOperation.Type.NullableAnnotation`
- `Array.Clear`/`Span<T>.Clear`: element type's `NullableAnnotation`
- `stackalloc`: struct-only — no change needed

## File map

### Modified files
| File | Change |
|------|--------|
| `src/Spire.Analyzers/MustBeInitAttribute.cs` | `AttributeTargets.Struct \| AttributeTargets.Class` |
| `src/Spire.Analyzers.Utils/MustBeInitChecks.cs` | Add `IsNullableAnnotatedReference` utility |
| `src/Spire.Analyzers/Descriptors.cs` | Update SPIRE001/002/003/006 messages: "struct" → "type" |
| `src/Spire.Analyzers/Rules/SPIRE001ArrayOfMustBeInitStructAnalyzer.cs` | Add nullable check for reference element types |
| `src/Spire.Analyzers/Rules/SPIRE002MustBeInitOnFieldlessTypeAnalyzer.cs` | Remove `TypeKind.Struct` guard |
| `src/Spire.Analyzers/Rules/SPIRE003DefaultOfMustBeInitStructAnalyzer.cs` | Remove `TypeKind.Struct` guard, add nullable check |
| `src/Spire.Analyzers/Rules/SPIRE006ClearOfMustBeInitElementsAnalyzer.cs` | Remove `TypeKind.Struct` guard, add nullable check |

### Modified test files (add class/record types)
| File | Change |
|------|--------|
| `tests/.../SPIRE001/cases/_shared.cs` | Add `MustInitClass`, `MustInitRecord`, `PlainClass` |
| `tests/.../SPIRE002/cases/_shared.cs` | Add class/record types |
| `tests/.../SPIRE003/cases/_shared.cs` | Add `MustInitClass`, `MustInitRecord`, `PlainClass` |
| `tests/.../SPIRE006/cases/_shared.cs` | Add `MustInitClass`, `MustInitRecord` |

### New test case files (per rule)
See individual tasks below.

---

## Task 1: Attribute + utility + descriptors

**Files:**
- Modify: `src/Spire.Analyzers/MustBeInitAttribute.cs`
- Modify: `src/Spire.Analyzers.Utils/MustBeInitChecks.cs`
- Modify: `src/Spire.Analyzers/Descriptors.cs`

- [ ] **Step 1: Change attribute target**

In `MustBeInitAttribute.cs`:
```csharp
[AttributeUsage(AttributeTargets.Struct | AttributeTargets.Class)]
```

- [ ] **Step 2: Add nullable utility to MustBeInitChecks**

In `MustBeInitChecks.cs`, add:
```csharp
/// Returns true if the type is a nullable-annotated reference type (T?).
/// When true, null is explicitly allowed — callers should skip the diagnostic.
public static bool IsNullableAnnotatedReference(ITypeSymbol type)
{
    return type.IsReferenceType && type.NullableAnnotation == NullableAnnotation.Annotated;
}
```

- [ ] **Step 3: Update descriptor messages**

In `Descriptors.cs`, update these fields to replace "struct" with "type":
- `SPIRE001`: title/messageFormat/description — change "struct" references to "type"
- `SPIRE002`: title/description — change "type" references are already generic, check only
- `SPIRE003`: title/messageFormat/description — change "struct" references to "type"
- `SPIRE006`: title/description — change "struct" references to "type"

- [ ] **Step 4: Build**

Run: `dotnet build`
Expected: success, no errors

- [ ] **Step 5: Run existing tests (no regressions)**

Run: `dotnet test`
Expected: all existing tests pass (struct behavior unchanged)

- [ ] **Step 6: Commit**

```
feat: extend MustBeInit attribute to classes, add nullable utility
```

---

## Task 2: SPIRE002 — extend to classes/records

SPIRE002 warns when `[MustBeInit]` is applied to a type with no instance fields. Simple: remove the `TypeKind.Struct` guard.

**Files:**
- Modify: `src/Spire.Analyzers/Rules/SPIRE002MustBeInitOnFieldlessTypeAnalyzer.cs`
- Modify: `tests/.../SPIRE002/cases/_shared.cs`
- Create: `tests/.../SPIRE002/cases/Detect_FieldlessClass.cs`
- Create: `tests/.../SPIRE002/cases/Detect_FieldlessRecord.cs`
- Create: `tests/.../SPIRE002/cases/NoReport_ClassWithFields.cs`
- Create: `tests/.../SPIRE002/cases/NoReport_RecordWithFields.cs`

- [ ] **Step 1: Add class/record types to `_shared.cs`**

No shared types needed for SPIRE002 — the test cases define their own types inline since each case tests a different type shape.

- [ ] **Step 2: Write failing test — `Detect_FieldlessClass.cs`**

```csharp
//@ should_fail
// Ensure that SPIRE002 IS triggered when [MustBeInit] is on a class with no fields.
[MustBeInit] //~ ERROR
public class EmptyMustInitClass { }
```

- [ ] **Step 3: Write failing test — `Detect_FieldlessRecord.cs`**

```csharp
//@ should_fail
// Ensure that SPIRE002 IS triggered when [MustBeInit] is on a record class with no fields.
[MustBeInit] //~ ERROR
public record EmptyMustInitRecord;
```

- [ ] **Step 4: Write passing test — `NoReport_ClassWithFields.cs`**

```csharp
//@ should_pass
// Ensure that SPIRE002 is NOT triggered when [MustBeInit] is on a class with instance fields.
[MustBeInit]
public class MustInitClassWithField
{
    public int Value;
}
```

- [ ] **Step 5: Write passing test — `NoReport_RecordWithFields.cs`**

```csharp
//@ should_pass
// Ensure that SPIRE002 is NOT triggered when [MustBeInit] is on a record class with properties.
[MustBeInit]
public record MustInitRecordWithField(int Value);
```

- [ ] **Step 6: Run tests — verify detection tests fail, false-positive tests pass**

Run: `dotnet test --filter "FullyQualifiedName~SPIRE002"`
Expected: `Detect_FieldlessClass` and `Detect_FieldlessRecord` FAIL (no diagnostic), others pass.

- [ ] **Step 7: Modify analyzer**

In `SPIRE002MustBeInitOnFieldlessTypeAnalyzer.cs`, replace:
```csharp
if (type.TypeKind != TypeKind.Struct)
    return;
```
with:
```csharp
if (type.TypeKind != TypeKind.Struct && type.TypeKind != TypeKind.Class)
    return;
```

- [ ] **Step 8: Run tests — verify all pass**

Run: `dotnet test --filter "FullyQualifiedName~SPIRE002"`
Expected: all pass

- [ ] **Step 9: Commit**

```
feat: extend SPIRE002 to classes and records
```

---

## Task 3: SPIRE003 — extend to classes/records with nullable awareness

SPIRE003 flags `default(T)` and `default` literal. For reference types, `default` is `null`. Skip when type is nullable-annotated.

**Files:**
- Modify: `src/Spire.Analyzers/Rules/SPIRE003DefaultOfMustBeInitStructAnalyzer.cs`
- Modify: `tests/.../SPIRE003/cases/_shared.cs`
- Create: 6 test case files (see below)

- [ ] **Step 1: Add class/record types to SPIRE003 `_shared.cs`**

Append to existing `_shared.cs`:
```csharp
#nullable enable

[MustBeInit]
public class MustInitClass
{
    public int Value;
    public MustInitClass(int value) { Value = value; }
}

[MustBeInit]
public record MustInitRecord(int Value);

public class PlainClass
{
    public int Value;
}
```

- [ ] **Step 2: Write failing test — `Detect_DefaultClass.cs`**

```csharp
//@ should_fail
// Ensure that SPIRE003 IS triggered when using default on a [MustBeInit] class.
#nullable enable
public class Detect_DefaultClass
{
    MustInitClass Bad() => default!; //~ ERROR
}
```

- [ ] **Step 3: Write failing test — `Detect_DefaultRecord.cs`**

```csharp
//@ should_fail
// Ensure that SPIRE003 IS triggered when using default(T) on a [MustBeInit] record.
#nullable enable
public class Detect_DefaultRecord
{
    MustInitRecord Bad() => default(MustInitRecord)!; //~ ERROR
}
```

- [ ] **Step 4: Write failing test — `Detect_DefaultClass_NullableDisabled.cs`**

```csharp
//@ should_fail
// Ensure that SPIRE003 IS triggered when nullable is disabled and default is used on [MustBeInit] class.
#nullable disable
public class Detect_DefaultClass_NullableDisabled
{
    MustInitClass Bad() => default(MustInitClass); //~ ERROR
}
```

- [ ] **Step 5: Write passing test — `NoReport_DefaultNullableClass.cs`**

```csharp
//@ should_pass
// Ensure that SPIRE003 is NOT triggered when using default on a nullable [MustBeInit] class.
#nullable enable
public class NoReport_DefaultNullableClass
{
    MustInitClass? Ok() => default;
}
```

- [ ] **Step 6: Write passing test — `NoReport_DefaultNullableRecord.cs`**

```csharp
//@ should_pass
// Ensure that SPIRE003 is NOT triggered when using default(T?) on a nullable [MustBeInit] record.
#nullable enable
public class NoReport_DefaultNullableRecord
{
    MustInitRecord? Ok() => default(MustInitRecord?);
}
```

- [ ] **Step 7: Write passing test — `NoReport_DefaultPlainClass.cs`**

```csharp
//@ should_pass
// Ensure that SPIRE003 is NOT triggered when using default on a class without [MustBeInit].
#nullable enable
public class NoReport_DefaultPlainClass
{
    PlainClass? Ok() => default;
}
```

- [ ] **Step 8: Run tests — verify detection tests fail, false-positive tests pass**

Run: `dotnet test --filter "FullyQualifiedName~SPIRE003"`
Expected: 3 `Detect_` tests FAIL, 3 `NoReport_` tests pass, all existing tests pass.

- [ ] **Step 9: Modify analyzer**

In `SPIRE003DefaultOfMustBeInitStructAnalyzer.cs`, replace:
```csharp
// Must be a struct
if (type.TypeKind != TypeKind.Struct)
    return;
```
with:
```csharp
if (type.TypeKind != TypeKind.Struct && type.TypeKind != TypeKind.Class)
    return;

// For reference types, skip if nullable-annotated (null is explicitly allowed)
if (MustBeInitChecks.IsNullableAnnotatedReference(type))
    return;
```

Note: `type` here is `operation.Type as INamedTypeSymbol`. For `default(T?)` where T is a reference type, `operation.Type` will have `NullableAnnotation.Annotated`, so we skip. For `default(T)` where T is non-nullable, it will be `NotAnnotated`, so we flag.

Add `using Spire.Analyzers.Utils;` if not present.

- [ ] **Step 10: Run tests — verify all pass**

Run: `dotnet test --filter "FullyQualifiedName~SPIRE003"`
Expected: all pass

- [ ] **Step 11: Commit**

```
feat: extend SPIRE003 to classes and records with nullable awareness
```

---

## Task 4: SPIRE001 — extend to classes/records with nullable awareness

SPIRE001 flags array allocations. No `TypeKind.Struct` guard exists — the attribute's `AttributeTargets` was the only gate, already changed in Task 1. Need to add nullable annotation checks for reference element types.

**Files:**
- Modify: `src/Spire.Analyzers/Rules/SPIRE001ArrayOfMustBeInitStructAnalyzer.cs`
- Modify: `tests/.../SPIRE001/cases/_shared.cs`
- Create: 6+ test case files (see below)

- [ ] **Step 1: Add class/record types to SPIRE001 `_shared.cs`**

Append to existing `_shared.cs`:
```csharp
#nullable enable

[MustBeInit]
public class MustInitClass
{
    public int Value;
    public MustInitClass(int value) { Value = value; }
}

[MustBeInit]
public record MustInitRecord(int Value);

public class PlainClass
{
    public int Value;
}
```

- [ ] **Step 2: Write failing test — `Detect_NewArrayClass.cs`**

```csharp
//@ should_fail
// Ensure that SPIRE001 IS triggered when creating non-empty array of [MustBeInit] class.
#nullable enable
public class Detect_NewArrayClass
{
    void Bad()
    {
        var arr = new MustInitClass[5]; //~ ERROR
    }
}
```

- [ ] **Step 3: Write failing test — `Detect_NewArrayRecord.cs`**

```csharp
//@ should_fail
// Ensure that SPIRE001 IS triggered when creating non-empty array of [MustBeInit] record.
#nullable enable
public class Detect_NewArrayRecord
{
    void Bad()
    {
        var arr = new MustInitRecord[5]; //~ ERROR
    }
}
```

- [ ] **Step 4: Write failing test — `Detect_NewArrayClass_NullableDisabled.cs`**

```csharp
//@ should_fail
// Ensure that SPIRE001 IS triggered in nullable-disabled context for [MustBeInit] class.
#nullable disable
public class Detect_NewArrayClass_NullableDisabled
{
    void Bad()
    {
        var arr = new MustInitClass[5]; //~ ERROR
    }
}
```

- [ ] **Step 5: Write passing test — `NoReport_NullableArrayClass.cs`**

```csharp
//@ should_pass
// Ensure that SPIRE001 is NOT triggered when array element type is nullable [MustBeInit] class.
#nullable enable
public class NoReport_NullableArrayClass
{
    void Ok()
    {
        var arr = new MustInitClass?[5];
    }
}
```

- [ ] **Step 6: Write passing test — `NoReport_NullableArrayRecord.cs`**

```csharp
//@ should_pass
// Ensure that SPIRE001 is NOT triggered when array element type is nullable [MustBeInit] record.
#nullable enable
public class NoReport_NullableArrayRecord
{
    void Ok()
    {
        var arr = new MustInitRecord?[5];
    }
}
```

- [ ] **Step 7: Write passing test — `NoReport_EmptyArrayClass.cs`**

```csharp
//@ should_pass
// Ensure that SPIRE001 is NOT triggered for empty array of [MustBeInit] class.
#nullable enable
public class NoReport_EmptyArrayClass
{
    void Ok()
    {
        var arr = new MustInitClass[0];
    }
}
```

- [ ] **Step 8: Run tests — verify detection tests fail, false-positive tests pass**

Run: `dotnet test --filter "FullyQualifiedName~SPIRE001"`
Expected: 3 `Detect_` class/record tests FAIL, `NoReport_` tests pass, all existing struct tests pass.

- [ ] **Step 9: Modify analyzer — add nullable check**

In `SPIRE001ArrayOfMustBeInitStructAnalyzer.cs`, in every method that calls `IsMustBeInitWithFields`, add a nullable annotation check **after** the `IsMustBeInitWithFields` check passes.

**`AnalyzeArrayCreation`** — after line 86 (`IsMustBeInitWithFields` check), add:
```csharp
if (MustBeInitChecks.IsNullableAnnotatedReference(elementType))
    return;
```
Wait — for array creation, the `elementType` comes from `arrayType.ElementType`. Its `NullableAnnotation` may or may not match `arrayType.ElementNullableAnnotation`. Use `arrayType.ElementNullableAnnotation` explicitly. Change the check to:
```csharp
if (elementType.IsReferenceType && arrayType.ElementNullableAnnotation == NullableAnnotation.Annotated)
    return;
```

**`AnalyzeArrayCreateInstance`** — element type from `typeof(T)`. `typeof` never carries nullable annotations for reference types (`NullableAnnotation.None`). We flag `None`. No additional check needed — `IsNullableAnnotatedReference` returns false for `None`.

Still, add after `IsMustBeInitWithFields` check:
```csharp
if (MustBeInitChecks.IsNullableAnnotatedReference(elementType))
    return;
```

**`AnalyzeArrayResize`** — element from `method.TypeArguments[0]`. Add after `IsMustBeInitWithFields`:
```csharp
if (MustBeInitChecks.IsNullableAnnotatedReference(elementType))
    return;
```

**`AnalyzeGCAllocate`** — same pattern, add after `IsMustBeInitWithFields`.

**`AnalyzeArrayPoolRent`** — same pattern.

**`AnalyzeSimpleAssignment`** (ImmutableArray Builder) — same pattern.

**`AnalyzeStackAlloc`** — No change. Stackalloc only applies to value types; C# compiler rejects reference types.

- [ ] **Step 10: Run tests — verify all pass**

Run: `dotnet test --filter "FullyQualifiedName~SPIRE001"`
Expected: all pass

- [ ] **Step 11: Commit**

```
feat: extend SPIRE001 to classes and records with nullable awareness
```

---

## Task 5: SPIRE006 — extend to classes/records with nullable awareness

SPIRE006 flags `Array.Clear` and `Span<T>.Clear()`. For reference types, Clear nullifies elements.

**Files:**
- Modify: `src/Spire.Analyzers/Rules/SPIRE006ClearOfMustBeInitElementsAnalyzer.cs`
- Modify: `tests/.../SPIRE006/cases/_shared.cs`
- Create: 4 test case files

- [ ] **Step 1: Add class/record types to SPIRE006 `_shared.cs`**

Append to existing `_shared.cs`:
```csharp
#nullable enable

[MustBeInit]
public class MustInitClass
{
    public int Value;
    public MustInitClass(int value) { Value = value; }
}

[MustBeInit]
public record MustInitRecord(int Value);
```

- [ ] **Step 2: Write failing test — `Detect_ArrayClearClass.cs`**

```csharp
//@ should_fail
// Ensure that SPIRE006 IS triggered when clearing array of [MustBeInit] class.
#nullable enable
public class Detect_ArrayClearClass
{
    void Bad(MustInitClass[] arr)
    {
        Array.Clear(arr, 0, arr.Length); //~ ERROR
    }
}
```

- [ ] **Step 3: Write failing test — `Detect_ArrayClearClass_NullableDisabled.cs`**

```csharp
//@ should_fail
// Ensure that SPIRE006 IS triggered in nullable-disabled context when clearing array of [MustBeInit] class.
#nullable disable
public class Detect_ArrayClearClass_NullableDisabled
{
    void Bad(MustInitClass[] arr)
    {
        Array.Clear(arr, 0, arr.Length); //~ ERROR
    }
}
```

- [ ] **Step 4: Write passing test — `NoReport_ArrayClearNullableClass.cs`**

```csharp
//@ should_pass
// Ensure that SPIRE006 is NOT triggered when clearing array of nullable [MustBeInit] class.
#nullable enable
public class NoReport_ArrayClearNullableClass
{
    void Ok(MustInitClass?[] arr)
    {
        Array.Clear(arr, 0, arr.Length);
    }
}
```

- [ ] **Step 5: Write passing test — `NoReport_ArrayClearPlainClass.cs`**

```csharp
//@ should_pass
// Ensure that SPIRE006 is NOT triggered when clearing array of class without [MustBeInit].
#nullable enable
public class NoReport_ArrayClearPlainClass
{
    void Ok(object[] arr)
    {
        Array.Clear(arr, 0, arr.Length);
    }
}
```

- [ ] **Step 6: Run tests — verify detection tests fail, false-positive tests pass**

Run: `dotnet test --filter "FullyQualifiedName~SPIRE006"`
Expected: `Detect_` tests FAIL, `NoReport_` tests pass, existing struct tests pass.

- [ ] **Step 7: Modify analyzer**

In `SPIRE006ClearOfMustBeInitElementsAnalyzer.cs`:

Replace:
```csharp
if (namedElement.TypeKind != TypeKind.Struct)
    return;
```
with:
```csharp
if (namedElement.TypeKind != TypeKind.Struct && namedElement.TypeKind != TypeKind.Class)
    return;
```

After the `HasInstanceFields` check, add:
```csharp
// For reference types, skip if nullable-annotated
if (MustBeInitChecks.IsNullableAnnotatedReference(elementType!))
    return;
```

Note: `elementType` (from `TryGetArrayClearElement` or `TryGetSpanClearElement`) carries the nullable annotation from usage context. For `Array.Clear(MustInitClass[] arr, ...)`, the element type has the array's element annotation. For `Array.Clear(MustInitClass?[] arr, ...)`, it's annotated.

Wait — `elementType` comes from `IArrayTypeSymbol.ElementType` in `TryGetArrayClearElement`. We need the `ElementNullableAnnotation` from the array symbol, not from `elementType` directly. But `elementType` should already carry the annotation since it's from the array symbol.

Actually, to be safe, check both: use `elementType.NullableAnnotation` (which should reflect the usage-site annotation). If that doesn't work, we may need to pass the `IArrayTypeSymbol` and check `ElementNullableAnnotation`. Start with `elementType.NullableAnnotation` via `IsNullableAnnotatedReference`.

For `Span<T>.Clear()`, element type comes from `containingType.TypeArguments[0]`. The `NullableAnnotation` on the type argument reflects the generic instantiation: `Span<MustInitClass?>` gives `Annotated`.

- [ ] **Step 8: Run tests — verify all pass**

Run: `dotnet test --filter "FullyQualifiedName~SPIRE006"`
Expected: all pass

- [ ] **Step 9: Commit**

```
feat: extend SPIRE006 to classes and records with nullable awareness
```

---

## Task 6: Full regression test

- [ ] **Step 1: Run entire test suite**

Run: `dotnet test`
Expected: all tests pass (existing struct tests + new class/record tests)

- [ ] **Step 2: Commit (if any fixups needed)**

---

## Out of scope

- SPIRE004 (new T()) — compiler handles `new` for classes
- SPIRE005 (Activator.CreateInstance) — calls constructor for classes, not equivalent to default
- SPIRE007 (Unsafe.SkipInit) — value types only
- SPIRE008 (GetUninitializedObject) — could extend but not requested
- File/descriptor renaming (Struct → Type in file names) — cosmetic, deferred
- Doc updates (`docs/rules/SPIRE001.md` etc.) — update after implementation
