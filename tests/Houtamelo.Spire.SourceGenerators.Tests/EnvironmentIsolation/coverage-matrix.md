# Unity/CoreCLR environment isolation coverage matrix

This matrix defines the boundary between the ordinary CoreCLR/NuGet generator
suite and the Unity Editor/player suite. The test compilation, rather than the
presence of a `System.SerializableAttribute` or a referenced Unity assembly,
declares the environment.

The harness should let each case select both metadata references and
preprocessor symbols independently:

| Harness input | Meaning |
|---|---|
| no Unity references, no symbols | ordinary CoreCLR/NuGet consumer |
| Unity runtime references, no `UNITY_5_3_OR_NEWER` | a CoreCLR consumer that happens to reference Unity metadata; still not Unity |
| Unity runtime references + `UNITY_5_3_OR_NEWER` | Unity player/runtime compilation |
| Unity runtime and Editor references + `UNITY_5_3_OR_NEWER;UNITY_EDITOR` | Unity Editor compilation |
| any references + `UNITY_EDITOR` without `UNITY_5_3_OR_NEWER` | ordinary compilation; Editor symbol alone must not activate Unity generation |

`includeUnityReferences` and `includeEditorReferences` continue to choose
metadata files. When `preprocessorSymbols` is omitted, the harness supplies the
matching Unity symbols for the selected metadata so existing Unity-positive
tests remain meaningful; pass an explicit empty collection to test metadata
without environment symbols. The cases below use
`UnityGeneratorTestHarness.Run`, inspect generated trees and output symbols, and
call `AssertNoCompilationErrors` so the generated result is a real Roslyn
compilation rather than a text-only snapshot.

## Category A: CoreCLR `[Serializable]` stays a normal source-generator feature

### should_pass

| File Name | Description |
|---|---|
| `CoreClr_SerializableMutableStruct_EmitsCorePayloadMarkersOnly` | With trusted CoreCLR references and no Unity symbols, a mutable `[Serializable, DiscriminatedUnion]` struct generates its normal union API and `[Serializable]` payload markers. It has no Unity callbacks, `_serializedPayload`, `UnitySerializationPayloadBase`, Unity serialization support tree, drawer tree, or Unity `Debug.Assert` call. |
| `CoreClr_SerializableWithUnityReferences_NoUnitySymbol_DoesNotActivate` | Repeat the same source with Unity runtime metadata references but no `UNITY_5_3_OR_NEWER`; output is identical in Unity-specific surface to the ordinary CoreCLR case and has no Unity diagnostic. Referencing `UnityEngine` metadata alone is not an environment declaration. |
| `CoreClr_EditorSymbolWithoutUnityEnvironment_DoesNotActivate` | Supply Unity runtime and Editor references plus `UNITY_EDITOR`, but omit `UNITY_5_3_OR_NEWER`; `[Serializable]` remains CoreCLR-only, with no callbacks, wrapper, drawer, shared drawer support, or Unity assertion. |
| `CoreClr_NonSerializableUnion_RemainsUnmarkedAndUnityFree` | A normal non-`[Serializable]` union under CoreCLR has no payload `System.SerializableAttribute` and no Unity generated surface. This protects the existing opt-in contract while the new environment gate is added. |
| `CoreClr_SystemSerializableAliasAndPartialDeclaration_RemainsSemantic` | Put `System.SerializableAttribute` behind a using alias on one partial declaration and variants on another. CoreCLR discovers the attribute semantically, emits core payload markers, and emits no Unity source. |
| `CoreClr_ForeignSerializableAttribute_IsNotUnityOrCoreOptIn` | A user-defined `Foreign.SerializableAttribute` with the same short name does not mark payloads and does not activate Unity behavior. |

## Category B: Unity player/runtime activation requires the Unity symbol

### should_pass

| File Name | Description |
|---|---|
| `UnityPlayer_SerializableMutableStruct_EmitsRuntimeSerialization` | With Unity runtime metadata and `UNITY_5_3_OR_NEWER` but no `UNITY_EDITOR`, a mutable serializable struct emits `ISerializationCallbackReceiver` callbacks, serialized payload storage, payload wrapper/base types, and Unity field/reference attributes. It compiles without Editor references and emits no property drawer or shared Editor support tree. |
| `UnityPlayer_SerializableAliasAndSplitPartial_EmitsRuntimeSerialization` | The qualified/aliased/other-partial declaration shape from the CoreCLR control emits the same runtime Unity surface when the Unity symbol and runtime metadata are present. This prevents the environment gate from depending on source spelling or declaration order. |
| `UnityPlayer_SerializableGenericAndNestedStructs_KeepRuntimeBoundary` | A generic union and a union nested in a non-generic containing type emit runtime serialization under the Unity symbol, while their generated core payload APIs remain usable. Assert no Editor drawer/support tree in this player compilation. |
| `UnityPlayer_NonSerializableUnion_DoesNotEmitRuntimeSurface` | Unity runtime references and `UNITY_5_3_OR_NEWER` do not make an unmarked union serializable. No callbacks, wrapper, payload serialization attributes, or drawer/support surface are emitted. |
| `UnityPlayer_UnityReferencesWithEditorMetadataButNoEditorSymbol_EmitsNoDrawer` | Include Editor metadata in the reference set but omit `UNITY_EDITOR`. Runtime serialization remains available, but no `.UnityDrawer.g.cs`, `__SpireUnityPropertyDrawer`, `__SpireUnityDrawerMetadata`, or shared support tree is generated. |
| `UnityPlayer_RuntimeAssertionsUseUnityOnlyWhenOptedIn` | Compare a serializable player compilation with the equivalent CoreCLR compilation that references Unity metadata. Only the Unity-symbol compilation may contain Unity assertion calls; the CoreCLR generated source must not mention `UnityEngine.Debug.Assert`. |

### should_fail

| File Name | Description |
|---|---|
| `UnityPlayer_ReadonlyStructSerializable_IsRejectedAsUnitySerialization` | A `[Serializable, DiscriminatedUnion] readonly struct` with Unity runtime references and `UNITY_5_3_OR_NEWER` retains the existing Unity-shape diagnostic (`SPIRE_DU013`) and emits no runtime wrapper/callback surface. The same source without the Unity symbol belongs to Category E and must not receive this Unity diagnostic. |
| `UnityPlayer_RefStructSerializable_IsRejectedAsUnitySerialization` | A serializable `ref struct` under the Unity symbol retains the mutable-struct Unity restriction (`SPIRE_DU013`) and no Unity runtime/editor support is emitted. CoreCLR without the symbol remains a normal generated union. |
| `UnityPlayer_RecordSerializable_IsRejectedAsUnitySerialization` | A serializable record union under the Unity symbol retains the existing Unity serialization restriction (`SPIRE_DU013`) and does not receive struct Unity callbacks/wrappers. Record generation must remain available as the ordinary core record surface. |

## Category C: Unity Editor drawer activation requires both symbols and metadata

### should_pass

| File Name | Description |
|---|---|
| `UnityEditor_SerializableMutableStruct_EmitsDrawerAndSharedSupport` | With runtime + Editor references and both `UNITY_5_3_OR_NEWER` and `UNITY_EDITOR`, a valid mutable serializable union emits one drawer wrapper and one compilation-scoped shared support tree. Runtime callbacks and payload wrappers remain present. |
| `UnityEditor_TwoSerializableUnions_EmitOneSharedSupportTree` | Two valid serializable unions generate two target drawers but exactly one shared support tree, with no duplicate type/member diagnostics. This preserves the existing compilation-scoped emitter contract under the new environment gate. |
| `UnityEditor_GenericAndNestedUnions_KeepDrawerTargetBoundaries` | Generic and nested valid unions emit drawer wrappers with the same target/type identity rules already covered by the drawer suite, while the support tree remains single and Editor-only. |
| `UnityEditor_NonSerializableUnion_HasNoDrawerOrSharedSupport` | `UNITY_EDITOR` and all Unity metadata alone do not opt a union into serialization or drawers. A union without `[Serializable]` has no drawer wrapper, metadata/support types, or runtime serialization surface. |
| `UnityEditor_EditorMetadataWithoutEditorSymbol_DoesNotEmitDrawer` | Keep runtime references and `UNITY_5_3_OR_NEWER`, include `UnityEditor.dll`, but omit `UNITY_EDITOR`; no drawer/support tree is generated. This directly protects against using metadata presence as a proxy for the active Editor. |
| `UnityEditor_EditorSymbolWithoutRuntimeSymbol_DoesNotEmitDrawer` | Include Editor metadata and `UNITY_EDITOR` but omit `UNITY_5_3_OR_NEWER`; no drawer/support or runtime Unity serialization is generated, even though the Editor assembly is referenceable. |

## Category D: Unity symbols without runtime metadata retain the core API and report the missing environment

These symbol-only controls deliberately omit Unity metadata, so they remain in
the portable CoreCLR lane and do not require an installed Unity Editor. The
`UNITY_5_3_OR_NEWER` symbol models the target environment while the absent
runtime reference set exercises the generator's missing-environment diagnostic.

### should_fail

| File Name | Description |
|---|---|
| `UnitySymbol_MissingRuntimeReferences_ReportsDu016AndKeepsCorePayload` | With `UNITY_5_3_OR_NEWER` but no Unity runtime metadata, a serializable mutable struct retains its core union/payload output and `[Serializable]` markers, reports `SPIRE_DU016`, and emits no callbacks, wrapper, Unity fields, drawer, or shared support tree. |
| `UnityEditorSymbols_MissingRuntimeReferences_ReportsDu016WithoutDrawer` | With `UNITY_5_3_OR_NEWER;UNITY_EDITOR` but neither runtime nor Editor metadata available, report the same missing-runtime diagnostic and keep all Unity generated surfaces absent. Editor symbols cannot manufacture runtime Unity types. |

### should_pass

| File Name | Description |
|---|---|
| `NoUnitySymbol_MissingRuntimeReferences_IsOrdinaryCoreClr` | The same serializable mutable source with no Unity symbols and no Unity references has no `SPIRE_DU016`; core generation and payload markers remain valid. This prevents the missing-runtime diagnostic from leaking into NuGet/CoreCLR consumers. |

## Category E: CoreCLR shape controls prevent Unity-only validation from leaking

### should_pass

| File Name | Description |
|---|---|
| `CoreClr_ReadonlySerializableStruct_RemainsCoreOnly` | A readonly serializable struct union with no Unity symbol keeps its ordinary generated API/payload markers and has no Unity-shape diagnostic, callbacks, wrapper, drawer, or Unity assertion. |
| `CoreClr_RefSerializableStruct_RemainsCoreOnly` | A `ref struct` serializable union with no Unity symbol remains a core generated union and receives no Unity serialization rejection merely because `System.SerializableAttribute` is present. |
| `CoreClr_RecordSerializableUnion_RemainsRecordOnly` | A serializable record union with no Unity symbol keeps the record generator behavior and has no Unity serialization diagnostic or Unity-specific generated types. |
| `CoreClr_GenericAndNestedSerializableUnions_KeepCoreMarkers` | Generic and nested serializable unions in an ordinary CoreCLR compilation preserve payload markers and generated API without callbacks, wrappers, drawer/support trees, or Unity diagnostics. |
| `CoreClr_QualifiedAndAliasedSerializableNames_DoNotChangeEnvironment` | `using SerializableAlias = System.SerializableAttribute`, `global::System.Serializable`, and unqualified `Serializable` all produce the same CoreCLR-only result; a foreign attribute with the same short name remains a non-opt-in control. |
| `CoreClr_FormerlySerializedAsAliases_KeepCoreAliasValidation` | Valid `FormerlySerializedAs` aliases still generate the ordinary core alias payload types outside Unity. Invalid or ambiguous aliases still report the existing alias diagnostic (`SPIRE_DU017`) and mark only the offending aliases invalid, so suppressing Unity-only validation does not reintroduce duplicate generated declarations or compiler errors. |

## Category F: Compilation isolation and suite boundaries

### should_pass

| File Name | Description |
|---|---|
| `IndependentRuns_CoreClrThenUnityEditor_DoNotLeakSharedSupport` | Run the same generator instance or driver lifecycle against a CoreCLR compilation followed by a Unity Editor compilation. The first output has no drawer/support types; the second has exactly its own one shared support tree. No static/incremental state from one environment leaks into the other. |
| `IndependentRuns_UnityEditorThenCoreClr_DoNotLeakUnitySurface` | Reverse the order. A later CoreCLR compilation has no Unity callbacks, wrappers, drawer/support types, Unity diagnostics, or Unity assertion calls. |
| `StandaloneNuGetConsumer_UsesCoreClrLane` | The existing standalone packed-package consumer test remains in the normal .NET test project/feed lane and builds complete and incomplete analyzer consumers without Unity references or symbols. Its project must not depend on the Unity harness or installed Unity Editor. |
| `UnityReferenceHarness_UsesUnityLaneOnly` | The Unity-reference metadata tests and drawer tests remain explicitly filtered/grouped as Unity structural coverage; they may use installed Unity assemblies but must pass Unity symbols intentionally instead of relying on `includeUnityReferences` side effects. |

## Existing tests to update rather than duplicate

The following tests already cover useful positive or negative contracts and
should be migrated to the explicit symbol inputs:

- `UnityMetadataRegressionsTests.SerializableUnion_CSharp9_HasNoGeneratedNullableWarnings`, `SerializableAndFactoriesOnOtherPartialDeclarations_AreDiscoveredSemantically`, and `SerializablePayloadFields_EmitExpectedUnityMetadataMatrix` become player cases with `UNITY_5_3_OR_NEWER`.
- `UnityMetadataRegressionsTests.NonSerializableUnion_HasNoUnitySerializationOrDrawerSurface` remains a no-`[Serializable]` control; its references may stay Unity-backed, but it must assert the symbol-independent negative result.
- `SerializablePayloadTests.SerializableUnion_FieldedAndFieldlessPayloadsAreSerializable` becomes the positive Unity-player case; `SerializableUnionWithoutUnityReferences_RetainsPayloadMarker` keeps the missing-runtime diagnostic only when it supplies `UNITY_5_3_OR_NEWER`; add the no-symbol control from Category A.
- `UnityDrawerSharedSupportTests` Editor cases pass both Unity symbols; `EditorDrawer_PlayerCompilationExcludesSupportAndWrappers` passes `UNITY_5_3_OR_NEWER` without `UNITY_EDITOR`; its no-support matrix adds the no-symbol/editor-metadata control.
- Existing `UnityVariantPayloadGeneratorTests`, `UnityDrawerSharedSupportTests`, `UnitySerializationStateTests`, and readability/field-metadata tests that request Unity references must select the appropriate player or Editor symbol explicitly. Do not change ordinary CoreCLR behavioral tests to include Unity symbols.
- Keep `FormerlySerializedAs` validation independent of Unity activation. The attribute is a migration input used by core alias/payload emission as well as Unity serialization; moving the `SPIRE_DU017` checks behind the Unity environment gate would allow invalid aliases to corrupt ordinary CoreCLR generated output.

The matrix intentionally does not duplicate native Unity persistence, Mono,
IL2CPP, or Inspector interaction. Those remain the Unity integration gates;
these cases prove the generator's environment decision before generated code
reaches those hosts.
