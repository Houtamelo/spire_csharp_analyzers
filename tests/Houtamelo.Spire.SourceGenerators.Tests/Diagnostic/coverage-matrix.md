# Generator Diagnostic Test Coverage Matrix

## Infrastructure notes

**ID mapping in Diagnostics.cs (actual IDs, not task description):**
- SPIRE_DU002: ref struct not supported — exists in descriptor map but is NEVER emitted (ref struct is now supported). No new cases needed.
- SPIRE_DU003: No variants found
- SPIRE_DU004: Layout parameter ignored for record/class
- SPIRE_DU005: Generic structs cannot use Overlap layout
- SPIRE_DU006: System.Text.Json not referenced
- SPIRE_DU007: Newtonsoft.Json not referenced
- SPIRE_DU008: ref struct cannot use JSON generation
- SPIRE_DU009: UnsafeOverlap requires AllowUnsafeBlocks
- SPIRE_DU010: Field name conflict across variants

**SPIRE_DU009 is untestable with the current test helper**: `GeneratorTestHelper.RunGenerator` hardcodes
`allowUnsafe: true` in `CSharpCompilationOptions`. UnsafeOverlap never fires SPIRE_DU009. Any case for
SPIRE_DU009 would be a false should_fail. (?) Lead should decide whether to make AllowUnsafe configurable
in the test helper or skip SPIRE_DU009 coverage.

**Location.None diagnostics**: SPIRE_DU006, SPIRE_DU007, SPIRE_DU009, and SPIRE_DU010 are reported with
`Location.None`. In the test framework, `GetLineSpan().StartLinePosition.Line + 1` resolves to line 1 for
these. The `//~ ERROR` marker must appear on line 1 of the case file (the `//@ should_fail` header line
itself is stripped before compilation but the line numbering in the test framework is based on the original
source). Since the header comment `//@ should_fail` is on line 1 and is stripped as a comment, the
diagnostic check matches line 1 of the source. The `//~ ERROR` marker must be placed on line 1.

---

## Category 1: SPIRE_DU003 — No variants found (new union kinds)

The existing test covers only `struct`. Missing: `record` and `class` kinds.

### should_fail

| File Name | Description |
|-----------|-------------|
| `Detect_NoVariants_Record` | Record with no nested inheriting types — SPIRE_DU003 on type identifier |
| `Detect_NoVariants_Class` | Class with no nested inheriting types — SPIRE_DU003 on type identifier |
| `Detect_NoVariants_RecordGeneric` | Generic record with no nested inheriting types |
| `Detect_NoVariants_ClassGeneric` | Generic class with no nested inheriting types |
| `Detect_NoVariants_StructMethodsNotVariant` | Struct with static methods but none marked `[Variant]` |

### should_pass

| File Name | Description |
|-----------|-------------|
| `NoReport_Record_WithVariants` | Record with two nested inheriting records — valid |
| `NoReport_Class_WithVariants` | Class with two nested inheriting classes — valid |
| `NoReport_Record_GenericWithVariants` | Generic record with two variants |
| `NoReport_Struct_ManyVariants` | Struct with five `[Variant]` methods — valid, no diagnostic |
| `NoReport_Class_NestedClassVariants` | Class whose variants are plain nested classes (no ctor params) |

---

## Category 2: SPIRE_DU004 — Layout ignored on record/class (all layout values)

Existing tests use only `Layout.Overlap` on record and `Layout.BoxedFields` on class. Need all other
layout values on both kinds.

### should_fail

| File Name | Description |
|-----------|-------------|
| `Detect_LayoutOnRecord_BoxedFields` | `[DiscriminatedUnion(Layout.BoxedFields)]` on record |
| `Detect_LayoutOnRecord_BoxedTuple` | `[DiscriminatedUnion(Layout.BoxedTuple)]` on record |
| `Detect_LayoutOnRecord_Additive` | `[DiscriminatedUnion(Layout.Additive)]` on record |
| `Detect_LayoutOnRecord_UnsafeOverlap` | `[DiscriminatedUnion(Layout.UnsafeOverlap)]` on record |
| `Detect_LayoutOnClass_Overlap` | `[DiscriminatedUnion(Layout.Overlap)]` on class |
| `Detect_LayoutOnClass_BoxedTuple` | `[DiscriminatedUnion(Layout.BoxedTuple)]` on class |
| `Detect_LayoutOnClass_Additive` | `[DiscriminatedUnion(Layout.Additive)]` on class |
| `Detect_LayoutOnClass_UnsafeOverlap` | `[DiscriminatedUnion(Layout.UnsafeOverlap)]` on class |
| `Detect_LayoutOnGenericRecord` | Layout attribute on a generic record |
| `Detect_LayoutOnGenericClass` | Layout attribute on a generic class |

### should_pass

| File Name | Description |
|-----------|-------------|
| `NoReport_Record_AutoLayout` | `[DiscriminatedUnion]` (Layout.Auto default) on record — no warning |
| `NoReport_Class_AutoLayout` | `[DiscriminatedUnion]` (Layout.Auto default) on class — no warning |
| `NoReport_Struct_OverlapExplicit` | `[DiscriminatedUnion(Layout.Overlap)]` on struct — valid |
| `NoReport_Struct_BoxedFields` | `[DiscriminatedUnion(Layout.BoxedFields)]` on struct — valid |
| `NoReport_Struct_BoxedTuple` | `[DiscriminatedUnion(Layout.BoxedTuple)]` on struct — valid |
| `NoReport_Struct_Additive` | `[DiscriminatedUnion(Layout.Additive)]` on struct — valid |
| `NoReport_Struct_UnsafeOverlap` | `[DiscriminatedUnion(Layout.UnsafeOverlap)]` on struct — valid (allowUnsafe=true) |
| `NoReport_ReadonlyStruct_OverlapLayout` | `readonly` struct with explicit Overlap layout — valid |

---

## Category 3: SPIRE_DU005 — Overlap on generic struct (more variants)

Existing test uses `Layout.Overlap` explicitly on a generic struct with two type parameters. Need edge cases.

### should_fail

| File Name | Description |
|-----------|-------------|
| `Detect_OverlapGeneric_SingleTypeParam` | `[DiscriminatedUnion(Layout.Overlap)]` on `struct Foo<T>` with one type param |
| `Detect_OverlapGeneric_ThreeTypeParams` | `[DiscriminatedUnion(Layout.Overlap)]` on `struct Foo<T, U, V>` |
| `Detect_OverlapGeneric_Readonly` | `readonly` generic struct with Overlap — still an error |

### should_pass

| File Name | Description |
|-----------|-------------|
| `NoReport_Overlap_NonGenericStruct` | Explicit `Layout.Overlap` on non-generic struct — valid |
| `NoReport_BoxedFields_GenericStruct` | `Layout.BoxedFields` on generic struct — valid |
| `NoReport_BoxedTuple_GenericStruct` | `Layout.BoxedTuple` on generic struct — valid |
| `NoReport_Additive_GenericStruct` | `Layout.Additive` on generic struct — valid |

---

## Category 4: SPIRE_DU006 — SystemTextJson not referenced

No existing tests. STJ is NOT included in the test compilation's references (only BCL trustedAssemblies).
The diagnostic is reported at `Location.None` — line 1 in the test framework. The `//~ ERROR` marker must
be on line 1 of the test case file.

### should_fail

| File Name | Description |
|-----------|-------------|
| `Detect_STJNotReferenced` | `Json = JsonLibrary.SystemTextJson` on a valid struct — STJ not in refs |
| `Detect_STJNotReferenced_Generic` | `Json = JsonLibrary.SystemTextJson` on valid generic struct (BoxedFields auto-strategy) |
| `Detect_STJNotReferenced_Record` | `Json = JsonLibrary.SystemTextJson` on valid record |
| `Detect_STJNotReferenced_Class` | `Json = JsonLibrary.SystemTextJson` on valid class |
| `Detect_BothJsonNotReferenced` | `Json = JsonLibrary.SystemTextJson \| JsonLibrary.NewtonsoftJson` — both missing; two diagnostics on line 1 |
| `Detect_STJNotReferenced_Additive` | `Json = JsonLibrary.SystemTextJson` on Additive layout struct |

### should_pass

| File Name | Description |
|-----------|-------------|
| `NoReport_STJ_JsonNone` | `Json = JsonLibrary.None` explicitly — no STJ diagnostic |
| `NoReport_STJ_DefaultNoJson` | `[DiscriminatedUnion]` with no Json property — defaults to None |

---

## Category 5: SPIRE_DU007 — NewtonsoftJson not referenced

No existing tests. NSJ is NOT in the test compilation references. Same location mechanics as SPIRE_DU006.

### should_fail

| File Name | Description |
|-----------|-------------|
| `Detect_NSJNotReferenced` | `Json = JsonLibrary.NewtonsoftJson` on a valid struct |
| `Detect_NSJNotReferenced_Record` | `Json = JsonLibrary.NewtonsoftJson` on valid record |
| `Detect_NSJNotReferenced_Class` | `Json = JsonLibrary.NewtonsoftJson` on valid class |

### should_pass

| File Name | Description |
|-----------|-------------|
| `NoReport_NSJ_JsonNone` | `Json = JsonLibrary.None` explicitly — no NSJ diagnostic |

---

## Category 6: SPIRE_DU008 — ref struct with JSON (more combinations)

Two existing cases: `JsonLibrary.SystemTextJson` and `JsonLibrary.None | JsonLibrary.SystemTextJson`.
Missing: NSJ-only and both libraries combined.

### should_fail

| File Name | Description |
|-----------|-------------|
| `Detect_RefStructJson_NSJ` | `Json = JsonLibrary.NewtonsoftJson` on ref struct |
| `Detect_RefStructJson_BothLibraries` | `Json = JsonLibrary.SystemTextJson \| JsonLibrary.NewtonsoftJson` on ref struct |
| `Detect_RefStructJson_ReadonlyRef` | `readonly ref partial struct` with `Json = JsonLibrary.SystemTextJson` |

### should_pass

| File Name | Description |
|-----------|-------------|
| `NoReport_RefStruct_NoJson` | ref struct with no Json property — no diagnostic |
| `NoReport_RefStruct_JsonNone` | ref struct with `Json = JsonLibrary.None` — already exists, confirm pattern |
| `NoReport_RegularStruct_STJ` | Non-ref struct with STJ — not reported by SPIRE_DU008 (only SPIRE_DU006 fires) |

---

## Category 7: SPIRE_DU010 — Field name conflict across variants

No existing tests. Diagnostic is reported at `Location.None` → line 1 in the test framework.

The conflict is: same field name, different types across variants. Same name + same type is OK.

### should_fail

| File Name | Description |
|-----------|-------------|
| `Detect_FieldConflict_TwoVariants` | Two variants share field name `value` but types differ (`int` vs `string`) |
| `Detect_FieldConflict_ThreeVariants` | Three variants all have field `x` but with three different types |
| `Detect_FieldConflict_MultipleConflicts` | Two different conflicting field names across variants — two SPIRE_DU010 diagnostics on line 1 |
| `Detect_FieldConflict_NullableVsNonNullable` | Field `value` typed as `string` in one variant vs `string?` in another — conflict (?) |
| `Detect_FieldConflict_GenericStruct` | Generic struct with field `item` typed as `T` in one variant and `int` in another |
| `Detect_FieldConflict_BoxedFieldsLayout` | Same conflict with explicit BoxedFields layout |
| `Detect_FieldConflict_AdditiveLayout` | Same conflict with Additive layout |

### should_pass

| File Name | Description |
|-----------|-------------|
| `NoReport_SharedFieldSameType` | Two variants share field name `value` with the same type `int` — no conflict |
| `NoReport_UniqueFieldNames` | All variants have completely distinct field names |
| `NoReport_FieldlessVariants` | Mix of fieldless and fielded variants, no name overlap |
| `NoReport_SharedFieldAllVariants` | All three variants share field `id` of type `int` — same type, no conflict |

---

## Category 8: Multiple diagnostics and combined edge cases

Cases where multiple rules fire simultaneously or where previous error-gated checks interact.

### should_fail

| File Name | Description |
|-----------|-------------|
| `Detect_LayoutOnRecord_NoVariants` | Layout on record AND no variants — both SPIRE_DU004 and SPIRE_DU003 fire on same type identifier |
| `Detect_OverlapGeneric_NoVariants` | Overlap on generic struct with no `[Variant]` methods — only SPIRE_DU005 fires (early-exit path) |
| `Detect_FieldConflict_AndSTJMissing` | Field conflict + STJ requested but not referenced — both SPIRE_DU010 (line 1) and SPIRE_DU006 (line 1) fire |

### should_pass

| File Name | Description |
|-----------|-------------|
| `NoReport_NestedInsideClass` | `[DiscriminatedUnion]` struct nested inside a plain class — valid, no diagnostics |
| `NoReport_GlobalNamespace` | `[DiscriminatedUnion]` struct in global namespace (no `namespace` declaration) |
| `NoReport_InternalStruct` | `internal` struct with variants — valid |
| `NoReport_ReadonlyStruct_NoLayout` | `readonly partial struct` with auto-layout — valid |
| `NoReport_RecordStruct` | `record struct` with `[Variant]` methods (treated as "struct" path) — valid |
