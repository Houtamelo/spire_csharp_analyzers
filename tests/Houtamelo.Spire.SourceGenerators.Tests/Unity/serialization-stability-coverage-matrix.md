# Unity Serialization Stability Test Coverage Matrix

This matrix is the contract for the payload-first Unity serialization change.
It covers the existing five runtime layouts (Additive, BoxedFields,
BoxedTuple, UnsafeOverlap, and non-generic Overlap) without changing their
runtime storage. Every layout has one active per-variant managed-reference
payload whose concrete type identifies the variant; `_kind` remains runtime
state and is not a Unity serialized field.

The alias API is one nullable `Variant.FormerlySerializedAs` string. `null` and
the empty string mean unset, and the current variant name is a harmless
no-op. A non-empty alias causes a private former-name payload class with the same
typed `[SerializeField]` fields to be emitted. A former-name payload is read through
the current factory and the next serialization writes the canonical payload
type. Valid old assets are payload-first: a valid payload remains readable even
if an old serialized integer discriminator is present or stale. An integer-only
asset does not require a compatibility overload.

The exact invalid-data warning is:
`invalid union serialization data, defaulting to {FirstVariantName}`.
The warning is emitted once for one invalid deserialization event; property
drawer repaint must not repeat it. In the Editor, including Play Mode, the
payload cache remains attached. In a player, reconstruction replaces the
concrete payload with a shared non-concrete marker after the runtime value is
restored. The player tests must distinguish a
source cache that is intentionally reusable after `Instantiate` from a clone
cache that is eligible for collection.

Snapshot rows describe a source fixture and its generated output. Semantic
assertions inspect symbols and attributes, so private names are asserted only
when they are part of the serialized payload identity or an approved diagnostic
surface. Native rows run against the installed Unity 6000.5.6f1 fixture and
must use actual Unity serialization, drawer, Mono-player, and IL2CPP-player
paths where stated.

## Section 1: Snapshot Tests

### Category A: Canonical payload schema across layouts

| Case Name | Description |
|-----------|-------------|
| `Canonical_AdditiveActivePayload` | Input: serializable Additive union with fielded, multi-field, and fieldless variants. Output: one active `[SerializeReference]` payload hierarchy with typed `[SerializeField]` fields for every variant; no per-variant serialized fields are emitted. |
| `Canonical_BoxedFieldsActivePayload` | Input: equivalent explicit BoxedFields union. Output: the same canonical payload-type serialized shape while BoxedFields runtime slots remain nonserialized. |
| `Canonical_BoxedTupleActivePayload` | Input: equivalent explicit BoxedTuple union. Output: the same active typed payload shape while tuple/object runtime storage remains nonserialized. |
| `Canonical_UnsafeOverlapActivePayload` | Input: equivalent explicit UnsafeOverlap union. Output: the same active typed payload shape; raw bytes and managed runtime slots are not serialized. |
| `Canonical_OverlapActivePayload` | Input: accepted non-generic Overlap union. Output: the same active typed payload shape; explicit runtime storage remains outside the Unity schema. |
| `Canonical_FieldlessAndFieldedPayloads` | Input: fieldless and fielded variants in one union. Output: a serializable empty payload class for the fieldless variant and typed fields only on fielded payloads; payload type alone identifies either variant. |
| `Canonical_MultiFieldNullableTupleFields` | Input: payload fields containing nullable references, nullable values, and a tuple. Output: every declared field remains typed and `[SerializeField]`; no field is silently dropped while generated source compiles. |
| `Canonical_GenericIntAndStringPayloads` | Input: `StabilityGenericUnion<int>` and `StabilityGenericUnion<string>`. Output: closed typed payloads preserve `int` and `string` fields and share the non-generic payload base required by the Unity managed-reference surface. |
| `Canonical_NestedGenericContainingUnion` | Input: a serializable union nested in `GenericOwner<T>` and closed for `int` and `string`. Output: generated payload and drawer qualifications remain valid for each closure. |
| `IncrementalGeneration_UnchangedUnionOutputIsCachedWhenUnrelatedSourceChanges` | A real tracked incremental-driver run changes an unrelated syntax tree while preserving the union tree; the union generated source remains identical and its tracked output is cached. |
| `Canonical_RuntimeKindIsNonSerialized` | Input: any serializable union. Output: runtime `_kind` (or equivalent generated discriminator state) has `[NonSerialized]`, while no discriminator field has `[SerializeField]`; the only active Unity value is the managed-reference payload. |
| `Canonical_NoSerializedIntegerDiscriminator` | Input: all five layout declarations. Output: generated source contains no serialized integer discriminator and callback code selects the variant by payload runtime type. |
| `Canonical_FormerNamePayloadFieldParity` | Input: a variant with primitive, nullable, tuple, Unity object, nested-union, and collection fields plus one former name. Output: the canonical and private former-name payload classes have the same typed `[SerializeField]` field set and compatible factory-copy paths. |

### Category B: Former payload names and canonical migration output

| Case Name | Description |
|-----------|-------------|
| `FormerlySerializedAs_BasicFieldedVariant` | Input: `[Variant(FormerlySerializedAs = "OldText")] Text(string value)`. Output: current `Text` payload plus a private `OldText` payload class with the same serialized field. |
| `FormerlySerializedAs_FieldlessVariant` | Input: fieldless variant renamed from `OldEmpty`. Output: both empty payload types exist, and the former-name type maps to the current fieldless factory. |
| `FormerlySerializedAs_NestedChild` | Input: parent payload containing a child union whose variant has a former name. Output: former-name child payload mapping is emitted without changing parent field type or callback recursion. |
| `FormerlySerializedAs_GenericInt` | Input: `StabilityGenericUnion<int>` with one former variant name. Output: the closed former-name payload carries `int` fields and maps to the current `int` factory. |
| `FormerlySerializedAs_GenericString` | Input: `StabilityGenericUnion<string>` with one former variant name. Output: the closed former-name payload carries `string` fields and maps to the current `string` factory. |
| `FormerlySerializedAs_AllFiveLayouts` | Input: the same one-alias rename in Additive, BoxedFields, BoxedTuple, UnsafeOverlap, and Overlap declarations. Output: every accepted layout emits the same alias migration surface and keeps layout-specific runtime storage nonserialized. |
| `FormerlySerializedAs_CanonicalizeAfterRead` | Input: a union with current and former payload classes. Output: callback flow accepts either payload, constructs the current variant, and the next `OnBeforeSerialize` path writes only the current payload type. |
| `FormerlySerializedAs_ReorderedVariants` | Input: a source declaration whose current variant order differs from the source used to author an old asset. Output: payload-type mapping is independent of enum ordinal/order; no integer discriminator is consulted for a valid payload. |
| `FormerlySerializedAs_CurrentNameNoOp` | Input: a variant whose former name equals its current name. Output: no duplicate former-name class, no duplicate migration branch, and no diagnostic. |
| `FormerlySerializedAs_NullAndEmptyUnset` | Input: omitted, `null`, and empty former-name values. Output: canonical-only payload generation with no former-name class and no diagnostic. |
| `FormerlySerializedAs_OneAliasOnly` | Input: each variant has at most one nullable alias property. Output: exactly one former-name payload type per non-empty alias; no list/array of aliases or additional compatibility schema is generated. |
| `FormerlySerializedAs_KeywordAliasIsValid` | Input: a valid C# keyword used as the former variant name and an escaped current variant declaration. Output: one private former-name payload with the same typed fields and no diagnostic. |
| `FormerlySerializedAs_SanitizedNamesRemainDistinct` | Input: valid former names whose generated payload identifiers remain distinct. Output: each old payload type has a deterministic private name and no collision with canonical or other former-name types. |

### Category C: Alias and generated-surface diagnostics

| Case Name | Description |
|-----------|-------------|
| `FormerlySerializedAs_InvalidOrAmbiguousAlias_ReportsDU017` | Theory inputs cover malformed punctuation, aliases equal to another current variant, duplicate aliases, and sanitized former-name-payload collisions; each reports `SPIRE_DU017` at the alias declaration without invented secondary diagnostics. |
| `FormerlySerializedAs_UserGeneratedMemberCollision_SPIRE_DU015` | Input: a user declaration colliding with an existing generated member, payload, callback, or drawer name. Output: the established `SPIRE_DU015` remains the diagnostic; alias validation does not relabel a user-member collision as `SPIRE_DU017`. |
| `UnitySerializedVariantName_GeneratesDistinctPayloadTypes` | Input: a current variant named `Serialized`. Output: the renamed private abstract payload base and concrete `__SpireSerializedPayload` remain distinct and generation succeeds. |
| `FormerlySerializedAs_SerializedAliasGeneratesDistinctPayloadTypes` | Input: a former name `Serialized`. Output: the renamed private abstract payload base, canonical payload, and former-name `__SpireSerializedPayload` remain distinct and generation succeeds. |
| `NonUnitySerializedVariantName_RemainsValid` | Input: a non-`[Serializable]` union with a variant named `Serialized`. Output: normal non-Unity generation remains valid and emits no Unity payload surface. |
| `UnitySerializedNameSubstrings_RemainValid` | Input: names such as `SerializedValue` and `OldSerialized` that contain `Serialized` without equaling the generated payload-base name. Output: valid typed payload generation with no false collision. |
| `FormerlySerializedAs_NonUnityGenerationIgnored` | Input: the same alias on a non-`[Serializable]` union. Output: normal non-Unity generation is unchanged and no Unity alias diagnostic or former-name payload class is emitted. |
| `FieldPolicy_NoSPIRE_DU014Regression` | Input: Unity payload fields whose persistence policy belongs to Unity (for example nullable values, tuples, dictionaries, or object/interface fields). Output: typed `[SerializeField]` fields are still generated and no `SPIRE_DU014` field-policy diagnostic is restored. |
| `FormerlySerializedAs_NullEmptyAndSelfNoOp` | Input: `null`, empty, and self-name aliases. Output: no `SPIRE_DU017`, no duplicate class or callback branch, and canonical-only payload generation. |
| `InvalidPayloadFallbackBranch` | Input: valid union declaration with a first variant, including both fielded-first and fieldless-first forms. Output: generated callback branch handles null, missing, and unrecognized payloads by logging the specified warning and constructing the first variant with default field values. |
| `FormerNamePayload_PrivateAccessibility` | Input: valid alias migration declaration. Output: old-name payload types are private implementation types with Unity serialization metadata, not public runtime API or additional user-visible variants. |

### Category D: Property-drawer generated surface

| Case Name | Description |
|-----------|-------------|
| `Drawer_PayloadTypeSelectorWithoutKindProperty` | Input: a fielded/fieldless drawer union. Output: drawer reads the active payload runtime type and has no dependency on a serialized integer-kind property. |
| `Drawer_PayloadTypeDispatchUsesExactTypeIdentity` | Input: canonical `Old` and former-name `OldPayload` payload identities. Output: the drawer dispatches by exact payload type and does not select variants with substring matching. |
| `Drawer_FormerNamePayloadMapsToCurrentVariant` | Input: drawer target containing a former-name payload object. Output: selected variant and fields resolve through the current factory path; the old payload class is never exposed as an extra variant choice. |
| `Drawer_FieldlessAndFieldedHeight` | Input: fieldless and fielded payload instances. Output: generated drawer height and child-property enumeration distinguish an empty payload from a payload with fields. |
| `Drawer_NestedGenericPayloadPaths` | Input: nested array/list and closed generic union properties. Output: generated property paths bind the active typed payload and preserve the containing write-back path. |
| `Drawer_UnknownPayloadFallbackPath` | Input: an unrecognized managed-reference object. Output: drawer can display the defaulted first variant and uses the exact invalid-data warning path without a repaint loop. |

## Section 2: Behavioral Type Definitions

| Union Name | Description | Variants |
|------------|-------------|----------|
| `StabilityAdditiveUnion` | Canonical Additive control for factory, payload-type inference, and callbacks. | `First(int amount, string label)`, `Empty()`, `Nested(StabilityNestedChildUnion child)` |
| `StabilityBoxedFieldsUnion` | BoxedFields layout with the same logical variant matrix as the Additive control. | `First(int amount, string label)`, `Empty()`, `Nested(StabilityNestedChildUnion child)` |
| `StabilityBoxedTupleUnion` | BoxedTuple layout with fielded, fieldless, and nested cases. | `First(int amount, string label)`, `Empty()`, `Nested(StabilityNestedChildUnion child)` |
| `StabilityUnsafeOverlapUnion` | UnsafeOverlap layout whose raw/runtime storage must remain outside Unity serialization. | `First(int amount, string label)`, `Empty()`, `Nested(StabilityNestedChildUnion child)` |
| `StabilityOverlapUnion` | Accepted non-generic Overlap layout for serialized payload and explicit-layout persistence. | `First(int amount, string label)`, `Empty()`, `Nested(StabilityNestedChildUnion child)` |
| `StabilityFieldlessFirstUnion` | Fallback control whose first declared variant has no fields. | `First()`, `Value(int amount)`, `Text(string label)` |
| `StabilityPayloadFieldMatrix` | Field-shape control for typed payload copying and current/former-name field parity. | `Value(int? count, string? label, (int X, string Y) pair)`, `Native(UnityEngine.Object asset)`, `Empty()` |
| `StabilityNestedChildUnion` | Child union used in parent callbacks, arrays/lists, copies, and explicit hydration. | `Text(int count, string label)`, `Empty()`, `Number(float value)` |
| `StabilityNestedParentUnion` | Parent union whose payload contains a generated child union and a marker. | `Child(StabilityNestedChildUnion child, int marker)`, `Empty()`, `Text(string label)` |
| `StabilityGenericUnion<T>` | Generic union closed over `int`, `string`, and a child union; exercises canonical and former-name payload names. | `First(T value)`, `Nested(StabilityNestedChildUnion child)`, `Empty()` |
| `StabilityGenericParent<T>` | Generic containing type with nested union field and collection elements. | `Holder(StabilityGenericUnion<T> value)`, `Empty()` |
| `StabilityMigrationUnion` | Canonical/former-name asset fixture with a renamed fielded variant and renamed fieldless variant. | `Renamed(int value)` `[FormerlySerializedAs = "OldValue"]`, `Empty()` `[FormerlySerializedAs = "OldEmpty"]`, `Other(string label)` |
| `StabilityMigrationNestedChild` | Child migration fixture nested below canonical and former-name parent payloads. | `Current(int count, string label)` `[FormerlySerializedAs = "Prior"]`, `Empty()` |
| `StabilityAliasDiagnosticUnion` | Compile-only declaration family for malformed, colliding, and sanitized-ambiguous aliases. | `First()`, `Second(int value)`, aliases varied per fixture |
| `StabilityDrawerUnion` | Small drawer host with one fieldless and one multi-field variant. | `Empty()`, `Value(int amount, string label)`, `Nested(StabilityNestedChildUnion child)` |
| `StabilityEditorHost` | ScriptableObject/Mono host containing all five layouts, generic closures, nested parent values, arrays, lists, and a `UnityEngine.Object` reference. | Fields: one instance of each union, `StabilityGenericUnion<int>`, `StabilityGenericUnion<string>`, nested arrays/lists, migration fixture |
| `StabilityPlayerHost` | MonoBehaviour host used by standalone Mono and IL2CPP callback/Instantiate probes. | Scalar, nested, array, and list fields for every layout |
| `StabilityReferenceReleaseHost` | Player-only host for weak-reference collection checks after payload cleanup. | One cloneable fielded union plus a nested child and object reference |

## Section 3: Behavioral Test Cases

### Category A: .NET semantic and generated behavioral assertions

| Test Name | Type | Description |
|-----------|------|-------------|
| `CanonicalPayloadSchema_AllFiveLayouts` | unions: all five `Stability*Union` layouts | Compile generated output against the Unity metadata harness and assert one active managed-reference payload, typed `[SerializeField]` fields, and no serialized runtime backing storage for every layout. |
| `CanonicalPayloadType_InfersVariant` | union: `StabilityAdditiveUnion` | Construct each factory variant, retain or inject its typed payload, run reconstruction, and assert `_kind`/public kind reflects the payload type rather than an integer field. |
| `RuntimeKind_IsNonSerialized` | unions: all five layouts | Reflect generated fields and verify runtime kind state is `[NonSerialized]`; generated/compiled Unity fields contain no serialized discriminator. |
| `FieldlessPayloadType_InfersVariant` | union: `StabilityFieldlessFirstUnion` | Serialize a fieldless payload and assert it reconstructs the fieldless variant with all fielded properties at defaults. |
| `FormerNamePayloadClass_FieldParity` | union: `StabilityMigrationUnion` | Reflect canonical and private former-name payload types and compare typed field names/types and `[SerializeField]` metadata one-for-one. |
| `GenericPayloads_ClosedIntAndString` | union: `StabilityGenericUnion<int>` / `StabilityGenericUnion<string>` | Construct, prepare, and reconstruct both closures; assert typed fields and variant inference remain distinct and no open-generic payload is used as serialized data. |
| `NestedGenericPayloads_MapCurrentAndFormerName` | union: `StabilityGenericParent<int>` | Exercise a generic parent containing a child with a former payload name; assert the child maps through the current factory and preserves its closed type. |
| `FactoryFallback_FieldedFirstDefaults` | union: `StabilityAdditiveUnion` | Give reconstruction a null/missing/unrecognized payload and assert the first `First` factory is called with default `amount`/`label`, replacing any previous runtime fields. |
| `FactoryFallback_FieldlessFirstDefaults` | union: `StabilityFieldlessFirstUnion` | Repeat invalid reconstruction with a fieldless first variant and assert no stale fielded values survive. |
| `MalformedPayload_ReplacesExistingRuntimeValue` | union: `StabilityAdditiveUnion` | Start from a valid runtime value, inject a malformed/unrecognized payload, invoke deserialization, and verify the valid value is overwritten by the first-variant default instead of being retained. |
| `UnknownPayloadType_DoesNotThrow` | union: `StabilityAdditiveUnion` | Inject an unrelated `UnitySerializationPayloadBase` subtype and assert safe fallback, exact warning text, and no cast/reflection exception. |
| `FormerNamePayload_UsesCurrentFactory` | union: `StabilityMigrationUnion` | Inject the private old payload object, reconstruct, and assert public fields/kind match the current variant factory rather than an obsolete storage path. |
| `NonUnityAlias_DoesNotChangeGeneration` | non-Unity Additive control | Apply the alias property without `[Serializable]` and assert ordinary factory, pattern, deconstruct, and layout behavior remains unchanged. |
| `FormerlySerializedAs_NullEmptyAndSelfNoOp` | union: `StabilityAliasNoOps` | Use `null`, empty, and self-name aliases and assert canonical-only payload generation, no duplicate branch, and no diagnostic. |
| `SerializationFootprint_FieldCollisions_SaveReload` | native Unity asset: `UnitySerializationFootprintAsset` | Compile collision and nullable same-name variants in Unity, exercise `count_value`/`text_value` property-var arms, retain original payload field names, and verify both forms across save/reload. |

### Category B: Nested bridge hydration, snapshot reuse, and isolation

| Test Name | Type | Description |
|-----------|------|-------------|
| `Bridge_ReplacesChangedPayloadWithoutMutatingPriorSnapshot` | union: `StabilityNestedParentUnion` | Prepare a destination with an existing snapshot, pass changed runtime fields plus the prior destination, and require a replacement snapshot while the prior fields remain unchanged; an equal source reuses the prior snapshot. |
| `Bridge_SnapshotComparisonUsesSerializedFieldsAndScalarMarkers` | union: `StabilityEqualsProbeUnion` | Compare a custom reference type whose `Equals` always reports equality plus a changed scalar marker; serialized fields drive replacement and the prior snapshot remains unchanged. |
| `PlayerCleanup_UsesSharedMarkerAndReleasesPayloadGraph` | union: `StabilityNestedChildUnion` | Two same-typed player reconstructions share the non-concrete marker, retain runtime values through repeated hydration, replace the marker with a concrete snapshot before serialization, and release the old concrete payload graph so a `WeakReference` dies after GC. |
| `Bridge_RepeatedExplicitHydration_PreservesValues` | union: `StabilityNestedParentUnion` | Call explicit nested hydration twice with the same already-hydrated value; values and payload identity remain stable and the second call does not reset fields to defaults. |
| `Bridge_ExplicitHydration_IsNotNativeDeserialize` | union: `StabilityNestedParentUnion` | Mark an explicit hydration call separately from a new native deserialize event; repeated explicit hydration preserves values, while a subsequent native callback with null/malformed payload takes the invalid-data fallback. |
| `Bridge_CallbackOrdering_IsIndependent` | host: `StabilityEditorHost` | Invoke parent/child callbacks in both parent-first and child-first order and assert the same logical values and payload classes result. |
| `Bridge_CopyIsolation` | union: `StabilityNestedParentUnion` | Copy a parent with a nested child, mutate only the copy, and assert distinct snapshots and unchanged original values. |
| `Bridge_SiblingArrayIsolation` | host: `StabilityEditorHost` | Prepare two array elements with equal child types, mutate one, and assert the sibling snapshot, payload fields, and runtime value remain independent. |
| `Bridge_SiblingListIsolation` | host: `StabilityEditorHost` | Repeat the isolation proof for `List<T>` elements, including remove/add and repeated preparation. |
| `Bridge_GenericChildIsolation` | union: `StabilityGenericParent<int>` | Reuse a prior cache for a generic nested child and assert closed-type identity, values, and copy isolation survive repeated explicit preparation. |
| `Bridge_FieldlessChildHydration` | union: `StabilityNestedParentUnion` | Hydrate a fieldless nested child repeatedly and assert the empty payload is reused without treating it as null or resetting the parent marker. |
| `Bridge_VariantSwitch_DropsPriorSnapshot` | union: all five layout controls | Switch a nested child from fielded to fieldless and back; old fields and old snapshots never reappear, while a newly selected payload is independent. |
| `Bridge_AllFiveLayouts_Parity` | unions: all five `Stability*Union` layouts | Run the same nested prepare/restore/copy/sibling sequence for each layout and assert the bridge contract is layout-independent. |
| `Bridge_CurrentFormatPayload_NotLost` | union: `StabilityMigrationUnion` | Prepare and restore a current-format payload through a parent and repeated callbacks; assert no valid current fields are replaced by a default or former-name snapshot. |

### Category C: Property-drawer behavior

| Test Name | Type | Description |
|-----------|------|-------------|
| `Drawer_InfersVariantFromPayloadType` | host: `StabilityDrawerUnion` | In the real Editor drawer, select each canonical payload type and assert the selector, active fields, and runtime value match without reading a serialized kind property. |
| `Drawer_PayloadTypeDispatchUsesExactTypeIdentity` | union: semantic drawer fixture with `Old` and `OldPayload` identities | Assert generated dispatch distinguishes canonical and former-name payload types by exact type identity rather than a name substring. |
| `Drawer_FormerNamePayload_MapsToCurrentVariant` | host: `StabilityEditorHost` | Load a former-name payload into the drawer and assert it displays current variant fields and writes canonical payload data on apply. |
| `Drawer_FieldlessVsFieldedHeight` | host: `StabilityDrawerUnion` | Compare property height and child enumeration for fieldless and fielded payloads; no stale child property is drawn after a variant switch. |
| `Drawer_SharedPayload_EditUndoRedoIsolated` | host: `UnityDrawerHost` | Two struct copies initially share one unchanged payload; editing one through the generated drawer-bound `SerializedProperty` isolates both runtime and serialized values, and Undo/Redo restores original/edited states. |
| `Drawer_UnknownPayload_FallsBackSafely` | host: `StabilityDrawerUnion` | Give the drawer an unrecognized managed-reference object and assert first-variant default display, no exception, and the exact invalid-data warning. |
| `Drawer_Repaint_DoesNotRepeatWarning` | host: `StabilityDrawerUnion` | Repaint/update an invalid property for multiple frames, including a selector repaint, and assert the warning count remains one for that invalid event. |
| `Drawer_SelectionChange_UsesCurrentFactory` | host: `StabilityDrawerUnion` | Change the selector, apply the property, and assert the new canonical payload is constructed through the current factory with default inactive fields. |
| `Drawer_NestedArrayListWriteback` | host: `StabilityEditorHost` | Edit nested array/list payload fields through their serialized property paths; assert write-back changes only the addressed element and survives Undo/Redo. |
| `Drawer_GenericAliasPath` | host: `StabilityGenericParent<int>` | Draw and edit a generic child loaded from a former-name payload; assert closed-type field paths and current payload replacement remain valid. |
| `Drawer_NoSerializedKindProperty` | all drawer hosts | Inspect serialized properties and assert no integer-kind property is queried or required by either the generated drawer or its runtime reconstruction helper. |

### Category D: Native Editor persistence and migration

| Test Name | Type | Description |
|-----------|------|-------------|
| `Editor_CurrentFormat_SaveReload_AllFiveLayouts` | host: `StabilityEditorHost` | Save, unload, reload, and assert exact variants, field values, nested values, and Unity object references for all five layouts. |
| `Editor_CurrentFormat_HasZeroSerializedKind` | host: `StabilityEditorHost` | Force-text save a current-format asset and assert no serialized `_kind`/integer discriminator field exists in the owner or payload references. |
| `Editor_RepeatedSave_YamlIdsStable` | host: `StabilityEditorHost` | Save, import/update, save again, and compare complete YAML plus every nested `references.RefIds` entry; canonical payload IDs remain stable after the first canonical save. |
| `Editor_NestedRepeatedSave_YamlIdsStable` | host: `StabilityEditorHost` | Repeat the complete nested graph across direct children, helper-contained children, arrays/lists, generic helpers, and all five layouts; no nested payload ID changes on later saves. |
| `Editor_CacheRetained_EditMode` | host: `StabilityEditorHost` | After reload in Edit Mode, assert the payload cache remains attached and unchanged saves reuse its managed object. |
| `Editor_CacheRetained_PlayMode` | host: `StabilityEditorHost` | Repeat while `Application.isPlaying` is true; Editor/Play Mode keeps the payload cache instead of applying player cleanup. |
| `Editor_FormerNamePayload_ReloadsThroughAlias` | asset fixture: `StabilityMigrationUnion` | Load an actual old YAML payload class, assert the current variant and fields are restored, and capture exactly one migration result without requiring a obsolete public API. |
| `Editor_FormerNamePayload_CanonicalizesNextSave` | asset fixture: `StabilityMigrationUnion` | After loading the old payload, save again and assert YAML contains the canonical payload type and fields, no former-name payload type, and no serialized integer discriminator. |
| `Editor_ReorderedVariants_PayloadFirst` | asset fixture: reordered `StabilityMigrationUnion` | Open a persisted asset authored with a different variant order and a stale old `_kind`; a valid payload type still restores the same logical variant and fields. |
| `Editor_FormerNameNestedGeneric_IntAndString` | asset fixtures: nested generic unions | Load former-name nested payloads for `int` and `string` closures, save canonical forms, and assert both closed values survive without type confusion. |
| `Editor_FormerNameFieldlessPayload` | asset fixture: `StabilityMigrationUnion` | Load an old fieldless payload and assert it maps to the current empty factory, remains empty after canonicalization, and does not resurrect inactive fields. |
| `Editor_MalformedPayload_OverwritesValidRuntime` | host: `StabilityEditorHost` | Start with a valid runtime value, replace its serialized payload with null/malformed/unknown data, reload, and assert first-variant defaults replace the old value and warning text is exact. |
| `Editor_InvalidPayload_WarnsOncePerDeserialize` | host: `StabilityEditorHost` | Trigger one invalid reload and count one exact warning; repeated `SerializedObject.Update`/repaint without a new deserialize does not log again. |
| `Editor_CurrentFormat_NoDataLoss` | host: all current-format fixtures | Round-trip every current canonical fixture, including nested collections, nullable/tuple fields, fieldless variants, generic closures, and object references; compare logical values and canonical YAML. |
| `Editor_CopyAndSiblingIsolation_AfterSave` | host: `StabilityEditorHost` | Save copied parents and sibling array/list values repeatedly, mutate one copy, and assert no payload ID, field, or runtime value leaks to another. |
| `Editor_ScriptReload_PreservesCanonicalPayload` | host: `StabilityEditorHost` | Domain/script reload followed by save keeps the canonical payload class, payload ID, and reconstructed values. |
| `Editor_PreviousIntegerField_IsIgnoredWhenPayloadValid` | asset fixture: old payload-first YAML | Provide an old serialized integer kind that disagrees with a recognized payload type; assert the payload wins. A payload-less integer-only asset is outside the required compatibility contract. |
| `Editor_FormerlyAliasConflicts_FailAtGeneration` | fixtures: invalid alias declarations | Build each malformed/ambiguous alias fixture and assert `SPIRE_DU017` before creating an asset; valid current-format assets are not silently reinterpreted. |

### Category E: Player reconstruction, Instantiate, and cache lifetime

| Test Name | Type | Description |
|-----------|------|-------------|
| `Mono_PlayerDeserialize_ClearsPayload` | host: `StabilityPlayerHost` | In a standalone Mono player, deserialize a canonical asset, verify the runtime value first, then assert the serialized payload reference is replaced by the shared non-concrete marker. |
| `Mono_PlayerInstantiate_SourceRetainsReusableCache` | host: `StabilityPlayerHost` | Instantiate the same source repeatedly; the source may retain its reusable cache and still produces correct clones without source-value loss. |
| `Mono_PlayerInstantiate_CloneHasDistinctPayload` | host: `StabilityPlayerHost` | Assert clone `OnAfterDeserialize` reconstructs the right values, uses the shared marker, and does not retain the source's concrete payload snapshot. |
| `Mono_PlayerInstantiate_CallbackCounts` | host: `StabilityPlayerHost` | Instrument holder callbacks and assert source/clone callback ordering and counts: source preparation occurs once as needed, each clone reconstructs through `OnAfterDeserialize`, and no Editor-only drawer code is loaded. |
| `Mono_PlayerNestedCallbacks_OrderAndValues` | host: nested all-layout fields | Instantiate a host with nested parents, arrays/lists, and generic children; assert callback order does not reset values and all clone fields are isolated. |
| `Mono_PlayerNewDeserialize_InvalidPayloadOverwrites` | host: `StabilityPlayerHost` | After a valid runtime value exists, perform a new native deserialize whose payload is null/malformed and assert player guards do not preserve the old value: the first-variant defaults and one exact warning win. |
| `IL2CPP_PlayerDeserialize_ClearsPayload` | host: `StabilityPlayerHost` | Repeat canonical player deserialization under IL2CPP/AOT and assert runtime values restore before the shared marker replaces the concrete payload. |
| `IL2CPP_PlayerInstantiate_Callbacks` | host: `StabilityPlayerHost` | Repeat source/clone callback counts, distinct payload identity, and value preservation under IL2CPP; generated private payload types remain discoverable. |
| `IL2CPP_PlayerNestedCallbacks_OrderAndValues` | host: nested all-layout fields | Exercise nested, generic, fieldless, and collection values through IL2CPP `Instantiate` and assert no callback-order-dependent reset. |
| `IL2CPP_PlayerNewDeserialize_InvalidPayloadOverwrites` | host: `StabilityPlayerHost` | Repeat the new-native-deserialize malformed-payload proof under IL2CPP; explicit hydration guards must not suppress the required fallback. |
| `Player_ClearedPayload_WeakReferenceCollects` | host: `StabilityPlayerHost` and player hook | Capture a real prepared payload through a weak reference, invoke the generated valid-data `OnAfterDeserialize` callback, return before GC, and verify restored values, the shared marker, and collection of the concrete payload in standalone Mono and IL2CPP. Native scene/clone checks independently verify marker installation and correct values. This does not measure Unity's internal native deserialization reference registry. |
| `Player_SourceCacheMayRemainAlive` | host: `StabilityReferenceReleaseHost` | Keep the source alive after repeated `Instantiate`; assert a source cache can remain reusable, so the test does not require its weak reference to collect. |
| `Player_CloneCleanup_DoesNotChangeRuntimeValue` | host: `StabilityReferenceReleaseHost` | After replacing the clone's concrete payload with the shared marker, read the reconstructed runtime value and assert fields/references remain correct until the next explicit serialization event. |
| `Player_AllFiveLayouts_InstantiateParity` | hosts: all five layout unions | Run Mono and IL2CPP source/clone/cleanup checks for every layout and compare logical values, payload identity rules, and cleanup timing. |

### Category F: Migration diagnostics and warning contract

| Test Name | Type | Description |
|-----------|------|-------------|
| `FormerlySerializedAs_InvalidOrAmbiguousAlias_ReportsDU017` | fixture theory: malformed, current-name collision, duplicate alias, and sanitized-name collision | Assert `SPIRE_DU017` is tied to each offending alias and no invented secondary diagnostics appear. |
| `FormerlySerializedAs_UserGeneratedMemberCollision_SPIRE_DU015` | fixture: reserved generated member plus a valid alias | Assert user-declared generated-surface collisions retain `SPIRE_DU015` and are not misclassified as alias errors. |
| `FormerlySerializedAs_NullEmptyAndSelfNoOp` | fixture: null, empty, and self aliases | Assert no diagnostic and no duplicate generated type for these three explicitly permitted forms. |
| `Warning_InvalidPayload_ExactText` | all invalid-payload hosts | Assert `Debug.LogWarning` text is exactly `invalid union serialization data, defaulting to {FirstVariantName}`, with the actual first variant substituted and no extra punctuation. |
| `Warning_InvalidPayload_NotRepeatedOnRepaint` | drawer host: `StabilityDrawerUnion` | Assert one warning for one invalid event across repeated drawer repaint/update cycles; a new deserialize event may produce its own single warning. |
| `Migration_ValidCurrentAsset_NoLoss` | all current-format asset fixtures | Assert every valid current asset remains readable after generator upgrade, regardless of aliases, variant reorder, nested/generic fields, or layout. |
| `Migration_FormerNamePayloadFirst_NoIntegerOverload` | old payload-first asset fixtures | Assert recognized payloads are sufficient for migration; do not add or require a public integer-discriminator compatibility overload, and document any external asset that would require one before expanding scope. |

### Writer categories

| Writer | Matrix scope |
|--------|--------------|
| `dotnet-semantic-snapshots` | Section 1 Categories A–C: canonical five-layout schema, alias payload classes, generated fallback branches, and `SPIRE_DU017`/`SPIRE_DU015` diagnostics. |
| `dotnet-behavioral` | Section 3 Categories A–B: payload-type inference, factory fallback, exact field copying, nested explicit hydration, snapshot reuse/replacement, and copy/sibling isolation. |
| `unity-drawer` | Section 1 Category D and Section 3 Category C: payload-first selection, former-name display, fieldless layout, nested write-back, and repaint warning behavior. |
| `unity-editor` | Section 3 Category D: real asset save/reload, YAML managed-reference IDs, Editor/Play Mode cache retention, old payload migration, reorder, and no current-format data loss. |
| `unity-player` | Section 3 Category E: standalone Mono and IL2CPP callback/Instantiate behavior, player cleanup, source cache reuse, clone isolation, and weak-reference collection. |
| `unity-migration-diagnostics` | Section 1 Categories B–C and Section 3 Category F: one-string alias semantics, conflict diagnostics, payload-first old assets, canonicalization, and exact warning contract. |

The matrix intentionally leaves the exact `SPIRE_DU017` descriptor wording and
diagnostic location policy to the lead; writers should assert the ID and the
offending alias location, then use the descriptor as the source of truth for
message text. The minimum diagnostic set is one actionable error for each
malformed or ambiguous alias condition, while null/empty/self aliases stay
silent.
