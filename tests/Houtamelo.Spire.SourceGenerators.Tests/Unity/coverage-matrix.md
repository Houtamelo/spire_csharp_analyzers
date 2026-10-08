# Unity Serialization and Property Drawer Test Coverage Matrix

This is the pre-implementation contract for the Unity 6000 serialization and
property-drawer emitters. Snapshot cases compare generated syntax and compile
the result; they must not assert private storage names unless the name is part
of the serialized Unity surface. Diagnostic cases assert the diagnostic ID and
that the Unity-specific generated surface is absent. Runtime cases run in the
installed Unity 6000.5.6f1 Editor/Mono fixture and use ordinary Unity fields;
the owner does not prepare snapshots, hold a wrapper, or implement callbacks.

The Roslyn harness must reference the installed Unity assemblies, especially
`Editor/Data/Managed/UnityEngine/UnityEngine.CoreModule.dll` and, for drawer
compilation, `Editor/Data/Managed/UnityEditor.dll`. It must not provide fake
`UnityEngine` types. Core-only metadata tests may run in the normal Roslyn test
process; persistence, Inspector, Undo, prefab, and player assertions require
the real Editor. The matrix targets the installed `6000.5.6f1` compiler and
does not claim compatibility with another Unity minor. Explicit layout support
is phased: BoxedFields and BoxedTuple are the first non-Additive cases; the
serialized schema and callback behavior must remain layout-independent while
their existing runtime storage and allocation behavior remains visible in the
tests.

Unity facts used by the cases are documented in the Unity 6000.5 primary
references: [serialization rules](https://docs.unity3d.com/6000.5/Documentation/Manual/script-serialization-rules.html),
[`ISerializationCallbackReceiver`](https://docs.unity3d.com/6000.5/Documentation/ScriptReference/ISerializationCallbackReceiver.html),
[`SerializeReference`](https://docs.unity3d.com/6000.5/Documentation/ScriptReference/SerializeReference.html),
and [`SerializedProperty.boxedValue`](https://docs.unity3d.com/6000.5/Documentation/ScriptReference/SerializedProperty-boxedValue.html).

## Section 1: Snapshot Tests

Each accepted case is an `input.cs`/`output.cs` pair. Cases marked `diagnostic`
are generator diagnostic cases with a source input and an expected diagnostic;
they do not require a fabricated output file. `(?)` means the declaration is a
feasibility probe and must not become an implementation decision without the
answer in the bounded questions below.

### Category A: Semantic opt-in, layout resolution, and non-regression

| Case Name | Description |
|-----------|-------------|
| `Serializable_BasicStruct` | `[Serializable]` plus omitted layout on a basic struct union; Unity support is emitted and the runtime layout is Additive. |
| `Serializable_AliasAttribute` | `using SerializableAlias = System.SerializableAttribute;` applied through the alias; semantic attribute lookup emits the same Unity surface. |
| `Serializable_OnOtherPartial` | `[DiscriminatedUnion]` and `[Serializable]` are on different partial declarations; the complete union symbol opts in. |
| `Serializable_VariantsOnOtherPartial` | Variants and the serialization attribute are split across partial declarations; generated payloads include every variant exactly once. |
| `Serializable_AutoLayout` | Explicit `Layout.Auto` still selects Additive for a serializable struct. |
| `Serializable_GlobalNonAdditiveOverride` | Omitted layout with `Spire_DU_DefaultLayout=Overlap` still selects Additive because serialization opt-in overrides the global strategy. |
| `Serializable_ExplicitAdditive` | Explicit `Layout.Additive` is accepted and emits the serialization surface. |
| `Serializable_ExplicitOverlap` | Explicit `Layout.Overlap` is accepted for a non-generic mutable union once its explicit offsets and appended serialized references satisfy the CLR contract. |
| `Serializable_ExplicitBoxedFields` | Explicit `Layout.BoxedFields` is accepted; generated Unity serialization uses the canonical kind/payload schema while runtime access retains BoxedFields storage. |
| `Serializable_ExplicitBoxedTuple` | Explicit `Layout.BoxedTuple` is accepted; generated Unity serialization uses the canonical kind/payload schema while runtime access retains BoxedTuple storage. |
| `Serializable_ExplicitUnsafeOverlap` | Explicit `Layout.UnsafeOverlap` is accepted with unsafe compilation and the existing `Unsafe` dependency; raw and managed runtime storage remain excluded from Unity serialization. |
| `NormalAdditive_NoSerializable` | The same Additive union without `System.SerializableAttribute` keeps its existing generated API and has no Unity fields, callbacks, payload classes, or drawer. |
| `Serializable_HelperTypeOnly_NoOptIn` | A `[Serializable]` helper type or variant payload type beside a non-serializable union does not opt the union into Unity generation. |

### Category A.1: Phase 1 explicit boxed layouts

These cases are the first non-Additive extension. They retain the current
omitted/`Auto`/global-default resolution to Additive; only an explicit layout
selects the strategy under test. Every accepted case must still emit exactly
one serialized discriminator and one managed-reference payload, with the
layout-specific runtime storage kept out of Unity's serialized schema.

| Case Name | Description |
|-----------|-------------|
| `Serializable_BoxedFields_Basic` | A mutable `[Serializable]` struct with fielded, multi-field, and fieldless variants using explicit `Layout.BoxedFields`; generated source compiles against installed Unity metadata and selects the BoxedFields runtime storage. |
| `Serializable_BoxedFields_MixedReferences` | BoxedFields variants combine primitives, strings, and a concrete `UnityEngine.Object` reference; the payload types retain all declared fields and the runtime boxed slots remain nonserialized. |
| `Serializable_BoxedFields_GenericIntString` | A generic BoxedFields union is emitted for `int` and `string` closed arguments; both payloads compile under the Unity 6000 Roslyn profile. |
| `Serializable_BoxedFields_NestedContainingType` | A BoxedFields union nested in a non-generic containing type retains its generated qualification, drawer target, and payload type names. |
| `Serializable_BoxedFields_CSharp9_NoIsExternalInit` | The same BoxedFields declaration compiles as C# 9 without relying on `IsExternalInit`; callback/factory writes use generated internal storage paths rather than unavailable public `init` accessors. |
| `Serializable_BoxedTuple_Basic` | A mutable `[Serializable]` struct with fielded, multi-field, and fieldless variants using explicit `Layout.BoxedTuple`; generated source selects the single runtime tuple payload. |
| `Serializable_BoxedTuple_MixedReferences` | BoxedTuple variants combine primitives, strings, and a concrete `UnityEngine.Object` reference; the typed Unity payload remains distinct from the runtime tuple box. |
| `Serializable_BoxedTuple_GenericIntString` | A generic BoxedTuple union is emitted for `int` and `string` closed arguments and retains the canonical payload hierarchy. |
| `Serializable_BoxedTuple_NestedContainingType` | A BoxedTuple union nested in a non-generic containing type retains generated qualification and a discoverable standalone drawer target. |
| `Serializable_BoxedTuple_CSharp9_NoIsExternalInit` | The C# 9 profile compiles and the generated callbacks can restore runtime tuple storage without public `init` support. |
| `Serializable_BoxedLayouts_CanonicalSchemaParity` | The BoxedFields and BoxedTuple outputs expose the same serialized kind enum, payload base/concrete field declarations, callbacks, and drawer metadata; only nonserialized runtime storage differs. |
| `Serializable_BoxedLayouts_CompatibleUserNames` | Variants use names that resemble layout helpers (`payload`, `data`, `buffer`, `element`, `kind`) and compile without generated-member collisions; any reserved generated surface is diagnosed consistently with the existing Unity conflict rule. |

### Category A.2: Phase 2 explicit UnsafeOverlap layout

UnsafeOverlap keeps its existing unsafe runtime representation. The generated
Unity surface persists logical getter values only; neither the raw byte buffer
nor managed reference slots become Unity fields. The accepted cases require an
unsafe compilation and the project's `System.Runtime.CompilerServices.Unsafe`
reference.

| Case Name | Description |
|-----------|-------------|
| `Serializable_UnsafeOverlap_Basic` | Explicit `Layout.UnsafeOverlap` with primitive, multi-field, and fieldless variants compiles against Unity metadata and selects the raw-buffer runtime storage. |
| `Serializable_UnsafeOverlap_MixedReferences` | UnsafeOverlap combines raw primitive fields with string and concrete `UnityEngine.Object` slots; callbacks capture both storage classes through getters and restore both through factories. |
| `Serializable_UnsafeOverlap_GenericIntString` | Supported generic UnsafeOverlap closures for `int` and `string` retain the canonical typed payload and compile with unsafe enabled. |
| `Serializable_UnsafeOverlap_CSharp9_FallbackBuffer` | C# 9 generation without `InlineArrayAttribute` compiles and the fallback byte-buffer constructor/accessor paths address the same offsets used by the runtime properties. |
| `Serializable_UnsafeOverlap_CSharp9_NoIsExternalInit` | The C# 9 profile has no `IsExternalInit` dependency and callbacks still initialize/reset the raw buffer and reference slots. |
| `Serializable_UnsafeOverlap_CompatibleUserNames` | User fields resembling `_data`, `_buffer`, `_element`, `kind`, or `payload` do not collide with generated runtime helpers or serialized members. |
| `Serializable_UnsafeOverlap_UnsafeDisabled_Diagnostic` | A serializable UnsafeOverlap union compiled without unsafe support preserves `SPIRE_DU009`; it must not silently fall back to Additive or emit a partial Unity surface. |
| `Serializable_UnsafeOverlap_CanonicalSchemaParity` | UnsafeOverlap and Additive expose identical serialized kind/payload field declarations and drawer metadata; only nonserialized raw/reference runtime storage differs. |

### Category A.3: Phase 3 explicit Overlap layout

Overlap is intentionally last because Unity support adds managed serialized
references to an existing explicit-layout struct. The accepted implementation
must append aligned, nonoverlapping generated payload/cache references without
changing the public runtime field layout or introducing a wrapper type.

| Case Name | Description |
|-----------|-------------|
| `Serializable_Overlap_NonGenericBasic` | Explicit `Layout.Overlap` on a non-generic mutable struct with unmanaged fielded and fieldless variants compiles and selects `StructLayout(LayoutKind.Explicit)`. |
| `Serializable_Overlap_NonGenericManagedReferences` | A non-generic Overlap union with the runtime's supported reference fields plus the generated managed serialized payload loads on Mono; reference fields retain identity after save/reload and never overlap value storage. |
| `Serializable_Overlap_FieldOffsetReferencePlacement` | Generated metadata gives every explicit-layout instance field a valid nonoverlapping offset; discriminator, runtime fields, serialized payload, ownership/cache references, and any managed slots are placed in legal aligned regions. |
| `Serializable_Overlap_NestedInNonGenericType` | An Overlap union nested in a non-generic containing type retains valid explicit layout, generated qualification, and drawer discovery. |
| `Serializable_Overlap_GenericUnion_Diagnostic` | A generic Overlap union reports the existing `SPIRE_DU005` CLR restriction and emits no incomplete Unity serialization surface. |
| `Serializable_Overlap_GenericContainingType_Diagnostic` | A non-generic union nested in a generic containing type is rejected consistently with generic CLR explicit-layout restrictions using `SPIRE_DU005`; there is no silent Additive fallback. |
| `Serializable_Overlap_CSharp9_NoIsExternalInit` | Explicit offsets and callback writes compile under the Unity C# 9 profile without `IsExternalInit` or nullable warnings. |
| `Serializable_Overlap_CompatibleUserNames` | User fields resembling generated explicit-layout members (`_kind`, `_payload`, `_data`, `_cache`, `buffer`) are either safely disambiguated or diagnosed by the established conflict rule. |
| `Serializable_Overlap_CanonicalSchemaParity` | Overlap has the same serialized kind/payload field declarations and drawer metadata as Additive; the only permitted differences are explicit runtime offsets and excluded runtime storage. |

### Category B: Declaration shape and Unity reference diagnostics

| Case Name | Description |
|-----------|-------------|
| `Serializable_ReadonlyStruct_Diagnostic` | A readonly serializable struct union reports `SPIRE_DU013`; no mutable serialized discriminator is generated. |
| `Serializable_RefStruct_Diagnostic` | A `ref struct` with `[Serializable]` reports `SPIRE_DU013`. |
| `Serializable_Record_Diagnostic` | A serializable record union reports `SPIRE_DU013`; record-union serialization is outside the initial scope. |
| `Serializable_RecordStruct_Diagnostic` | A serializable `record struct` is covered by the struct-only decision and reports `SPIRE_DU013`. |
| `Serializable_GenericStruct_ClosedIntAndString` | Closed `GenericProbeUnion<int>` and `GenericProbeUnion<string>` compile against real Unity metadata and snapshot the serializable payload/drawer shape; both are positive cases. |
| `Serializable_GenericStruct_OpenTypeParameter` | A generic declaration whose payload field is the unconstrained `T` compiles and preserves the established open-generic shape; supported closed arguments are positive and known unsupported closed arguments use the existing field-policy diagnostics. |
| `Serializable_NestedInGenericType_CompileAgainstUnity` | A union nested inside a generic containing type remains a positive Additive case; explicit Overlap in the same containing context is covered by the established `SPIRE_DU005` restriction. |
| `Serializable_WithoutUnityEngine_Diagnostic` | A serializable union compiled without UnityEngine metadata reports `SPIRE_DU016`; tests must omit the reference rather than add a fake namespace. |
| `Serializable_WithUnityEngineAlias` | `using UE = UnityEngine;` in the input still resolves the actual Unity attributes/types and produces the same output as the fully qualified form. |
| `NormalGeneric_NoSerializable` | A generic Additive union without `[Serializable]` remains on the existing non-Unity path for every closed type argument. |

### Category C: Supported payload field shapes

| Case Name | Description |
|-----------|-------------|
| `Fields_PrimitivesAndNullableReferences` | Payloads containing Unity-supported primitive values, `string`, and nullable-reference annotations preserve their declared typed fields. |
| `Fields_EnumsWithin32Bits` | An enum with an underlying type no wider than 32 bits is emitted as a serializable payload field and remains distinct from the discriminator. |
| `Fields_UnityNativeTypesAndConcreteObjectRefs` | `Vector2`/`Vector3`/`Color` plus concrete `UnityEngine.Object` references such as `Texture2D` are retained in typed payload classes. |
| `Fields_SerializableConcreteClassAndStruct` | `[Serializable]` concrete helper classes and structs are accepted as payload fields and retain nested values. |
| `Fields_OneDimensionalArraysAndLists` | One-dimensional arrays and `List<T>` whose element types are supported Unity values are emitted without silently dropping fields. |
| `Fields_FieldlessMixedWithFielded` | Fieldless and fielded variants share one union; every variant still receives the correct payload type and factory path. |
| `Fields_DuplicateNamesAcrossPayloadClasses` | Different variants use the same field name and type; separate payload classes prevent cross-variant member collisions. |
| `Fields_EscapedKeywordIdentifiers` | Variant/field identifiers escaped with `@` (for example `@event` and `@class`) generate compilable payload members and drawer paths. |
| `Fields_NestedUnionField` | A supported child union is used as a parent payload field; generated types compile and the established real-Unity nested graph cases cover preparation, repeated saves, arrays/lists, and copy isolation. |

### Category D: Unsupported native Unity payload fields

Every row expects `SPIRE_DU014` at the variant field and no generated output
that silently omits the declaration.

| Case Name | Description |
|-----------|-------------|
| `Fields_DecimalDateTimeGuid_Diagnostic` | `decimal`, `DateTime`, and `Guid` payload fields are rejected as unsupported native Unity field shapes. |
| `Fields_NullableValue_Diagnostic` | `int?`/`Nullable<T>` payload fields are rejected; nullable-reference annotations remain covered by the supported case. |
| `Fields_Tuple_Diagnostic` | `ValueTuple`/tuple payload fields are rejected unless a later Unity-specific field policy explicitly supports them. |
| `Fields_Dictionary_Diagnostic` | `Dictionary<TKey,TValue>` payload fields are rejected. |
| `Fields_MultidimensionalArray_Diagnostic` | `int[,]` and other multidimensional arrays are rejected. |
| `Fields_JaggedArray_Diagnostic` | `int[][]` and equivalent nested arrays are rejected. |
| `Fields_NestedContainer_Diagnostic` | A list/array whose element is itself a list/array or other nested container is rejected. |
| `Fields_Object_Diagnostic` | `object` payload fields are rejected in the initial typed-payload policy. |
| `Fields_Interface_Diagnostic` | Interface-typed payload fields are rejected; the initial scope does not add a polymorphic managed-reference dropdown. |
| `Fields_AbstractClass_Diagnostic` | Abstract-class payload fields are rejected for the same initial no-polymorphism policy. |
| `Fields_LongBackedEnum_Diagnostic` | An enum with a wider-than-32-bit underlying type is rejected. |

### Category D.1: Generic closed-argument field-policy probes

These cases remain alongside the positive `int`/`string` generic cases. The
field policy is already settled: diagnose a known unsupported closed argument
precisely and never silently omit the value; do not blanket-reject generic
unions or defer the decision to an Editor-only failure.

| Case Name | Description |
|-----------|-------------|
| `GenericClosedArg_Object_Diagnostic` | Exercise `UnityGenericUnion<object>`; the constructed payload reports the established `SPIRE_DU014` field-policy diagnostic. |
| `GenericClosedArg_Dictionary_Diagnostic` | Exercise `UnityGenericUnion<Dictionary<int,string>>`; the constructed payload reports the established `SPIRE_DU014` field-policy diagnostic and never silently omits the `Some` value. |

### Category E: Generated storage and callback shape

| Case Name | Description |
|-----------|-------------|
| `Generated_PayloadHierarchy` | The output has one serializable payload base class and one serializable, typed concrete payload class per variant, including a fieldless variant. |
| `Generated_KindAndSinglePayloadStorage` | The owner has one mutable `[SerializeField]` discriminator and one private `[SerializeReference]` payload; no per-variant serialized storage is emitted. |
| `Generated_RuntimeFieldsNonSerialized` | Every Additive runtime field is explicitly `[NonSerialized]`, including fields that would otherwise be included during hot reload. |
| `Generated_AutoPropertyBackingFieldsNonSerialized` | Compiler-generated backing fields for generated runtime properties carry field-targeted `[NonSerialized]`; a property-only attribute is insufficient. |
| `Generated_CallbackInterfaceAndMethods` | The owner implements `ISerializationCallbackReceiver` and emits both callbacks under the normal runtime compilation path. |
| `Generated_OnBeforeSerializeRefreshesRuntime` | The output refreshes the active typed payload from current runtime fields on every `OnBeforeSerialize`, including values changed through generated init accessors. |
| `Generated_OnBeforeSerializeReusesMatchingPayload` | The output updates an existing same-type active payload in place and allocates a new payload when the active variant type changes. |
| `Generated_OnAfterDeserializeUsesFactory` | The output reconstructs through the variant factory and then retains the serialized payload for Inspector access. |
| `Generated_KnownKindMissingPayload` | A known discriminator with a null/missing payload reconstructs the known variant with that variant's fields at default values and does not throw. |
| `Generated_KnownKindMismatchedPayload` | A known discriminator paired with another variant's payload reconstructs the known variant with default fields and does not read stale fields. |
| `Generated_UnknownKindKeepsInvalidDefaultStorage` | An unknown discriminator takes a safe switch default, leaves runtime storage at its default/invalid state, and does not throw in either callback. |
| `Generated_VariantSwitchDropsStalePayload` | Switching from variant A to B creates B's payload and clears A's runtime/persisted data; switching back does not resurrect old A values. |
| `Generated_NestedCallbackPreparationAndRefresh` | Parent/child callback code contains the established recursive child snapshot preparation and same-type in-place refresh used by the one-child, repeated-save, array/list, and copy-isolation contract. |
| `Generated_CopyInitMutationDetachesSerializedCache` | Any generated runtime init mutation on a copied struct detaches the serialized cache before refresh, preventing payload aliasing between independent values. |
| `Generated_NestedCollectionInitPath` | Init/write-back paths for array/list elements of nested unions preserve independent payload ownership; native support is validated by the established real Editor fixture. |
| `Generated_GenericPayloadBaseShape` | A generic serializable union uses a nongeneric serializable payload base with nested typed payload classes carrying the closed type parameter; the generated shape must remain valid for `int` and `string`. |
| `Generated_BoxedFields_RuntimeStorageSelected` | Reflection and generated-tree metadata prove that explicit BoxedFields retains its object-backed variant slots, while every runtime slot/cache is `[NonSerialized]` and the only Unity serialized fields are the canonical kind and managed payload. |
| `Generated_BoxedTuple_RuntimeStorageSelected` | Reflection and generated-tree metadata prove that explicit BoxedTuple retains its single runtime tuple/object payload, separate from the canonical managed-reference payload; no per-field Unity storage is introduced. |
| `Generated_BoxedLayouts_CallbacksUseGettersAndFactories` | Both boxed layouts capture logical values through public getters and restore through factories, regardless of whether the runtime value is held in field slots or a tuple box. |
| `Generated_BoxedLayouts_InitAndReset` | Generated init mutations update the selected boxed storage, variant changes reset inactive values, and missing/mismatched payloads restore defaults without reading stale runtime boxes. |
| `Generated_BoxedLayouts_CopyAndAliasIsolation` | Copies and intentionally aliased nested payloads detach before mutation for both boxed layouts; changing one copy never changes the original or a sibling collection element. |
| `Generated_BoxedLayouts_FactoryAllocationMatchesBaseline` | Warmed allocation probes compare each serializable boxed union with the same non-serializable layout and require no extra factory allocation for the Unity payload/cache; existing BoxedFields/BoxedTuple baseline allocations remain unchanged. |
| `Generated_BoxedLayouts_CSharp9_NoInitDependency` | C# 9 generated trees have no `IsExternalInit` requirement or nullable warnings, while callbacks still write and restore the nonserialized boxed runtime storage. |
| `Generated_UnsafeOverlap_RuntimeStorageSelected` | Reflection proves that explicit UnsafeOverlap retains its raw byte buffer plus separate managed slots; all are `[NonSerialized]`, and no Unity serializer-visible field points at the raw representation. |
| `Generated_UnsafeOverlap_FallbackBufferOffsets` | The C# 9/fallback output's constructor, read, and write paths use the same byte-buffer element and offsets; no declared fallback field is dead or bypassed by accessors. |
| `Generated_UnsafeOverlap_InitResetAndAliasIsolation` | Init mutation, variant reset, nested child preparation, copied-value detachment, and repeated save/reload preserve logical values across raw and managed storage. |
| `Generated_UnsafeOverlap_FactoryAllocationMatchesBaseline` | The serializable UnsafeOverlap factory has the same allocation behavior as the non-Unity UnsafeOverlap control; serialized payload allocation occurs only during callback capture. |
| `Generated_Overlap_ExplicitLayoutSelected` | Reflection sees `LayoutKind.Explicit`; every instance field has an explicit offset, and no Additive-only field grouping or hidden wrapper is emitted. |
| `Generated_Overlap_ReferencePlacementIsLegal` | Generated serialized payload/cache references and any supported runtime references occupy aligned, nonoverlapping regions appended to the existing explicit layout; the type loads on Mono and survives GC movement. |
| `Generated_Overlap_InitResetAndAliasIsolation` | Init mutation, variant reset, nested preparation, copied-value detachment, and repeated save/reload operate through the explicit-offset backing fields without corrupting adjacent regions. |
| `Generated_Overlap_FactoryAllocationMatchesBaseline` | The serializable Overlap factory has the same allocation behavior as the non-Unity Overlap control; adding the serialized payload/cache does not introduce per-factory allocations. |
| `Generated_ExplicitLayouts_SerializedSchemaIsIdentical` | Additive, BoxedFields, BoxedTuple, UnsafeOverlap, and accepted non-generic Overlap all expose one serialized kind plus one typed managed-reference payload with identical variant field names/types and no serialized runtime backing fields. |

### Category F: Conditional property-drawer output

| Case Name | Description |
|-----------|-------------|
| `Drawer_EditorGuard` | The generated drawer and all `UnityEditor` references are enclosed by `#if UNITY_EDITOR`; the runtime/player compilation has no editor dependency. |
| `Drawer_CustomPropertyDrawerRegistration` | The output registers one drawer for the generated union type and does not require a physical `Editor` source folder. |
| `Drawer_AllThreeEntryPoints` | The drawer overrides `CreatePropertyGUI`, `OnGUI`, and `GetPropertyHeight` with the expected Unity 6000 signatures. |
| `Drawer_SelectorAndActivePayload` | Both UI systems expose a variant selector and draw only the active payload fields. |
| `Drawer_FactoryReplacementPath` | The generated editor code can replace a selected variant by constructing the union through its factory and writing the replacement to the owner. |
| `Drawer_BoxedValueSetterPath` | The generated editor code supports whole-value replacement through `SerializedProperty.boxedValue` setter where the actual Unity property path accepts it. |
| `Drawer_ExplicitSerializedReconstruction` | Reads use the serialized discriminator and `managedReferenceValue` payload to reconstruct the runtime value; they do not rely on the known-broken `boxedValue` getter path. |
| `Drawer_NestedArrayListPaths` | The output derives child property paths for array/list elements and nested owners; boxed assignment/write-back behavior is covered by the established real-Editor assertions. |
| `Drawer_FieldHeightAndFieldlessHeight` | `GetPropertyHeight` differs correctly for fieldless and fielded active variants and remains stable after variant changes. |
| `Drawer_GenericOpenTarget` | A nongeneric drawer target for the open generic union is discoverable by Unity type metadata and dispatches factories for constructed `int`/`string` union types; the drawer is not a nested generic class. |

## Section 2: Behavioral Type Definitions

These declarations are compiled into the real Unity fixture. Invalid field-policy
cases remain snapshot/diagnostic inputs so the Editor suite only loads valid
generated code.

| Union Name / Fixture | Description | Variants / Serialized Fields |
|----------------------|-------------|------------------------------|
| `UnityShapeSerializable` | Basic serializable Additive union for factory and field assertions. | `Circle(float radius)`, `Label(string text, Texture2D icon)`, `Point()` |
| `UnityPayloadFieldMatrix` | Exercises the accepted native field policy in one generated payload set. | `Values(bool flag, int count, float amount, double precise, string? label, SmallEnum mode)`, `Native(Vector3 position, Color tint, Texture2D texture)`, `Nested(SerializableData data)`, `Collections(int[] samples, List<SerializableData> entries)` |
| `UnityCallbackChild` | Child union used in managed-reference callback graphs. | `Text(int count, string label, Texture2D asset)`, `Number(float value)`, `Empty()` |
| `UnityCallbackParent` | Parent payload containing a child union and a marker. | `Parent(UnityCallbackChild child, int marker)`, `Text(string label)`, `Empty()` |
| `UnityCollectionUnion` | Collection element coverage for arrays and lists of union values. | `Child(UnityCallbackChild child)`, `Number(float value)`, `Empty()` |
| `UnityCopyProbe` | Copy/init mutation and payload ownership coverage. | `Text(int count, string label, Texture2D asset)`, `Number(float value)`, `Parent(UnityCallbackChild child, int marker)` |
| `UnityDrawerUnion` | Union with fieldless, scalar, text, and object-reference Inspector shapes. | `Empty()`, `Number(float value)`, `Text(int count, string label)`, `ObjectRef(Texture2D texture)` |
| `UnityNestedOwnerData` | Ordinary `[Serializable]` containing struct; no wrapper/callback code authored by the user. | `UnityShapeSerializable value`, `UnityCollectionUnion[] array`, `List<UnityCollectionUnion> list` |
| `UnitySerializationAsset` | `ScriptableObject` host for persistence and managed-reference identity. | `UnityShapeSerializable single`, `UnityCollectionUnion[] array`, `List<UnityCollectionUnion> list`, `UnityCallbackParent parent`, `UnityCopyProbe original/sharedCopy/detachedCopy`, plus a `Texture2D reference` |
| `UnitySerializationMono` | `MonoBehaviour` host for script reload, Play Mode, and Mono player checks. | Same union fields as `UnitySerializationAsset`, with scene-authored values |
| `UnityDrawerHost` | Editor host with ordinary serialized fields, array/list elements, nested owner data, and prefab instance data. | `UnityDrawerUnion single`, `UnityDrawerUnion[] array`, `List<UnityDrawerUnion> list`, `UnityNestedOwnerData nested` |
| `UnityGenericUnion<T>` | Generic serializable struct with proven closed-type coverage. | `Some(T value)`, `None()`; `int` and `string` closed instances save/reload exactly, and the drawer targets the open generic definition. |
| `UnityGenericUnsupportedClosedArg<T>` | Negative closed-argument probe for a generic payload whose concrete type is a known unsupported Unity field shape. | `Some(T value)` with `Dictionary<int,string>`/other unsupported closed arguments; existing `SPIRE_DU014` field-policy diagnostics apply. |
| `UnityNestedGenericOwner<T>` | Nested generic containing-type control for the settled Additive/boxed policy and explicit Overlap restriction. | A serializable union nested in `Outer<T>`; Additive and boxed layouts retain generated qualification and persistence, while Overlap receives `SPIRE_DU005`. |
| `UnityBoxedFieldsUnion` | Phase 1 explicit BoxedFields union used for runtime storage, persistence, drawer, copy, and nested-child checks. | `Number(int value)`, `Text(string label, Texture2D icon)`, `Child(UnityCallbackChild child)`, `Empty()` |
| `UnityBoxedTupleUnion` | Phase 1 explicit BoxedTuple union with the same logical field matrix as BoxedFields for schema-parity comparisons. | `Number(int value)`, `Text(string label, Texture2D icon)`, `Child(UnityCallbackChild child)`, `Empty()` |
| `UnityBoxedFieldsGeneric<T>` | Generic BoxedFields union with supported closed `int` and `string` payloads. | `Some(T value)`, `None()` |
| `UnityBoxedTupleGeneric<T>` | Generic BoxedTuple union with supported closed `int` and `string` payloads. | `Some(T value)`, `None()` |
| `UnityBoxedLayoutParent` | Parent union used to prove nested child preparation and copy isolation in both boxed strategies. | `Parent(UnityBoxedFieldsUnion child, int marker)`, `TupleParent(UnityBoxedTupleUnion child, int marker)`, `Empty()` |
| `UnityUnsafeOverlapUnion` | Explicit UnsafeOverlap union covering raw value storage, separate managed slots, and a fieldless arm. | `Number(int value)`, `Text(string label, Texture2D icon)`, `Child(UnityCallbackChild child)`, `Empty()` |
| `UnityUnsafeOverlapGeneric<T>` | Generic UnsafeOverlap union with supported closed `int` and `string` probes. | `Some(T value, int code)`, `None()` |
| `UnityUnsafeOverlapParent` | Parent union used for nested callback preparation and copy/alias isolation when a child uses UnsafeOverlap. | `Parent(UnityUnsafeOverlapUnion child, int marker)`, `Empty()` |
| `UnityOverlapUnion` | Explicit non-generic Overlap union with unmanaged runtime fields and generated managed payload storage. | `Number(int value)`, `Rectangle(float width, float height)`, `Point()` |
| `UnityOverlapReferenceUnion` | Explicit non-generic Overlap probe containing the supported runtime reference shape needed to validate appended reference placement; if direct placement is rejected by Mono, implementation may move the runtime references into GC-safe nested storage without changing the public union API or serialized schema. | `Text(string label, Texture2D icon)`, `Number(int value)`, `Empty()` |
| `UnityOverlapParent` | Parent union used for nested callback preparation, copied-value detachment, and GC/reference persistence with Overlap children. | `Parent(UnityOverlapUnion child, int marker)`, `Reference(UnityOverlapReferenceUnion child)`, `Empty()` |
| `SerializableData` / `SmallEnum` | Concrete nested types used by supported payload cases. | `[Serializable] struct/class SerializableData`; enum with a 32-bit-or-smaller underlying type |

## Section 3: Behavioral Test Cases

The following are actual Unity Editor/Mono integration assertions (NUnit or
the repository's Unity test runner as appropriate), not Roslyn-only reflection
tests. Every test uses the generated types above through an ordinary owner
field and does not manually call a snapshot-preparation helper.

### Category A: Factory, callbacks, and invalid serialized state

| Test Name | Type | Description |
|-----------|------|-------------|
| `Factory_Text_KindAndFields` | union: `UnityShapeSerializable` | Factory construction sets the expected kind and every active field. |
| `Factory_Fieldless_KindAndDefaults` | union: `UnityShapeSerializable` | Fieldless construction sets its kind and leaves all inactive runtime fields at defaults. |
| `OnBeforeSerialize_RefreshesWithoutManualPrepare` | union: `UnityShapeSerializable` | Assign a factory value to an owner, save/serialize it, and assert the managed payload contains the latest runtime values without an owner wrapper or prepare call. |
| `OnBeforeSerialize_RefreshesMatchingPayloadInPlace` | union: `UnityCopyProbe` | After a runtime init mutation, serialization updates the existing same-type managed payload and preserves its active object reference. |
| `OnAfterDeserialize_ReconstructsThroughFactory` | union: `UnityShapeSerializable` | Asset unload/reload reconstructs the active runtime value and resets inactive fields. |
| `OnAfterDeserialize_KnownKindMissingPayload` | union: `UnityShapeSerializable` | Remove the managed payload while retaining a known kind; reload completes without exception and the known variant fields are default. |
| `OnAfterDeserialize_KnownKindMismatchedPayload` | union: `UnityShapeSerializable` | Pair a known kind with another variant payload; reload completes without exception and does not copy mismatched fields. |
| `OnAfterDeserialize_UnknownKindNoThrow` | union: `UnityShapeSerializable` | Write an invalid discriminator value; callbacks do not throw, runtime fields remain default/invalid, and the serialized invalid storage is observable for diagnosis. |
| `VariantSwitch_DropsStaleInactiveData` | union: `UnityDrawerUnion` | Switch Text to Number and back through generated replacement; neither the payload nor runtime storage resurrects the old text. |
| `PayloadOnlyEdit_ReconstructsImmediateRuntime` | union: `UnityShapeSerializable` | Change only the managed-reference payload through `SerializedProperty`; applying properties reconstructs the immediate runtime value from serialized data. |
| `GeneratedInitMutation_RefreshesPayload` | union: `UnityCopyProbe` | Mutate each generated init-accessor path, serialize, and assert the matching typed payload reflects the mutation. |

### Category B: Persistence, references, copies, and callback graphs

| Test Name | Type | Description |
|-----------|------|-------------|
| `AssetSaveUnloadReload_SingleAndObjectReference` | host: `UnitySerializationAsset` | A factory-created active payload survives actual asset save/unload/reload with exact values and the same `Texture2D`/Unity object reference. |
| `Array_RoundTrip` | host: `UnitySerializationAsset` | Array elements containing fielded, fieldless, and different active variants retain element order and values after reload. |
| `List_RoundTrip` | host: `UnitySerializationAsset` | List elements retain exact active variants, null/reference values, and count after reload. |
| `ContainingStruct_RoundTrip` | host: `UnityNestedOwnerData` | A union inside an ordinary serializable containing struct survives save/reload. |
| `NestedParent_AutomaticChildSnapshotRepeatedSave` | union: `UnityCallbackParent` | Parent construction automatically owns/prepares the child snapshot; two save/reload cycles preserve child values and object references. |
| `NestedParent_MatchingPayloadRefreshRepeatedSave` | union: `UnityCallbackParent` | Parent and child same-type payloads refresh in place across repeated saves without depending on callback order. |
| `NestedParent_CopyIsolation` | union: `UnityCallbackParent` | Copy a parent, mutate the child in the copy, and assert the original parent has independent runtime and serialized values. |
| `NestedArrayElements_IndependentChildSnapshots` | host: `UnitySerializationAsset` | Nested child unions in array elements serialize independently; changing one element does not alter another element's payload. |
| `NestedListElements_IndependentChildSnapshots` | host: `UnitySerializationAsset` | The same isolation assertion holds for `List<UnityCollectionUnion>` elements and list growth/write-back. |
| `NestedCollection_InitMutationDetachesCache` | host: `UnitySerializationAsset` | Mutate a nested union through an array/list element's generated init path, save twice, and assert the changed element detaches its serialized cache from siblings. |
| `CopiedValue_SharedPayloadMutationDetaches` | host: `UnitySerializationAsset` | Construct an intentional managed-reference alias control, mutate one copied struct, and require the generated init path to detach it so the original retains its own values. |
| `CopiedValue_DetachedPayloadRepeatedSaveVariantChange` | union: `UnityCopyProbe` | A detached copy survives repeated save/reload and a Text-to-Number variant change while the original remains Text with its original values. |
| `ActivePayloadOnly_NoInactivePayloadOrRuntimeFields` | host: `UnitySerializationAsset` | Force-text serialized data contains only the active payload class; inactive payload classes and all runtime Additive fields are absent. |
| `BoxedFields_AssetSaveUnloadReload` | host: `UnityBoxedFieldsUnion` | A BoxedFields value with primitive, string, object-reference, and fieldless variants survives actual asset save/unload/reload with exact logical values and references. |
| `BoxedTuple_AssetSaveUnloadReload` | host: `UnityBoxedTupleUnion` | A BoxedTuple value with the same variant matrix survives save/unload/reload; the runtime tuple box is reconstructed from the typed serialized payload. |
| `BoxedLayouts_RepeatedSaveRefreshesMatchingPayload` | host: `UnityBoxedLayoutParent` | Two save cycles after same-variant init mutations refresh matching payloads in place for both boxed layouts and preserve child object references. |
| `BoxedLayouts_VariantSwitchResetsRuntimeStorage` | host: `UnityBoxedFieldsUnion`/`UnityBoxedTupleUnion` | Switching fielded to fieldless and back through serialized state creates defaults and never resurrects stale object-backed fields or tuple elements. |
| `BoxedLayouts_CopyAndNestedAliasIsolation` | host: `UnityBoxedLayoutParent` | Copy a parent or intentionally alias a child snapshot, mutate only one copy, save twice, and require independent runtime values and payload identities. |
| `BoxedLayouts_ArrayListNestedElements` | host: `UnitySerializationAsset` | Arrays and lists containing BoxedFields and BoxedTuple unions persist independent active variants, child snapshots, and list growth/write-back. |
| `BoxedLayouts_ActivePayloadOnlyYaml` | host: `UnitySerializationAsset` | Serialized YAML for either boxed strategy contains only the active typed payload plus the discriminator; runtime boxed fields and tuple storage do not appear as extra Unity fields. |
| `UnsafeOverlap_AssetSaveUnloadReload` | host: `UnityUnsafeOverlapUnion` | Primitive fields in the raw buffer, managed reference slots, and fieldless state survive actual asset save/unload/reload with exact values and object references. |
| `UnsafeOverlap_RepeatedSaveAndVariantReset` | host: `UnityUnsafeOverlapUnion` | Same-variant updates refresh the typed payload; switching variants clears raw and managed inactive storage and never resurrects stale data on a later reload. |
| `UnsafeOverlap_CopyNestedAndCollectionIsolation` | host: `UnityUnsafeOverlapParent`/`UnitySerializationAsset` | Nested UnsafeOverlap children in copied values, arrays, and lists detach their payload caches before mutation and remain independent across repeated saves. |
| `UnsafeOverlap_ActivePayloadOnlyYaml` | host: `UnitySerializationAsset` | Unsafe raw buffers and managed slots are absent from serialized YAML; only the canonical kind and active typed payload are persisted. |
| `Overlap_AssetSaveUnloadReload` | host: `UnityOverlapUnion` | A non-generic Overlap value survives actual asset save/unload/reload; explicit runtime offsets remain valid and the typed payload restores exact logical fields. |
| `Overlap_ReferencePlacementSaveReload` | host: `UnityOverlapReferenceUnion` | Generated managed payload/cache references and supported runtime object references survive GC, save/unload/reload, and retain exact Unity object identity without offset corruption. |
| `Overlap_RepeatedSaveAndVariantReset` | host: `UnityOverlapUnion` | Same-variant updates refresh the payload; switching variants clears explicit runtime regions and no stale inactive fields return after reload. |
| `Overlap_CopyNestedAndCollectionIsolation` | host: `UnityOverlapParent`/`UnitySerializationAsset` | Nested Overlap children in copied values, arrays, and lists retain independent caches and explicit storage after mutation and repeated saves. |
| `ExplicitLayouts_SameSchemaActivePayloadOnly` | host: all four explicit-layout unions | Compare YAML/property paths for equivalent values across layouts: the discriminator and active typed payload schema are identical and inactive runtime storage is absent. |

### Category C: Drawer rendering and serialized replacement

| Test Name | Type | Description |
|-----------|------|-------------|
| `IMGUI_Drawer_ShowsSelectorAndActiveFields` | host: `UnityDrawerHost` | In an actual IMGUI inspector, the drawer renders the selector and fields for the active variant only. |
| `UIToolkit_Drawer_ShowsSelectorAndActiveFields` | host: `UnityDrawerHost` | In a real UI Toolkit inspector, `CreatePropertyGUI` renders the same selector and active field set. |
| `Drawer_GetPropertyHeight_FieldlessVsFielded` | union: `UnityDrawerUnion` | `GetPropertyHeight` reports fieldless and fielded heights correctly and changes after a selector switch. |
| `Drawer_SelectionChange_UsesFactoryReplacement` | host: `UnityDrawerHost` | Selector change constructs the selected variant through its factory, writes it back to the owner, and updates the active managed payload. |
| `Drawer_WholeValue_UsesBoxedValueSetter` | host: `UnityDrawerHost` | Whole-value replacement through `SerializedProperty.boxedValue` setter applies and preserves the selected variant in the real 6000.5.6f1 Editor. |
| `Drawer_ReadsKindAndManagedReference` | host: `UnityDrawerHost` | The drawer reconstructs reads from serialized kind plus `managedReferenceValue`; the known failing `boxedValue` getter does not determine the active fields. |
| `Drawer_ArrayElement_NestedPathWriteback` | host: `UnityDrawerHost` | Editing a union inside an array element writes back to the containing struct and keeps sibling elements unchanged. |
| `Drawer_ListElement_NestedPathWriteback` | host: `UnityDrawerHost` | Editing a union inside a list element writes back after list reallocation and preserves element identity/value. |
| `Drawer_UndoRedo_WholeValueAndPayloadEdit` | host: `UnityDrawerHost` | Undo and Redo restore both selector changes and payload-only field edits. |
| `Drawer_UndoRedoWhileDetached_ReattachesCurrentTree` | host: `UnityDrawerHost` | A detached existing drawer tree is reattached after real Undo and Redo operations and reflects the current active controls. |
| `Drawer_UndoBeforeFirstPanelAttach_BindsCurrentState` | host: `UnityDrawerHost` | Undo performed after binding but before the first panel attachment is reflected when the real PropertyField tree attaches. |
| `Drawer_MultiObjectEditing` | host: `UnityDrawerHost` | Multi-object editing applies a common variant/value safely and does not read a runtime boxed getter that is empty. |
| `Drawer_PrefabOverrideApplyRevert` | host: `UnityDrawerHost` | A prefab instance selector/payload edit creates an override; Apply and Revert restore the expected serialized union state. |
| `BoxedLayouts_IMGUIAndUIToolkit_UseCanonicalPayload` | host: `UnityBoxedFieldsUnion`/`UnityBoxedTupleUnion` | Both drawer APIs render the same selector and active typed payload fields; neither exposes the BoxedFields slots nor the BoxedTuple runtime box as Unity fields. |
| `BoxedLayouts_SelectorReplacementAndUndoRedo` | host: `UnityDrawerHost` | Selector changes replace either boxed union through its factory, update the canonical payload, and Undo/Redo restores the previous logical variant and fields. |
| `BoxedLayouts_NestedArrayListWriteback` | host: `UnityDrawerHost` | Editing a boxed union through nested struct, array, and list property paths writes back to the containing owner and leaves sibling elements unchanged. |
| `UnsafeOverlap_IMGUIAndUIToolkit_DrawCanonicalPayload` | host: `UnityUnsafeOverlapUnion` | Both drawer APIs render the selector and active typed payload fields without exposing raw-buffer or managed-slot implementation details. |
| `UnsafeOverlap_NestedPathWritebackUndoRedo` | host: `UnityDrawerHost` | Selector/payload edits through nested array/list paths write back, and Undo/Redo reconstructs the current UnsafeOverlap value from serialized kind/payload. |
| `Overlap_IMGUIAndUIToolkit_DrawCanonicalPayload` | host: `UnityOverlapUnion` | Both drawer APIs render the canonical selector and active payload for an explicit-layout union; they never edit raw explicit fields directly. |
| `Overlap_NestedPathWritebackUndoRedo` | host: `UnityDrawerHost` | Selector/payload edits through nested paths write back, and Undo/Redo preserves explicit offsets and reconstructed logical fields. |

### Category D: Unity lifecycle and compile-surface integration

| Test Name | Type | Description |
|-----------|------|-------------|
| `ScriptReload_PreservesUnionState` | host: `UnitySerializationMono` | Script recompile/domain reload preserves active kind, payload values, and Unity object references. |
| `PlayModeTransition_PreservesUnionState` | host: `UnitySerializationMono` | Entering and leaving Play Mode reconstructs the same active values on Mono. |
| `MonoPlayer_DeserializesGeneratedUnion` | host: `UnitySerializationMono` | A Mono player build/load path compiles and reads the generated serializable union without `UnityEditor` references. |
| `EditorReimport_DoesNotDuplicateGeneratedSurface` | generated type: `UnityShapeSerializable` | Reimport/reload does not register duplicate callbacks, payload types, or property drawers. |
| `NoUserPreparationOrWrapperRequired` | host: `UnitySerializationAsset` | A plain `ScriptableObject`/`MonoBehaviour` field is enough for save, reload, Inspector edits, Undo, and prefab operations. |
| `RuntimeCompilation_UsesInstalledUnityEngineOnly` | generated type: `UnityShapeSerializable` | Runtime generated code compiles against installed UnityEngine metadata without UnityEditor; editor-only code compiles only when `UNITY_EDITOR` is defined. |
| `NormalNonSerializableUnion_BehaviorUnchanged` | union: normal Additive control | Existing factory, kind, field, pattern, and deconstruct behavior remains unchanged when `[Serializable]` is absent. |
| `GenericUnion_Int_SaveReload` | union: `UnityGenericUnion<int>` | Save/unload/reload a `Some(42)` and `None()` instance; active kind and value are exact. |
| `GenericUnion_String_SaveReload` | union: `UnityGenericUnion<string>` | Save/unload/reload a `Some("text")` and `None()` instance; the typed payload and null/fieldless state are exact. |
| `GenericUnion_DrawerTypeDiscovery` | union: `UnityGenericUnion<int>`/`UnityGenericUnion<string>` | Unity discovers one nongeneric open-definition drawer through type metadata and the drawer dispatches the constructed factories without a nested generic drawer class. |
| `BoxedFieldsGeneric_IntString_SaveReload` | union: `UnityBoxedFieldsGeneric<int>`/`UnityBoxedFieldsGeneric<string>` | Explicit BoxedFields generic closures save and reload exact `Some` and `None` states, including string null normalization. |
| `BoxedTupleGeneric_IntString_SaveReload` | union: `UnityBoxedTupleGeneric<int>`/`UnityBoxedTupleGeneric<string>` | Explicit BoxedTuple generic closures save and reload exact `Some` and `None` states using the canonical typed payload. |
| `BoxedLayouts_ScriptReloadAndMonoPlayer` | host: `UnitySerializationMono` | Script/domain reload and the Mono player preserve boxed runtime values from the serialized kind/payload without loading editor-only references. |
| `BoxedLayouts_ReimportDoesNotDuplicateGeneratedSurface` | generated types: `UnityBoxedFieldsUnion`/`UnityBoxedTupleUnion` | Reimport/reload does not duplicate payload classes, callbacks, or drawer registrations for either explicit boxed strategy. |
| `UnsafeOverlap_ScriptReloadAndMonoPlayer` | host: `UnitySerializationMono` | Script/domain reload and the Mono player deserialize UnsafeOverlap's canonical payload without editor-only references and without relying on unsafe data being serialized. |
| `Overlap_ScriptReloadAndMonoPlayer` | host: `UnitySerializationMono` | Script/domain reload and the Mono player load the explicit-layout union and reconstruct its payload without a `TypeLoadException`, field-offset error, or editor-only dependency. |
| `ExplicitLayouts_ReimportDoesNotDuplicateGeneratedSurface` | generated types: all accepted explicit layouts | Reimport/reload does not duplicate payload classes, callbacks, or drawer registrations across the strategy-specific generated surfaces. |

## Bounded implementation checks

These checks are deliberately visible in the matrix so the implementer cannot
silently change a settled schema or field policy.

### Layout phases and stop conditions

Phase 1 is BoxedFields followed by BoxedTuple. They use the same proven
getter-to-typed-payload and factory-from-payload callback bridge as Additive;
the implementation should stop only if their existing runtime storage cannot
be excluded with `[NonSerialized]` while preserving the canonical serialized
kind/payload schema. Phase 2 is UnsafeOverlap, where the fallback byte-buffer
accessor and raw/reference reset paths must be proven under the C# 9 profile.
Phase 3 is Overlap, where the implementation must first assess a GC-safe
nested runtime-storage arrangement if Mono refuses an explicit-layout struct
containing the appended serialized/cache references. The investigation stops
for a decision only if that arrangement requires a material public API,
runtime-layout, or serialized-contract tradeoff. Generic-containing Overlap
types are a technical consistency case and must receive the established
`SPIRE_DU005` rejection.

All phases preserve the following policy: omitted, `Auto`, and the inherited
global layout default resolve to Additive for a serializable union; only an
explicit layout selects BoxedFields, BoxedTuple, UnsafeOverlap, or Overlap.
Every accepted strategy has one serialized discriminator and one typed
managed-reference payload, and uses the strategy's normal runtime storage for
getters, factories, init mutation, copy detachment, and reset.

1. **Generic payloads and drawers.** Closed `int`/`string` generic payloads,
   open-definition drawer discovery, and known unsupported closed arguments
   already have coverage and an established outcome. Preserve those cases;
   do not reopen the generic field policy while adding layout-specific storage.
2. **Nested callback graphs.** Child preparation, repeated saves, arrays/lists,
   and copy/alias isolation are already part of the serialization contract.
   Reuse those controls for each runtime layout and do not widen the supported
   graph policy during this work.
3. **Native field policy.** The supported and rejected Unity field shapes are
   already settled by the existing `SPIRE_DU014` fixtures. Layout-specific
   emitters must use the same policy without adding strategy-dependent field
   exceptions.
4. **Record structs and readonly/ref declarations.** The initial contract is
   settled: mutable struct unions only. `record`, `record struct`, readonly
   struct, and ref struct all use `SPIRE_DU013`; layout-specific work must not
   introduce a second serialization design for them.
5. **Overlap CLR placement and generic contexts.** A non-generic explicit
   Overlap union must keep its existing offsets while appending aligned,
   nonoverlapping serialized/cache references. Confirm the actual Mono loader
   behavior for those references. If direct appended references fail, first
   investigate GC-safe nested runtime storage without changing the public API.
   A generic Overlap union and a non-generic union inside a generic containing
   type both retain `SPIRE_DU005`; the corresponding BoxedFields/BoxedTuple
   generic closures are positive cases.
6. **Reserved generated members.** Specify the exact generated member/type
   names that trigger `SPIRE_DU015`: discriminator, managed payload, cache or
   helper members, callback methods, payload base/concrete classes, and drawer
   type. Add one diagnostic case per reserved surface once names are fixed.
7. **Schema migration.** Rename/reorder variant and payload-field migration is
   called out by the design but has no contract yet. Do not add compatibility
   assertions for old serialized assets until the serialized schema/version
   policy is specified.
8. **Drawer nested write-back.** The installed probe showed an empty
   `boxedValue` getter for an ordinary union field. Reuse the established
   explicit serialized reconstruction or direct owner-field path for
   array/list/nested paths, prefab overrides, and multi-object behavior; do not
   regress to relying on the incomplete `boxedValue` getter.
9. **Non-Mono backends.** Initial validation is Mono. IL2CPP/AOT payload type
   discovery and player serialization are follow-up coverage unless the lead
   expands the target.

## Writer scopes

| Scope | Owns |
|-------|------|
| `unity-snapshot-opt-in` | Category A semantic/output pairs and normal non-serializable control. |
| `unity-snapshot-declarations` | Category B declaration/reference cases and `SPIRE_DU012`, `SPIRE_DU013`, `SPIRE_DU016` diagnostics. |
| `unity-snapshot-field-policy` | Categories C and D accepted/rejected native field shapes and `SPIRE_DU014`. |
| `unity-snapshot-storage-callbacks` | Category E payload hierarchy, field attributes, callback branches, invalid-state defaults, stale-data reset, copy-detach, and nested collection source cases. |
| `unity-snapshot-drawer` | Category F guarded drawer source and compile checks with actual `UnityEditor.dll`; no runtime Unity launch from the Roslyn suite. |
| `unity-editor-serialization` | Behavioral Categories A and B using durable 6000.5.6f1 ScriptableObject/Mono fixtures, asset save/reload, arrays/lists, nested graphs, object references, copy alias/detach, and active-only YAML. |
| `unity-editor-drawer` | Behavioral Category C using actual IMGUI and UI Toolkit inspectors, selector/replacement paths, explicit reconstruction, nested paths, Undo/Redo, multi-object, and prefab overrides. |
| `unity-mono-lifecycle` | Behavioral Category D script reload, Play Mode, and Mono player compilation/load checks. |
| `unity-layout-followup` | Records Overlap loader evidence and any internal GC-safe storage investigation without reopening the settled generic, native-field, or nested-graph contract. |
| `unity-explicit-boxed-phase1` | Phase 1 BoxedFields and BoxedTuple snapshots, strategy storage metadata, schema parity, factory allocation controls, callbacks, nested/copy isolation, real Editor persistence/drawers, and Mono lifecycle checks. |
| `unity-explicit-unsafe-phase2` | Phase 2 UnsafeOverlap snapshots, unsafe-disabled diagnostic, fallback byte-buffer/C# 9 checks, raw/reference reset, callback persistence, drawer, nested/copy isolation, and Mono player checks. |
| `unity-explicit-overlap-phase3` | Phase 3 non-generic Overlap snapshots, explicit offset/reference placement, generic and generic-containing restrictions, GC/persistence, drawer, and Mono loader checks. |
