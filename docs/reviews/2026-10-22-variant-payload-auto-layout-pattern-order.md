# Variant payload Auto layout and pattern evaluation order

Unity 6000.5.6f1 release Linux x64 Mono and IL2CPP players produced identical
results for default Auto layout and the kind-first pattern probes: Auto did not
compact the tested payload, and no inactive variant accessor was invoked. A
later reference-overlap probe also passed on both players, as recorded below.

This is an isolated representation/compiler probe. It does not implement or
replace the proposed union storage design.

Decision after this investigation: use Auto layout without specifying Pack for
generated per-variant unmanaged payload structs. Accept runtime padding and leave
bespoke packing to user-authored types. The [storage redesign draft](../designs/2026-10-20-union-storage-redesign.md#latest-decision-variant-payload-layout)
records this choice; generator-driven field reordering and packing searches are
outside this approach.

Design assumption accepted for the variant-payload discussion: rely on
left-to-right evaluation of the kind-first access patterns. Do not treat the
specification's unspecified ordering as a blocker or continue investigating it
unless a concrete problem appears. This is a project assumption, rather than a
change to the language specification.

## Auto layout

The probe measures managed `sizeof` and field addresses, without marshalling.

| Payload fields and layout | Mono bytes | IL2CPP bytes |
|---|---:|---:|
| Sequential: byte a, int b, byte c | 12 | 12 |
| Auto: byte a, int b, byte c | 12 | 12 |
| Sequential, reordered: int b, byte a, byte c | 8 | 8 |

The Auto payload retained offsets 0, 4, and 8. Both players also loaded an
explicit-layout helper overlaying this Auto payload with a long at offset zero.
That helper occupied 16 bytes; writing and reading its payload's int succeeded.

Auto therefore works in this tested overlay, but does not substitute for
padding-aware field ordering on these Unity backends. These measurements do not
establish behavior for every field combination, platform, or runtime.

The reordered layout preserves natural alignment: the int starts at offset 0,
and the two bytes start at offsets 4 and 5. A byte needs only one-byte alignment.
Offsets 6 and 7 are tail padding, making the 8-byte array stride preserve the
struct's four-byte alignment.

### Auto with Pack = 4

A follow-up on the same release Linux x64 player profiles measured:

| Field order | Layout | Mono size / offsets | IL2CPP size / offsets |
|---|---|---|---|
| byte, int, byte | Auto, Pack = 4 | 12 / 0, 4, 8 | 12 / 0, 4, 8 |
| byte, long, byte | Auto, default Pack | 24 / 0, 8, 16 | 24 / 0, 8, 16 |
| byte, long, byte | Auto, Pack = 4 | 16 / 0, 4, 12 | 24 / 0, 8, 16 |
| byte, long, byte | Sequential, Pack = 4 | 16 / 0, 4, 12 | 16 / 0, 4, 12 |

Pack = 4 does not change the original byte/int/byte layout. It does not request
field reordering. For Sequential layout, packing caps field alignment rather
than forcing every field onto that boundary: bytes still align to one byte.

The long-field control establishes a backend difference for Auto. Mono applies
the four-byte packing cap in this case; IL2CPP leaves the layout unchanged.
The generated C++ confirms the distinction: the Auto type has no packing
directive, while the Sequential control has `#pragma pack(push, tp, 4)` around
its declaration. Do not infer a common Auto packing policy across the backends.

Each player passed four layout assertions and a field write/read check. The
first probe incorrectly expected Mono's packed-long layout on both backends and
failed on IL2CPP. The final probe asserts the measured backend-specific outcomes
and includes the Sequential control. No Unity runtime code was changed.

- [Pack4 probe source](../../tmp/variant-layout-pattern-order-20261022/PackFourProbe.cs)
- [Pack4 Mono results](../../tmp/variant-layout-pattern-order-20261022/results/pack4-mono.json)
- [Pack4 IL2CPP results](../../tmp/variant-layout-pattern-order-20261022/results/pack4-il2cpp.json)
- [Pack4 runner](../../tmp/variant-layout-pattern-order-20261022/run-pack-four.mjs)

Both final Pack4 BuildReports recorded Succeeded, zero errors, and zero warnings.
The Editor again exited abnormally during shutdown, this time with confirmed
SIGSEGV. The runner preserves that failure instead of treating it as a clean
build-process exit. Both players were then run separately and exited zero.
An intermediate incremental compilation failed with CS2012/read-only output;
the directory was writable from the invoking process, and shutting down Unity's
shared compiler server allowed the next build to complete. The runner now shuts
down that server before a build. These environment/process issues are separate
from the measured player layouts.

## Pattern order

Nine assertions per backend cover single `is` patterns, switches, guards,
nested property patterns, logical alternatives, mismatched variants, and an
invalid discriminator. Each variant accessor casts shared object storage to a
different reference type, so invoking the wrong accessor would throw.

Every tested kind-first pattern read Kind before the selected variant accessor.
Mismatched and invalid cases read only Kind. No assertion failed and neither
player invoked an inactive variant accessor.

Eight additional diagnostic scenarios exercise captures, discards, guards,
logical patterns, reversed property ordering across arms, and shared reads.
These demonstrate omitted discard reads and reuse across arms. They do not
demonstrate swapping two fresh property reads within a single ordinary
kind-first property pattern. No such counterexample was reproduced.

The installed Unity Roslyn compiler reports `4.10.0-3.25064.8 (85262f5f)`.
Its [test-selection implementation](https://github.com/dotnet/roslyn/blob/85262f5f/src/Compilers/CSharp/Portable/Binder/DecisionDagBuilder.cs#L2400)
chooses the first test of the first possible matching case, with exceptions
that combine null checks with type/value tests on the same input.

The [language specification](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/language-specification/patterns#1126-property-pattern)
does not promise evaluation order. That language-level freedom is distinct from
an observed counterexample in this compiler; these experiments provide no such
counterexample for the proposed kind-first access pattern.

### Broader compiler/runtime check

An additional probe uses an actual readonly struct with a discriminator and
shared object payload storage. Its variant accessors deliberately cast to
incompatible reference types. Nine C# 8-compatible cases test single patterns,
switches, guards, nested property patterns, and mismatched/invalid kinds.
Newer compilers also run the original nine class-based assertions and eight
order diagnostic scenarios.

The matrix ran these seven toolchains, comprising six distinct Roslyn builds:

| Toolchain | Reported compiler version |
|---|---|
| Toolset 3.3.1 | 3.3.1-beta4-19462-11 (66a912c9) |
| Toolset 3.8.0 | 3.8.0-5.20562.2 (8de9e4b2) |
| Unity 6000.5.6f1 SDK | 4.10.0-3.25064.8 (85262f5f) |
| SDK 8.0.319 | 4.10.0-3.25064.8 (85262f5f) |
| SDK 10.0.203 | 5.3.0-2.26219.105 (c23858a6d860abe3ca94f9ac630c3e61b2625664) |
| SDK 10.0.401 | 5.9.0-1.26423.113 (e34a38d2ae1fc26406a317517196e55c68ff83ab) |
| SDK 11.0.100-preview.3.26207.106 | 5.7.0-1.26207.106 (a8b33e7593686eaee701cd124daaabff2311634f) |

Every toolchain compiled optimized and unoptimized assemblies. Each assembly
ran on Unity's bundled Mono CLI and CoreCLR 8.0.22, 10.0.7, 10.0.12, and
11.0.0-preview.3.26207.106. These are separate CLI runs, not additional Unity
Players. CoreCLR is varied as the runtime; Roslyn is varied as the compiler.

All 70 toolchain/optimization/runtime combinations completed successfully,
executing 1,170 kind-first case assertions. The nine struct cases retained the
same exact traces in every configuration: Kind alone for rejected variants,
Kind followed by the correct accessor for accepted variants. No inactive
accessor was called. This broadens the negative result; it does not prove that
every possible C# pattern follows source order.

The [2018 language-design resolution](https://github.com/dotnet/csharplang/issues/1054)
explains the unspecified-order decision: allow reordering and common-value reuse
while assuming pattern getters and deconstruction are pure/idempotent. It also
expresses caution about changing the generated order in future compilers.

- [Readonly struct matrix source](../../tmp/pattern-order-compiler-matrix-20261022/MatrixProbe.cs)
- [Compiler/runtime matrix results](../../tmp/pattern-order-compiler-matrix-20261022/results.json)
- [Matrix runner and exact compiler invocations](../../tmp/pattern-order-compiler-matrix-20261022/run.mjs)

## Overlapping reference fields

The following declaration compiled and ran on both release Linux x64 Players:

```cs
[StructLayout(LayoutKind.Explicit)]
public struct ManagedFields
{
    [FieldOffset(0)] public string text;
    [FieldOffset(0)] public int[] numbers;
}
```

Each Player passed 1,541 assertions covering correctly typed string/array reads,
replacement of one reference kind with the other, struct array copies, class
fields, null writes through either view, and value retention after forced
collections. The helper was nested under an internal nongeneric static class.
Both Player processes exited zero. This verifies the tested declaration and
access patterns; it is not a performance benchmark or validation of arbitrary
managed struct overlays.

The [CLI specification, II.10.7](https://ecma-international.org/wp-content/uploads/ECMA-335_6th_edition_june_2012.pdf#page=179)
allows one object reference to overlap another completely. Partial reference
overlap and overlap between reference bytes and non-reference bytes are not
allowed. Accessing overlapped references is unverifiable, even though this C#
declaration requires no `unsafe` keyword or compiler option. Correct reads must
use the view corresponding to the stored reference; a field read does not add
the runtime type check that an object-to-string cast would perform.

IL2CPP emitted a C++ union containing `String_t*` and `Int32[]*` views at the same
location. The typed reference accesses do not require object-to-target casts.
This suggests an alternative to shared `object?` storage for concrete reference
types. It does not establish a policy for generic-dependent fields or structs
containing both references and non-reference fields; those remain separate cases.
Final design decision after the inactive-view investigation: reject overlapping
managed references. Retain `ReferenceStorage.Typed` for direct typed access and
`ReferenceStorage.Shared` for compact `object?` slots with checked casts or
unboxing. Shared may box reference-containing structs. The successful overlap
probe is research evidence only, not a selected generation strategy.

Both BuildReports recorded Succeeded with zero errors and zero warnings. The
Editor again exited with SIGSEGV during shutdown after completing the builds.
The build runner reported that failure; both Players were then launched
separately and passed. No clean Editor-process exit is claimed.

- [Reference overlap source](../../tmp/variant-layout-pattern-order-20261022/ReferenceOverlapProbe.cs)
- [Mono results](../../tmp/variant-layout-pattern-order-20261022/results/ref-overlap-mono.json)
- [IL2CPP results](../../tmp/variant-layout-pattern-order-20261022/results/ref-overlap-il2cpp.json)
- [Reference overlap runner](../../tmp/variant-layout-pattern-order-20261022/run-reference-overlap.mjs)

Re-run the built Players with:

```sh
node tmp/variant-layout-pattern-order-20261022/run-reference-overlap.mjs --players-only
```

Omit `--players-only` to rebuild both Players. Inspect the BuildReports and
Editor shutdown result separately if the build-process exit is abnormal.

### Reading an inactive reference view

A follow-up deliberately stored `new string('A', 64)` and read the overlapping
`int[]` field. On both release Unity 6000.5.6f1 Linux x64 Players:

```text
Actual runtime type=System.String
Correct string length=64
Wrong array length=4259905
```

The field read copied the same reference without throwing InvalidCastException.
Reading `Length` then interpreted the string's memory using the array layout.
The observed number is specific to these tested layouts and contents, not a
guaranteed result of inactive access. The [CLI's ldlen correctness requirement,
III.4.12](https://ecma-international.org/wp-content/uploads/ECMA-335_6th_edition_june_2012.pdf#page=434)
requires an actual array or null. An inactive view violates that requirement;
further dereferences can return invalid data or crash, and writes can corrupt
memory. No out-of-bounds read, indexing, or write through the wrong view was
intentionally attempted by this probe. Its Length read stayed within the
allocated 64-character string's storage on the measured targets.

Both Player processes exited zero. Both BuildReports recorded Succeeded with
zero errors and zero warnings; the Editor again exited with SIGSEGV during
shutdown afterward. The runner preserved the abnormal build-process result,
and the Players were launched separately.

- [Inactive-view source](../../tmp/variant-layout-pattern-order-20261022/WrongReferenceViewProbe.cs)
- [Mono observation](../../tmp/variant-layout-pattern-order-20261022/results/wrong-ref-view-mono.json)
- [IL2CPP observation](../../tmp/variant-layout-pattern-order-20261022/results/wrong-ref-view-il2cpp.json)
- [Inactive-view runner](../../tmp/variant-layout-pattern-order-20261022/run-wrong-reference-view.mjs)

Re-run with `node tmp/variant-layout-pattern-order-20261022/run-wrong-reference-view.mjs --players-only`;
omit `--players-only` to rebuild both Players.

## Reusing public payload structs for serialization

Unity's [field serialization rules](https://docs.unity3d.com/6000.0/Documentation/Manual/script-serialization-rules.html)
support serializable structs with mutable serializable fields, but exclude readonly
fields. Its [SerializeReference rules](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/SerializeReference.html)
exclude struct values, including values boxed into an object field. Consequently,
the current managed-reference payload cannot directly hold a public payload struct.

A shared generic class wrapper is viable on the measured Editor:

```cs
[Serializable]
public sealed class ReusePayload<T> : ReusePayloadBase
{
    public T value;
}
```

One ScriptableObject payload field was declared as
`[SerializeReference] public ReusePayloadBase payload`. Two assets used
`ReusePayload<ReuseCircle>` and `ReusePayload<ReuseRectangle>`. Circle contained a
float and a SerializeReference class field; Rectangle contained two floats.
The structs had public mutable fields and the Circle struct had Deconstruct.
Unity [introduced support for generic instantiated SerializeReference values in
2023.1.0a20](https://unity.com/releases/editor/alpha/2023.1.0a20).

On Unity 6000.5.6f1 Linux Editor Mono, the write phase passed six assertions:
the wrapper appeared as a managed reference, its struct value and fields were
available through SerializedProperty, a nested managed-reference field was
available, and editing the float applied to the live object. Both native assets
were saved. A fresh Editor process then passed nine assertions confirming the
two closed wrapper types, all field values, the edited float, the nested reference,
Deconstruct results, and the reloaded property paths.

The Circle asset YAML contained one generic wrapper record with an inline
`value.radius: 2.5` and a separate nested effect managed-reference record. Rectangle
contained its own wrapper specialization and only the rectangle values. No
per-variant wrapper subclasses were declared. This reduces source type definitions;
the distinct closed generic runtime wrapper types and the active snapshot
allocation still exist.

Both phases wrote their completed success reports, then each Editor process
exited with SIGSEGV during shutdown, as in earlier probes. The runner returned
nonzero and preserved both abnormal exits. The fresh-process read independently
verified the saved data; no clean Editor exit or Player/IL2CPP validation is
claimed. This prototype does not test generated union callbacks, rename aliases,
or the full existing Inspector workflow.

- [Serializable structs and shared wrapper](../../tmp/variant-layout-pattern-order-20261022/project/Assets/PayloadReuseProbeAsset.cs)
- [Native asset write/read assertions](../../tmp/variant-layout-pattern-order-20261022/project/Assets/Editor/PayloadReuseProbe.cs)
- [Write results](../../tmp/variant-layout-pattern-order-20261022/results/payload-reuse-write.json)
- [Fresh-process read results](../../tmp/variant-layout-pattern-order-20261022/results/payload-reuse-read.json)
- [Saved Circle asset](../../tmp/variant-layout-pattern-order-20261022/project/Assets/PayloadReuseCircle.asset)
- [Saved Rectangle asset](../../tmp/variant-layout-pattern-order-20261022/project/Assets/PayloadReuseRectangle.asset)
- [Runner](../../tmp/variant-layout-pattern-order-20261022/run-payload-reuse.mjs)

Re-run with `node tmp/variant-layout-pattern-order-20261022/run-payload-reuse.mjs`.

## Native Inspector data flow

A handwritten union prototype tested the proposed minimal data flow on Unity
6000.5.6f1 Linux Editor Mono. It contained a serialized kind, one SerializeReference
generic wrapper, and nonserialized runtime float slots. OnBeforeSerialize refreshed
the snapshot from runtime values only when changed. OnAfterDeserialize reconstructed
runtime slots and defaulted mismatched kind/payload pairs to the selected variant.
The editor driver used native SerializedObject/SerializedProperty edits and Unity
Undo operations, with no generated drawer, custom undo history, preview host, or
explicit invocation of serialization callbacks or reconstruction methods.

Fourteen assertions completed. Observed states:

| Operation | Native result |
|---|---|
| Edit Circle radius | Runtime value changed from 3 to 9 after ApplyModifiedProperties. |
| Undo / redo radius | Runtime value restored to 3, then 9. |
| Change serialized kind to Rectangle | Callback created the corresponding wrapper and runtime value defaulted to 0. |
| Undo / redo kind | Circle radius 9 restored, then Rectangle value 0 restored. |
| Edit nested inline union and array element | Their runtime values became 10 and 11. |
| Edit one of two unions sharing a cached wrapper | Both runtime radii changed from 7 to 12; wrapper identity remained shared. |

Callback counters confirmed that native Apply and Undo operations invoked
OnAfterDeserialize. The default selected wrapper was created inside the callback
using managed operations; no Unity API was called from that callback. The alias
observation demonstrates a missing cache-isolation policy, not desired value-union
semantics. It is distinct from intentional reference sharing in user payload data.

The [ApplyModifiedProperties documentation](https://docs.unity3d.com/6000.5/Documentation/ScriptReference/SerializedObject.ApplyModifiedProperties.html)
describes recording undo before loading serialized content into each target.
The [callback documentation](https://docs.unity3d.com/6000.5/Documentation/ScriptReference/ISerializationCallbackReceiver.html)
defines reconstruction after deserialization and notes threading and managed-reference
callback ordering constraints. The observed simple paths support the minimal
approach; this prototype does not establish every nested callback order or rendered
Inspector behavior. Focus, prefab overrides, multi-object editing, and actual
UI Toolkit controls were not exercised here.

The probe wrote its completed report and then the Editor exited with SIGSEGV
during shutdown, matching earlier observations. The runner returned nonzero and
preserved the abnormal exit; no clean Editor process result is claimed.

- [Union and host model](../../tmp/variant-layout-pattern-order-20261022/project/Assets/NativeDrawerFlowProbeAsset.cs)
- [Native edit and undo assertions](../../tmp/variant-layout-pattern-order-20261022/project/Assets/Editor/NativeDrawerFlowProbe.cs)
- [Observed values and callback counters](../../tmp/variant-layout-pattern-order-20261022/results/native-drawer-flow.json)
- [Runner](../../tmp/variant-layout-pattern-order-20261022/run-native-drawer-flow.mjs)

Re-run with `node tmp/variant-layout-pattern-order-20261022/run-native-drawer-flow.mjs`.

## Fresh serialization snapshots

A follow-up enabled an AlwaysFresh flag in the handwritten native-flow union.
OnBeforeSerialize always constructed a new active wrapper from the runtime values,
instead of retaining a matching cached wrapper. The existing native-flow probe
keeps this flag disabled by default, preserving its original alias experiment.

On Unity 6000.5.6f1 Linux Editor Mono, 15 assertions completed. Direct payload
edits, native undo/redo of values and kinds, nested inline reconstruction, and
array-element reconstruction retained their earlier successful results. Native
serialization produced distinct wrapper identities for the two copied unions.
Editing the left radius from 7 to 12 left the right runtime radius at 7.

This prototype shows that fresh serialization snapshots can address the observed
cache alias without a drawer-owned undo history or per-control editing handlers.
It does not measure allocation cost or exercise rendered focus, lists, prefab
overrides, arbitrary nested callback graphs, multi-object editing, or user-managed
reference identity. The additional wrapper allocation on every OnBeforeSerialize
is a known representation tradeoff, not a measured timing result.

Unity's [6000.5.6f1 PropertyField source](https://github.com/Unity-Technologies/UnityCsReference/blob/6000.5.6f1/Editor/Mono/UIElements/Controls/PropertyField.cs#L218)
compares managed-reference type names when deciding whether its standard child
field can be reused. This is a reason to investigate native control reuse when
wrapper identity changes but type does not; it is not evidence that this
prototype exercised keyboard focus.

The probe wrote its completed report, then the Editor again exited with SIGSEGV
during shutdown. The runner returned nonzero and preserved that abnormal exit.
No clean Editor process result or Player/IL2CPP validation is claimed.

- [Union model and AlwaysFresh flag](../../tmp/variant-layout-pattern-order-20261022/project/Assets/NativeDrawerFlowProbeAsset.cs)
- [Fresh-snapshot native assertions](../../tmp/variant-layout-pattern-order-20261022/project/Assets/Editor/NativeFreshSnapshotProbe.cs)
- [Observed values](../../tmp/variant-layout-pattern-order-20261022/results/native-fresh-snapshot.json)
- [Runner](../../tmp/variant-layout-pattern-order-20261022/run-native-fresh-snapshot.mjs)

Re-run with `node tmp/variant-layout-pattern-order-20261022/run-native-fresh-snapshot.mjs`.

## Unchanged save stability

The fresh-wrapper proposal was challenged because new managed-reference identities
could create version-control noise without a logical edit. A native asset probe
compared the same union model with AlwaysFresh enabled versus matching-wrapper
reuse on Unity 6000.5.6f1 Linux Editor Mono. Each asset contained one union with a
Circle radius of 3. A second Editor process reopened the saved assets and repeated
the save scenarios. Native inspection used SerializedObject.Update and an unchanged
ApplyModifiedProperties; this did not exercise a rendered Inspector window.

| Operation, with no logical value changes | Cached matching wrapper | Fresh wrapper on every callback |
|---|---|---|
| Load asset and SaveAssets without marking dirty | Identical YAML | Identical YAML |
| Native property inspection and SaveAssets | Identical YAML; not dirty | Identical YAML; not dirty |
| SetDirty and SaveAssetIfDirty | Identical YAML | Wrapper rid changed in two YAML locations |
| ForceReserializeAssets | Identical YAML | Wrapper rid changed again |

Both processes showed the same outcome. The first fresh-wrapper unchanged dirty
save changed the ID from 1321628885659156481 to 1321628885659156484; the wrapper's
type and radius remained identical. Forced reserialization changed it to
1321628885659156485. After reopening, a further unchanged dirty save changed that
ID to 1321628885662040066. The cached wrapper retained 1321628885659156480 through
all saves and the process boundary.

Unity documents that [SerializeReference assigns an ID to each managed reference](https://docs.unity3d.com/6000.5/Documentation/ScriptReference/SerializeReference.html)
and that [ForceReserializeAssets writes assets without needing a manual dirty change](https://docs.unity3d.com/6000.5/Documentation/ScriptReference/AssetDatabase.ForceReserializeAssets.html).
These observations confirm serialized identity churn when a fresh wrapper is
written. They do not show that merely inspecting an asset marks it dirty: that
path remained clean in this probe.

Unconditional wrapper replacement is withdrawn as the recommended isolation
mechanism. A replacement must address copied-cache aliases while preserving
unchanged serialized output. Cache reuse by itself still fails the earlier alias
test; no alternative mechanism is validated by this save-stability experiment.

The write phase completed six assertions and the reopen phase eight, with twenty
YAML snapshots retained. Both Editor processes then hit the previously observed
SIGSEGV during shutdown. The runner preserved both signals and returned nonzero;
these are completed probe observations, not clean Editor process results.

- [Asset host](../../tmp/variant-layout-pattern-order-20261022/project/Assets/SaveStabilityProbeAsset.cs)
- [Native save and inspection probe](../../tmp/variant-layout-pattern-order-20261022/project/Assets/Editor/SaveStabilityProbe.cs)
- [Write observations](../../tmp/variant-layout-pattern-order-20261022/results/save-stability/write.json)
- [Reopen observations](../../tmp/variant-layout-pattern-order-20261022/results/save-stability/reopen.json)
- [Fresh wrapper before unchanged dirty save](../../tmp/variant-layout-pattern-order-20261022/results/save-stability/fresh-write-inspect-and-save.yaml)
- [Fresh wrapper after unchanged dirty save](../../tmp/variant-layout-pattern-order-20261022/results/save-stability/fresh-write-dirty-without-value-edit.yaml)
- [Runner](../../tmp/variant-layout-pattern-order-20261022/run-save-stability.mjs)
- [Process exits](../../tmp/variant-layout-pattern-order-20261022/save-stability-processes.json)

Re-run with `node tmp/variant-layout-pattern-order-20261022/run-save-stability.mjs`.

## Artifacts and reproduction

- [Payload and overlay source](../../tmp/variant-layout-pattern-order-20261022/AutoLayoutProbe.cs)
- [Nine kind-first assertions](../../tmp/variant-layout-pattern-order-20261022/KindFirstProbe.cs)
- [Additional order diagnostics](../../tmp/variant-layout-pattern-order-20261022/OrderProbe.cs)
- [Mono player results](../../tmp/variant-layout-pattern-order-20261022/results/mono.json)
- [IL2CPP player results](../../tmp/variant-layout-pattern-order-20261022/results/il2cpp.json)
- [Build runner](../../tmp/variant-layout-pattern-order-20261022/run-unity.mjs)

Both BuildReports recorded Succeeded with zero errors and warnings. Players use
nondevelopment builds, Low managed stripping, and IL2CPP Release/OptimizeSpeed.
The Editor ended with a signalled exit during shutdown after completing both
builds; its exit code was null, and the original runner did not retain the signal
name. This was not a clean Editor exit. Each player was then run separately and
exited zero with the expected backend/version metadata and its own result file.

The build runner scopes a cp shim to this probe to avoid an environment-specific
ACL preservation failure. The shim preserves ordinary permission bits without
copying source ACLs. It does not modify the Unity installation.

Re-run the existing players:

```sh
node tmp/variant-layout-pattern-order-20261022/run-unity.mjs --players-only
```

Omit `--players-only` to rebuild both players. A nonzero or signalled Editor exit
is reported as failure; inspect the BuildReports and shutdown log separately.
