# Managed-reference stability probe

Historical evidence for the former owner-token implementation. Current cache
behavior is described in the [one-reference redesign](designs/2026-10-10-unity-serialization-footprint.md).

Verified on 2026-10-08 with Unity 6000.5.6f1, Linux Editor Mono, under Xvfb.
Production serialization behavior was unchanged during the probe.

Recreating a union's managed-reference payload changes its saved `rid` even when
the variant and field values are identical. Clearing the payload after loading
therefore creates version-control diffs when the asset is subsequently saved.
Unity normally retains managed-reference IDs across unloading and reloading as
long as the referenced object remains attached to its host. See
[Unity's managed-reference ID documentation](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Serialization.ManagedReferenceUtility.SetManagedReferenceIdForObject.html).

The scratch probe used the real `UnitySerializationAsset` and generated unions:

| Operation | Entire YAML unchanged | Outer `Label` payload ID unchanged |
|---|---|---|
| Unload, reload, force save | Yes | Yes |
| First repeated save/import/SerializedObject.Update, without clearing | No | Yes |
| Second repeated save/import/SerializedObject.Update, without clearing | No | Yes |
| Clear the outer payload and owner, update, force save | No | No |
| Repeat save after recreation | No | Yes |

The repeated-save controls revealed a separate existing problem: nine nested
union payload IDs changed, including direct child unions, helper-contained
children, and children in BoxedFields, BoxedLayout, UnsafeOverlap, and Overlap
parents. Their types and field values remained unchanged. This happened before
any experimental cache clearing. For example:

```diff
 child:
   _kind: 0
   _serializedPayload:
-    rid: 1321628571539865638
+    rid: 1321628571539865675
```

The corresponding `references.RefIds` entry changed to the same new ID.

## Confirmed ownership mismatch

A second native Editor probe exercised the real generated preparation and
deserialization methods. Nested unions are prepared through boxed struct copies.
The new owner token assigned during preparation remains in the snapshot copy;
the authoritative runtime child still has no owner token.

`UnitySerializationBridge.Transform` adopts a previous snapshot's payload when
the incoming child's cache is null, but does not adopt the snapshot's owner
token. Generated preparation creates a new token, fails the existing payload's
ownership claim, and allocates a replacement. When the runtime child already has
a cache, the bridge ignores the previous snapshot entirely, so the same owner
mismatch can recur.

After an actual generated `OnAfterDeserialize` call, the first preparation reused
the unclaimed payload. Preparing the same hydrated source a second time replaced
it. The runtime source's owner remained null after both calls. This explains why
the first reload/save control was stable while later save/refresh cycles changed
nested IDs.

In a proof-only control, assigning both the previous snapshot's payload and
owner to the temporary child before invoking generated preparation preserved
the payload object and owner. Its serialized fields correctly changed from
`count = 20, label = "prior"` to `count = 21, label = "control-update"`.
The control used `ReferenceEquals` to establish object reuse; diagnostic identity
hashes in its report are not Unity managed-reference IDs.

The probe suggested preparing into the previous destination snapshot's cache and
owner, using the incoming child for runtime values. At that point this was a
proof-only control, with copy isolation and collection behavior still unverified.
The implemented fix and permanent checks are described below.

Reproduction artifacts are ignored scratch files under
`tmp/unity-il2cpp-integration`: `Assets/Editor/SpirePayloadCacheProbe.cs`,
`spire-payload-cache-probe.json`, and `spire-payload-cache-probe-*.yaml`.
Run `Spire.UnityIntegration.SpirePayloadCacheProbe.Run` through the tool's
`unity-integration --method` option. The probe deletes its temporary asset after
copying the YAML evidence.

The ownership probe is `Assets/Editor/SpirePayloadOwnershipProbe.cs`, invoked
through `Spire.UnityIntegration.SpirePayloadOwnershipProbe.Run`, with results in
`spire-payload-ownership-probe.json`. Both probes leave production code unchanged.

## Player cloning and cleanup

A standalone Unity 6000.5.6f1 Linux Mono player confirmed the runtime cloning
path. A freshly created GameObject held a generated `Label("probe", null)` union
with no payload. After `Object.Instantiate(source)`:

- The source holder had one `OnBeforeSerialize` call and zero
  `OnAfterDeserialize` calls, and its union retained a newly created payload.
- The clone holder had zero `OnBeforeSerialize` calls and one
  `OnAfterDeserialize` call. The clone's runtime value was correct and its payload
  was a distinct object.
- Repeating instantiation from the same source reused the same source payload
  object in this simple scalar-field case.
- Manually clearing the first clone's payload after instantiation preserved its
  runtime value. Instantiating that cleared clone successfully reconstructed the
  new copy's value.

Thus player-only cleanup after deserialization can release snapshots on loaded
objects and clones, but does not release the source snapshot produced by runtime
cloning. The generated union has no post-serialization callback or containing
host/field location through which to schedule exact source cleanup. This lifetime
issue also applies to a concrete inline payload class; avoiding managed-reference
IDs alone does not remove the intermediary snapshot.

This probe did not implement cleanup in the generated callback, measure allocated
bytes, or test IL2CPP. Its callback counters were on the holder component; payload
field observations exercised the actual generated union. Scratch files are
`Assets/Editor/SpirePlayerCloneProbeBuild.cs`,
`Assets/Runtime/SpirePlayerCloneProbeBehaviour.cs`,
`spire-player-clone-probe-build.json`, and
`spire-player-clone-probe-runtime.json` under the same disposable project. The
runtime report confirms `isEditor = false`, `backend = Mono`, and
`applicationPlatform = LinuxPlayer`.

The YAML stability results cover ScriptableObject assets. Scene saves, prefab overrides, and
continuous cache clearing through actual deserialization callbacks were not
tested. Do not adopt Editor cache clearing without addressing saved-ID stability
and adding permanent native YAML regression tests.

## Implemented behavior

The 2026-10-09 implementation retains Editor caches and prepares nested values
into the prior destination's payload and ownership token. Runtime values remain
authoritative; copies and sibling slots retain independent snapshots. A
nonserialized bool distinguishes reconstructed values from uninitialized data,
so explicit nested hydration can preserve values after player cache cleanup.

The latest Unity 6000.5.6f1 Linux Editor integration run passes 106 scenarios,
including complete ScriptableObject YAML and recursive managed-reference ID
comparisons across repeated saves and unload/reload/save cycles. It also covers all-layout former-name
payload migration, multi-field BoxedTuple storage, mixed-object drawers,
Undo/Redo, script reload, and Play Mode. The separate Unity-hosted compiler and
behavioral harness passes 2,158 tests with exact case-identity matching. Current
results are recorded in
[managed-reference validation](designs/unity-variant-managed-references.md#validation-2026-10-09).

Stability comparisons use saved and reloaded data. Unity expands null inline
helper objects into initialized objects; repeatedly saving a fresh `Some(null)`
helper can recreate its nested payload before that normalization reaches runtime
storage. Loaded canonical data remains byte-identical on subsequent saves.
Spire does not construct replacement helper objects to imitate Unity's serializer.

Variant parameters can now opt into managed-reference field serialization with
`[UnitySerializeReference]`. Such fields preserve null, runtime subtypes, and
sharing instead of Unity's inline helper normalization. Unmarked helper fields
retain the inline boundary described above. Adding or removing the marker changes
the stored field encoding and requires handling any existing data separately.

Player callbacks clear payload and owner fields after reconstructing runtime
values. Runtime cloning still creates a reusable source snapshot; it cannot
clear that source through the clone's deserialization callback. Standalone
player results are recorded in the validation section of
[the implementation brief](designs/unity-payload-stability.md).
