# Unity serialization footprint redesign

Status: implemented and verified on Unity 6000.5.6f1. The October 11 script-registration
correction also makes the complete IL2CPP/High serialization fixture pass in one player.

This revisits the implementation described in [payload stability](unity-payload-stability.md)
and [native stability evidence](../unity-managed-reference-stability.md).
Those documents describe the prior implementation and its historical evidence.

The separately agreed [struct field-name collision policy](2026-10-10-struct-union-field-collisions.md)
also affects generated runtime accessors. It leaves serialized payload field
names and record/class-based unions unchanged.

This incorporates the implementation-member visibility task in [the TODO](../todo.md).

## Agreed constraints

- Serialization adds at most one instance field to a union: a reference field.
  A reference still occupies one pointer plus any alignment padding in the struct.
- Serialization comparison and payload allocation during player serialization
  events are acceptable. Ordinary construction, copying, reads, and matching
  must not perform serialization work.
- Remove the generated storage-slot setters that invalidate serialization caches,
  such as `_s0.set` calling `__SpireDetachSerializedCache(0)`. The redesign must
  detect changed serialized values without requiring those setters.
- Do not emit unused helpers. In particular, omit serialization write helpers
  for fields that have no generated call site.
- Apply `[EditorBrowsable(EditorBrowsableState.Never)]` to remaining public/internal
  implementation members that are not intended as the user-facing API, including
  generated serialization callbacks and required `__Spire...` helpers.

## Implemented design

Keep only `_serializedPayload` as the extra instance field. Runtime fields remain
authoritative. Generated serialization preparation reuses an unchanged snapshot
and replaces a changed snapshot rather than mutating a shared existing snapshot.
The separate owner reference/token allocation and state flag are removed.

After player reconstruction, the same reference points to a shared, nonserializable
empty marker. It retains no payload graph and needs no per-value holder. Repeated
hydration recognizes the marker; actual deserialization still validates raw data
and reports invalid-data fallback. Serialization replaces the marker with a real
payload. Inspector edits use a separate preview payload and replace the union.
Preview allocations and Undo history belong to the Editor drawer. They add no
union fields. Undo processing uses Unity's edit group and direction; delayed
visual refresh does not discard later Undo/Redo events. Native checks cover nested
graphs, exact numeric and enum edits, multi-step Undo/Redo, and unchanged saved IDs.

## Private serialization write helpers

The prior generator emitted `__SpireSetSerializedField_<name>` for every field,
although only directly nested unions needed those calls. The redesign prepares
children into the serialized graph without installing caches into runtime child
storage. These helpers and the cache-invalidating storage wrappers are removed.

## Payload validation

Each union has a private abstract `__SpireSerializedPayloadBase`; all generated
current-name and former-name payload classes derive from it. Checking membership
in that hierarchy does not require enumerating every concrete generated class.
Use the union-specific base, not the shared package-wide payload base.

Concrete variant recognition and validity reporting share one deserializer
dispatch. The separate concrete-type enumeration is removed. Invalid input still
produces the first-variant fallback and warning during `OnAfterDeserialize`.

## Implementation-member visibility

Audit the required emitted members after removing unnecessary helpers. Ensure
`EditorBrowsable(Never)` covers remaining implementation-facing public/internal
members such as `__SpireSerializedStateCurrent`, `__SpirePrepareForSerialization`,
`OnBeforeSerialize`, and `OnAfterDeserialize`. Do not keep an otherwise unnecessary
member merely to annotate it. User-facing factories, discriminators, variant tests,
and supported matching/accessor APIs are not newly classified as hidden plumbing.

Verify the attributes in generated source and emitted metadata, and verify their
completion behavior in the supported Rider environment. This attribute is an IDE
hint, not an accessibility restriction. Microsoft documents that C# browsing does
not suppress members from the same assembly; generated members live in the
consumer's assembly. Do not claim that applying the attribute guarantees hiding
every generated member in every IDE.

Reference: [EditorBrowsableAttribute remarks](https://learn.microsoft.com/en-us/dotnet/api/system.componentmodel.editorbrowsableattribute#remarks).

## Verification constraints

The one-reference snapshot checks preserve struct-copy isolation, nested
reconstruction after player cleanup, Inspector Undo/Redo, and unchanged canonical
saved IDs. The tests distinguish a missing raw payload from an already-reconstructed
value whose payload was cleared. The shared marker preserves that distinction
without a second instance field or per-value state-holder allocation.

These are implementation questions rather than additional user-facing policy
choices. If validation shows that the agreed constraints cannot coexist, report
the specific conflicting requirements before changing the footprint, lifetime,
or persistence policy.

## Validation

Verified on Unity 6000.5.6f1, Linux x64:

| Environment | Result |
|---|---|
| Normal .NET solution | 2,242 passed |
| Full Editor Mono compiler harness | 2,266 passed; exact portable case identities and all original behavioral assertions executed |
| Original IL2CPP behavioral suite | 129 original assertions plus eight adapter/backend checks passed |
| Native Inspector integration | October 11 rerun: 120 passed; zero failures; three metadata observations covered by separate checks or delegated Unity type policy |
| Standalone Mono serialization fixture | Values, cloning, shared reconstruction markers, and weak-reference payload collection passed |
| High-stripping IL2CPP main profile | Final package passed all required assertions, including managed-reference graphs |
| High-stripping IL2CPP stability profile | Final package passed every required stability and managed-reference assertion |
| High-stripping IL2CPP complete fixture | Build and player exit 0; every main, stability, and managed-reference assertion passes after correcting the fixture host's script registration |

Reports are under `tmp/lead-unity-redesign-final-harness`,
`tmp/lead-unity-redesign-il2cpp-behavioral`, and
`tmp/lead-unity-redesign-final-editor`. Player results are in
`tmp/lead-unity-redesign-mono-player.json` and
`tmp/lead-unity-redesign-il2cpp-main-player.json`, plus
`tmp/lead-unity-redesign-final-il2cpp-stability-player.json`.

The October 11 resumption also fixed a rendered enum edit that reverted to its
old value. Unity's enum popup emits a string display label, while enum/flags
controls can emit `System.Enum`; both now normalize to the declared enum before
snapshot comparison and commit. Exact numeric storage is updated with the
actual enum value, preserving nonordinal values and combined flags. The fresh
Inspector run covers distinct `InspectorName` labels, flags, unavailable choices,
precise long/double fields, and Undo/Redo. Results are in
`tmp/unity-resume-editor-final/unity-integration-results.json`; all 2,242 .NET
tests pass in `tmp/unity-resume-final-dotnet-results`.

The final package also passes a fresh combined `Full` IL2CPP/High build and
player on 6000.5.6f1: every required main, stability, and managed-reference
assertion is true, including shared reconstruction markers, copy isolation, and
payload collection. Evidence is under `tmp/unity-resume-player-final`, with the
runtime report at `tmp/unity-resume-final-player.json`. The refreshed installable
package is `tmp/unity-package/com.houtamelo.spire`.

The earlier combined-player `level0` corruption was traced to the test fixture's
embedded `MonoScript`: `StabilityPlayerHost` lived in a source file whose name
did not match the component. Moving it to `StabilityPlayerHost.cs` and correcting
the scene's script reference made the frozen fixture pass without changing its
serialized values or Spire DLLs. The complete tracked fixture now passes in one
IL2CPP/High player; focused profiles remain available. Builds reject the invalid
registration before player compilation. Inspector-only numeric fixtures remain
Editor-only. See the [Unity-only MRP and native comparison](../issues/unity-il2cpp-opaque-managed-reference-slots.md#mrp-and-corrected-fixture).

Unity 6000.6.5f1 still crashes while invoking serialization callbacks on generic
structs instantiated with reference types. A [separate Unity-only MRP](../issues/unity-6.6-generic-struct-serialization-callback.md)
reproduces that Editor issue without Spire. Both crashes have been reported to
Unity; native serialization support remains validated on 6000.5.6f1, Linux.
The compiler harness and behavioral player passing on 6.6 do not establish
native persistence compatibility there.
