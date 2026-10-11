# UnitySerializeReference Test Coverage Matrix

This matrix is the contract for `[UnitySerializeReference]` on a union variant
parameter. The marker is a Spire parameter annotation; the generated payload
field retains `[SerializeField]` and additionally receives Unity's
`[SerializeReference]`. An unmarked parameter remains an ordinary inline
`[SerializeField]` field.

The same source template is instantiated for all five supported Unity layouts:
`Additive`, `BoxedFields`, `BoxedTuple`, `UnsafeOverlap`, and `Overlap`.
Layout-specific snapshot rows should therefore use one semantic theory where
the expected field metadata is identical, rather than five copies of the same
generated-source assertion. The real Editor cases still run once per layout,
because explicit-layout storage and boxed/tuple storage can fail independently.

The Roslyn cases compile generated code against the installed Unity 6000
assemblies. Persistence, Inspector, Undo, YAML, managed-reference IDs, and
player cases use the real Unity 6000.5.6f1 Editor and its generated players;
they must not use fake `UnityEngine` or `UnityEditor` types. Standalone Mono and
High-stripping IL2CPP results are reported separately.

## Section 1: Snapshot and compiled semantic cases

Snapshot output is checked semantically for the serialized surface. Private
generated names are not part of the contract except where they are the Unity
payload type names or serialized field names.

### Category A: Attribute binding and field metadata

| Case Name | Description |
|-----------|-------------|
| `ManagedReference_AllLayouts_FieldMetadata` | One source fixture declares equivalent `[Serializable]` unions for all five layouts. Each has a managed-reference variant, an unmarked inline variant, a fieldless variant, and a renamed managed-reference variant. The canonical and `FormerlySerializedAs` payload fields have both `SerializeField` and `SerializeReference`; the inline field has `SerializeField` only. Every generated union compiles. |
| `ManagedReference_MultipleParameters_MixedAttributes` | A fielded variant has several parameters, mixing annotated and unannotated parameters and repeating the same reference type. Attributes are applied independently to the corresponding payload fields; an annotation on one parameter does not bleed into neighboring fields. |
| `ManagedReference_Collections` | Array and `List<T>` parameters annotated with `[UnitySerializeReference]` retain their collection type and receive the managed-reference field metadata. The output does not invent a managed-reference container type or change the element type. |
| `ManagedReference_CanonicalAndFormerNameParity` | A variant with `FormerlySerializedAs = "OldReference"` emits the current and former-name payload classes with identical typed fields and identical Unity serialization attributes. The alias payload remains private and is not exposed as a second runtime variant. |
| `ManagedReference_QualifiedAndAliasedSpireAttribute` | Fully qualified Spire syntax, a normal `using`, and a `using` alias for `Houtamelo.Spire.UnitySerializeReferenceAttribute` all enable the same generated attributes. Matching is by the resolved attribute symbol, not by source text. |
| `ManagedReference_UnrelatedSameNameAttribute` | A local or imported attribute class named `UnitySerializeReferenceAttribute` from another namespace does not enable managed-reference generation. The corresponding field remains inline and no Unity-specific diagnostic is invented. |

### Category B: Type shapes and Unity-owned eligibility

| Case Name | Description |
|-----------|-------------|
| `ManagedReference_GenericBaseAndInterfaceFields` | Annotated parameters use an unconstrained generic `T`, a concrete abstract base class, and an interface. Open and closed generic declarations compile against Unity metadata; the generated fields retain their declared types. No Spire field-eligibility diagnostic is produced. |
| `ManagedReference_ConcreteDerivedAndUnityObjectFields` | An annotated base-class field is populated by a concrete derived type, and a separate control uses a concrete `UnityEngine.Object` type. The generator emits the annotation without classifying or replacing either field; Unity decides persistence eligibility. |
| `ManagedReference_HelperWithNestedUnion` | A serializable helper field contains a generated child union and annotated managed-reference fields. The parent payload remains strongly typed and the generated code compiles without requiring a Unity-specific helper type. |
| `ManagedReference_PrivateHelperFields` | The fixture contains private helper fields annotated directly with Unity's `[SerializeReference]`, including a private base-class field and a private `object` field. The generator neither rewrites nor validates these user fields. |
| `ManagedReference_NestedContainingGenericType` | A union nested in a generic containing type and a closed generic union payload compile with the correct generated qualification and managed-reference metadata. Native tests instantiate only closed types. |
| `ManagedReference_GraphShapes` | Serializable node types form a shared graph and a cycle through user `[SerializeReference]` fields. Generated payload fields preserve the declared graph types; generation must not attempt to flatten or clone the graph. |

### Category C: Non-Unity and control behavior

| Case Name | Description |
|-----------|-------------|
| `ManagedReference_NonUnityUnionUnchanged` | The same variant declarations, including `[UnitySerializeReference]`, are used without `[Serializable]` on the union and without Unity references. Normal generation has no payload callbacks, Unity attributes, drawer surface, or Unity assembly dependency. Existing factory, kind, field, pattern, and deconstruct output remains unchanged. |
| `ManagedReference_UnmarkedInlineControl` | A serializable union containing both annotated and unannotated parameters proves that the unmarked path retains its previous inline field behavior. It must not acquire `SerializeReference` transitively because another field or nested type is managed-reference serialized. |
| `ManagedReference_UnityOwnedUnsupportedShapes` | Annotated `object`, interface, abstract-class, dictionary, multidimensional-array, and other Unity-sensitive fields compile as typed fields with the Unity attributes. The generator reports no unsupported-type diagnostic and never silently removes `SerializeReference`; native persistence is only asserted for Unity-supported concrete shapes. |
| `ManagedReference_CSharp9_And_EditorCompile` | All positive fixtures compile under the Unity Roslyn/C# profile. Runtime generated code references only UnityEngine serialization symbols, while the generated drawer compiles only in the editor compilation. |

## Section 2: Behavioral type definitions

These are the smallest reusable definitions for the runtime theories. The
five layout unions use the same variant template so one assertion can be run
against every layout.

| Union or type | Description | Variants / fields |
|---------------|-------------|-------------------|
| `ManagedReferenceAdditiveUnion` | Additive control for the full managed-reference behavior. | `Reference(NodeBase? value)`, `Inline(NodeBase? value)`, `Pair(NodeBase? first, NodeBase? second)`, `Array(NodeBase[] values)`, `List(List<NodeBase> values)`, `Nested(ManagedReferenceHelper helper)`, `Empty()` |
| `ManagedReferenceBoxedFieldsUnion` | Same logical fields using explicit `Layout.BoxedFields`. | Same template as `ManagedReferenceAdditiveUnion` |
| `ManagedReferenceBoxedTupleUnion` | Same logical fields using explicit `Layout.BoxedTuple`. | Same template as `ManagedReferenceAdditiveUnion` |
| `ManagedReferenceUnsafeOverlapUnion` | Same logical fields using explicit `Layout.UnsafeOverlap`. | Same template as `ManagedReferenceAdditiveUnion` |
| `ManagedReferenceOverlapUnion` | Same logical fields using explicit non-generic `Layout.Overlap`. | Same template as `ManagedReferenceAdditiveUnion` |
| `ManagedReferenceRenamedUnion` | Focused alias fixture used by canonical/former-name payload and migration cases. | `[Variant(FormerlySerializedAs = "OldReference")] Reference([UnitySerializeReference] NodeBase? value)`, `Inline(NodeBase? value)`, `Empty()` |
| `ManagedReferenceGeneric<T>` | Generic managed-reference union. | `Value([UnitySerializeReference] T? value)`, `Inline(T? value)`, `Empty()`; close over `NodeBase`, an interface, and `NodeLeaf` |
| `ManagedReferenceBaseInterfaceUnion` | Base/interface field controls independent of generic substitution. | `Base([UnitySerializeReference] NodeBase? value)`, `Interface([UnitySerializeReference] INode? value)`, `Derived([UnitySerializeReference] NodeLeaf? value)`, `Empty()` |
| `NodeBase` / `NodeLeaf` / `NodeOther` | Serializable polymorphic graph nodes. `NodeLeaf` and `NodeOther` are concrete derived classes with distinguishable fields. | `NodeBase.next` is a user `[SerializeReference] NodeBase?`; concrete fields identify the runtime subtype |
| `INode` | Serializable graph interface implemented by the concrete node classes. | Used only as a managed-reference field type; no generator validation is expected |
| `ManagedReferenceHelper` | Serializable helper nested inside a union payload. | Public child union; private `[SerializeReference] NodeBase? node`; private `[SerializeReference] object? opaque`; optional `ManagedReferenceHelper? next` for a helper cycle |
| `ManagedReferenceAsset` | Real Unity `ScriptableObject`/host for Editor persistence. | One field of each layout union, renamed union, generic closures, arrays and lists, plus shared/cyclic graph roots |
| `ManagedReferenceDrawerHost` | Real Editor host for Inspector and Undo. | A drawer-visible union, an array/list of unions, and a helper containing nested managed references |
| `ManagedReferencePlayerHost` | Mono and High-stripping IL2CPP player host. | The same reference, alias, generic, collection, nested-helper, shared-identity, and cycle probes as the asset host |

The template's `Reference`, `Pair`, `Array`, and `List` parameters are
annotated with `[UnitySerializeReference]`; `Inline` and the `Nested` helper
parameter are unannotated. The helper's private fields use Unity's own
`[SerializeReference]`. `FormerlySerializedAs` is applied only to the renamed reference
variant. This keeps one fixture capable of asserting field-level opt-in,
former-name payload parity, collection element behavior, and the inline control.

## Section 3: Behavioral test assertions

### Category A: Generated metadata and .NET-side behavior

| Test Name | Type | Description |
|-----------|------|-------------|
| `Metadata_AllFiveLayouts_ManagedAndInlineFields` | all five layout unions | Inspect canonical and alias payload fields. Annotated fields have exactly the required `SerializeField` + `SerializeReference` pair; unannotated fields have `SerializeField` and no `SerializeReference`. |
| `Metadata_AttributeBindingForms` | binding-form fixture | Fully qualified and aliased Spire attributes opt in; an unrelated same-name attribute does not. Assert no false-positive Unity surface. |
| `Metadata_EligibilityBelongsToUnity` | generic/base/interface fixture | `object`, interface, abstract base, generic, and Unity-sensitive fields generate and compile without a Spire unsupported-field diagnostic. |
| `Metadata_NonUnityOutputUnchanged` | normal non-Unity union | Compile and compare the existing non-Unity surface: no generated Unity payload, callback, drawer, or Unity dependency, even when a variant parameter carries the marker. |
| `Factory_NullManagedReferenceRoundTrip` | all five layout unions | Construct the annotated reference variant with `null`, prepare and reconstruct it through the generated payload path, and assert the value remains `null`. The inline control follows its existing inline semantics. |
| `Factory_DerivedRuntimeTypeRoundTrip` | all five layout unions | Store `NodeLeaf` in a `NodeBase` managed-reference field and reconstruct it; assert the concrete runtime type and derived fields survive. |
| `Factory_DistinctManagedReferenceReplacementWinsOverCachedPayload` | all five layout unions | Replace a managed-reference field, array element, or list element with a distinct instance having identical serialized fields; the replacement payload must retain the new instance rather than the old cached reference. |
| `Factory_PairPreservesSharedIdentity` | all five layout unions | Pass one node instance to both annotated parameters. Payload preparation and reconstruction retain `ReferenceEquals(first, second)`; no detached clone is introduced. |
| `Factory_CollectionsPreserveElementIdentity` | all five layout unions | Arrays and lists retain null elements, concrete derived elements, and repeated references to the same node. The collection object remains the collection field's value; only its elements are managed references. |
| `Factory_NestedHelperPreservesPrivateReferences` | nested helper fixture | A helper's private `[SerializeReference]` field and child union survive preparation/reconstruction with the same node identity. Generated bridge code does not replace the helper graph with a flattened copy. |
| `Bridge_SameManagedReferenceRootRefreshesNestedUnionState` | dynamic managed root fixture | A managed root reference that remains identical between preparations still refreshes a nested generated union payload after its runtime state changes, while preserving the root cycle. |
| `Bridge_FieldlessVariantChangeDoesNotReusePriorChildPayload` | fieldless nested union fixture | Switching unchanged fieldless child variants creates the correct payload type instead of reusing the prior empty payload. |
| `Factory_CyclicGraphDoesNotCloneOrRecurseForever` | graph fixture | A node or helper graph containing a cycle can be prepared and reconstructed without stack overflow. The cycle closes on the original managed object identity. |
| `Factory_IndependentUnionSnapshotsShareOnlyUserReferences` | all five layout unions | Two union values may share one user node intentionally, but their generated payload/cache objects remain independent. Updating one union's scalar fields or active variant cannot mutate the sibling snapshot. |
| `FormerlySerializedAs_FormerNameManagedFieldRestores` | renamed union | A former-name payload instance restores the managed-reference field, including `null`, derived runtime type, and shared identity; the next preparation uses the canonical payload type and keeps the reference object. |

### Category B: Native Editor serialization and persistence

Each row in this category is run for all five layouts unless the row names a
focused alias/generic/helper fixture.

| Test Name | Host | Description |
|-----------|------|-------------|
| `AssetSaveReload_NullManagedReference` | `ManagedReferenceAsset` | Save, unload, and reload a union whose annotated field is null. The null remains null; no inline helper object is synthesized. |
| `AssetSaveReload_DerivedManagedReference` | `ManagedReferenceAsset` | Save/reload a base-typed field containing `NodeLeaf`; Unity restores the concrete managed-reference type and all derived fields. |
| `AssetSaveReload_SharedManagedReferenceIdentity` | `ManagedReferenceAsset` | Two annotated fields and repeated collection elements point at one node. After reload they still point at the same managed-reference object within the host. A different host may have a different managed-reference identity. |
| `AssetSaveReload_ArrayAndListElements` | `ManagedReferenceAsset` | Arrays and lists retain order, nulls, concrete subtypes, repeated element identity, and ordinary container behavior. The test does not require the array/list container itself to receive a managed-reference ID. |
| `AssetSaveReload_NestedHelperAndPrivateFields` | `ManagedReferenceAsset` | A managed-reference helper containing a nested generated union and private `[SerializeReference]` fields survives save/reload. The nested union's bridge retains user reference identity. |
| `AssetSaveReload_CyclicGraph` | `ManagedReferenceAsset` | A real cyclic node/helper graph survives Unity serialization and reload; no generated cache traversal duplicates nodes or loops indefinitely. |
| `AssetSaveReload_FormerNameAndCanonicalPayloads` | `ManagedReferenceAsset` | Persisted canonical and former-name payloads both restore their annotated fields; after one canonicalizing save, subsequent YAML uses the current payload type without losing reference values. |
| `RepeatedSave_YamlAndManagedReferenceIdsStable` | `ManagedReferenceAsset` | After initial load, repeated unchanged saves produce byte-stable YAML and stable managed-reference IDs. A deliberate user graph mutation changes only the expected graph data. |
| `IndependentSnapshots_DoNotShareGeneratedPayloads` | `ManagedReferenceAsset` | Two union fields intentionally share a user node but have separate generated payload snapshots. Editing one union or replacing its variant leaves the other union's payload and runtime scalar values unchanged. |

### Category C: Inspector, Undo, and property drawer behavior

| Test Name | Host | Description |
|-----------|------|-------------|
| `Drawer_ManagedReferenceFieldUsesUnityProperty` | `ManagedReferenceDrawerHost` | The generated drawer displays the active payload and delegates annotated child fields to Unity's managed-reference property. It never edits an inline clone of the child object. |
| `Drawer_AssignDerivedValueAndUndo` | `ManagedReferenceDrawerHost` | Assign a concrete derived managed-reference value through `SerializedProperty`, apply it, then Undo and Redo. The runtime value, concrete type, and shared identity are restored at each step. |
| `Drawer_NullAssignmentAndUndo` | `ManagedReferenceDrawerHost` | Set an annotated child reference to null in the Inspector, apply, Undo, and Redo. Null is preserved and no default inline helper is created. |
| `Drawer_ArrayListNestedWriteback` | `ManagedReferenceDrawerHost` | Edit managed-reference elements through array/list property paths. Writeback updates the containing union and leaves sibling elements and their object identities unchanged. |
| `Drawer_UnionVariantSwitchPreservesManagedReferences` | `ManagedReferenceDrawerHost` | Switch between annotated, inline, and fieldless variants. The selected union is replaced through its factory, inactive data is not resurrected, and managed references in the active payload remain intact. |
| `Drawer_UndoBeforeAndAfterPanelAttach` | `ManagedReferenceDrawerHost` | Undo and Redo both before and after the first UI Toolkit panel attachment refresh the drawer from the current serialized payload. |
| `Drawer_RepeatedInspectorSaveKeepsIdsStable` | `ManagedReferenceDrawerHost` | Opening, editing, applying, and reopening the Inspector without a graph mutation does not churn managed-reference IDs or YAML. |

### Category D: Player and stripping coverage

| Test Name | Backend | Description |
|-----------|---------|-------------|
| `MonoPlayer_ManagedReferencesRoundTrip` | standalone Linux Mono player | The generated runtime code deserializes null, derived, shared, collection, nested-helper, alias, generic, and cyclic managed-reference cases without editor-only dependencies. |
| `Il2CppHighStripping_ManagedReferencesRoundTrip` | standalone Linux IL2CPP, High stripping | The same cases execute under IL2CPP with stripping enabled. Canonical and former-name payload constructors/factories needed by Unity deserialization are retained; no missing-type or missing-constructor failure is hidden by a test-only link file. |
| `MonoAndIl2Cpp_IndependentHostIdentity` | Mono and IL2CPP | Identity is preserved within one Unity serialization host, while separately loaded/cloned hosts do not incorrectly share a managed-reference ID or generated cache object. |
| `Player_InlineControlRemainsInline` | Mono and IL2CPP | The unmarked variant remains on its established inline serialization path in both player backends while adjacent annotated variants use managed references. |

## Required controls and boundaries

- Do not add a Spire diagnostic for field eligibility. Generated fields keep
  their declared type and requested Unity attributes; Unity decides whether a
  particular type, generic closure, collection, or Unity object can actually
  be persisted.
- The test for arrays/lists asserts managed-reference behavior for elements,
  not a managed-reference container identity.
- All identity assertions are scoped to one Unity serialization host. Managed
  reference IDs are not expected to be stable across different hosts, clones,
  or newly created assets.
- A user-authored `[SerializeReference]` field inside a helper is independent
  from the Spire parameter marker. The generator must preserve it and must not
  add a second annotation or copy its object graph.
- Native tests must use real generated compilation and the installed Unity
  Editor. No fake `UnityEngine`, fake `UnityEditor`, or mock serializer may
  satisfy a persistence assertion.

## Declaration boundaries

Resolved by the lead before writers began. The new marker does not change
variant declaration legality or introduce a second field-type validation policy.

| Case | Treatment |
|----------|-----------------------------|
| `[UnitySerializeReference]` on `ref`, `in`, `out`, or `params` variant parameters | Reuse the existing variant-parameter legality rules and do not introduce a Unity-specific diagnostic. Add a compile-boundary case only if those modifiers are currently supported. |
| Marker on a `UnityEngine.Object` or other Unity-rejected managed-reference field | Emit the requested Unity attribute and let Unity report/handle eligibility. Keep this a generated-compilation assertion, not a successful persistence assertion. |
| Former variant names | Keep the existing one-alias rule on the variant method; canonical and former-name payload fields carry the same reference annotation. |

## Verification

Semantic fixtures cover actual attribute binding, a foreign assembly with the same
metadata name, nested generic containing types, canonical/former-name parity, and
Unity-owned unsupported types. Behavioral regressions also exercise private
managed fields, derived objects containing unions, inline-first aliases,
operation-state cleanup after a real bridge exception, user callback isolation,
and ignored multidimensional arrays. Native validation results and artifact
boundaries are recorded in
[the implementation brief](../../../docs/designs/unity-variant-managed-references.md).
