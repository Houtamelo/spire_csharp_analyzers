# Variant-payload migration coverage review

This is a read-only audit of the migrated tests. The source-generator suites are
green (583 cases), the behavioral suite is green (73 cases), the compact Unity
integration fixture reports 9/9, and the recorded Mono/IL2CPP player profiles
pass their required main, stability, and managed-reference assertions. The
current tests preserve most of the old graph, collection, alias, copy, and
stripping behavior. The following items still need attention before the
phase-three gate can be called complete.

Follow-up validation closed the actionable gaps: fresh-process Read now performs
a no-op save with exact YAML/ID comparison (53 checks); Write includes direct
callback isolation (92 checks). Four additional Unity-reference cases cover C# 9,
partial declaration opt-in, and broader field metadata. The final rendered run
passes 16 cases/147 checks, including external union edits, shared-node Undo/Redo,
and repeated native IMGUI input. The malformed-Player-resource boundary remains
explicitly documented: Unity can canonicalize that resource during the build,
while Editor Recovery and direct generated Player fallback are verified. The
findings below preserve the pre-fix audit evidence.

## Required behavior without a passing assertion

### P1: persisted malformed payload fallback is not verified in Players

`SerializedUnionPlayerProbe` loads the malformed null-payload resource, but lines
104–124 of
[`SerializedUnionPlayerProbe.cs`](../../tests/UnityVariantPayloadCore/Assets/Runtime/SerializedUnionPlayerProbe.cs)
only log whether Unity canonicalized or omitted it. The only assertion is the
direct `__SpireFromSerialized(null)` call at lines 127–137. The Editor Recovery
phase does verify null, missing, and unknown native deserialization, but the
Mono/IL2CPP player result does not prove that an invalid serialized payload
defaults to the first variant in a Player build. Preserve a malformed resource
through the Player build, or retain this as an explicit unverified Unity
limitation in the phase report rather than counting it as `PlayerInvalidPayloadFallback`.

### P1: fresh-process no-op save and managed-reference ID stability are incomplete

`WritePhase` exercises no-op saves and force reserialization in one Editor
process at lines 173–186 of
[`SerializedUnionEditorTests.cs`](../../tests/UnityVariantPayloadCore/Assets/Editor/SerializedUnionEditorTests.cs).
`ReadPhase` loads the baseline in a fresh process and compares bytes before any
edit at lines 191–213, but it never performs `SetDirty`/`SaveAssetIfDirty`,
`SaveAssets`, or force reserialization after that fresh load. Add one fresh-process
no-op save and compare complete YAML and managed-reference IDs. This is the
matrix's `FreshEditorProcessKeepsCanonicalIds` case, distinct from the current
same-process checks.

### P1: external payload changes do not exercise preview refresh

`CheckUndoRedoAndExternalChange` changes only `ordinaryField` through a second
`SerializedObject` at lines 977–987 of
[`SerializedUnionInspectorTests.cs`](../../tests/UnityVariantPayloadCore/Assets/Editor/SerializedUnionInspectorTests.cs).
It then verifies that the union value remains unchanged. The required contract is
that an external change to the actual union's `_kind` or `_serializedPayload.value`
refreshes the hidden preview and does not commit stale preview data. Add that
union-specific mutation while the drawer is open; the current test would pass
even if payload changes left a stale preview.

### P1: the rendered Inspector gate is currently failing/unverified

The latest archived rendered run under `tmp/unity-union-inspector/TestResults`
reports 81 checks with 7 failures and 4 unverified cases. The failures include
`UndoRedoAndExternalSerializedChange`, `NestedAndCollectionPathsBind`, and
`MultiObjectMixedKindsAndPerObjectFields`; fieldless switching, numeric focus,
and copied-preview editing are unverified. The fixture contains meaningful
assertions for these cases (for example, lines 922–975 and 1080–1144), so this is
not a missing-test problem; it is an unresolved native validation gate. Do not
count phase-three interaction coverage as complete until a fresh report has no
failed or unverified cases.

## Lower-priority structural coverage gaps

### P2: Unity C# 9 and generated nullable-warning controls were removed without a replacement

The old Unity generator suite compiled every accepted serializable fixture under
C# 9 and checked generated nullable warnings, while also checking non-serializable
warning baselines. The current
[`UnityVariantPayloadGeneratorTests.cs`](../../tests/Houtamelo.Spire.SourceGenerators.Tests/Unity/UnityVariantPayloadGeneratorTests.cs)
uses preview parsing for all structural cases; its C# 9 checks are in the
non-Unity core suite only. Add a small Unity-reference C# 9 control for a
serializable union and a normal non-`[Serializable]` union asserting no Unity
callbacks, wrapper, `SerializeField`, or drawer are emitted.

### P2: semantic Serializable opt-in across partial declarations is not covered

The replacement structural sources put `[Serializable]` and
`[DiscriminatedUnion]` on the same declaration. The old suite separately covered
the attribute on another partial declaration and variants declared on another
partial. Keep one real Unity-reference driver case for each shape, including a
qualified/aliased `SerializableAttribute`; this protects the symbol-level opt-in
that the generator relies on.

### P2: field-shape metadata is narrower than the retired matrix

The current structural metadata control checks a marked managed-reference field
and an `int`; `UnitySerializationDiagnosticTests` adds `object`, `Dictionary`,
nullable, and `Texture2D`. Native fixtures exercise Vector2, strings, textures,
arrays, and lists at runtime, but no structural case verifies enum fields,
escaped/keyword field identifiers, or the required `[SerializeField]`/
`[SerializeReference]` annotations across arrays and lists. Add one compact
metadata case. Unity should own unsupported-shape persistence; the test should
only assert emitted declarations and attributes.

### P2: generated preparation/restoration is not isolated from user callbacks

The old bridge tests explicitly called preparation/restoration with a callback-
bearing helper and asserted that its callback counters stayed unchanged. The
current compact core helper has no callback-bearing nested type. The broader
UnityIntegration source still contains `StabilityCallbackChildHelper`, but its
Player checks validate that Unity's native callback eventually ran; they do not
prove that a direct generated bridge prepare/restore call does not invoke the
user helper. Add a focused real Editor/driver control for this approved contract.

## Preserved coverage confirmed

The compact Editor fixture does verify nested inline helpers, nested child unions,
arrays/lists, generic `int` payloads, former-name aliases, Unity object identity,
managed-reference sharing/cycles, copied-value isolation, wrapper reuse and
replacement, exact invalid-data warnings, and canonical next saves. The recorded
Player reports additionally cover generic/reference managed-reference closures,
former aliases, nested arrays/lists, callback lifecycle, clone isolation, and
High-stripping Mono/IL2CPP profiles. Those retired layout-specific tests do not
need to return; their semantic assertions are represented by these current
payload fixtures.
