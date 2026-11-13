# Record-struct variant coverage matrix

This matrix defines the generator, analyzer, code-fix, and runtime contract for
annotated nested `record struct` variants in struct discriminated unions. It is
derived from [the approved RFC](../../../docs/designs/2026-11-13-record-struct-variants-rfc.md).
The diagnostic IDs are fixed below; the implementation may choose wording within
each confirmed diagnostic category, and each diagnostic/fix outcome below is
required.

The existing method form remains a compatibility control throughout:

```csharp
[Variant] public static partial Shape Circle(float radius);
```

The new form is:

```csharp
[Variant] public record struct Slider(SliderKind kind, int value);
```

The two forms must work independently and in one union. Record variants replace
only the generated public `Types.Variants` payload. Existing union storage,
field extraction/reconstruction, internal Unity serialization wrappers, JSON
converters, guards, equality, and `FormerlySerializedAs` behavior are reused.

## Writer ownership

Split implementation and test work into three independent categories:

1. **Core writer:** generator errors (`SPIRE_DU019`), core behavioral runtime
   tests, JSON, equality, declaration order, and accessor naming.
2. **Unity writer:** Unity serialization generation, `SPIRE028` serialization
   diagnostics, `SPIRE029` reconstruction warnings, managed-reference behavior,
   and `SPIRE028` code-fix before/after tests.
3. **Consumer writer:** generated consumer behavior for guards, exhaustiveness,
   positional/property patterns, and missing-arm code fixes, plus snapshots that
   verify those consumers bind to authored record payloads.

The exact diagnostic IDs are now selected: `SPIRE_DU019` is the generator error
for invalid record variants, `SPIRE028_RecordVariantSerialization` reports
missing Unity serialization attributes, and `SPIRE029_RecordVariantReconstruction`
reports authored positional-member reconstruction behavior. The implementation
chooses diagnostic messages within those categories.

## Snapshot and generated-source cases

Snapshot cases should assert stable public API and diagnostics. They should not
assert unspecified whitespace or Roslyn-generated record members. Each row is an
`input.cs`/`output.cs` pair or a diagnostic snapshot; diagnostic rows use the
confirmed IDs documented above.

### Declaration and generated payload shape

| Case | Input | Required output assertions | Owner |
|---|---|---|---|
| `RecordStructVariant_Basic` | Namespace-level struct union with `[Variant] public record struct Slider(SliderKind kind, int value);` | No generated `Slider` factory; no `Types.Variants.Slider`; `Customization.Slider` is the canonical payload type; union kind, accessor, conversion, and reconstruction use it. | Core writer |
| `RecordStructVariant_Fieldless` | `[Variant] public record struct None;` and `[Variant] public record struct Empty();` | Both are variants with no fields; no duplicate public payloads; kind entries and fieldless accessors exist; no generated factory. | Core writer |
| `RecordStructVariant_MixedForms` | Interleaved record, method, record, and method variants | One unified declaration-ordered `Kind`; generated factories/payloads only for methods; authored record types only for records. | Core writer |
| `RecordStructVariant_NoPartial` | Public non-partial positional record struct with fields | Compiles and uses the authored nested type as canonical payload; no `partial` requirement is introduced. | Core writer |
| `RecordStructVariant_Readonly` | `public readonly record struct` variant with value fields | Generated accessors/reconstruction preserve readonly record payload and field order. | Core writer |
| `RecordStructVariant_GenericUnion` | Namespace-level `Customization<T>` whose record variant fields use `T` and a concrete field | Containing union type parameters are closed correctly; record variant declares no own type parameters; canonical payload uses the containing union's generic arguments with no extra own arity. | Core writer |
| `RecordStructVariant_NestedUnion` | Union nested in a non-generic containing type | Authored payload is `Outer.Customization.Slider`; generated method payloads remain under the existing nested `Types` container. | Core writer |
| `RecordStructVariant_NestedManagedRecordOnly` | Managed nested union whose only variants are record structs, inside a non-generic containing type | Record positional parameters are included in managed storage classification even without factory methods; the union does not incorrectly become an empty/unmanaged overlay. | Core writer |
| `RecordStructVariant_DeclarationOrder` | Four variants split across source partial declarations and interleaved forms | Deterministic cross-partial order selected by implementation is reflected consistently in `Kind`, storage, accessors, JSON, and Unity output. | Core writer |
| `RecordStructVariant_AccessorCollision` | Record type/factory names that normalize to an accessor name (`Slider`/`slider`, `HTTPRequest`, `URL`) | Accessor uses camelCase and exactly one `_value` suffix when it matches a factory or record type; remaining collisions remain compiler diagnostics. | Core writer |
| `RecordStructVariant_FormerName` | Record variant with `FormerlySerializedAs = "OldSlider"` | Alias is serialization-only; no `OldSlider` kind, factory, conversion, or equality member, and no second canonical record payload. Existing internal/serialization-only alias helper types remain allowed. | Core writer |
| `RecordStructVariant_JsonNames` | `[JsonName]` on record type and positional parameters | Variant and field JSON names match existing method-form behavior. No property/field `JsonName` surface is added. | Core writer |
| `RecordStructVariant_ConsumerPatterns` | Mixed union consumed by property and positional patterns | Generated authored payload accessors bind to existing `kind`/payload patterns and deconstruction shapes; no generated `Types.Variants.Record` path is required. | Consumer writer |
| `RecordStructVariant_ConsumerCodeFix` | Missing-arm switch over a record-only or mixed union | Exhaustiveness diagnostic and missing-arm code fix emit canonical authored record payload patterns and compile after the fix. | Consumer writer |
| `RecordStructVariant_ManagedReferenceMarkers` | Positional fields using Spire parameter marker, `[field: SerializeReference]`, and both | Each logical field receives one managed-reference request; both forms are recognized; no duplicate attributes or extra schema fields. | Unity writer |
| `RecordStructVariant_PartialSerialization` | Partial record with synthesized positional properties in a serializable Unity union | Generated partial declaration adds `[Serializable]`; generated properties use `[field: SerializeField]` or `[field: SerializeReference]` according to the marker; existing wrapper shape remains. | Unity writer |
| `RecordStructVariant_PartialAuthoredProperty` | Partial record explicitly redeclares one positional property with no field attribute and one with `[field: SerializeReference]` | Authored property is preserved; missing annotation produces a warning/fix opportunity; existing managed-reference annotation is preserved; generated members do not trigger authored-member warning. | Unity writer |
| `RecordStructVariant_NonPartialSerializable` | Non-partial record already marked `[Serializable]` with all positional backing fields annotated | No missing-serialization warning; existing internal `SerializedPayloads` wrappers remain and use the authored payload. | Unity writer |
| `RecordStructVariant_NonPartialMissingSerialization` | Non-partial record missing type marker, field markers, or both in a serializable union | `SPIRE028_RecordVariantSerialization` points at the variant and describes all applicable remedies; generator still emits a coherent API where existing continuity rules require it. | Unity writer |

### Invalid declarations and continuity diagnostics

| Case | Input | Required output assertions | Owner |
|---|---|---|---|
| `RecordStructVariant_Private` | `[Variant] private record struct Hidden(int value);` | `SPIRE_DU019` reports an error for non-public record variant; no valid canonical payload contract is advertised. | Core writer |
| `RecordStructVariant_Internal` | `[Variant] internal record struct Hidden(int value);` | Same `SPIRE_DU019` accessibility error; public-only requirement is enforced for nested record variants. | Core writer |
| `RecordStructVariant_IndependentGeneric` | `[Variant] public record struct Item<T>(T value);` | `SPIRE_DU019` reports an error for a record variant declaring its own type parameter. | Core writer |
| `RecordStructVariant_RecordClass` | `[Variant] public record Item(int value);` and explicit `record class` | `SPIRE_DU019` rejects both class-based record spellings. | Core writer |
| `RecordStructVariant_OrdinaryStruct` | `[Variant] public struct Item { ... }` | `SPIRE_DU019` rejects ordinary structs; only record structs and method declarations are accepted. | Core writer |
| `RecordStructVariant_OrdinaryClass` | `[Variant] public class Item { ... }` | `SPIRE_DU019` rejects ordinary classes. | Core writer |
| `RecordStructVariant_ParameterlessRecord` | `[Variant] public record struct Item { public int Extra; }` | Valid fieldless variant; extra members do not become union fields. | Core writer |
| `RecordStructVariant_UnannotatedNestedType` | Nested record struct without `[Variant]` beside valid variants | Unannotated type is ignored and does not affect kind order, storage, JSON, or equality. | Core writer |
| `RecordStructVariant_InvalidMethodControl` | Existing invalid non-partial/non-static method declaration | Existing method-form diagnostics remain unchanged. | Core writer |
| `RecordStructVariant_GeneratedApiContinuity` | Invalid record declaration plus source that references generated union API | Intended `SPIRE_DU019` is present without avoidable cascaded missing factory/payload errors where generator continuity is promised. | Core writer |
| `RecordStructVariant_GenericContainingType` | `[DiscriminatedUnion]` struct nested inside `class Outer<T>` with a record variant | Existing `SPIRE_DU018` generic-containing-type restriction remains enforced; this is separate from the valid namespace-level generic union case. | Core writer |

The invalid declaration rows should be paired with controls proving that a
valid public record variant using containing-union type parameters remains legal.

## Behavioral type definitions

Behavioral types should be compiled through the generator driver and loaded for
runtime assertions. Do not depend on private storage names. These types are
suggested shared fixtures; writers may combine cases when the test remains
focused.

| Type | Purpose | Variants |
|---|---|---|
| `RecordVariantBasicUnion` | Basic record-only public API | `Slider(SliderKind kind, int value)`, `None` |
| `RecordVariantMixedUnion` | Interleaved declaration forms and conversion APIs | record `Slider(SliderKind kind, int value)`, method `Color(ColorKind kind, Rgb value)`, record `Point(float x, float y)` |
| `RecordVariantFieldlessUnion` | Empty and absent parameter-list records | `None`, `Empty()`, method `Value(int value)` |
| `RecordVariantGeneric<T>` | Containing union generic field schema | record `Some(T value, int count)`, method `Other(string text)` |
| `RecordVariantGenericPayload<T>` | Canonical payload generic arity | Namespace-level `Union<T>` has record `Some(T value)` with no own type parameters; the authored payload uses the containing union's generic argument and no independent `Some<T>` declaration is generated. |
| `RecordVariantReadonly` | Readonly record payload | record `Value(int value)`, method `Empty()` |
| `RecordVariantNaming` | Accessor collision and acronym behavior | record `HTTPRequest()`, record `URL()`, method `HttpRequest(int value)` |
| `RecordVariantJson` | JSON discriminator and field names | record `[JsonName("slider")] Slider([JsonName("kind")] SliderKind kind, [JsonName("amount")] int value)` |
| `RecordVariantEquality` | Union equality independent of record-generated equality | record `Value(int value, string? note)`, method `Other(int value)` |
| `RecordVariantCustomMember` | Positional replacement warning/reconstruction semantics | record `Counter(int value)` with authored `value` property that transforms the constructor input; additional computed helper property |
| `RecordVariantUnityPartial` | Generated Unity attributes and internal wrappers | partial record `Node(NodeBase? value)` with Spire managed-reference marker |
| `RecordVariantUnityFieldMarker` | Unity attribute forms | record fields covering parameter marker, backing-field marker, and both |
| `RecordVariantUnityNonPartial` | User-authored serialization metadata | non-partial records with fully annotated, type-only missing, field-only missing, and fully missing forms |
| `RecordVariantAlias` | Unity former-name compatibility | record `Current(int value)` with one former name and a method control |
| `RecordVariantNestedManaged` | Managed nested union classification with no method factories | record-only `Node(NodeBase? value)` and `Empty`; record positional parameters still determine managed storage and the union is not misclassified as unmanaged/empty |

## Behavioral assertions

### Construction, conversion, access, and matching

| Test | Type | Required assertion | Owner |
|---|---|---|---|
| `RecordPayload_ConstructsWithNestedType` | `RecordVariantBasicUnion` | `new Union.Slider(...)` compiles and the value can be assigned to the union through the canonical implicit conversion. | Core writer |
| `RecordPayload_NoGeneratedFactoryOrPayload` | `RecordVariantBasicUnion` | Reflection/compilation confirms no `Union.Slider(...)` factory and no `UnionTypes.Variants.Slider` duplicate. | Core writer |
| `RecordPayload_MethodAndRecordConversions` | `RecordVariantMixedUnion` | Method payload and authored record payload both convert to the union and preserve their discriminators. | Core writer |
| `RecordPayload_AccessorReconstructsRecord` | `RecordVariantMixedUnion` | Guarded record accessor returns a fresh `Union.Slider` reconstructed from stored positional values. | Core writer |
| `RecordPayload_FieldExtractionIsShallow` | `RecordVariantMixedUnion` | Reference-valued positional field retains its reference; scalar values are copied into union storage. | Core writer |
| `RecordPayload_NestedManagedRecordOnly` | `RecordVariantNestedManaged` | A record-only nested union with a managed positional field uses the existing managed storage path and round-trips without an empty/factory-only classification failure. | Core writer |
| `RecordPayload_FieldlessMatching` | `RecordVariantFieldlessUnion` | Absent and empty positional records are fieldless, match by kind, and expose no field deconstruction requirement. | Core writer |
| `RecordPayload_PositionalDeconstruct` | `RecordVariantBasicUnion` | Record compiler-generated `Deconstruct` remains usable on the returned payload; method payloads retain generated typed deconstruction. | Core writer |
| `RecordPayload_AdditionalMembersExcluded` | `RecordVariantCustomMember` | Helper/computed/non-positional members do not become union fields or change kind/storage shape. | Core writer |

### Equality and hashes

| Test | Type | Required assertion | Owner |
|---|---|---|---|
| `RecordPayload_UnionEqualsUnion` | `RecordVariantEquality` | Same tag and equal active positional values compare equal; different tags or values compare unequal. | Equality writer |
| `RecordPayload_UnionEqualsRecordPayload` | `RecordVariantEquality` | `union.Equals(recordPayload)` and `(union == recordPayload)` work in both operand orders for the authored canonical type. | Equality writer |
| `RecordPayload_RecordGeneratedEqualityIsNotUsed` | `RecordVariantEquality` | Union equality follows tag/field schema even if the authored record has extra state or custom equality members. | Equality writer |
| `RecordPayload_ObjectEqualsAcceptsOnlyUnion` | `RecordVariantEquality` | Boxed same-union values compare through `Equals(object?)`; boxed record payloads and unrelated types are unequal. | Equality writer |
| `RecordPayload_HashUsesActiveFieldsOnly` | `RecordVariantEquality` | Equal unions have equal hashes; inactive storage, serialization cache, and extra record state do not alter the hash. | Equality writer |
| `RecordPayload_CollectionsUseUnionEquality` | `RecordVariantEquality` | `HashSet<Union>`/`Dictionary<Union, ...>` use generated equality without a custom hasher. | Equality writer |
| `RecordPayload_RefStructAndRecordUnionControls` | Existing ref/record unions | New equality surface remains limited to ordinary non-ref struct unions. | Equality writer |

`union == default` and typed/default ambiguity stay outside this feature and
retain the existing SPIRE003 behavior; add a regression control only to ensure
this implementation does not change that deferred policy.

### Ordering, names, and JSON

| Test | Type | Required assertion | Owner |
|---|---|---|---|
| `RecordPayload_KindPreservesDeclarationOrder` | `RecordVariantMixedUnion` | Interleaved record/method `Kind` values and ordered generated branches follow the unified declaration order. | Core writer |
| `RecordPayload_PartialDeclarationOrderIsDeterministic` | Partial union fixture | Same source parts produce stable order across generator runs; method-only ordering remains unchanged. | Core writer |
| `RecordPayload_AccessorCollisionUsesOneSuffix` | `RecordVariantNaming` | CamelCase accessor colliding with factory or nested record type gets one `_value` suffix; no repeated/custom suffix. | Core writer |
| `RecordPayload_RemainingCollisionUsesCompiler` | Collision fixture | Generator leaves unrelated collisions to normal C# diagnostics. | Diagnostic writer |
| `RecordPayload_JsonRoundTrip` | `RecordVariantJson` | System.Text.Json and Newtonsoft.Json round-trip mixed record/method unions using canonical variant and field names. | JSON writer |
| `RecordPayload_JsonNameParity` | `RecordVariantJson` | Type and positional-parameter `[JsonName]` values match method-form wire names; no property/backing-field `JsonName` requirement. | JSON writer |
| `RecordPayload_FormerNameIsSerializationOnly` | `RecordVariantAlias` | Existing former serialized data restores into the canonical record variant; old name is absent from runtime kind/equality/factory/conversion API. Existing serialization-only alias helper types remain allowed. | Unity writer |

### Authored positional members and warnings

| Test | Type | Required assertion | Owner |
|---|---|---|---|
| `RecordPayload_CustomPositionalMemberWarning` | `RecordVariantCustomMember` | Explicit replacement/transformation of a positional member emits `SPIRE029_RecordVariantReconstruction` with the descriptive reconstruction warning. | Unity writer |
| `RecordPayload_CustomMemberReconstructsDeterministically` | `RecordVariantCustomMember` | Test demonstrates stored field extraction and changed value after reconstruction, as documented. | Core writer |
| `RecordPayload_ExtraComputedMemberNoWarning` | `RecordVariantCustomMember` | Helper methods and unrelated computed properties alone emit no `SPIRE029_RecordVariantReconstruction` warning. | Unity writer |
| `RecordPayload_GeneratedSerializationMemberNoWarning` | Partial Unity fixture | Properties emitted by Spire for serialization do not trigger the authored-member warning. | Unity writer |

### Consumer analyzers and fixes

| Test | Type | Required assertion | Owner |
|---|---|---|---|
| `RecordPayload_PropertyPatternMatches` | `RecordVariantMixedUnion` | A switch/property pattern can match a record variant through its generated accessor and positional fields, while method variants retain their existing pattern shape. | Consumer writer |
| `RecordPayload_PositionalPatternMatches` | `RecordVariantBasicUnion` | A positional pattern uses the authored record's compiler-generated `Deconstruct`; field order and names match the union schema. | Consumer writer |
| `RecordPayload_GuardAnalyzerAcceptsStableRecordAccessor` | `RecordVariantMixedUnion` | Existing SPIRE013/SPIRE014 guard analysis recognizes `IsVariant` plus the authored record payload accessor and does not report a valid guarded read. | Consumer writer |
| `RecordPayload_GuardAnalyzerRejectsUnguardedRead` | `RecordVariantMixedUnion` | Existing guard diagnostics still report an unguarded authored-record accessor read with the same policy as generated payloads. | Consumer writer |
| `RecordPayload_ExhaustivenessFindsRecordVariants` | `RecordVariantMixedUnion` | Exhaustiveness analysis enumerates record and method variants in unified declaration order and reports the correct missing arm. | Consumer writer |
| `RecordPayload_ExhaustivenessFieldlessRecord` | `RecordVariantFieldlessUnion` | Exhaustiveness handles absent/empty positional record variants as fieldless arms. | Consumer writer |
| `RecordPayload_AddMissingArmsRecordBeforeAfter` | Record-only union | Code fix adds the authored record's canonical property/deconstruction arm; before/after source compiles and no generated payload namespace is referenced. | Consumer writer |
| `RecordPayload_AddMissingArmsMixedBeforeAfter` | `RecordVariantMixedUnion` | Code fix adds missing record and method arms in declaration order, preserving existing style and escaping names where needed. | Consumer writer |
| `RecordPayload_ConsumerGeneric` | `RecordVariantGeneric<T>` | Exhaustiveness, patterns, guards, and code fixes close the authored payload with the containing generic arguments and do not invent an extra payload arity. | Consumer writer |

## Unity serialization and managed-reference integration

These cases require the existing Unity generator harness and should distinguish
generated-compilation checks from actual Editor/player persistence. Do not claim
native Unity runtime validation from ordinary Roslyn tests.

| Test | Input | Required assertion | Owner |
|---|---|---|---|
| `Unity_RecordPartial_GeneratesSerializableAndFields` | Partial record in serializable union | Generated partial declaration supplies `[Serializable]` and positional backing-field attributes while preserving the existing internal `SerializedPayloads` wrapper. | Unity writer |
| `Unity_RecordPartial_AuthoredPropertyMissingFieldAttribute` | Partial record redeclares a positional property without field annotation | Diagnostic reports missing annotation and code-fix offer targets the authored property; initializer/body remains intact. | Unity writer |
| `Unity_RecordPartial_ManagedReferencePreserved` | Partial record with Spire marker, Unity field marker, and both | Corresponding generated/retained field uses `[SerializeReference]` once; no `[SerializeField]` is added over it. | Unity writer |
| `Unity_RecordNonPartial_MissingTypeAttribute` | Non-partial record lacks `[Serializable]` in a serializable union | Diagnostic explains type attribute, field attributes, partial conversion, and removal of union serialization opt-in. | Unity writer |
| `Unity_RecordNonPartial_MissingFieldAttributes` | Type is serializable but one or more positional backing fields lack annotations | Diagnostic identifies missing field attributes; code fix adds the appropriate field-targeted attribute. | Unity writer |
| `Unity_RecordNonPartial_MissingTypeAndFields` | Both type and fields are missing attributes | Three applicable fixes are offered independently: type marker, field markers, and `partial`. | Unity writer |
| `Unity_RecordNonPartial_FullyAnnotated` | Type and all positional fields are correctly annotated | No missing-attribute diagnostic; existing wrapper and callback generation compiles. | Unity writer |
| `Unity_RecordFieldless_Serializable` | Fieldless partial and non-partial records | Only type serialization eligibility is required; no positional field annotation is requested. | Unity writer |
| `Unity_RecordFormerName_CanonicalParity` | Canonical record plus one former name | Canonical/former internal wrappers preserve the same logical field attributes and restore behavior; a serialization-only former helper may exist, but it is not a runtime variant. | Unity writer |
| `Unity_RecordManagedReferenceNullAndDerived` | Nullable base-type positional field with either marker | Generated field keeps null, derived runtime type, and managed-reference semantics under Unity-owned eligibility rules. | Unity writer |
| `Unity_RecordManagedReferenceBothMarkers` | Both Spire parameter and Unity field annotations | No duplicate emitted attribute or divergent behavior. | Unity writer |
| `Unity_RecordExtraSerializedMemberExcluded` | Record has extra `[SerializeField]`/`[SerializeReference]` member outside positional schema | Extra member is not included in union storage, payload equality, generated accessor reconstruction, or variant field schema. | Unity writer |

## Code-fix before/after cases

Code fixes must use real Roslyn before/after compilation. Exact diagnostic IDs
and titles are implementation choices, but each fix must be independently
offered only when applicable and must preserve unrelated user text.

| Case | Before | After | Owner |
|---|---|---|---|
| `RecordVariantSerialization_AddSerializable` | Non-partial `record struct Slider(int value)` in serializable union, no type marker | Adds `[Serializable]` to `Slider`; source otherwise unchanged. | Unity writer |
| `RecordVariantSerialization_AddFieldAttributes` | Serializable non-partial record with two positional members missing backing-field markers | Adds `[field: SerializeField]` to both positional members/properties; preserves names/types/initializers. | Unity writer |
| `RecordVariantSerialization_AddManagedReferenceField` | Positional member requested as managed reference but missing `[field: SerializeReference]` | Adds the field-targeted managed-reference attribute, never `[SerializeField]` simultaneously. | Unity writer |
| `RecordVariantSerialization_MarkPartial` | Non-partial record with missing type/field annotations | Adds `partial` to the record declaration so the generator can synthesize missing serialization members. | Unity writer |
| `RecordVariantSerialization_MarkPartialWithAuthoredProperty` | Non-partial record explicitly defines one positional property | Adds `partial` without deleting or replacing the authored property; subsequent diagnostic remains fixable by annotation. | Unity writer |
| `RecordVariantSerialization_AllFixesOffered` | Type and every positional field lack required annotations | Each applicable fix produces a compilable result; applying one fix does not silently apply the other fixes. | Unity writer |
| `RecordVariantSerialization_NoFixForAnnotatedField` | Existing `[field: SerializeReference]` or `[field: SerializeField]` | No duplicate-attribute fix is offered. | Unity writer |
| `RecordVariantSerialization_FieldlessOnlyTypeFix` | Fieldless non-partial record missing `[Serializable]` | Offers type marker and partial fixes; does not invent a field annotation. | Unity writer |
| `RecordVariantSerialization_NonSerializableUnionControl` | Union has no Unity serialization opt-in | No record serialization diagnostic/fix is offered solely due to absent Unity attributes. | Unity writer |
| `RecordVariantMemberWarning_DescriptiveWarningControl` | Explicit positional property changes extracted/reconstructed value | `SPIRE029_RecordVariantReconstruction` is emitted, but no serialization code fix is offered for this separate concern. | Unity writer |

## Boundaries and verification

- Method-only unions must pass all pre-existing generator, JSON, Unity, equality,
  compatibility, and snapshot tests unchanged except for intentional shared
  infrastructure needed to recognize mixed variants.
- Record classes, ordinary classes, ordinary structs, non-public records, and
  independently generic records are errors. A record variant using containing
  union type parameters is valid.
- Record payload conversion is field extraction and reconstruction; it never
  retains the authored payload instance as a hidden union object.
- The internal serialization wrapper remains the canonical wrapper around the
  selected public payload type. Do not introduce a second public or internal
  mirror payload solely for record variants.
- Equality is tag plus active positional fields using the existing comparer
  contract; record-generated equality, extra members, inactive storage, and
  serialization caches do not change union equality.
- `union == default` policy is deferred and must remain unchanged.
- Run compiler-backed tests under the repository's existing method-compatible
  profile and C# 10-capable record profile. Unity claims require the existing
  installed-Editor harness where applicable.
