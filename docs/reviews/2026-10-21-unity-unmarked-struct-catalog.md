# Forty additional Unity catalog candidates

Reviewing the **377 unmarked entries** identified **40 recommended additions**.
The combined shortlist now contains **373 types**, preserving all 333 prior
recommendations. The original annotated CSV remains unchanged.

Approved on 2026-10-21: all 40 additions are accepted, and the combined 373 types
form the [engine portion](../designs/unity-initial-layout-targets.engine-only.csv)
of the initial target. Later accepted package additions bring the
[canonical membership CSV](../designs/unity-initial-layout-targets.csv) to 664 types.

- [The 40 additions, with per-type reasons](../../tmp/unity-layout-catalog-20261020/public-structs-unity/unmanaged.additions.csv)
- [Combined shortlist: 373 entries](../../tmp/unity-layout-catalog-20261020/public-structs-unity/unmanaged.included.csv)
- [Review of every unmarked entry](../../tmp/unity-layout-catalog-20261020/public-structs-unity/unmanaged.unmarked-review.csv)
- [Complete 878-row review](../../tmp/unity-layout-catalog-20261020/public-structs-unity/unmanaged.reviewed.csv)

`OriginalInclude` still contains the user's original marks. The revised
`Include` column contains the combined recommendations; `SelectionOrigin`
distinguishes original selections from suggested additions. The prior
[333-entry shortlist](../../tmp/unity-layout-catalog-20261020/public-structs-unity/unmanaged.included.marked-only.csv)
and [marked-only review](../../tmp/unity-layout-catalog-20261020/public-structs-unity/unmanaged.reviewed.marked-only.csv)
are preserved as snapshots.

## Additions

All additions are public, non-generic, non-ref-like runtime types with at least
two instance storage fields. Their source and compiled field inventories match,
and none has a native pointer dependency path in the inventory.

| Area | Count | Additions or rationale |
|---|---:|---|
| Navigation | 7 | `NavLocation`, `NavMeshHit`, `NavMeshBuildMarkup`, `NavMeshBuildSettings`, `NavMeshBuildSource`, `NavMeshLinkData`, `OffMeshLinkData`. Query results and build/link settings. |
| Physics | 10 | Seven query commands, `ArticulationDrive`, `ArticulationReducedSpace`, and `ClothSkinningCoefficient`. Complements the selected raycast and joint data. |
| Input | 3 | `Touch`, `PenData`, `AccelerationEvent`: natural event-union payloads. |
| Audio | 4 | `AudioConfiguration`, generator `Setup`/`Configuration`, and processor `CreationParameters`: value-only setup/configuration records. |
| Camera, mesh, textures, VFX | 7 | `Camera.GateFitParameters`, `CombineInstance`, `CullingGroupEvent`, `CustomRenderTextureUpdateZone`, `TextureMipmapLimitSettings`, `Mesh.LodSelectionCurve`, `VFXCameraXRSettings`. |
| Terrain | 2 | `DetailInstanceTransform`, `PatchExtents`. Complements selected terrain/tree data. |
| Animation and text | 2 | `MatchTargetWeightMask`, `CharacterInfo`. Complements selected animation and glyph records. |
| Other runtime identifiers/configuration | 5 | `HierarchyNode`, `HierarchyPropertyDescriptor`, `ContentSceneParameters`, `BuildCompression`, `ConsentState`. |

The seven query commands are `BoxcastCommand`, `CapsulecastCommand`,
`SpherecastCommand`, `ClosestPointCommand`, `OverlapBoxCommand`,
`OverlapCapsuleCommand`, and `OverlapSphereCommand`. They follow the same
catalog rationale as the previously selected `RaycastCommand`.

Several details required checking the actual storage or API scope:

- `ArticulationReducedSpace` contains an inline fixed float buffer plus
  `dofCount`; the buffer is not a native pointer field.
- `CombineInstance` exposes a `Mesh` property but stores an `EntityId`, together
  with transform/lightmap values, in this Unity version.
- `CharacterInfo` has obsolete fields but no type-level `ObsoleteAttribute`.
  Its current properties use the existing physical storage.
- Processor `CreationParameters` has a partial declaration under a deprecated
  API file, but the type itself remains current.
- `BuildCompression` is also used for runtime AssetBundle recompression.
- Use `Unity.AI.Navigation.LowLevel.NavLocation`; the experimental
  `NavMeshLocation` and `NavMeshQuery` types have explicit deprecation attributes.

The CSV records the full type name, source location, fields, assembly, and
specific reason for each addition.

## Remaining unmarked entries

These outcomes apply only to the 377 originally unmarked rows. No previously
selected recommendation was changed in this pass.

| Outcome | Count |
|---|---:|
| Recommended additions | 40 |
| Empty types | 162 |
| Single-field types | 48 |
| Ref-like types | 6 |
| Deprecated types | 12 |
| Editor-only APIs | 64 |
| Native pointer/handle/view storage | 14 |
| Iterator/enumerator state | 2 |
| Flattened hierarchy traversal cache | 1 |
| Profiling/performance/diagnostic data | 12 |
| Experimental light-baking backend records | 10 |
| Vendor-specific GPU SDK configuration | 5 |
| Editor build-program version record | 1 |

The baking and vendor SDK records are viable value data. Their deferral is a
scope recommendation for the initial catalog. The same applies to profiling
records and `HierarchyFlattenedNode`; none is being classified as managed merely
because it is deferred. The planned unknown-size helpers remain the fallback
for unmanaged types without a catalog entry.

## Validation

The source and compiled metadata are the same Unity **6000.5.6f1** snapshots
used by the [marked-type review](2026-10-21-unity-selected-struct-catalog.md).
All **376 available compiled unmarked types and their 904 instance fields**
matched the source inventory. Together with the first pass, that covers 877
types and 3,053 fields.

`BeeBuildProgramCommon.Data.Version` is the one entry absent from the inspected
runtime assemblies. Its source is under the Editor incremental build pipeline;
it remains deferred as build tooling.

The [unmarked review manifest](../../tmp/unity-layout-catalog-20261020/selection-review/unmarked-review-summary.json)
records input/output hashes and reconciliation counts. The original CSV still
has SHA-256
`711c65d35c56f8d63dfb9376d6e6e24549e46bfb85383aacb0fec8a755eaa317`.
Reproduction, from the first-pass artifacts:

```sh
python3 tmp/unity-layout-catalog-20261020/selection-review/review_unmarked.py
```

Checks cover the preserved 333 recommendations, exactly 40 additions from
previously blank rows, the combined 373-entry list, all 878 original rows, field
counts/types, fixed buffers, and the existing structural filter. No scanner or
generator C# behavior was changed, and no size/alignment constants were added.
