# Unity 6000

The `unity-6000` branch targets Unity 6000.5 with Roslyn 4.10 and Mono. The
normal NuGet build remains on `main`. IL2CPP and other Unity minor versions
require separate validation. Rider is the supported IDE.

All build and acceptance workflows use the C# CLI in `tools/Spire.Tool`. It
requires the .NET 10 SDK for preparation; using the built package requires only
Unity. Run `dotnet run -c Release --project tools/Spire.Tool -- --help` for commands
and options. `Spire.Tool.Core` targets .NET Standard 2.1 so the same preparation
contracts can be tested in Editor Mono. The SDK, installed Editor, and Rider/JDK
requirements of their respective workflows remain unchanged.

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

Unity stores the discriminator and one typed payload for the active variant.
Runtime storage is excluded from serialization. Generated callbacks rebuild
the union through its factories, and a generated editor drawer provides a
variant selector and active fields in UI Toolkit and IMGUI.

The stored payload schema is independent of the runtime layout. `Overlap` keeps
its existing runtime field offsets and appends separate managed references for
the payload and ownership tracking. Serialization adds managed references to
every layout, so a serializable union is no longer an unmanaged type even when
all its variant fields are unmanaged. Raw runtime bytes are never persisted.

Variant changes initialize the new variant's fields to their defaults. Missing
or mismatched payloads for a known discriminator also produce that variant with
default fields. Unknown discriminators keep invalid/default runtime storage.
Unity's native normalization still applies, including null strings becoming
empty strings after persistence.

Fields must satisfy Unity's native serialization rules. Supported shapes include
primitive values, enums up to 32 bits, Unity values/object references, concrete
serializable classes/structs, and one-dimensional arrays or `List<T>`. Dictionaries,
multilevel containers, nullable value types, tuples, unsupported System values,
and polymorphic object/interface/abstract fields are rejected. Generic fields
follow the same contract; known unsupported closed arguments are diagnosed.
Closures supplied by another assembly or constructed dynamically must also use
native-supported types; the generator cannot validate every external closure.
Validation also checks serialized fields inside helper types. Callback-bearing
helpers containing nested generated unions are rejected because their callback
ordering cannot safely preserve the generated snapshots. Helpers without nested
unions keep Unity's ordinary callback behavior.

Generated public properties retain the existing compiler-profile behavior:
`init` accessors when `IsExternalInit` is available, otherwise getter-only access.
Unity's default profile needs no consumer polyfill for generated code.

Variant order and generated payload names are part of the initial serialized
schema. Renaming/reordering them can invalidate existing assets; automatic schema
migration is not provided. Serialization also allocates payload objects and
prepares nested state, so measure costs for frequently serialized large unions.

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
are Editor-only test assets, outside the shipped Spire package.

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
installed Editor; validation currently covers 6000.5.6f1 on Linux.

Validated results: five feasibility probes and all 2,036 full-harness checks pass
in Editor Mono, including nine adapter checks. The normal .NET host passes all
2,027 repository tests. Both full runs have zero failures or skips. The separate
eight-assembly acceptance fixture, matching InspectCode 2026.2.3.1 inspection,
and actual Rider 2026.2.3.1 quick-fix/recompilation pipeline also pass.
