# Suggested optional-package additions

Review of all **478 package candidates** recommends **291 additions**, defers
**172**, and excludes **15** from the initial catalog. Every input row has a
decision and reason. All 291 suggestions were accepted on 2026-10-21, bringing
the [official initial target](../designs/unity-initial-layout-targets.csv) to
**664 types**. The remaining decisions are unchanged.

- [Suggested additions: 291 entries](unity-optional-package-suggested.csv)
- [Complete review: all 478 entries and reasons](unity-optional-package-reviewed.csv)
- [Original candidates, preserved](unity-optional-package-candidates.csv)
- [Package versions, provenance and coverage gaps](2026-10-21-unity-optional-package-catalog.md)

The suggested CSV marks `Include` with `x`. The complete review preserves input
order and fields, and adds `Recommendation`, `ReviewCategory`, `ReviewReason`,
`TypeObsolete`, declaration conditions/layouts, and declaration hashes. These are
recommendations about membership; sizes, alignments and target compatibility
still require validation.

| Decision | Count | Treatment |
|---|---:|---|
| Include | 291 | Useful value data, identifiers, operation inputs/results or configuration. |
| Defer | 172 | Valid APIs outside the initial scope, or needing a separate lifetime/platform review. |
| Exclude: deprecated type | 6 | Use current APIs. |
| Exclude: Editor-only declaration | 2 | Not a player API. |
| Exclude: relative-offset blob/header | 7 | Not an independent by-value payload. |

## Suggested additions

The list favors data that application code can construct, pass to an operation,
or receive as a result. Representative groups are:

| Area | Examples |
|---|---|
| ECS identity and transforms | `Entity`, `EntityGuid`, `SystemHandle`, `LocalTransform`, `TimeData`, component descriptors and scene identifiers. |
| Fixed strings and math | All five `FixedString*Bytes` values, `v128`, `v256`, AABB variants and fixed bitsets. |
| Physics and characters | Geometry, hit/query records, collision filters, mass/material/joint/velocity values and character settings/hits. |
| Network data | Connection events/state, ghost identities, prediction requests, endpoints, relay/TLS settings and public tick-rate configuration. |
| Input and XR | Device states, concrete commands, pose/bone/eye/hand data, teleport requests, AR query results and OpenXR geometry/identifiers. |
| Rendering and cameras | Camera/probe/frame options, LOD/material selection, spherical harmonics, HDR settings and water-surface query inputs/results. |
| Application data | Text geometry/glyph values, UI options/dates, splines, inference shapes, serialization options and content progress/results. |

Fixed strings remain included despite implementing collection interfaces: they
own their character bytes inline. Their fixed buffers are not external native
storage. This agrees with the [Collections documentation](https://docs.unity3d.com/Packages/com.unity.collections@2.6/api/Unity.Collections.FixedString128Bytes.html);
the review itself uses the pinned package sources, not a layout assumption from
that documentation version.

## Significant exclusions and deferrals

Six types have actual type-level `Obsolete` attributes:

- `Unity.Entities.SystemHandleUntyped`: use `SystemHandle`.
- `UnityEngine.Rendering.BatchRendererGroupGlobals`: Unity now sets the globals.
- `UnityEngine.Rendering.HighDefinition.HDPhysicalCamera`: settings moved to `Camera`.
- `UnityEngine.Rendering.Universal.ShaderInput.ShadowData`: replaced by the current GPU-buffer path.
- `UnityEngine.XR.Interaction.Toolkit.AffordanceSystem.State.AffordanceStateData`: its affordance API is deprecated.
- `UnityEngine.XR.OpenXR.Input.Pose`: use Input System `PoseState`.

Two declarations passed the folder/assembly scope heuristic but are guarded by
`UNITY_EDITOR`: `RuntimeContentManager.InstanceHandle` and
`NetworkTransform.BufferEntry` (the latter also requires `DEBUG_LINEARBUFFER`).
They remain in the original CSV for provenance and are excluded in the review.

Seven records contain relative-offset blob references or variable-sized blob
headers: `Unity.Physics.BlobArray`, `CompoundCollider`, `CompoundCollider.Child`,
`ConvexCollider`, `MeshCollider`, `TerrainCollider`, and
`Unity.Scenes.ResourceCatalogData`. Copying their struct-sized prefix does not
copy the referenced data. The pinned collider sources explicitly document
by-reference requirements where applicable. Unity's [blob documentation](https://docs.unity3d.com/Packages/com.unity.entities@1.4/manual/blob-assets-create.html#accessing-blob-assets-on-a-component)
and [ConvexCollider documentation](https://docs.unity3d.com/Packages/com.unity.physics@1.4/api/Unity.Physics.ConvexCollider.html)
describe the same restriction. Unmanaged classification alone does not establish
that a record is a self-contained by-value payload.

`SceneSectionData` needs a different caution: its `RuntimeBlobHeaderRef` stores
a `BlobAssetReference` through a `long`, reinterpreting it with `UnsafeUtility.As`.
The pointer-field filter cannot detect this encoded native reference. It is
deferred along with scene-streaming implementation state.

The five fixed-size primitive collider implementations are deferred separately.
They are not mislabeled as variable-sized headers; geometry records and query
results are the stronger initial targets, while standalone collider-by-value
usage warrants its own review.

Other deferrals cover baking data, executable jobs/systems, iterators, patcher
records, parser/solver/renderer caches, metrics, graph/stream/provider handles,
and serialization schemas. Specialized Arm/experimental NEON and Steam
Controller APIs are deferred pending their feature/target scope. Ordinary
platform-conditioned input/XR data is suggested with its declaration conditions
recorded; inclusion does not imply availability on every player target.

Legacy names are not treated as deprecation attributes. For example,
`GlyphValueRecord_Legacy` and Barracuda's `TensorShape` are deferred as scope
choices, while current glyph and Inference Engine equivalents are suggested.

## Evidence and checks

The review uses the exact package versions in the
[inventory](2026-10-21-unity-optional-package-catalog.md). A Roslyn helper parsed
all **478 types (480 declaration parts) across 341 source files**, including type/containing-type
attributes, comments, interfaces, fields, layout annotations and surrounding
preprocessor conditions. Targeted code inspection checked blob addressing,
encoded references, lifetime restrictions and intended API use. Comments are
context; the per-type reasons record the review's scope judgments.

`tmp/unity-package-catalog-20261021/evidence` contains the extraction helper.
`review-evidence.json`, `review_candidates.py`, and `candidate-review-summary.json`
retain evidence, explicit decisions, and hashes. No default rule assigns a
decision: the script fails on missing/duplicate entries or a changed input.

Reconciliation checks all 478 rows, the six deprecated types, two Editor-only
types, seven blob exclusions, source declarations, 291 `Include` marks, and the
unchanged input and original engine-only snapshot. The accepted additions were
reconciled against the new 664-entry official target. Repeated generation produces
identical CSVs.
No shipped C# behavior or layout constants were changed.
