# Variant-payload core validation

Core generation and the combined serialization Player probe for the
[approved redesign](../designs/2026-10-20-union-storage-redesign.md) were validated
on 2026-10-23. The implementation, legacy-fixture migration, rendered Inspector
gate, and final package verification are complete.

Final .NET results: 826 analyzer, 339 pattern-analysis, 589 generator, 73
compile-time behavioral, 94 tooling, 20 catalog-tool, and 11 global-configuration
tests pass (1,952 total). No skipped cases are counted. The benchmark project
builds successfully; no new throughput or copy benchmark claims are made.

Native Unity 6000.5.6f1 Linux validation also passes 16 Showcase EditMode tests
and 16 rendered Inspector cases with 147 checks and zero failures/unverified
cases. The latter includes repeated UI Toolkit and native IMGUI text input,
focus, copied previews, shared-node Undo/Redo with aliases/cycles, external
serialized edits, nested collections, prefab overrides, and multi-object editing.

The real generator-driver core suite has 53 passing tests. It covers mutable sibling
payloads, typed deconstruction, reference-slot reuse and Auto selection,
runtime-laid-out unmanaged helpers, generic fallbacks and constraints, nesting,
accessor naming, defaults, copies, and continued emission for diagnosed generic
ancestors. Emitted-IL checks reject boxing, unboxing, and runtime reference casts
in Typed nullable-slot transfers/accessors. Nullable annotations are merged from
Roslyn symbols; their erased runtime identity does not require object casts.

```sh
dotnet test tests/Houtamelo.Spire.SourceGenerators.Tests --no-restore \
  --filter FullyQualifiedName~VariantPayloadCoreTests
dotnet test tests/DevTools.Tests --no-restore \
  --filter FullyQualifiedName~ToolCliTests
```

CLI regression coverage passes 8 tests, including missing-Editor failure before
project creation and inappropriate backend-option rejection.

The isolated [Unity fixture](../../tests/UnityVariantPayloadCore/README.md) builds
the actual package compiler extensions into Linux x64 release players. Both
backends use High managed stripping. The Editor process exits 0; each backend's
build report records zero errors. The combined fixture build records two
SPIRE014 warnings in Inspector test setup; those are separate from Player checks.

| Unity | Backend | Runtime assertions | Failures | Debug build | Player exit |
|---|---|---:|---:|---|---:|
| 6000.5.6f1 | Mono | 39 | 0 | false | 0 |
| 6000.5.6f1 | IL2CPP | 39 | 0 | false | 0 |

Native assertions exercise Vector2/Vector3, concrete unmanaged structs without
generator size information, generic-dependent typed fields, managed structs in
Typed and Shared storage, simultaneous reference fields, object identity,
default Shared values, detached payload copies, deconstruction, nested unions,
shared nullable tuples, and actual serialized Resources reconstruction.
Editor Write, fresh-process Read, and Recovery phases pass 92, 53, and 59 checks.
These include direct helper callback isolation and fresh-process no-op saves
with exact YAML and managed-reference ID comparisons. The migrated behavioral
IL2CPP player passes 81 checks, including all 73 original facts and backend probes.

```sh
dotnet run -c Release --project tools/Spire.Tool -- \
  unity-union-core --timeout 1200
```

The fixture pins Unity's SDK 8.0.318 for IL2CPP tools. This installation also used
the existing task-local cp compatibility shim and native-library directory from
the earlier Unity probes. Their environment was supplied to the tool process:
`tmp/variant-layout-pattern-order-20261022/bin` precedes PATH and
`tmp/unity-il2cpp-native-libs` supplies LD_LIBRARY_PATH. These are host preparation,
not runtime package dependencies.

The latest combined native reports, build logs, and package/source hash
comparisons are archived under `tmp/union-redesign-results/phase4-final`.
Earlier `core-phase1/*.trx`, `cli/*.trx`, and isolated core reports remain
historical evidence. The tested native package snapshot hashes are:

| Assembly | SHA-256 |
|---|---|
| Houtamelo.Spire.Analyzers.dll | `b178a5caba12c452dfac421dc65b4aa49ad8e4504a93b0d4e548776c8bf8922a` |
| Houtamelo.Spire.dll | `52dbc4cefa170e9fa0a88008d4a2591cfecd585ad3b34f5dcc1b63237a79edb3` |

`final-export-package-hashes.json` records all four source/export/native-package
assembly hashes. The exact final export was rebuilt into both release Players,
which each pass 39 checks and exit 0. The exported Showcase preset matches the
validated authored asset, and UnityShowcase's manifest resolves to that export.

The same drawer-template bytes are embedded in the rendered-tested and final
analyzers. The last correction shares unmanaged classification with the benchmark
generator; its regression emits and loads nested managed-union storage and
checks factory/accessor behavior alongside an unmanaged control.

Unity can canonicalize malformed Resources while building a Player. Native
malformed-asset callback execution is therefore not claimed by those Player
results; Editor Recovery and the direct generated Player fallback are verified.
This boundary is separate from the passing rendered Inspector gate.

The first fixture attempt placed both backend executables in one directory.
IL2CPP's runtime files then prevented Mono startup. That attempt is not counted;
the table uses the corrected isolated backend directories. Later consumer/editor
changes require their own validation and final package checks.

## Private payload fields follow-up

The subsequent field-visibility change replaces generated public payload fields
with private `_`-prefixed serialized fields and getter-only public properties.
Payload structs are no longer partial; constructors and deconstruction use the
backing fields. The mutable public-field API described above is historical.

Follow-up .NET validation passes 596 generator, 73 behavioral, and 11 global
configuration tests (680 total). Native Unity validation passes 16 Showcase
EditMode tests, nine integration checks, Editor Write/Read/Recovery phases with
92/53/59 checks, and 39 checks in each Mono and IL2CPP player. The integration
probe explicitly verifies that Unity exposes the private backing field through
`SerializedProperty` and does not serialize its public getter.

The Inspector regression caught a queued duplicate attach refresh rebuilding
controls after typing began. A native call-stack trace identified
`OnDelayedAttachRefresh` as the caller. The drawer retains its synchronous attach
refresh and removes the queued duplicate; delayed callback registration remains.
The rendered checks cover repeated text input and focus alongside variant
changes, managed-reference Undo/Redo, nested collections, and prefab editing.

The clean export, after removing temporary tracing, passes all 16 Inspector cases
and 147 checks with zero failures or unverified cases; the Editor exits 0. Its
analyzer SHA-256 is
`f9f326fa8534a2af1a40384b6bea990f17e3ff20eab1d25f27e5e448dd44703f`.
All four source/export/native-fixture DLL hashes match. The exported preset
matches the authored asset, including the renamed private-field YAML keys, and
UnityShowcase resolves to this local export. Player checks covered the private
payload schema before the final Editor-only attach-callback correction.

## Hydration state overhead follow-up

On 2026-10-24, removed the per-instance hydration boolean. Serialization now
adds only `_serializedPayload` relative to an equivalent nonserializable union.
Players use that reference for one shared reconstructed marker per compilation;
Editor caches remain concrete for stable serialized output. Preparation replaces
the marker with an active payload wrapper. New native deserialization callbacks
still recover from invalid data rather than skipping recovery.

The regression suite compares generated instance fields and executes emitted
callbacks in both Player and Editor compilations. All 600 generator tests and
73 behavioral tests pass. Unity 6000.5.6f1 passes Write/Read/Recovery with
109/70/59 checks and Player preparation with 101 checks. Mono and IL2CPP release
players each pass 54 checks and exit 0, including cross-type marker reuse,
non-default values, nested hydration, copies, and invalid-payload recovery.

Inspector validation exposed a collection-edit issue: a leaf edit could change
the preview managed-reference ID without changing its kind or payload type.
Rebuilding the tree at that point discarded the edit before committing it. The
drawer now updates its existing preview ID for that event and preserves the
controls. No additional union field is introduced. The clean package passes all
16 Inspector cases and 147 checks with zero failures or unverified cases.

The final analyzer SHA-256 is
`0c6540b5dfed9a3de80ddec775a05d4c29057d2958f26e52de5d27d3ed14c7d3`.
The final Inspector correction affects only generated Editor support; the Player
checks above exercise the same hydration implementation.
