# Unity integration

**Unity 2020 will not work.** Its compiler lacks the source-generator support and
C# 9 features required by Spire. Non-extended Unity 2021/2022 LTS results are
tracked in the [LTS follow-up](reviews/2026-11-04-unity-lts-followup.md).

Concrete per-variant wrappers pass native write/read checks for non-generic
unions on `2022.3.62f3`. **Generic unions cannot use native Unity serialization
on Unity 2022.** Newer Editors retain their existing generic support. See the
[wrapper validation](reviews/2026-11-04-concrete-unity-wrappers.md) and
[original failure](issues/2026-11-04-unity-2022-generic-managed-reference-payloads.md).

The [initial Unity 2020–2023 validation](reviews/2026-11-04-unity-2020-2023-validation.md)
records the original failures. The November 5 Linux Editor runs pass 2,689
checks on both Unity 2022 and 2023. Both also pass applicable native serialization
and actual Mono/IL2CPP player checks; Unity 2022 excludes unsupported native
generic serialization. The separate rendered Inspector suite also passes on
2022.3.62f3, 2023.2.22f1, and 6000.5.6f1 after two Editor drawer corrections;
see the [rendered validation](reviews/2026-11-05-rendered-inspector-validation.md)
for exact counts and revalidation scope. The declared minimum is 2022.3.62f3.
See the [current-package recheck](reviews/2026-11-05-unity-2022-2023-current-validation.md).
Unity 2021's Roslyn 3.9 compiler cannot load the current Roslyn 4.3 extensions.

The [Windows version matrix](reviews/2026-11-06-windows-unity-version-matrix.md)
passes 2,692 complete Editor cases on each of `2022.3.62f3`, `2023.2.22f1`, and
`6000.5.6f1`, including compiler probes and six new filesystem regressions.
Applicable native persistence, rendered Inspector, High-stripping Mono/IL2CPP
players, and Visual Studio Community acceptance also pass on all three.
Twelve Linux-only cases are independently recorded as platform exclusions.

The `unity-6000` compiler extensions target the Roslyn 4.3 API baseline. Native validation
covers the versions and platform boundaries in the linked matrices; player
serialization and stripping coverage are detailed below. The same sources also
ship through NuGet; [CoreCLR and Unity suites](test-platforms.md) validate their
separate compilation environments. Other Unity versions and player platforms require separate
validation. Rider and Visual Studio Community have verified IDE workflows,
including one representative live Quick Action and subsequent Unity recompilation.
See the [current Windows validation evidence](reviews/2026-11-06-windows-unity-version-matrix.md)
and [initial environment validation](reviews/2026-11-04-windows-unity-validation.md).

Unity now marks [6.5 end of life](https://discussions.unity.com/t/unity-6-5-is-now-available/1723176).
This guide records compatibility evidence; a maintained release baseline remains
unresolved. The Roslyn 4.3.1 downgrade resolves the initial 6000.3.26f1 LTS
compiler rejection. The historical Inspector fixture needed an additional
runtime Unsafe DLL for the removed `UnsafeOverlap` mode. The current payload
representation has no Unsafe dependency; its native validation is tracked separately.
The shipping minimum is 2022.3.62f3; the optional Showcase requires 6000.5.6f1. See the
[LTS evaluation](issues/unity-6.3-roslyn-host-compatibility.md) and
[commercial review](reviews/2026-10-11-commercial-readiness.md).

Build and Unity acceptance workflows use the C# CLI in `tools/Spire.Tool`. It
requires the .NET 10 SDK for preparation; using the built package requires only
Unity. Run `dotnet run -c Release --project tools/Spire.Tool -- --help` for commands
and options. `Spire.Tool.Core` targets .NET Standard 2.1 so the same preparation
contracts can be tested in Editor Mono. The SDK, installed Editor, and Rider/JDK
requirements of their respective workflows remain unchanged.

Automated tests and acceptance workflows use inputs under `tests/` and run in
disposable projects under `tmp/`. Package sample inputs and customer project
settings belong to [`tests/UnityPackageSample`](../tests/UnityPackageSample/README.md).
They do not depend on the interactive `examples/` tree. Validation project and
package output paths inside that tree are rejected before modification.
Test project references and validation subprocess builds disable Source Link's
Git queries, which otherwise inspect example submodules. Direct production
builds retain their normal source-control metadata policy.

On Linux, Unity commands automatically start an isolated `Xvfb` display and stop
it when Unity exits. Install `Xvfb` before running these workflows. Diagnostics
are written beside each Unity log as `*.xvfb.log`.

Windows commands use the signed-in guest desktop and Direct3D 11. The default
Editor path is under `%ProgramFiles%\Unity\Hub\Editor\6000.5.6f1`;
`SPIRE_UNITY_EDITOR` overrides it. Native input is opt-in and confined to the
foreground Unity process. Run rendered checks with other guest dialogs closed.

## Install

To build a self-contained `.unitypackage`, run:

```sh
dotnet run -c Release --project tools/Spire.Tool -- unitypackage
```

The command builds Spire, stages an isolated project under `tmp/unitypackage-export`,
and uses Unity's [`-exportPackage`](https://docs.unity.com/en-us/engine/6000.6/manual/unity-editor/command-line-arguments/editor) to produce
`tmp/unity-package/Spire-6.0.0.unitypackage`. The filename follows the current package
version. The default Editor is `6000.5.6f1`; select another installation with
`--editor /path/to/Editor/Unity` or `SPIRE_UNITY_EDITOR`. Use `--output FILE.unitypackage`
to choose the archive location. This workflow requires an installed, licensed Editor
and, on Linux, `Xvfb`.

In the receiving project, remove any existing Spire UPM installation, then use
**Assets → Import Package → Custom Package** and select the archive. Keep all DLLs
and their metadata selected. The imported runtime, compiler extensions, and offline
guides live under `Assets/Spire`, so the archive and export project can be deleted
after import. This export includes no samples or URP dependency; samples remain
available through the UPM package. The DLL `.meta` files retain their GUIDs, compiler
labels, and plugin import settings across both formats.
The [archive acceptance fixture](../tests/UnityAssetPackageAcceptance/README.md)
verifies a fresh import, generated APIs and drawers, native JSON persistence,
and an unsafe consumer rejected by SPIRE014.

For a local UPM package, run:

```sh
dotnet run -c Release --project tools/Spire.Tool -- package
```

In Unity's Package Manager, choose **Add package from disk** and select
`tmp/unity-package/individual.antonio-pedro-goncalves-ferreira.spire/package.json`.
This installation references the source folder directly; keep it in place, or copy
the whole package into your project's `Packages` folder for an embedded installation.

The manifest declares `2022.3.62f3` as its minimum version. This is not an upper
compatibility bound. On `6000.6.5f1`, the current package passes 126 native Editor
serialization write checks and 87 fresh-process read checks. Generic unions
closed over reference-type arguments, such as `UnityGenericUnion<string>`, still
trigger the [Unity generic-struct callback crash](issues/unity-6.6-generic-struct-serialization-callback.md).
Use `6000.5.6f1` for those serialized generic unions. The
[November 2 recheck](reviews/2026-11-02-unity-6.6-serialization-recheck.md)
records the passing cases, isolated failure, and exact package hashes; it does
not extend the complete Inspector/Player validation matrix to 6.6.

The package contains the runtime DLL and compiler-only Spire analyzer,
PatternAnalysis, and CodeFixes DLLs. Compiler-only assets have the
`RoslynAnalyzer` label and all plugin platforms disabled. Unity and Rider supply
their compiler assemblies; the package does not contain Roslyn, Workspaces, or
System.Collections.Immutable DLLs.

The bundle also includes runtime XML documentation, Third Party Notices,
offline guides under `Documentation~`, a dependency-free `Samples~/QuickStart`,
and an optional `Samples~/Showcase`.
For Showcase, use Unity 6000.5.6f1 and install URP 17.5.0 and Unity Test Framework 1.7.0 before importing the sample
from Package Manager. Its authored asset GUIDs are preserved, and its menus
resolve the imported location instead of assuming a project-root folder.
Importing the sample does not change render settings; its explicit configuration
menu does. See the [customer getting-started guide](unity-package/Documentation~/GettingStarted.md).

For signed artifacts, exact tarball acceptance, integrity failure controls, and
the Publisher Portal handoff, see the [UPM release workflow](upm-release.md).

The runtime DLL uses Auto Reference. Assemblies with Override References enabled
must include `Houtamelo.Spire.dll` explicitly, including assemblies that only
consume unions defined in another asmdef. Without that reference, SPIRE009 cannot
resolve Spire's exhaustiveness attributes and skips analysis.
Analyzer scope is separate from
runtime DLL references; verify that every intended consuming asmdef receives
the analyzer. Code fixes execute in the IDE, while Unity executes analyzers and
source generators.

The DLL package does not import the NuGet MSBuild `.props` into Unity's compiler.
Use explicit attribute arguments for union settings; editing Unity-generated
`.csproj` files does not configure Unity's compilation. The MSBuild defaults in
the repository README apply to SDK projects.

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

For Windows Visual Studio Community, run:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File tools\VisualStudioIntegrationTestDriver\Run-Windows.ps1
```

This prepares Unity-generated Visual Studio projects, requires live diagnostics,
invokes the IDE's actual Spire Quick Action, resolves a source-generated symbol,
and recompiles the saved edit in Unity. It uses the `SpireValidation` experimental
profile. See the [runner instructions](../tools/VisualStudioIntegrationTestDriver/README.md)
for prerequisites, reports, and repeat runs.

## Examples and test fixtures

The local-only `examples/UnityShowcase` project has a saved scene and preset
asset. It is ignored by the parent repository and is not required for packaging.
When available, build the local package, then open it in Unity Hub. It demonstrates union authoring,
generic results, managed-reference fields, rename aliases, enum checks,
LINQ filtering, and generated inliners. Its EditMode tests include native asset
persistence and a Play Mode smoke test of the scene.

The existing projects under `tests/UnityIntegration`, `tests/UnityBehavioral`, and
`tests/UnityAsmdefIntegration` are tracked test fixtures. Their CLI runners copy
the fixture contents into disposable projects under `tmp/`, add the built package
and any test dependencies, and run there. Automated package/sample validation
uses `tests/UnityPackageSample`; edits to this interactive showcase do not change
test inputs or exported sample assets.

## Serialize a union

Known Burst/ECS limitation: generated Unity serialization adds a managed-reference
cache to the union. Such a type cannot serve as unmanaged job, native-container,
or ECS component data, even when its variant parameters are unmanaged. Users who
need both must manually declare two unions: a `[Serializable]` authoring type and
a runtime type without that attribute, containing only Burst-compatible fields.
Convert between them explicitly, for example in an ECS baker. Removing
`[SerializeField]` from a consuming field does not change the union's generated
storage. Spire does not generate the second type or the conversion.

For native Burst execution, use only supported unmanaged fields and prefer
exhaustive switch statements. Switch expressions can synthesize a boxed
unmatched-value exception path, which Burst rejects even when Spire proves the
variants exhaustive. Unity-hosted accessors use Unity's assertion API; their
nonserialized form uses a constant message compatible with Burst.

The [path tracer](../examples/BurstPathTracerSpire/README.md) and
[Firefighters](../examples/FirefightersSpire/README.md) siblings exercise real
Burst rendering jobs and ECS state transitions. Their native validation is
recorded in the [integration report](reviews/2026-10-27-spire-demo-integration.md).

Add `System.Serializable` to the union. Generated public payload structs,
including former-name aliases, receive the marker only with this opt-in:

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

Unity serialization also requires the target compilation's
`UNITY_5_3_OR_NEWER` symbol and accessible Unity runtime APIs. Editor drawers
additionally require `UNITY_EDITOR` and accessible Editor APIs. Ordinary .NET
projects keep the normal `System.Serializable` payload markers without Unity
generation or Unity serialization diagnostics, even if they reference Unity
assemblies.

Concrete unmanaged variant payloads overlap without unsafe code or manual size
calculations. Generic-dependent fields use typed storage. Reference storage uses
`ReferenceStorage.Auto`, `Typed`, or `Shared`, with Auto as the project default.
A union may itself be generic; nesting inside a generic containing type reports
SPIRE_DU018. Readonly/ref structs, records, and record structs are outside this
serialization mode.

Unity stores `_kind` and one active managed-reference wrapper whose public
`value` field contains the generated payload struct from the union's sibling
types container (`ResultTypes.Variants.Success`). Each variant has a concrete
wrapper under `ResultTypes.SerializedPayloads`, inheriting `value` from a shared
generic base. Wrappers for generic unions remain generic; Unity 2022 cannot
serialize them. The same wrapper code is generated on all Unity versions.
Old shared-generic cache objects remain readable and are upgraded when saved;
resave old assets in a newer supported Editor before moving them to Unity 2022.
Runtime storage is excluded from
serialization. A recognized payload identifies the variant even if `_kind`
disagrees. Generated callbacks rebuild the union through its factories,
and a generated editor drawer displays the `Kind` selector and active fields as siblings
inside a foldout titled with the serialized field's display name in UI Toolkit
and IMGUI. Its expansion state follows Unity's serialized property.
Each compilation with serialized union drawers receives
one shared Editor helper and a small registration class with metadata for each
union. Each immutable variant entry groups its identifier, current payload type,
optional former payload type, and fields. Drawer entry points are inherited from
the shared helper. All drawer code is guarded by `UNITY_EDITOR`; the helper is generated
per Unity assembly/asmdef and requires no additional Editor DLL.
Every generated payload field carries `[SerializeField]` or `[SerializeReference]`;
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
adds only one instance reference, `_serializedPayload`, with no hydration flag
or other per-instance bookkeeping. A serializable union is therefore no longer unmanaged,
even when all its variant fields are unmanaged. Raw runtime bytes are never
persisted.

Variant changes initialize the new variant's fields to their defaults. Null,
missing, or unrecognized payloads default to the first declared variant and emit
`invalid union serialization data, defaulting to FirstVariantName` during
deserialization. The generated `__SpireCreateDefault(int kind)` helper also
returns the first variant with default payload arguments for out-of-range
integers. Drawer refresh does not repeat the deserialization warning.
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

Variant accessors return payload structs with getter-only properties. Their
private `_`-prefixed fields retain Unity serialization attributes and remain
editable through the Inspector. Replace a union through its factory to change
values from C# code. Unity's default
profile needs no consumer polyfill for generated code.

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

Player reconstruction releases the concrete payload cache and uses the existing
reference for a shared reconstructed marker. Repeated nested hydration therefore
preserves reconstructed runtime values without adding an instance field. The
next `OnBeforeSerialize` prepares a concrete payload. Runtime
`Instantiate` serializes its source, which can retain a reusable cache while its
deserialized clones release their graphs. Serialization events may allocate and
prepare nested state; measure costs for frequently serialized large unions.

Generated callbacks explicitly implement `ISerializationCallbackReceiver`.
They and the public/internal implementation helpers carry
`EditorBrowsable(Never)`. This is an IDE hint. The current
[Rider completion probe](issues/rider-editorbrowsable-generated-members.md) was
inconclusive; the attribute does not guarantee that every IDE hides these members.

## Validate

Run the compact AssetDatabase/SerializedObject integration fixture:

```sh
SPIRE_UNITY_EDITOR=/path/to/Editor/Unity dotnet run -c Release --project tools/Spire.Tool -- unity-integration
```

It copies the tracked fixture into `tmp/unity-integration`, builds the package,
and records real persistence and generated-surface checks. Rendered Inspector
coverage uses the dedicated payload fixture:

```sh
SPIRE_UNITY_NATIVE_INPUT=1 dotnet run -c Release --project tools/Spire.Tool -- unity-union-core --graphics --method Spire.UnityVariantPayloadCore.SerializedUnionInspectorTests.Run --timeout 600
```

This opens native UI Toolkit and IMGUI controls to check field edits, focus,
variant changes, Undo/Redo, prefab overrides, and multi-object editing. A missing
control or unavailable scenario is a failed verification, not a passing observation.
This graphics suite is separate from the headless Editor harness and must be
invoked explicitly. On Unity 2022, only the unsupported closed-generic Inspector
serialization case is marked not applicable.
On Linux the runner creates a private Xvfb display. The explicit input flag
enables XTest keyboard/pointer events on that display for the IMGUI checks.
On Windows, set `$env:SPIRE_UNITY_NATIVE_INPUT = '1'` before the same command;
the helper sends native Windows input only while Unity owns the foreground and
the target field's screen position.

The same fixture provides separate native asset phases, each run in its own
Editor process through `unity-union-core --method`:

- `Spire.UnityVariantPayloadCore.SerializedUnionEditorTests.Write`
- `Spire.UnityVariantPayloadCore.SerializedUnionEditorTests.Read`
- `Spire.UnityVariantPayloadCore.SerializedUnionEditorTests.Recovery`

Write creates persisted data, Read verifies fresh-process reconstruction and
unchanged saves, and Recovery exercises invalid/null/missing/former payloads.
See [the fixture README](../tests/UnityVariantPayloadCore/README.md) for reports.

Build both release x64 players with High stripping for the Editor's host platform:

```sh
dotnet run -c Release --project tools/Spire.Tool -- unity-union-core --timeout 1200
```

`unity-core-build-report.json` records both executable paths. Launch each with
`-batchmode -nographics -spire-report <absolute-report-path>`. Require a successful
build, player exit zero, the expected backend, and every runtime assertion.
Compiler-only Spire DLLs must be absent from player assemblies. Reflection-based
behavioral test linker roots are separate from this production-stripping check.
Windows outputs use `.exe`; Linux outputs use `.x86_64`. Neither platform's
execution substitutes for testing the other.

The earlier loading/stripping investigation is preserved in the
[native-loading report](issues/unity-il2cpp-opaque-managed-reference-slots.md).
Its old layout/profile results predate the current payload representation.
The runtime still preserves the generic type-loader dependency required by
Unity's managed-reference loader; no project linker descriptor is needed for
ordinary generated unions.

The Roslyn test suite also compiles generated code against installed Unity
assemblies. It does not substitute Unity types or prove native persistence by
itself. The design and feasibility evidence are recorded in
[the Unity design](designs/2026-10-06-unity-6000.md).

Run the existing runtime behavioral tests inside the Unity Editor:

```sh
dotnet run -c Release --project tools/Spire.Tool -- unity-behavioral
```

This runner copies the original union declarations and all 100 xUnit test
methods into `tmp/unity-behavioral`. Unity's compiler runs the packaged
generator; a Unity Test Framework adapter executes each original assertion in
Editor Mono and writes `behavioral-results.xml`. Input hashes and the expected
test count prevent a partial run from passing unnoticed.

The test assembly enables the compiler's preview language mode and supplies a test-only `IsExternalInit`
polyfill because these existing tests use newer syntax and records. The normal
consumer C# 9 profile remains covered separately. xUnit and JSON dependencies
are test assets, outside the shipped Spire package.

Both xUnit adapters yield once after successful execution so Unity Test Framework
1.4.5 observes an incomplete Task even for a synchronous test. Without this, the
framework can append a stale-yield failure to the last case after aggregating results.

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
The migrated suite contains 100 original assertions plus adapter/backend checks.
It covers payload accessors, reference storage, records, generics, both JSON
converters, and closure inliners. Require the current manifest count and case
identities; the historical 137-check result measured the retired layouts. Test-specific linker roots
preserve the test and dependency assemblies for the reflection-based adapter;
this run alone does not establish production stripping compatibility.

### Web runtime

Build the runtime assertions for the browser:

```sh
dotnet run -c Release --project tools/Spire.Tool -- unity-web --project tmp/unity-web-runtime --timeout 3600
npm ci --prefix tools/unity-web-runtime
npm exec --prefix tools/unity-web-runtime -- playwright install firefox
node tools/unity-web-runtime/run.mjs --project tmp/unity-web-runtime --browser chromium
node tools/unity-web-runtime/run.mjs --project tmp/unity-web-runtime --browser firefox
```

Install the selected Editor's Web Build Support module first; `--editor` selects
the installation. The Chromium runner uses installed Google Chrome, while the
Firefox runner uses Playwright's matching Firefox binary. Preparation reads
`tests/UnityBehavioral`, the original behavioral sources, `tests/UnityVariantPayloadCore`,
and `tests/UnityWebRuntime`; inputs and outputs stay outside `examples/`.
`--prepare-only` prepares the project without launching Unity.

On 2026-11-06, actual release WebGL/IL2CPP players passed in headless Chrome
157.0.8103.38 and Playwright Firefox 159.0 on Linux:

| Unity | Original behavioral Facts per browser | Native core checks per browser |
| --- | ---: | ---: |
| 2022.3.62f3 | 100 | 59 |
| 2023.2.22f1 | 100 | 60 |
| 6000.5.6f1 | 100 | 60 |

Every run confirmed `WebGLPlayer`, IL2CPP, 32-bit pointers, WebGL2, High managed
stripping, and a nondevelopment build. Chrome used SwiftShader and Firefox used
software rendering; these are runtime correctness results, not GPU performance
measurements. Unity 2022 retains its existing generic native-serialization
restriction. The original Fact bodies are copied unchanged. Reflection roots
preserve the behavioral test/dependency assemblies; native core probes remain
in the ordinary player assembly without blanket preservation of Spire.

The builder requests `FullWithStacktrace` exception support. An initial Unity
6000 build using the default `ExplicitlyThrownExceptionsOnly` passed all 60
native checks and 99 Facts, but the null-source `OfKind` test received
`MethodAccessException` instead of its expected `NullReferenceException`.
Unity's [Web exception settings](https://docs.unity3d.com/2023.2/Documentation/Manual/webgl-building.html)
require a full exception mode for implicit null-reference checks. The passing
matrix therefore establishes full-exception behavior; it does not establish
identical exception behavior with Unity's default Web setting.

The browser runner checks every expected Fact identity and both completed
reports, reads results through a test-only JavaScript plugin to avoid Unity's
console truncation, and fails on missing results or browser errors. Add
`--negative-control` to invoke an intentional xUnit assertion failure through the
same bridge after all 100 Facts; the command must exit 1. All six browser/version
negative controls propagated that failure. Builds, source hashes, browser reports,
and the initial failed attempt are retained under `tmp/web-runtime-20261106`.
The Web runner covers generated runtime behavior and built Resources loading;
Editor/IDE tooling, Safari, mobile browsers, and WebGPU are separate coverage.

### Editor compiler harness

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
100 compile-time behavioral tests also run through Unity's own compiler. The
normal .NET test projects retain their default targets and runner.

Independent xUnit discovery provides a case identity baseline. Unity must
discover and execute every applicable baseline case, and every original behavioral
method, with no failures or skips. Explicit Linux/Windows requirements are recorded
separately; the independent exporter and Unity bridge must agree on each excluded
identity and its reason. Ordinary skipped facts still fail discovery. See the
[platform coverage contract](test-platforms.md). Results, discovery manifests, source/reference hashes,
and dependency origins are saved in `tmp/unity-compiler-tests`; previous runs are
archived under `TestResults`.

Roslyn, Workspaces, and test dependencies are confined to the disposable test
project's Editor-only assets. One MSBuild dependency graph resolves their
versions; conflicting copies are rejected. The shipped Unity package remains
limited to Spire's five DLLs, including the Functional runtime. Analyzer and snapshot tests use .NET reference
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

### Historical validation before the payload redesign

The counts and layout-specific results below predate the current representation.
Current redesign evidence is tracked in the
[payload validation report](reviews/2026-10-23-union-redesign-core-validation.md)
and the [native fixture](../tests/UnityVariantPayloadCore/README.md).

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
are recorded in the [earlier implementation validation](designs/2026-10-10-unity-serialization-footprint.md#validation).
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
