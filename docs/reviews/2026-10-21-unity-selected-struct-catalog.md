# Review of the marked Unity catalog candidates

Of the **501 types marked for inclusion**, retain **333** as candidates and
remove or defer **168**. The requested empty/single-field filter accounts for
83 removals. The remaining changes exclude unsuitable or deprecated types and
defer lower-priority entries from the initial known-layout catalog.

The [follow-up review of unmarked entries](2026-10-21-unity-unmarked-struct-catalog.md)
adds 40 candidates. The combined [373 engine types](../designs/unity-initial-layout-targets.engine-only.csv)
were approved on 2026-10-21. Later package additions expand the
[official initial target](../designs/unity-initial-layout-targets.csv) to 664.
This document records the original marked-only pass.

- [Marked-only recommended list: 333 entries](../../tmp/unity-layout-catalog-20261020/public-structs-unity/unmanaged.included.marked-only.csv)
- [Marked-only review: original marks, revised Include, and reasons](../../tmp/unity-layout-catalog-20261020/public-structs-unity/unmanaged.reviewed.marked-only.csv)
- [Original annotated CSV, preserved](../../tmp/unity-layout-catalog-20261020/public-structs-unity/unmanaged.csv)

`unmanaged.reviewed.marked-only.csv` preserves all 878 input rows and copies the user's
marks to `OriginalInclude`. Its revised `Include` column marks the 333 retained
types. `unmanaged.included.marked-only.csv` contains only those rows. Both outputs use
standard comma-separated CSV; the original input uses tab separators.

| Decision category | Marked entries | Treatment |
|---|---:|---|
| No instance fields | 17 | Exclude as requested. |
| One instance field | 66 | Exclude as requested. |
| Ref-like types | 5 | Exclude: ordinary unions cannot embed them. |
| Deprecated types | 3 | Exclude from the new catalog. |
| Native pointer/handle/view storage | 63 | Defer until architecture and build-profile support is selected. |
| Iterator/enumerator state | 4 | Defer low-priority traversal helpers. |
| Editor-only settings | 1 | Defer from the runtime catalog. |
| Diagnostic/profiling snapshots | 3 | Defer low-priority instrumentation data. |
| Physics debug-drawing data | 6 | Defer low-priority debug output and styling. |
| Other selected value data | 333 | Retain, pending size/alignment and version validation. |

The 77 `defer` decisions are recommendations about the initial catalog's scope.
They can be restored individually from `OriginalInclude`. The planned
unknown-size unmanaged helpers remain available for types without catalog
entries; omitting an entry does not remove union support for that type.

## Significant removals

The five ref-like types are `UnityEngine.Audio.ChannelBuffer` and the
`ConvDescriptor`, `GemmDescriptor`, `IdentityDescriptor`, and `ReduceDescriptor`
nested in `UnityEngine.Rendering.MachineLearningOperatorFactory`. A ref struct
cannot be the field type of an ordinary struct, so these cannot be payloads of
the union representation under discussion. [C# ref struct rules](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/ref-struct).

The three deprecated entries surviving the field-count filter are:

- `UnityEngine.InstanceID`: obsolete with an error; use `EntityId`.
- `UnityEngine.Rendering.RayTracingAccelerationStructure.RASSettings`: obsolete
  with an error; use `Settings`.
- `UnityEngine.SocialPlatforms.Range`: deprecated with a warning and scheduled
  for removal according to its attribute.

Native-storage deferrals include `AtomicSafetyHandle`, file/read handles,
`JobHandle`, animation streams, mesh data views, particle job data, playable
handles, culling results, rendering contexts, and UI mesh allocators. The CSV
records each pointer dependency path, including paths through fields such as
`NativeArray<T>`. This is a conservative source-level check across conditional
branches. These entries need target/build-profile validation before their
size/alignment can enter the catalog.

The iterator decisions cover `BoundsInt.PositionEnumerator`, both
`PhysicsMask` bit iterators, and `ContactManifold.ManifoldPointIterator`.
The editor-only entry is `UnityEditor.Animations.CurveFilterOptions`.
`FrameTiming`, physics `WorldCounters`/`WorldProfile`, `DrawColors`, and the
five debug draw-result element types account for the instrumentation/debug cuts.

## Checks that affected the review

Count physical instance fields, including private and compiler-generated
storage; ignore static/const fields and computed properties. "Empty" here means
zero instance fields. No zero-byte managed size was assumed. The single-field
filter applies even to fixed buffers and wrappers with custom layout; removing
their catalog entries does not make them eligible for the ordinary-wrapper
shortcut automatically.

Fixed buffers are inline storage. Roslyn exposes their declared field types as
pointers, so the pointer-dependency check explicitly skips fixed buffers.
`LightProbeOcclusion`, `CameraProperties`, `DrawingSettings`,
`ScriptableCullingParameters`, and `ShadowSplitData` remain candidates.
`Color32` retains its private overlapping int in its field inventory.

An `.obsolete.cs` path does not establish that the type is obsolete. For example,
`PhysicsWorld` and `PhysicsBodyDefinition` have partial declarations containing
legacy members but remain current types. Their type-level metadata has no
`ObsoleteAttribute`, and they remain in the recommended list.

The 66 retained `Unity.Mathematics` entries were found in this Editor's built-in
`UnityEngine.MathematicsModule` assembly. Supporting package-provided
`Unity.Mathematics.dll` types with the same names still requires its own type
identity/version validation.

## Evidence and reproduction

The source remains pinned to Unity 6000.5.6f1, commit
`0e24f535c49ef35981d84b56f30858457d9fee79`. Every marked type was also found in
the installed Editor's compiled assemblies. All **2,149 instance fields across
the 501 marked types** matched the source inventory by name and type, with
fixed-buffer storage checked separately. Metadata was inspected with Mono.Cecil;
no Unity process or runtime size/alignment measurement was run for this review.

The original CSV's SHA-256 remains
`711c65d35c56f8d63dfb9376d6e6e24549e46bfb85383aacb0fec8a755eaa317`.
The [review manifest](../../tmp/unity-layout-catalog-20261020/selection-review/review-summary.json)
records counts and input hashes. The
[metadata inventory](../../tmp/unity-layout-catalog-20261020/selection-review/editor-metadata.json)
records assembly paths, attributes, and fields.

The scanner now accepts `--min-instance-fields 2`, composable with
`--public-only --exclude-generics`. On the full source inventory those three
filters leave 585 unmanaged entries; 418 were marked in the user's CSV before
the additional review decisions. Output is retained under
`tmp/unity-layout-catalog-20261020/public-composite-structs-unity`.

```sh
python3 tmp/unity-layout-catalog-20261020/selection-review/review_selection.py
python3 tmp/unity-layout-catalog-20261020/selection-review/review_unmarked.py
dotnet test tests/UnityStructCatalog.Tests/UnityStructCatalog.Tests.csproj
```

All **20 CLI integration tests passed**, including the minimum-field filter,
backing fields, static/const exclusions, closed generic/private dependencies,
flag composition, and invalid counts. CSV reconciliation also checks every
retained row against the original marks, the structural filter, and metadata.

These are reviewed candidates. No layout constants or generator storage
implementation were added.
