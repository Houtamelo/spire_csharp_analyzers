# Variant-payload Unity serialization and drawer coverage matrix

This matrix defines phase 3 of the approved variant-payload redesign. It covers
public mutable sibling payload structs, one generic serialized wrapper, serialized
kind recovery, cached snapshot reuse, isolated Inspector editing, and shared
generated Editor support. Phase 1 core generation and phase 2 consumers have
separate matrices; this file must not duplicate their runtime-storage, analyzer,
code-fix, or JSON assertions.

Structural tests target the installed Unity 6000.5.6f1 reference assemblies,
including real `UnityEngine.CoreModule.dll` and `UnityEditor.dll` references when
Unity attributes or drawer types are validated. Fake Unity namespaces are not
acceptable. Asset persistence, Inspector, Undo, prefabs, multi-object editing,
and player loading require the real Unity Editor/player fixtures. Every native
report records Unity version, backend, build profile, checks, failures, and
process exit status.

## Structural generator cases

These cases use the generator driver and compile the actual output. Do not assert
incidental private names except where Unity's serialized surface or a bridge
contract requires the name. Suggested ownership is
`UnityVariantPayloadGeneratorTests`, `UnityVariantPayloadRecoveryTests`, and
`UnityVariantPayloadDrawerTests`.

### Public payload and serialized wrapper shape

| Case | Required assertions |
|---|---|
| `PayloadSiblingTypes` | Serializable `Shape` with `Circle(float radius)`, `Move(Vector2 destination, float speed)`, and `Empty()` emits `ShapeCircle`, `ShapeMove`, and `ShapeEmpty` in the union's declaring scope as mutable serializable structs with public fields, constructors, and typed `Deconstruct`. No private payload class per variant remains. |
| `PayloadFieldsCarryUnityAttributes` | Payload fields containing primitives, Unity structs, strings, arrays, lists, and marked managed references retain declaration names/order and the required Unity field attributes. Unity owns unsupported-shape persistence; the generator does not add a new field whitelist. |
| `SerializedPayloadGenericDeclaredOnce` | Two serializable unions in one compilation receive one shared `SerializedPayload<T> : UnitySerializationPayloadBase` declaration. Each union has one active `[SerializeReference]` `_serializedPayload` whose closed value type is the generated public payload. |
| `SerializedPayloadValueIsPublicPayload` | The wrapper's serialized `value` field is `ShapeCircle`, including its public/nested fields, rather than a duplicate private payload class. |
| `SerializedKindIsSerialized` | A serialized kind field/control contains declaration-order enum values. Runtime-only unmanaged helpers, managed slots, and discriminator caches are not serialized. |
| `OnlyActivePayloadIsSerialized` | A three-variant union has one active managed-reference payload; inactive variants do not become parallel serialized fields. |
| `PayloadAccessorAndFlatApi` | Adapters use `shape.circle` and `ShapeCircle`; old flat union fields and union-level `(Kind, fields...)` deconstruction are absent. |
| `PayloadAccessibilityAndNesting` | Internal/private accessibility remains legal; `Outer.CommandRun` stays beside `Outer.Command`, with no namespace-level `CommandRun`. |
| `PayloadGenericUnion` | A namespace-level constrained generic union preserves type parameters/constraints in payloads, wrappers, factories, and callbacks. A union under a generic ancestor retains its generated declarations beside the approved diagnostic but is not a successful native serialization control. |
| `PayloadKeywordAndAliasNames` | Escaped fields/variants compile, acronym accessor names remain phase-1 names, and `FormerlySerializedAs` records canonical/former payload metadata. |

### Callback and bridge contracts

| Case | Required assertions |
|---|---|
| `BridgeHooksRemainDiscoverable` | Reflection finds `__SpirePrepareForSerialization`, `__SpireHydrateSerialized`, `__SpireSerializedStateCurrent`, `__SpireFromSerialized`, and `__SpireCreateDefault` with bridge-compatible signatures/visibility; `_serializedPayload` remains discoverable. |
| `FactoryDoesNotAllocateSerializedSnapshot` | A fresh factory value has no active serialized wrapper until serialization preparation requires one. |
| `PrepareReusesEquivalentWrapper` | Preparing an equal runtime value with an existing snapshot reuses the wrapper and inline values under Unity-aware equality. |
| `PrepareReplacesChangedWrapper` | A changed payload value or kind creates a replacement wrapper and does not mutate the previous snapshot. |
| `BridgeClonePreservesUserGraphs` | Cloning duplicates generated wrapper/inline infrastructure but preserves intentional user `SerializeReference` graph identity and `UnityEngine.Object` identity. |
| `BridgeCopiesAreIndependent` | Copied values may initially share an unchanged cache; preparing a changed value or editing through a preview must not mutate the other value's wrapper/inline state. |
| `CallbacksDoNotInvokeUserHelpers` | Generated preparation/restoration does not invoke unrelated callback-bearing helper objects outside Unity's native callback lifecycle. |

### Payload-authoritative deserialization

| Case | Serialized state and required result |
|---|---|
| `RecognizedPayloadDerivesKind` | A valid `SerializedPayload<ShapeMove>` with stale saved kind restores `Move`, preserves fields, and canonicalizes kind on the next save. |
| `RecognizedFormerPayloadAliasDerivesKind` | A former-name payload type is accepted after rename, values restore into the canonical variant, and later serialization uses canonical metadata. |
| `NullPayloadDefaultsFirstVariant` | Null payload, regardless of saved kind, replaces runtime state with the first declared variant/default fields and logs the exact invalid-data warning once. |
| `MissingPayloadDefaultsFirstVariant` | Missing managed-reference payload has the same first-variant fallback and warning; a valid kind does not fabricate another payload. |
| `UnknownPayloadDefaultsFirstVariant` | Unknown managed-reference typename has the same fallback and no exception escapes native deserialization. |
| `KindOnlyMismatchDoesNotDiscardPayload` | A recognized payload plus another valid kind keeps payload values and synchronizes kind to payload type. |
| `InvalidRecoveryCanonicalizesNextSave` | After recovery, the next serialization is canonical and repaint does not repeat the warning. |
| `IntentionalVariantSwitchIsNotRecovery` | Inspector kind changes initialize matching preview payload defaults and do not enter corruption recovery/logging. |

### Shared Editor source and metadata

| Case | Required assertions |
|---|---|
| `SharedEditorTemplateEmittedOnce` | One normal embedded Editor source template is emitted per compilation, with small per-union adapters. It is not a shipped Editor DLL or thousands of escaped StringBuilder fragments. |
| `DrawerMetadataUsesCanonicalAndFormerTypes` | Metadata stores variant names, canonical wrapper/payload typename, former typename alias, and payload field names; type matching handles Unity `/`, `+`, and `.` separators. |
| `DrawerTargetsPayloadValue` | The adapter resolves `_serializedPayload.value` and exposes public payload fields to native `PropertyField`, without legacy private payload classes. |
| `DrawerHasNormalKindControl` | The selector binds serialized kind through Unity's normal enum control and maps each selection to a matching wrapper payload. |
| `DrawerEntryPointsAreMinimal` | Per-union adapters contain metadata/construction glue only; shared support owns height, binding, preview, Undo synchronization, and focus. Redundant base-forwarding overrides are absent. |

## Native Editor serialization cases

Run these in a disposable Unity 6000.5.6f1 project, then repeat after a fresh
Editor process. Preserve YAML snapshots and managed-reference IDs around each
operation. Suggested ownership: `UnityVariantPayloadEditorSerializationTests`.

| Case | Required native assertions |
|---|---|
| `AssetSaveReloadRestoresFieldedPayload` | Save/reopen a ScriptableObject containing `Shape.Move`; kind, destination, speed, and payload type restore. |
| `AssetSaveReloadRestoresFieldlessPayload` | `Empty` restores without synthetic fields or stale values. |
| `AssetSaveReloadRestoresNestedPayload` | Serializable nested structs/classes and child unions restore all values. |
| `AssetSaveReloadRestoresCollections` | Arrays/lists of unions and union fields containing arrays/lists preserve order, variants, and null entries. |
| `AssetSaveReloadPreservesUnityObjectIdentity` | Unity object references preserve asset/object identity. |
| `AssetSaveReloadPreservesManagedReferenceGraph` | Marked managed-reference sharing/cycles remain intentional graph identity while generated infrastructure is isolated. |
| `UnchangedSaveDoesNotRewriteYaml` | No-op save, `SetDirty`, `SaveAssetIfDirty`, and forced reserialization retain YAML bytes, active managed-reference IDs, and typename. |
| `ChangedSaveReplacesOnlyChangedSnapshot` | One changed field replaces only required wrapper/value state; unrelated host fields/references/IDs remain stable. |
| `FreshEditorProcessKeepsCanonicalIds` | Process A save followed by process B no-op serialization causes no wrapper-ID churn. |
| `FormerlySerializedAsSurvivesReload` | Old payload typename restores through alias and serializes canonically afterward. |
| `InvalidPayloadRecoveryLogsOnce` | Null/missing/unknown payload YAML defaults first variant and warns during native deserialization; repeated repaint does not warn. |
| `StaleKindPayloadRecoveryIsPayloadAuthoritative` | Editing only saved kind does not discard recognized payload values; kind is corrected. |
| `ProgrammaticRuntimeChangePreparesSnapshot` | Runtime assignment followed by native serialization prepares matching wrapper/type/fields. |
| `RuntimeAndSerializedStateStaySynchronized` | SerializedProperty edits followed by apply/deserialize immediately update optimized runtime accessors. |
| `TwoCopiedHostFieldsDoNotAlias` | Editing one copied union payload leaves the other host field and wrapper unchanged. |
| `ExternalSerializedChangeRefreshesPreview` | External SerializedObject changes refresh preview and never commit stale preview data. |

## Isolated Inspector preview and UI paths

UI Toolkit and IMGUI use the same isolation semantics. Structural checks are
insufficient for the rendered cases. Suggested ownership is
`UnityVariantPayloadInspectorTests`.

| Case | UI path | Required assertions |
|---|---|---|
| `OpenDoesNotMutateActual` | UI Toolkit + IMGUI | Opening, binding, layout, and repaint clone wrapper/inline data into a hidden preview; actual value, payload type, managed-reference ID, and YAML stay unchanged. |
| `DrawsNativePayloadValue` | UI Toolkit + IMGUI | One native PropertyField targets isolated `payload.value`; Unity draws public fields/attributes without custom legacy per-field cloning. |
| `KindSelectorSwitchesVariant` | UI Toolkit + IMGUI | Selecting a kind creates matching payload defaults, refreshes controls, and commits only the user change. |
| `FieldEditCommitsReplacementUnion` | UI Toolkit + IMGUI | Editing a field writes a replacement union to the exact host property and does not mutate a shared generated wrapper. |
| `NestedAndCollectionPathsBind` | UI Toolkit + IMGUI | Nested payloads, arrays, lists, and child unions bind relative paths and commit the intended element. |
| `ManagedReferenceAndUnityObjectIdentityInPreview` | UI Toolkit + IMGUI | User managed references and Unity objects retain identity through clone/edit/commit/Undo. |
| `CopiedPreviewEditsAreIsolated` | UI Toolkit + IMGUI | Editing one displayed/copied host value cannot change another host or cache. |
| `MultiObjectMixedKinds` | UI Toolkit + IMGUI | Mixed kinds display as mixed; edits affect only targeted objects. |
| `PrefabOverrideIsRecorded` | UI Toolkit + IMGUI | Editing a prefab instance records the expected override; open/repaint records none. |

## Focus, Undo, and interaction cases

These protect the earlier bug where a control lost focus after each edit.
Suggested ownership is `UnityVariantPayloadUndoTests`.

| Case | Required assertions |
|---|---|
| `TextFieldKeepsFocusAcrossEachCharacter` | Delete/type multiple characters in a payload string; every edit stays in the same native control with caret/focus. |
| `NumericFieldKeepsFocusAcrossEachEdit` | Repeated numeric edits do not rebuild/rebind the focused control. |
| `OrdinaryValueEditDoesNotRebuildVisualTree` | Visual-tree generation/binding identity remains stable for ordinary field edits. |
| `KindChangeMayRebuildPayloadControls` | Structural kind changes may rebuild once and bind the matching payload controls. |
| `UndoRedoFieldEditTargetsActualHost` | Undo/redo field edit changes actual host/runtime state and expected wrapper IDs. |
| `UndoRedoVariantSwitchTargetsActualHost` | Undo/redo kind switch restores payload type/defaults/kind. |
| `UndoAfterExternalChangeDoesNotRestoreStalePreview` | Undo/redo after external serialized changes follows actual state, not stale preview. |
| `UndoDoesNotRecordOpenOrRepaint` | Opening/repainting without edits creates no Undo record or YAML change. |
| `FocusSurvivesUndoRefresh` | A focused control remains usable across Undo/Redo refresh. |

## Player and backend cases

Build release Linux x64 players with High managed stripping and run each case on
both Mono and IL2CPP Players. Editor-specific cases run in Editor Mono. Suggested ownership is
`UnityVariantPayloadPlayerTests` and the existing `tests/UnityVariantPayloadCore`
fixture.

| Case | Backend | Required assertions |
|---|---|---|
| `PlayerReconstructsFieldedPayload` | Mono + IL2CPP | Serialized assets restore kind, wrapper/payload type, and fields after player load. |
| `PlayerReconstructsFieldlessPayload` | Mono + IL2CPP | Fieldless payload and first-variant default recovery survive stripping. |
| `PlayerReconstructsNestedCollections` | Mono + IL2CPP | Nested payload/collection graphs reconstruct without missing generated types. |
| `PlayerReconstructsFormerAlias` | Mono + IL2CPP | Preserved former payload type metadata survives stripping and alias recovery. |
| `PlayerInvalidPayloadFallback` | Mono + IL2CPP | Null/unknown payload defaults first variant without startup exception. |
| `PlayerGeneratedSupportNoEditorDependency` | Mono + IL2CPP | Runtime player has no Editor DLL dependency; shared drawer source is excluded by `UNITY_EDITOR`. |
| `PlayerGenericUnionControl` | Mono + IL2CPP | Valid namespace-level generic closure reconstructs; diagnosed generic-ancestor inputs remain compile-only diagnostics. |

## Migration from the old Unity matrix

Preserve and rerun with new declarations: semantic `[Serializable]` opt-in across
partial declarations and aliases; Unity field attributes and Unity-owned field
serializability; active-only managed-reference persistence; payload-authoritative
recovery and exact warning text; `FormerlySerializedAs` aliases; nested graphs,
arrays/lists, Unity object identity, unchanged YAML/IDs, copied-value isolation,
native Undo/Redo, prefab overrides, multi-object editing, focus/caret retention,
and Mono/IL2CPP player reconstruction.

Rewrite representation-specific fixtures/assertions: remove every `Layout.*`
argument and old layout global; replace private per-variant payload classes with
sibling public payload structs and closed `SerializedPayload<T>` wrappers;
replace the nonserialized-kind expectation with serialized kind/control; replace
flat union fields with variant-qualified accessors and nested payload paths; remove
BoxedFields/BoxedTuple/UnsafeOverlap/Additive strategy, size catalog, and old union
deconstruction assertions; update drawer snapshots that count private payload
classes or inspect escaped StringBuilder fragments. The package is unreleased, so
no old-layout migration layer is required.

## Suggested test ownership and phase gate

| File/fixture | Scope |
|---|---|
| `UnityVariantPayloadGeneratorTests.cs` | Structural payload/wrapper/callback/metadata snapshots with actual Unity references. |
| `UnityVariantPayloadRecoveryTests.cs` | Generator-driver callback and invalid-state contracts. |
| `UnityVariantPayloadDrawerTests.cs` | Shared source/template, adapter metadata, and drawer compile checks. |
| `UnityVariantPayloadEditorSerializationTests.cs` | Native save/reload, YAML/IDs, aliases, nested graphs, copies, and external synchronization. |
| `UnityVariantPayloadInspectorTests.cs` | Rendered UI Toolkit/IMGUI selector, fields, focus, and commits. |
| `UnityVariantPayloadUndoTests.cs` | Host Undo/Redo, external changes, prefab overrides, and multi-object editing. |
| `UnityVariantPayloadPlayerTests.cs` | Mono/IL2CPP release-player reconstruction and stripping. |
| `tests/UnityVariantPayloadCore` | Focused native fixture/build reports; extend with serialized assets only when needed. |

Each implementation group adds its regression tests before changing emitter/support
code. A completed phase 3 gate requires structural checks, both drawer paths,
native save/reload/Undo/prefab/multi-object cases, and both player backends. A
passing Roslyn snapshot alone is insufficient.

## Legacy Unity fixture disposition

The package no longer exposes `Layout.*`, `ValueStorage`, or
`GenerateDeconstruct`. The old representation-specific Unity test drivers and
fixtures therefore are not compatibility controls. Their retained contracts map
as follows:

| Retired legacy surface | Current replacement |
|---|---|
| Additive/BoxedFields/BoxedTuple/UnsafeOverlap/Overlap storage tests | `UnityVariantPayloadGeneratorTests` plus the current no-layout payload fixtures |
| private per-variant payload and flat-field assertions | public sibling payload/accessor assertions in `UnityVariantPayloadGeneratorTests` |
| five-layout managed-reference, null, graph, copy, and alias cases | `tests/UnityVariantPayloadCore` recovery/player reports and the current `UnityIntegration` asset checks |
| old UnityIntegration 11k-line drawer/storage driver | the compact current-payload `SpireUnityIntegration` fixture; native Inspector ownership remains with the phase-3 Inspector fixture |
| generic-union-in-generic-owner success controls | explicit diagnostic/compile-only coverage; successful native controls use namespace-level generic unions or non-generic owners |
| legacy reserved-surface and Unity-owned field-shape driver | `UnitySerializationDiagnosticTests` current contracts for DU015/DU016, typed public payload fields, aliases, and closed generic payloads |

The retired source files are `UnityManagedReferenceBehaviorTests`,
`UnityManagedReferenceGeneratorTests`, `UnityNativeValueStabilityTests`,
`UnityOverlapGeneratorTests`, `UnityPayloadLifecycleTests`,
`UnitySerializationBridgeTests`, `UnitySerializationGeneratorTests`, and
`UnityUnsafeOverlapGeneratorTests`; their native graph/copy/hydration/stability
assertions are represented by the current asset checks and the phase-3 native
fixture reports, while their structural payload assertions are represented by
the current generator and drawer tests above.

Some existing host type identifiers retain historical words such as `BoxedFields`
to avoid changing unrelated player-scene wiring; they are ordinary no-layout
payload unions and do not select or assert a storage mode. New fixture source
uses block namespaces, `[DiscriminatedUnion]` without a layout argument, and
variant-qualified payload accessors.
