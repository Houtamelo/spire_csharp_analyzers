# Unity 6000

The `unity-6000` branch targets Unity 6000.5 with Roslyn 4.10. Native validation
covers Editor Mono and Linux IL2CPP runtime tests on 6000.5.6f1; player
serialization and stripping coverage are detailed below. The normal NuGet build
remains on `main`. Other Unity versions and player platforms require separate
validation. Rider is the supported IDE.

All build and acceptance workflows use the C# CLI in `tools/Spire.Tool`. It
requires the .NET 10 SDK for preparation; using the built package requires only
Unity. Run `dotnet run -c Release --project tools/Spire.Tool -- --help` for commands
and options. `Spire.Tool.Core` targets .NET Standard 2.1 so the same preparation
contracts can be tested in Editor Mono. The SDK, installed Editor, and Rider/JDK
requirements of their respective workflows remain unchanged.

On Linux, Unity commands automatically start an isolated `Xvfb` display and stop
it when Unity exits. Install `Xvfb` before running these workflows. Diagnostics
are written beside each Unity log as `*.xvfb.log`.

## Install

From this branch, build the local Unity package:

```sh
dotnet run -c Release --project tools/Spire.Tool -- package
```

In Unity's Package Manager, choose **Add package from disk** and select
`tmp/unity-package/com.houtamelo.spire/package.json`.

The package contains the runtime DLL and compiler-only Spire analyzer,
PatternAnalysis, and CodeFixes DLLs. Compiler-only assets have the
`RoslynAnalyzer` label and all plugin platforms disabled. Unity and Rider supply
their compiler assemblies; the package does not contain Roslyn, Workspaces, or
System.Collections.Immutable DLLs.

The runtime DLL uses Auto Reference. Assemblies with Override References enabled
must include `Houtamelo.Spire.dll` explicitly. Analyzer scope is separate from
runtime DLL references; verify that every intended consuming asmdef receives
the analyzer. Code fixes execute in Rider, while Unity executes analyzers and
source generators.

The assembly-definition acceptance fixture covers predefined assemblies,
runtime/editor asmdefs, explicit DLL references, disabled auto references, and
direct/transitive assembly dependencies:

```sh
dotnet run -c Release --project tools/Spire.Tool -- unity-asmdefs --timeout 900
```

It asserts generated APIs and actual compiler diagnostics in eight assemblies,
then exercises an analyzer error, a missing runtime DLL reference, and repair
of both. The report also checks Unity's compiler response files and the real
Rider-generated solution for all three Spire compiler-extension DLLs.

Inspect the generated solution with the matching JetBrains command-line tools:

```sh
dotnet tool install JetBrains.ReSharper.GlobalTools --tool-path tmp/jetbrains-cli --version 2026.2.3.1
dotnet run -c Release --project tools/Spire.Tool -- jetbrains --stage inspect
```

The acceptance check requires the expected `SPIRE002` diagnostics in all eight
assemblies and the `SPIRE016` quick-fix target in the generated project. It saves
the original SARIF output and inspection log under `tmp/unity-asmdefs`.

Run the complete IDE acceptance check after preparing the asmdef fixture:

```sh
dotnet run -c Release --project tools/Spire.Tool -- jetbrains --stage all --timeout 900
```

This requires a licensed Rider installation, its bundled JDK, and `Xvfb` on
Linux. `--rider` and `--editor` select other installation paths. InspectCode's
version must match Rider's. The driver uses an isolated Rider profile and
virtual display, opens the flagged source, observes Rider's actual quick-fix
popup, and selects **Replace with SpireEnum<T>.From(value)**. It requires the
exact saved source change, then recompiles all eight assemblies in Unity and
executes valid/invalid conversion probes. It preserves source snapshots,
hashes, IDE logs, SARIF, and `jetbrains-integration-results.json`.

Re-run the `unity-asmdefs` command before repeating the complete check; preparation
restores the unfixed source. Its disposable project pins SDK 8 to match Unity's
Rider toolset instead of inheriting this repository's SDK selection. This
acceptance test covers one representative IDE quick fix; the existing provider
tests cover the remaining transformations.

Keep Rider's **Editor → Inspection Settings → Roslyn Analyzers** enabled; disabling
it also disables source generators. Rider discovers analyzer DLL references and
supports external analyzer quick fixes. See
[Rider's Roslyn analyzer documentation](https://www.jetbrains.com/help/rider/Using_NET_Compiler_Analyzers.html).

## Examples and tracked projects

[UnityShowcase](../examples/UnityShowcase/README.md) is a complete tracked Unity
project with a saved scene and preset asset. Build the local package, then open
`examples/UnityShowcase` in Unity Hub. It demonstrates union authoring, all five
layouts, generic results, managed-reference fields, rename aliases, enum checks,
LINQ filtering, and generated inliners. Its EditMode tests include native asset
persistence and a Play Mode smoke test of the scene.

The existing projects under `tests/UnityIntegration`, `tests/UnityBehavioral`, and
`tests/UnityAsmdefIntegration` are tracked test fixtures. Their CLI runners copy
the fixture contents into disposable projects under `tmp/`, add the built package
and any test dependencies, and run there. The showcase can instead be opened and
tested directly; its authored assets and settings are retained in Git.

## Serialize a union

Add `System.Serializable` to the union:

```cs
using System;
using Houtamelo.Spire;
using UnityEngine;

[Serializable]
[DiscriminatedUnion]
public partial struct Result
{
    [Variant] public static partial Result Empty();
    [Variant] public static partial Result Success(int value);
    [Variant] public static partial Result Failure(string message);
}

public sealed class SpireExample : MonoBehaviour
{
    [SerializeField] private Result result = Result.Success(42);
}
```

Omitted/Auto layout selects Additive, overriding a global default. Explicit
`Layout.BoxedFields`, `Layout.BoxedTuple`, `Layout.UnsafeOverlap`, and
`Layout.Overlap` also support serialization. `UnsafeOverlap` requires Unity's
**Allow 'unsafe' Code** setting, or `allowUnsafeCode` in the consuming asmdef.
`Overlap` cannot be generic or nested in a generic containing type. Readonly/ref
structs, records, and record structs are outside this serialization mode.

Unity stores one typed payload for the active variant. Its concrete type identifies
the variant; the runtime `Kind` enum and runtime storage are excluded from
serialization. Generated callbacks rebuild
the union through its factories, and a generated editor drawer provides a
variant selector and active fields in UI Toolkit and IMGUI.
Every generated payload field carries an explicit `[SerializeField]` attribute;
Unity determines whether its type can be serialized. Spire does not diagnose
unsupported payload field types or closed generic arguments.

To preserve nulls, derived types, or shared references in a variant field, annotate
its parameter with Spire's `[UnitySerializeReference]`:

```cs
[Variant]
public static partial Result Node([UnitySerializeReference] NodeBase? value);
```

The corresponding current and former-name payload fields also receive Unity's
`[SerializeReference]`. This marker targets parameters and has no effect outside
Unity serialization. Arrays and lists apply managed-reference serialization to
their elements; their containers are still serialized by value. Reference sharing
is scoped to one Unity host object. Unity determines which managed types are
eligible; the marker does not make unsupported types serializable.

Nested helpers use Unity's own field attributes. Private `[SerializeReference]`
fields are traversed, and managed-reference graphs retain object identity while
Spire prepares or restores nested unions. Spire does not invoke helper callbacks.
Adding or removing this marker changes the field's serialized representation.
Existing inline data is not automatically migrated to managed-reference data;
variant rename aliases do not migrate field encodings.

The stored payload schema is independent of the runtime layout. Serialization
adds one instance reference, `_serializedPayload`, to each union. There is no
separate owner token or state flag. `Overlap` keeps its runtime field offsets and
appends that reference. A serializable union is therefore no longer unmanaged,
even when all its variant fields are unmanaged. Raw runtime bytes are never
persisted.

Variant changes initialize the new variant's fields to their defaults. Null,
missing, or unrecognized payloads default to the first declared variant and emit
`invalid union serialization data, defaulting to FirstVariantName` during
deserialization. Drawer refresh does not repeat this warning.
Unity's native normalization still applies, including null strings becoming
empty strings and null inline helper objects becoming initialized objects. A
managed-reference field preserves null instead.
The persisted representation can therefore differ from a fresh runtime value.

Fields must satisfy Unity's native serialization rules. Supported shapes include
primitive values, enums up to 32 bits, Unity values/object references, concrete
serializable classes/structs, and one-dimensional arrays or `List<T>`. Other field
types are still emitted into the typed payload with `[SerializeField]`; this does
not make them serializable. Unity may omit unsupported fields. The same applies
to generic arguments and fields inside helper types. Verify persistence in Unity
for the types your project uses. Callback-bearing helpers retain Unity's callback
behavior; the generator does not reject them based on field-type policy.

Generated public properties retain the existing compiler-profile behavior:
`init` accessors when `IsExternalInit` is available, otherwise getter-only access.
Unity's default profile needs no consumer polyfill for generated code.

Variant reordering does not change the meaning of saved payloads. To rename a
variant, give its declaration one previous name:

```cs
[Variant(FormerlySerializedAs = "Ok")]
public static partial Result Success(int value);
```

The generator recognizes the old payload type and restores `Success`. The next
save writes the current payload type. Only one previous name is supported; resave
affected assets before another rename replaces that alias. An ambiguous or invalid
alias produces `SPIRE_DU017`. This handles variant names, not field names/types or
union, containing-type, namespace, or assembly renames. Those changes can still
require migration.

Editor caches, including Play Mode caches, are retained. Serialization reuses an
unchanged snapshot and replaces a changed snapshot without mutating the prior
one. Runtime storage writes do not invalidate the cache; comparison happens at
serialization time. Ordinary construction adds no per-value serialization allocation.

Player reconstruction releases the payload graph and puts a shared empty marker
in the same reference field. The marker distinguishes reconstructed values from
missing serialized data, so repeated nested hydration preserves runtime values.
The next `OnBeforeSerialize` replaces it with a concrete payload. Runtime
`Instantiate` serializes its source, which can retain a reusable cache while its
deserialized clones release their graphs. Serialization events may allocate and
prepare nested state; measure costs for frequently serialized large unions.

Generated public/internal implementation members, including callbacks, carry
`EditorBrowsable(Never)`. This is an IDE hint. The current
[Rider completion probe](issues/rider-editorbrowsable-generated-members.md) was
inconclusive; the attribute does not guarantee that every IDE hides these members.

## Validate

Use the installed Editor executable:

```sh
SPIRE_UNITY_EDITOR=/path/to/Editor/Unity dotnet run -c Release --project tools/Spire.Tool -- unity-integration --graphics
```

The runner copies the tracked fixture to `tmp/unity-integration`, builds the
package, and runs real Editor tests. It preserves JSON results and logs in that
directory. The graphics run exercises rendered IMGUI/UI Toolkit drawers,
Undo/Redo, prefab overrides, script reload, and Play Mode. Without `--graphics`,
graphics-dependent checks are reported as observations rather than passes.

Build the Mono player fixture separately:

```sh
dotnet run -c Release --project tools/Spire.Tool -- unity-integration --method Spire.UnityIntegration.SpireUnityIntegration.BuildPlayer
```

The build report contains the executable path and runtime report argument. Run
that executable with `-batchmode -nographics -spire-report <absolute-report-path>`
to verify the serialized union in the player. Compiler-only Spire DLLs must be
absent from its managed assemblies.

Build and run the two IL2CPP serialization profiles:

```sh
dotnet run -c Release --project tools/Spire.Tool -- unity-integration --project tmp/unity-il2cpp-main --method Spire.UnityIntegration.SpireUnityIntegration.BuildIl2CppPlayerMainBaselineAndManaged --timeout 1200
dotnet run -c Release --project tools/Spire.Tool -- unity-integration --project tmp/unity-il2cpp-stability --method Spire.UnityIntegration.SpireUnityIntegration.BuildIl2CppPlayerStabilityBaselineAndManaged --timeout 1200
```

Both entrypoints select IL2CPP and **High** managed stripping. The main profile
checks all layouts and nested/generic unions; the stability profile checks
former-name payload reconstruction and cache lifetime. Both include the real
managed-reference graph checks. Run each executable reported by
`unity-player-build-report.json` with the runtime report argument above. Each
player requires its expected hosts and assertions, reports its actual backend
and profile, and exits with a failure code on a mismatch. The fixture rejects
`Assets/**/link.xml` so test preservation cannot mask production stripping
failures.

The profiles remain useful focused checks. The complete fixture also passes as
one IL2CPP/High player on Unity 6000.5.6f1, Linux, after correcting
`StabilityPlayerHost`'s script registration. It now lives in its own matching
source file; builds reject an embedded script reference. The
[native-loading investigation](issues/unity-il2cpp-opaque-managed-reference-slots.md)
records the Unity-only MRP and the frozen-scene comparison. A successful build
alone does not establish a passing player. To build the complete fixture:

```sh
dotnet run -c Release --project tools/Spire.Tool -- unity-integration --project tmp/unity-il2cpp-full --method Spire.UnityIntegration.SpireUnityIntegration.BuildIl2CppPlayer --timeout 1200
```

Player fixture builds request `BuildOptions.CleanBuildCache`. Fresh project
comparisons are important because repeated builds of the bad script registration
did not always produce the same native loading result.

The initial High-stripping player crashed in Unity's native managed-reference
type resolver. Native debugging and an isolated one-method linker descriptor
established that Unity stripped `System.Type.GetType(string)`, which its native
loader uses for generic payload types. Spire's runtime DLL now includes a small
preserved method that retains that dependency. The successful final fixture
uses the packaged DLL alone, without a project linker descriptor or whole-assembly
preservation. Consumers still enable serialization with `[Serializable]` only.

The Roslyn test suite also compiles generated code against installed Unity
assemblies. It does not substitute Unity types or prove native persistence by
itself. The design and feasibility evidence are recorded in
[the Unity design](designs/2026-10-06-unity-6000.md).

Run the existing runtime behavioral tests inside the Unity Editor:

```sh
dotnet run -c Release --project tools/Spire.Tool -- unity-behavioral
```

This runner copies the original union declarations and all 129 xUnit test
methods unchanged into `tmp/unity-behavioral`. Unity's compiler runs the packaged
generator; a Unity Test Framework adapter executes each original assertion in
Editor Mono and writes `behavioral-results.xml`. Input hashes and the expected
test count prevent a partial run from passing unnoticed.

The test assembly enables C# 12 and supplies a test-only `IsExternalInit`
polyfill because these existing tests use newer syntax and records. The normal
consumer C# 9 profile remains covered separately. xUnit and JSON dependencies
are test assets, outside the shipped Spire package.

Run the same original assertions in an IL2CPP Linux player:

```sh
dotnet run -c Release --project tools/Spire.Tool -- unity-behavioral --backend IL2CPP --timeout 1200
```

Install the matching Linux IL2CPP build module and Unity's required native build
dependencies first. Preparation uses a separate `tmp/unity-il2cpp-behavioral`
project, enables its test dependencies for the player, and pins SDK 8 for Unity's
build-tool environment. The C# preparation CLI itself still requires SDK 10.
`--editor` selects an Editor installation containing the module.

Results are written to `il2cpp-behavioral-results.xml`. The runner requires every
original case identity, zero failures or skips, and the IL2CPP root-domain marker.
All 129 original assertions pass, plus six adapter checks and two backend
capability probes: 137 checks total. This covers all five layouts, records,
generics, both JSON converters, and closure inliners. Test-specific linker roots
preserve the test and dependency assemblies for the reflection-based adapter;
this run alone does not establish production stripping compatibility.

The full compiler/test harness below remains in Editor Mono. Actual IL2CPP
probes reject dynamic IL emission and process launching, both required by that
harness. Its compile/emit/load tests and SDK subprocess tests cannot execute
unchanged in an AOT player. These restrictions affect the development harness;
consumers compile Spire's generated code before IL2CPP translation. See
[Unity's scripting restrictions](https://docs.unity.com/en-us/engine/6000.6/manual/programming-environment/scripting-backends/il2cpp/scripting-restrictions).

Run the complete repository harness in Editor Mono:

```sh
dotnet run -c Release --project tools/Spire.Tool -- unity-harness --timeout 900
```

Use `--probe` to run the five Roslyn feasibility checks first. They exercise
compilation/emission, generator execution, analyzer diagnostics, workspace
semantic models, and loading emitted assemblies in the actual Editor runtime.

The full runner builds portable versions of the analyzer, generator snapshot and
behavioral, PatternAnalysis, DevTools, and global configuration test assemblies.
A Unity Test Framework adapter executes their existing xUnit assertions; the
129 compile-time behavioral tests also run through Unity's own compiler. The
normal .NET test projects retain their default targets and runner.

Independent xUnit discovery provides a case identity baseline. Unity must
discover and execute every baseline case, and every original behavioral method,
with no failures or skips. Results, discovery manifests, source/reference hashes,
and dependency origins are saved in `tmp/unity-compiler-tests`; previous runs are
archived under `TestResults`.

Roslyn, Workspaces, and test dependencies are confined to the disposable test
project's Editor-only assets. One MSBuild dependency graph resolves their
versions; conflicting copies are rejected. The shipped Unity package remains
limited to Spire's four DLLs. Analyzer and snapshot tests use .NET reference
assemblies as compiler inputs, while runtime tests load generated assemblies
against Unity Mono. Preparation builds portable test assemblies and runs the
net10.0 independent discovery tool through `dotnet`. DevTools preparation tests
invoke that workflow again from inside Unity, using the C# CLI and `dotnet` child
processes. Global-configuration tests exercise SDK/MSBuild configuration during
their preparation build; their assertions execute in Mono. The .NET 8 reference
pack is compiler metadata and does not require a .NET 8 runtime.

`--prepare-only` creates the project without launching Unity. `--no-build`
reuses existing portable outputs and checks that the dependency bundle contains
the current test assemblies. Set `SPIRE_UNITY_EDITOR` or `--editor` to choose the
installed Editor; full-harness validation covers 6000.5.6f1 and 6000.6.5f1 on Linux.
Native serialization results differ between these versions, as recorded below.

Validated on October 10, 2026: all 2,266 full-harness checks pass in Editor Mono,
with zero failures or skips. The independent portable baseline contains 2,127
cases, and all 129 original compile-time behavioral assertions also execute.
The five Roslyn feasibility probes passed previously. The normal .NET host's
latest project runs passed 2,242 repository tests, including 57 tooling checks.
The October 11 native Editor rerun passes 120 assertions, including exact numeric
and enum edits, custom enum display names, combined flags, rejected unavailable
choices, nested fields, multi-step Undo/Redo, and unchanged payload IDs/YAML.
Three metadata/type-policy observations remain; none is a failed assertion.
Reports are under `tmp/unity-resume-editor-final`; the same resumption passes
all 2,242 .NET tests under `tmp/unity-resume-final-dotnet-results`.
Standalone Mono and High-stripping IL2CPP serialization/cloning results
are recorded in the [current implementation validation](designs/2026-10-10-unity-serialization-footprint.md#validation).
The separate eight-assembly acceptance fixture, matching InspectCode 2026.2.3.1
inspection, and actual Rider 2026.2.3.1 quick-fix/recompilation pipeline passed
on October 8.

Rechecked on Unity 6000.6.5f1 (Linux) on October 10: the complete Editor Mono
harness again passes all 2,266 checks with every expected identity matched, and
the IL2CPP behavioral player passes all 137 checks. Native graphics Editor
integration and Mono/IL2CPP serialization player preparation instead crash in
the Editor with `SIGSEGV`, before their final reports. The fixed failing scene
and all current fixtures were retained. These results do not extend native
serialization support to 6.6. See the [6.6 results and GDB evidence](issues/unity-il2cpp-opaque-managed-reference-slots.md#unity-66-recheck-on-october-10).
The [subsequent callback probes](issues/unity-6.6-generic-struct-serialization-callback.md)
reproduce the 6.6 Editor crash without Spire, while identical callback source
passes in 6.5; no safe workaround is established for generic struct serialization.
The [single-file MRP](../repros/UnityGenericStructCallback) contains only a null
managed-reference field and a logging callback; no payload allocation is required.

On October 11 the separate scene-loading failure was reduced to an incorrectly
registered component with an embedded `MonoScript`. Its Unity-only MRP also
crashes in a 6000.6.5f1 IL2CPP player. Correcting the Spire test fixture's script
registration makes its complete IL2CPP/High player pass on 6000.5.6f1 without
changing Spire DLLs or serialized values. Both engine issues have been reported
to Unity. See the [MRP, passing controls, and report receipt](issues/unity-il2cpp-opaque-managed-reference-slots.md#unity-report).
