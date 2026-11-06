# Concrete Unity Serialized Wrapper Test Coverage Matrix

This matrix is the contract for the per-variant concrete Unity payload wrapper
change. It is separate from the existing payload-schema and hydration matrices:
the serialized value remains the existing `value` field, but non-generic unions
use non-generic concrete wrapper types. Generic unions retain generic wrappers.

The shared `Houtamelo.Spire.Generated.SpireSerializedPayload<T>` remains the
legacy compatibility base and is no longer sealed. For every canonical variant
payload, generation emits an internal sealed wrapper below the union's existing
type container, for example
`ShapeTypes.SerializedPayloads.MoveSerializedPayload :
SpireSerializedPayload<ShapeTypes.Variants.Move>`. A former-name payload gets a
corresponding concrete wrapper as well, so assets written by an intermediate
version remain readable. Each concrete wrapper is `[Serializable]`, carries the
Unity `[Preserve]` marker, declares a public parameterless constructor, and
inherits the existing serialized `value` field; it must not declare a second
payload field.

New preparation creates the canonical concrete wrapper. Deserialization and
field copying still accept an unchanged legacy generic wrapper, but the
current-state/fast-path check requires the canonical concrete type so
preparation upgrades it, including nested legacy caches in arrays and lists.
Repeated preparation of an already-canonical unchanged value reuses the same
concrete object. Reflection uses `+` between
nested type names; Unity managed-reference YAML and
`SerializedProperty.managedReferenceFullTypename` use `/`.

The source/symbol rows can run through `UnityGeneratorTestHarness` against the
installed Unity metadata. Persistence, Inspector, and player rows must use the
real Unity fixtures in `tests/UnityVariantPayloadCore` or
`tests/UnityIntegration`; fake `UnityEngine` types are not a substitute. The
existing Inspector paths (`_serializedPayload.value.<field>`) and fieldless
shape remain part of the contract.

`(?)` marks behavior deliberately not decided here. The user confirmed retaining
generic-union Unity serialization on newer Editors
and documenting its Unity 2022 restriction. Generation uses one path across
versions; generic declarations retain generic wrapper subclasses.

## Section 1: Snapshot Tests

Snapshot rows are source/input and generated-source/output pairs. Symbol checks
should assert the serialized contract and type identity, not incidental private
implementation names, except where the wrapper name is the serialized
managed-reference identity.

### Category A: Canonical concrete wrapper hierarchy

| Case Name | Description |
|-----------|-------------|
| `Canonical_BasicFieldedAndFieldless` | Input: a `[Serializable]` `ConcreteShape` with `Move(Vector2 destination, float speed)` and `Empty()`. Output: the unsealed shared generic base plus `ConcreteShapeTypes.SerializedPayloads.MoveSerializedPayload` and `EmptySerializedPayload`, each deriving from the correctly constructed base. |
| `Canonical_MultiFieldNullableAndTuplePayload` | Input: a fielded variant containing nullable references, nullable values, a tuple, a `UnityEngine.Object`, and a collection. Output: the concrete wrapper remains a one-field inherited wrapper while the typed payload value retains every declared `[SerializeField]` field. |
| `Canonical_ManyVariantsHaveDistinctWrappers` | Input: one union with many fielded and fieldless variants. Output: exactly one canonical concrete wrapper per variant, deterministic names, and no wrapper reused for a different payload type. |
| `Canonical_FieldlessWrapperHasNoSyntheticFields` | Input: a union containing `Empty()` beside fielded variants. Output: `EmptySerializedPayload` is serializable and constructible, inherits `value`, and declares no synthetic serialized member. |
| `Canonical_SharedBaseIsUnsealed` | Input: any serializable union. Output: `SpireSerializedPayload<T>` remains present, serializable, and non-sealed so concrete subclasses can derive from it; its existing `value` field and generic argument are unchanged. |
| `Canonical_WrapperUsesInheritedValueField` | Input: a basic fielded union. Output: each concrete wrapper's base is `SpireSerializedPayload<VariantPayload>`, the base owns the serialized `value` field, and the wrapper declares no duplicate `value` field or payload storage. |
| `Canonical_PreparationSurfaceHasNoEagerAllocation` | Input: a union whose factory creates `Move` and `Empty`. Output: generated factories remain allocation-free; the preparation path is the first path that can instantiate `MoveSerializedPayload` or `EmptySerializedPayload`. |
| `Canonical_NoUnityVersionConditionalSource` | Input: the same non-generic serializable union used for all Unity profiles. Output: generated source has one wrapper shape and no Unity-version preprocessor branch, runtime-version probe, or alternate generic-wrapper path. |
| `Canonical_NonSerializableControl` | Input: the same union without `[Serializable]`. Output: the ordinary union surface is unchanged and no `SerializedPayloads` container or concrete Unity wrapper is emitted. |

### Category B: Scope, nesting, and identifier stress

| Case Name | Description |
|-----------|-------------|
| `Scope_DuplicateVariantNamesAcrossUnions` | Input: two serializable unions in one compilation, both with a `Move` variant. Output: each union owns an independent `Types.SerializedPayloads.MoveSerializedPayload`; generated hint names and symbols do not collide. |
| `Scope_NestedUnionInContainingType` | Input: a union nested in `Outer.CommandOwner`. Output: the wrapper is nested below that union's `CommandTypes.SerializedPayloads` container and its base argument resolves to the nested `Variants` payload. |
| `Scope_PrivateNestedUnion` | Input: a private nested serializable union with fielded and fieldless variants. Output: wrapper generation preserves the containing-type chain, compiles, and does not require making the user union public. |
| `Scope_DeepNamespaceAndContainingTypes` | Input: a union under multiple namespaces and containing types, including a generic containing type where existing non-generic support permits it. Output: wrapper qualification is complete and injective in source and metadata names. |
| `Scope_EscapedVariantAndFieldNames` | Input: escaped identifiers such as `@event(int @class)`. Output: generated wrapper and payload identifiers are valid C# symbols, retain semantic names used by Unity, and do not emit a literal `@` into metadata identity. |
| `Scope_VariantNamesContainingSerializedPayload` | Input: variants named `Serialized`, `SerializedValue`, and `OldSerialized`. Output: wrapper/container names remain distinct from the shared base and generation succeeds; exact reserved-name diagnostics, if any, are `(?)` and require a lead decision. |
| `Scope_UserDeclaredSerializedPayloadsContainer` | Input: a union whose generated-type scope already contains a user declaration named `SerializedPayloads`. Output: collision handling is tested explicitly; whether this is a diagnostic, deterministic alternate name, or unsupported is `(?)`. |

### Category C: Former-name wrappers and migration identities

| Case Name | Description |
|-----------|-------------|
| `Former_FieldedVariantGetsCanonicalAndFormerWrappers` | Input: `Current(int value)` with `FormerlySerializedAs = "Old"`. Output: canonical `CurrentSerializedPayload` and former `OldSerializedPayload` wrappers both derive from the appropriate generic base; the former wrapper's value type has the same typed field set. |
| `Former_FieldlessVariantGetsConcreteWrapper` | Input: fieldless `Empty()` with a former name. Output: both canonical and former-name wrappers are serializable, have public parameterless constructors, and carry an empty payload value without synthetic fields. |
| `Former_KeywordAliasAndEscapedCurrentName` | Input: an escaped current variant and a valid keyword former name such as `class`. Output: both wrapper identities are legal, deterministic, and distinguishable in generated source and metadata. |
| `Former_AliasPayloadFieldParity` | Input: a renamed variant with nullable, tuple, Unity-object, nested-union, and collection fields. Output: canonical and former payload values expose matching typed fields and both concrete wrapper bases use the corresponding payload type. |
| `Former_NullEmptyAndSelfAliasesAreNoOp` | Input: omitted, `null`, empty, and current-name aliases. Output: canonical wrappers only, with no duplicate former wrapper or extra migration branch. |
| `Former_DuplicateAliasNames` | Input: two variants that request the same former name or a former name colliding with a current variant. Output: the existing alias diagnostic policy is preserved; no ambiguous concrete wrapper surface is accepted. Exact diagnostic/location behavior is `(?)` where not already specified. |

### Category D: Generic and compatibility-shape probes

| Case Name | Description |
|-----------|-------------|
| `Compatibility_LegacyGenericWrapperRemainsEmittable` | Input: any serializable union. Output: `SpireSerializedPayload<T>` still has the old metadata identity and `value` field, is constructible by old serialized data, and is not sealed. |
| `Generic_ClosedIntAndStringConcreteWrappers` | Input: a generic serializable union closed over `int` and `string`. Output: each closure retains generic wrapper arguments and its closed payload type. Unity 2022 native serialization is unsupported; newer Editors retain generic support within the documented engine-version limits. |
| `Generic_FormerAliasConcreteWrappers` | Input: a generic union with a former-name variant. Output: canonical and former wrappers preserve generic arity/constraints and map to the correct closed payload. The same Unity 2022 native restriction applies. |
| `Generic_NestedContainingTypeQualification` | Input: a serializable union nested in a generic containing type. Output: preserve the existing `SPIRE_DU018` rejection on every Unity version; this wrapper change does not enable that declaration. |
| `Compatibility_NoConcreteWrapperForUnrelatedPayload` | Input: an ordinary non-union `[Serializable]` class/struct beside a union. Output: the generator does not synthesize a wrapper for unrelated types. |

## Section 2: Behavioral Type Definitions

| Union Name | Description | Variants |
|------------|-------------|----------|
| `ConcreteWrapperShape` | Primary non-generic fixture for canonical construction, inherited fields, fieldless payloads, and native save/reload. | `Move(Vector2 destination, float speed)`, `Speak(string speech)`, `Empty()` |
| `ConcreteWrapperFieldMatrix` | Typed payload-field parity fixture. | `Rich(int? count, string? label, (int X, string Y) pair, UnityEngine.Object asset, List<int> values)`, `Empty()` |
| `ConcreteWrapperAlias` | Current/former fielded and fieldless migration fixture. | `Current(int value)` `[FormerlySerializedAs = "Old"]`, `Empty()` `[FormerlySerializedAs = "PriorEmpty"]`, `Other(string label)` |
| `ConcreteWrapperNestedOwner.Command` | Non-generic nested-union qualification fixture. | `Run(float speed)`, `Stop()` |
| `ConcreteWrapperPrivateOwner.Hidden` | Private nested-union accessibility fixture. | `Value(int value)`, `Empty()` |
| `ConcreteWrapperEscapedNames` | C# escaped identifier fixture. | `@event(int @class)`, `Empty()` |
| `ConcreteWrapperLeft` / `ConcreteWrapperRight` | Same-variant-name isolation fixture; both contain a `Move` variant. | `Move(int value)`, `Empty()` |
| `ConcreteWrapperGeneric<T>` | Generic closed-argument and former-name source probe. | `Value(T value)`, `Empty()`; compile and inspect `int` and `string` closures. Native Unity 2022 persistence is excluded explicitly; runtime generic coverage remains active. |
| `ConcreteWrapperReservedNames` | Generated-container/name collision probe. | `Serialized()`, `SerializedPayloads(int value)`, `Empty()`; collision result is `(?)` |
| `ConcreteWrapperNativeAsset` | `ScriptableObject`/`MonoBehaviour` host for actual Editor persistence, fresh-process read, arrays/lists, aliases, and YAML typename checks. | Fields of `ConcreteWrapperShape`, `ConcreteWrapperAlias`, nested values, and collections |
| `ConcreteWrapperInspectorAsset` | Real Inspector host using the existing UI Toolkit and IMGUI probes. | `ConcreteWrapperShape command`, fielded/fieldless variants, and a formerly named payload |
| `LegacyGenericWrapperAsset` | Pre-change asset fixture whose managed-reference record names `SpireSerializedPayload<Payload>` rather than a concrete subclass. | One `ConcreteWrapperShape.Move` payload plus unchanged legacy `value` fields |

## Section 3: Behavioral Test Cases

### Category A: Roslyn symbols, source shape, and runtime type identity

| Test Name | Type | Description |
|-----------|------|-------------|
| `SharedPayloadBase_IsUnsealedAndKeepsLegacyValueField` | union: `ConcreteWrapperShape` | Inspect the generated generic wrapper definition; assert its old metadata name, non-sealed status, generic arity, and serialized `value` field remain available. |
| `ConcreteWrappers_AreEmittedPerCanonicalVariant` | union: `ConcreteWrapperShape` | Discover `ConcreteWrapperShapeTypes.SerializedPayloads.*SerializedPayload` symbols and assert one sealed wrapper for `Move`, `Speak`, and `Empty`, each with the matching constructed generic base. |
| `ConcreteWrappers_HaveSerializablePreserveAndCtorSurface` | union: `ConcreteWrapperShape` | Assert each concrete wrapper is internal/sealed, has `[Serializable]` and Unity `[Preserve]`, and declares a public parameterless constructor carrying `[Preserve]` for Unity deserialization/AOT. |
| `ConcreteWrappers_InheritValueWithoutRedeclaringIt` | union: `ConcreteWrapperFieldMatrix` | Assert the wrapper declares no duplicate payload field; the inherited `value` field is the exact typed payload value and retains its existing Unity serialization metadata. |
| `ConcreteWrappers_FieldlessAndFormerNames` | union: `ConcreteWrapperAlias` | Assert fieldless canonical/former wrappers exist with no synthetic fields and fielded canonical/former wrappers have matching payload-field types and attributes. |
| `ConcreteWrappers_ScopeNestedPrivateEscapedAndDuplicateNames` | unions: nested/private/escaped/left/right fixtures | Assert metadata and source identities include the complete containing-type chain, preserve private nesting, sanitize escaped identifiers, and isolate same-named variants across unions. |
| `ConcreteWrappers_GeneratedSourceHasNoUnityVersionBranch` | union: `ConcreteWrapperShape` | Assert the generated tree has one concrete-wrapper implementation shape and no version preprocessor symbols, `Application.unityVersion` probe, or 2022/2023/6000 branch. |
| `ConcreteWrappers_NonSerializableControlIsAbsent` | control union without `[Serializable]` | Assert no `SerializedPayloads` container or concrete wrapper appears while the ordinary generated union API still compiles. |
| `ConcreteWrappers_GenericClosedArguments` | union: `ConcreteWrapperGeneric<int>` / `ConcreteWrapperGeneric<string>` | Inspect closed wrapper arity, constraints, base type arguments, and per-closure identity. Generation uses the same path on all Unity versions; the 2022 native restriction does not disable generic runtime behavior. |

### Category B: Preparation, restoration, upgrade, and reuse

| Test Name | Type | Description |
|-----------|------|-------------|
| `Prepare_CreatesCanonicalConcreteWrapper` | union: `ConcreteWrapperShape` | A fresh `Move` or `Empty` factory value has no serialized payload; preparation creates the matching canonical concrete wrapper and copies all payload fields exactly. |
| `Prepare_ReusesUnchangedCanonicalWrapper` | union: `ConcreteWrapperShape` | Prepare an already hydrated value twice without changing fields; the second call reuses the same concrete wrapper object rather than allocating a generic or replacement wrapper. |
| `Prepare_UpgradesUnchangedLegacyGenericWrapper` | host: `LegacyGenericWrapperAsset` | Inject an unchanged old `SpireSerializedPayload<Move>` instance, prepare it, and assert the result is the canonical `MoveSerializedPayload` with identical field values and a different wrapper identity. |
| `Restore_AcceptsLegacyGenericWrapper` | host: `LegacyGenericWrapperAsset` | Pass the old generic wrapper directly to generated restoration; assert current kind and all values restore without requiring a concrete wrapper cast. |
| `StateCurrent_RequiresCanonicalConcreteWrapper` | union: `ConcreteWrapperShape` | The current-state comparison accepts the canonical concrete subclass when payload type/value match, but treats an old generic base instance as stale so preparation upgrades it; null, unrelated, or mismatched payloads remain rejected. Restoration itself still accepts the legacy base. |
| `Restore_AcceptsFormerConcreteWrapper` | union: `ConcreteWrapperAlias` | Pass the former-name concrete wrapper; assert the current public variant/factory is selected and all former payload fields copy to the canonical runtime shape. |
| `Prepare_FormerWrapperCanonicalizes` | union: `ConcreteWrapperAlias` | After former-name restoration, preparation writes the canonical current wrapper; repeated preparation of the canonical wrapper reuses it. |
| `Prepare_FieldlessConcreteWrapperDoesNotLeakFields` | union: `ConcreteWrapperAlias` | Switch from a fielded variant to fieldless and back through preparation; no stale field value or former generic wrapper remains attached. |
| `Prepare_UpgradesNestedAndCollectionLegacyCaches` | host: `ConcreteWrapperNativeAsset` | Put unchanged legacy generic wrappers in a nested child, array, and list, prepare the outer value, and assert every cache is upgraded to its canonical concrete subtype without changing logical values or sibling identity. |
| `Restore_InvalidGenericOrConcretePayloadFallsBack` | union: `ConcreteWrapperShape` | Null, an unrelated generic wrapper, and an unrelated concrete wrapper take the existing first-variant fallback/warning path without throwing or retaining stale runtime data. |

### Category C: Real Unity persistence and player loading

| Test Name | Type | Description |
|-----------|------|-------------|
| `EditorWrite_UsesConcreteManagedReferenceTypename` | host: `ConcreteWrapperNativeAsset` / existing `SerializedUnionEditorTests.Write` path | Save a fielded value in the real Editor and assert the managed-reference ID is nonzero, YAML/`managedReferenceFullTypename` identifies `Types/SerializedPayloads/MoveSerializedPayload`, and payload backing fields remain visible. The generic base must not be the typename for newly prepared data. |
| `EditorRead_FreshProcessRestoresConcreteWrapper` | host: `ConcreteWrapperNativeAsset` / existing `Read` path | Unload/reimport in a fresh Editor phase and assert kind, field values, fieldless state, nested values, and Unity object identity restore from the concrete wrapper. |
| `EditorSave_UnchangedLegacyAssetUpgradesToConcreteTypename` | host: `LegacyGenericWrapperAsset` / existing recovery fixture path | Import a pre-change asset whose managed reference is the generic wrapper, assert it reads successfully, then save without logical edits and assert the next YAML uses the canonical concrete wrapper. |
| `EditorRead_FormerConcreteTypenameCanonicalizes` | host: `ConcreteWrapperAlias` / existing alias recovery path | Replace a canonical typename with the former-name concrete typename, read it without an invalid-data warning, and assert the next save contains only the current concrete typename. |
| `Editor_FieldlessConcretePayloadHasNoSyntheticRows` | host: `ConcreteWrapperNativeAsset` | Save/reload the fieldless variant and assert the managed reference is `EmptySerializedPayload`, its `value` is generic/empty as expected, and no synthetic child fields are serialized. |
| `Unity2022_NonGenericConcreteWrapper_SaveAndFreshRead` | non-generic host: `ConcreteWrapperNativeAsset` | Run the real Unity 2022 Editor/Mono write and fresh-read phases for non-generic unions; assert concrete subclass construction, inherited payload fields, and reload values. This is bounded by the observed prototype success and does not imply generic support. |
| `PlayerMono_ConcreteCtorAndPayloadRestore` | host: `ConcreteWrapperNativeAsset` | Load the prepared asset in a standalone Mono player and assert the concrete wrapper/Preserve constructor survives stripping, deserialization restores values, and no `TypeLoadException`/missing-constructor path occurs. |
| `PlayerIL2CPP_ConcreteCtorAndPayloadRestore` | host: `ConcreteWrapperNativeAsset` | Repeat the same resource/payload assertions under IL2CPP/AOT; the concrete wrapper and public parameterless ctor must be discoverable without editor-only code. |
| `Native_YamlManagedReferenceIdsRemainStable` | host: `ConcreteWrapperNativeAsset` | Save, reimport, and save again; assert concrete wrapper type identity and managed-reference IDs remain stable when values are unchanged, while legacy upgrade changes type only once. |
| `Unity2022_GenericClosedUnionSerialization` | host: `ConcreteWrapperGeneric<T>` | Native generic persistence is explicitly inapplicable on Unity 2022. Verify that its serialized fixture field is absent and its report records the restriction, while generic factories, hydration, reflection, and accessor checks still run. Retain native generic controls on newer validated Editors. |

### Category D: Real Inspector shape and type identification

| Test Name | Type | Description |
|-----------|------|-------------|
| `UIToolkit_ConcreteWrapperBindsInheritedPayloadFields` | host: `ConcreteWrapperInspectorAsset` / existing `SerializedUnionInspectorTests` | In a real UI Toolkit Inspector, select `Speak`/`Move` and assert controls bind through `_serializedPayload.value.<field>` (or the established equivalent), with no synthetic `Payload.value` control. |
| `IMGUI_ConcreteWrapperBindsInheritedPayloadFields` | host: `ConcreteWrapperInspectorAsset` / existing IMGUI probe | Assert the IMGUI drawer displays the selector and active inherited payload fields for the concrete wrapper, with the same field count and write-back behavior as the generic compatibility shape. |
| `Inspector_TypenameUsesConcreteWrapperNotPayloadStruct` | host: `ConcreteWrapperInspectorAsset` | Inspect `managedReferenceFullTypename` and assert the active managed reference names `SerializedPayloads/<Variant>SerializedPayload`; the type string must not regress to the old generic wrapper or identify only `Variants/<Variant>`. |
| `Inspector_FieldlessVariantHasNoSyntheticValueRow` | host: `ConcreteWrapperInspectorAsset` | Switch to `Empty`, rebuild the Inspector, and assert no stale field controls or synthetic `value` row is rendered. |
| `Inspector_FormerConcreteTypenameShowsCurrentVariant` | host: `ConcreteWrapperAlias` | Load a former-name concrete managed reference and assert the selector exposes the current variant name and current field paths, not an extra former variant choice. |
| `Inspector_NestedArrayListPathsWriteBack` | host: `ConcreteWrapperInspectorAsset` | Edit concrete-wrapper payload fields through nested array/list paths and assert only the addressed element changes; the inherited `value` path remains discoverable to UI Toolkit and IMGUI. |
| `Inspector_SelectorChoicesAreVariantsNotWrapperTypes` | host: `ConcreteWrapperShape` | Assert the native selector contains `Move`, `Speak`, and `Empty` exactly once and never exposes `MoveSerializedPayload`, the generic base, or a former alias as a user-selectable variant. |
| `Inspector_LegacyGenericPayloadStillBinds` | host: `LegacyGenericWrapperAsset` | Open an old generic-wrapper asset before preparation and assert its payload fields are readable and editable; after the normal save/prepare lifecycle, the Inspector sees the canonical concrete typename. |
| `Inspector_InvalidOrMissingKindStillResolvesConcretePayload` | host: `ConcreteWrapperShape` and `ConcreteWrapperAlias` | Give the drawer a concrete canonical or former wrapper while the discriminator is missing/invalid; payload-type inference must walk the inherited `value` field, select the matching current variant when possible, and use the existing safe fallback for unknown data without throwing. |

The existing native typename constants and assertions that need to be extended
are in `tests/UnityVariantPayloadCore/Assets/Editor/SerializedUnionEditorTests.cs`
(`SerializedShapeMovePayloadType`, alias current/former constants, and the
`managedReferenceFullTypename` checks). The Inspector path assumptions are in
`tests/UnityVariantPayloadCore/Assets/Editor/SerializedUnionInspectorTests.cs`
(`_serializedPayload.value` binding and immediate-field checks). The package
integration reflection check in
`tests/UnityIntegration/Assets/Editor/SpireUnityIntegration.cs` currently proves
only that the shared generic wrapper exists; it should additionally inspect the
concrete wrapper identity and constructor. The drawer inference assertion must
resolve the inherited `value` field (a concrete wrapper has no own generic type
argument), including the invalid/missing-discriminator and former-alias paths.
These are test-writer targets, not permission to edit production emitters in
this matrix task.

## Lead execution scope

The initial implementation covers the approved shared-base/concrete-subclass
strategy through a bounded compile/emit/load behavioral suite and a dedicated
real Unity non-generic persistence fixture. The broad snapshot list above is a
research inventory; it is not a requirement to duplicate all existing generator
snapshots or to create 27 full-union snapshots. Existing exact-output fixtures
will be refreshed only for intentional generated-source changes.

Confirmed generic policy: preserve existing
newer-Unity generic serialization behavior, with the same wrapper emission path
carrying union/containing-type parameters. Unity 2022's generic serialization
limitation remains documented; there is no product code branch by Unity version.

Wrapper identity: `Types.SerializedPayloads.<Variant>SerializedPayload`, internal
sealed class, Serializable and Preserve class markers, public Preserve-marked
parameterless constructor. Canonical and formerly named payloads both receive
wrappers. There is no additional user configuration or layout policy. Existing
parser rules for duplicate/invalid variant aliases remain unchanged.

State-current checks require canonical concrete wrappers; restoration and prior
field reuse accept the legacy generic base. Nested and collection state checks
must consequently invalidate legacy caches so re-saving a loaded legacy asset
upgrades its entire prepared payload tree. Canonical unchanged caches remain
reusable. Inspector fallback resolves the payload through inherited `value` field
metadata instead of treating concrete-wrapper generic arguments as payload types.

No package minimum/version change is part of this implementation.

Executed coverage and remaining acceptance limits are recorded in the
[concrete-wrapper validation](../../../docs/reviews/2026-11-04-concrete-unity-wrappers.md).
