# Unity layout attributes and initial catalog candidates

The [optional-package survey](2026-10-21-unity-optional-package-catalog.md) extends
this reference-source inventory with 143 packages, including DOTS, and records
their versions and coverage gaps separately.

The source search provides useful catalog candidates, but checking only for a
written `StructLayout` attribute would miss several common unmanaged structs.
Color32 also demonstrates why all instance fields matter: its private overlapping
int makes its alignment 4, despite the public API exposing four bytes.

The reference source is pinned to Unity **6000.5.6f1**, commit
[`0e24f535c49ef35981d84b56f30858457d9fee79`](https://github.com/Unity-Technologies/UnityCsReference/tree/0e24f535c49ef35981d84b56f30858457d9fee79),
matching the installed Editor. The moving `6000.5` branch points at a later patch
and was not used for the inventory.

## Search results

Searching every C# file for `StructLayout` or `StructLayoutAttribute` constructor
syntax found 1,003 matching lines. One is a commented-out attribute. Excluding
that line leaves **1,002 occurrences in 515 files**:

| Layout | Occurrences |
|---|---:|
| Sequential | 972 |
| Explicit | 30 |

Of these, 22 specify `Size` and 15 specify `Pack`; those categories overlap.
There are 205 occurrences under Runtime, 608 under Modules, 150 under Editor,
and 39 under External. These are source-text counts, including conditional
branches and generated bindings, rather than a count of compiled public structs.
The complete inventory is preserved with file names and line numbers.

Several annotated types are classes, such as
[AnimationCurve](https://github.com/Unity-Technologies/UnityCsReference/blob/0e24f535c49ef35981d84b56f30858457d9fee79/Runtime/Export/Animation/AnimationCurve.bindings.cs#L147)
and
[ScriptableObject](https://github.com/Unity-Technologies/UnityCsReference/blob/0e24f535c49ef35981d84b56f30858457d9fee79/Runtime/Export/Scripting/ScriptableObject.bindings.cs#L14).
The attribute does not establish that a type is an unmanaged value type or that
its size is target-independent. Pointer-bearing generic containers such as
[NativeArray](https://github.com/Unity-Technologies/UnityCsReference/blob/0e24f535c49ef35981d84b56f30858457d9fee79/Runtime/Export/NativeArray/NativeArray.cs#L27)
are examples needing separate treatment.

## Measured candidates

The 23 candidates below were manually selected common Unity value types. They
were not the result of reviewing every attribute occurrence. The subsequent
[whole-source inventory](#whole-source-inventory) removes that selection step.

These 23 types were checked against the installed UnityEngine.CoreModule using
Unity 6000.5.6f1's **bundled Mono CLI on Linux x64**. All their instance fields
were inspected recursively for managed references. Size was measured with the
IL `sizeof` instruction; the alignment column is the measured field offset
after a byte prefix in a default-sequential generic wrapper. This avoids using
marshaling size as managed size. Measurements were executed automatically,
without hand-calculating composite sizes.

These are candidate catalog entries, not a finalized catalog. This survey did
not run an Editor session or Mono/IL2CPP player. The table does not establish
alignment on ARM, WebGL, or 32-bit targets.

| Unity type | Size | Observed alignment | Written layout attribute |
|---|---:|---:|---|
| Vector2 | 8 | 4 | Sequential |
| Vector3 | 12 | 4 | Sequential |
| Vector4 | 16 | 4 | None |
| Vector2Int | 8 | 4 | Sequential |
| Vector3Int | 12 | 4 | Sequential |
| Quaternion | 16 | 4 | Sequential |
| Color | 16 | 4 | Sequential |
| Color32 | 4 | 4 | Explicit |
| Rect | 16 | 4 | Sequential |
| RectInt | 16 | 4 | Sequential |
| Bounds | 24 | 4 | Sequential |
| BoundsInt | 24 | 4 | Sequential |
| Ray | 24 | 4 | None |
| Ray2D | 16 | 4 | None |
| Plane | 16 | 4 | Sequential |
| Matrix4x4 | 64 | 4 | Sequential |
| Hash128 | 16 | 8 | Sequential |
| Keyframe | 32 | 4 | None |
| GradientColorKey | 20 | 4 | None |
| GradientAlphaKey | 8 | 4 | None |
| BoneWeight | 32 | 4 | None |
| BoneWeight1 | 8 | 4 | None |
| Rendering.SphericalHarmonicsL2 | 108 | 4 | Sequential |

Names are under UnityEngine, except the last entry under UnityEngine.Rendering.
The following source details affect the catalog design:

- [Color32](https://github.com/Unity-Technologies/UnityCsReference/blob/0e24f535c49ef35981d84b56f30858457d9fee79/Runtime/Export/Math/Color32.cs#L16)
  overlaps a private int with its public color bytes. Unity's comment identifies
  enforcing 4-byte alignment for WebGL as its purpose. The Mono measurement also
  reports alignment 4. Treating it as four byte fields would miss this.
- [Vector4](https://github.com/Unity-Technologies/UnityCsReference/blob/0e24f535c49ef35981d84b56f30858457d9fee79/Runtime/Export/Math/Vector4.cs#L22),
  [Ray](https://github.com/Unity-Technologies/UnityCsReference/blob/0e24f535c49ef35981d84b56f30858457d9fee79/Runtime/Export/Geometry/Ray.cs#L12),
  [Ray2D](https://github.com/Unity-Technologies/UnityCsReference/blob/0e24f535c49ef35981d84b56f30858457d9fee79/Runtime/Export/Geometry/Ray2D.cs#L12),
  [Keyframe](https://github.com/Unity-Technologies/UnityCsReference/blob/0e24f535c49ef35981d84b56f30858457d9fee79/Runtime/Export/Animation/AnimationCurve.bindings.cs#L29),
  the [gradient keys](https://github.com/Unity-Technologies/UnityCsReference/blob/0e24f535c49ef35981d84b56f30858457d9fee79/Runtime/Export/Math/Gradient.bindings.cs#L14),
  and the [bone weights](https://github.com/Unity-Technologies/UnityCsReference/blob/0e24f535c49ef35981d84b56f30858457d9fee79/Runtime/Export/Graphics/Mesh.cs#L1837)
  rely on default struct layout. Bounds does have an explicit Sequential
  attribute.
- [Hash128](https://github.com/Unity-Technologies/UnityCsReference/blob/0e24f535c49ef35981d84b56f30858457d9fee79/Runtime/Export/Hashing/Hash128.bindings.cs#L18)
  stores two ulongs, not four uint fields. Its observed alignment is 8, so it
  should not be assigned the common four-byte alignment indiscriminately.
- The core math/geometry types provide a compact initial candidate list.
  Expanding to native binding records, rendering descriptors, or collections
  requires additional review of fields, packing, pointers, and build conditions.
  Public Unity.Mathematics package versions also need their own assembly and
  version scope; this inventory is of UnityCsReference.

## Primitive observations and a way to validate the catalog

The same Mono CLI probe measured these primitive sizes and alignments:

| Type | Size | Observed alignment |
|---|---:|---:|
| bool, byte, sbyte | 1 | 1 |
| char, short, ushort | 2 | 2 |
| int, uint, float | 4 | 4 |
| long, ulong, double | 8 | 8 |
| decimal | 16 | 8 |
| nint, nuint | 8 | 8 |

This remains an x64 observation. In particular, native integer sizes and target
alignment profiles must not be inferred from the machine running the generator.

Unity already exposes
[`UnsafeUtility.SizeOf<T>()`](https://docs.unity3d.com/6000.5/Documentation/ScriptReference/Unity.Collections.LowLevel.Unsafe.UnsafeUtility.SizeOf.html)
and
[`UnsafeUtility.AlignOf<T>()`](https://docs.unity3d.com/6000.5/Documentation/ScriptReference/Unity.Collections.LowLevel.Unsafe.UnsafeUtility.AlignOf.html).
Its [AlignOf implementation](https://github.com/Unity-Technologies/UnityCsReference/blob/0e24f535c49ef35981d84b56f30858457d9fee79/Runtime/Export/Unsafe/UnsafeUtility.cs#L121)
uses a sequential byte-plus-T helper and subtracts the payload size from the
helper size. These APIs are candidates for a Unity-side validation tool that
records or checks catalog entries across the supported player targets. They
were identified from source, not executed in this Mono CLI survey.

The generator can then use verified catalog entries and check type identity and
layout signatures. The remaining decisions are which entries to adopt initially,
which target profiles to support, and what to do when a signature or profile is
unrecognized. No general multi-field layout calculator is required by this
approach.

## Whole-source inventory

The [UnityStructCatalog tool](../../tools/UnityStructCatalog/Program.cs), added on
2026-10-21, scans every `.cs` file below the supplied source root, excluding
`.git`, `bin`, `obj`, and directory symlinks. It does not require a layout
attribute or restrict the search to known names. It merges partial declarations
and includes nested, generic, private, record, and ref-like structs.

Roslyn resolves instance-field types recursively, including private fields and
compiler-generated property, record, and event storage. Static and const fields
do not affect classification. The unmanaged partition also requires Roslyn's
`IsUnmanagedType` result; this is a type classification, not a size calculation.
The applicable language rules are described in Microsoft's
[unmanaged types reference](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/unmanaged-types).

Preprocessor directive lines are replaced with whitespace so declarations in
inactive branches are inventoried too. Fields from all alternatives are
considered together: a managed field in any branch excludes the type from the
unmanaged partition. Conflicting definitions or aliases, syntax errors,
unresolved dependencies, cycles, and unconstrained storage parameters are kept
in `unresolved.json`. This conservative source inventory does not establish
that all declarations compile together for a particular Unity target.

Running against the same pinned source with Unity's actual Linux Mono runtime
implementation assemblies, rather than the scanner host's framework, produced:

| Inventory | Count |
|---|---:|
| C# files | 7,319 |
| Struct declarations | 3,206 |
| Struct types after merging partial declarations | 3,107 |
| Unmanaged | 1,749 |
| Managed | 1,347 |
| Unresolved | 11 |
| Declarations not parsed or bound | 0 |

All 11 unresolved entries store generic parameters without an unmanaged
constraint. The unmanaged partition includes 66 generic and 24 ref-like types;
these flags are recorded separately. It contains 866 public, non-generic,
non-ref-like types across runtime, editor, and external source. These remain
catalog candidates requiring target/version and layout validation. All 23
previously measured candidates appear in the unmanaged partition.

The output includes all types and each partition as JSON, an
[unmanaged TSV](../../tmp/unity-layout-catalog-20261020/all-structs-unity/unmanaged.tsv),
and a [summary](../../tmp/unity-layout-catalog-20261020/all-structs-unity/summary.json)
with source hash and exact reference paths. JSON entries include source
locations, instance fields, layout attribute text, accessibility, generic/ref-like
flags, and the reason for exclusions.

```sh
dotnet run --project tools/UnityStructCatalog -- \
  --source tmp/unity-layout-catalog-20261020/UnityCsReference-0e24f535c49ef35981d84b56f30858457d9fee79 \
  --output tmp/unity-layout-catalog-20261020/all-structs-unity \
  --framework /home/houtamelo/Unity/Hub/Editor/6000.5.6f1/Editor/Data/MonoBleedingEdge/lib/mono/unityjit-linux \
  --reference /home/houtamelo/Unity/Hub/Editor/6000.5.6f1/Editor/Data/Managed
```

`--reference` accepts a DLL or a directory recursively and can be repeated.
`--framework` replaces the host framework; use runtime implementation assemblies
so private storage is available, rather than API-only reference assemblies.
Without it, the tool uses the current .NET host framework. No Unity process is
started by the scanner.

Add `--public-only --exclude-generics` to restrict every output partition to
public, non-generic types. Public accessibility must hold through every enclosing
type, and a non-generic struct nested in a generic type is excluded too. Filtering
happens after classification so private/internal or generic dependencies still
contribute to the result. `summary.json` records the flags and the original
`scannedStructs` count; its other type counts describe the filtered output.

Running with both flags and
`--output tmp/unity-layout-catalog-20261020/public-structs-unity` retains 1,202
types: **878 unmanaged**, 324 managed, and no unresolved entries. The
[filtered unmanaged TSV](../../tmp/unity-layout-catalog-20261020/public-structs-unity/unmanaged.tsv)
and [JSON](../../tmp/unity-layout-catalog-20261020/public-structs-unity/unmanaged.json)
contain no generic or non-public types. The 12 public, non-generic, ref-like
unmanaged types remain included and flagged.

`--min-instance-fields 2` additionally excludes empty and single-field structs,
counting private and generated instance storage while ignoring static/const
fields. Combined with the two filters above, this leaves 585 unmanaged types.
The [review of the user's marked candidates](2026-10-21-unity-selected-struct-catalog.md)
reconciles the annotated CSV with compiled metadata and records further
inclusion/defer decisions. The [unmarked-entry review](2026-10-21-unity-unmarked-struct-catalog.md)
adds 40 more candidates. The combined 373 types form the approved engine portion
of the [official initial target](../designs/2026-10-20-union-storage-redesign.md#official-initial-unity-target).
Accepted package additions bring the [canonical membership CSV](../designs/unity-initial-layout-targets.csv)
to 664 types.

The CLI integration suite runs synthetic source trees and a real external
assembly through the executable, including malformed inputs and conditional
branches, visibility/field-count filters, and nested generic types. All 20 tests passed:

```sh
dotnet test tests/UnityStructCatalog.Tests/UnityStructCatalog.Tests.csproj
```

## Reproduction and artifacts

- [Pinned source provenance and archive hash](../../tmp/unity-layout-catalog-20261020/source.json)
- [Inventory summary](../../tmp/unity-layout-catalog-20261020/inventory-summary.json)
- [All source hits, with paths and line numbers](../../tmp/unity-layout-catalog-20261020/structlayout-hits.json)
- [Measured sizes, embedding offsets, and instance fields](../../tmp/unity-layout-catalog-20261020/mono-measurements.json)
- [Managed measurement code](../../tmp/unity-layout-catalog-20261020/MeasureCatalog.cs)

```sh
python3 tmp/unity-layout-catalog-20261020/fetch_source.py
python3 tmp/unity-layout-catalog-20261020/inventory.py
python3 tmp/unity-layout-catalog-20261020/measure.py
```

The source archive is retained under ignored temporary research files. No
generator implementation or catalog entries were added to the package.
