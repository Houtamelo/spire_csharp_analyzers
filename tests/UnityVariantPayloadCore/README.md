# Unity variant-payload core fixture

This fixture validates the generated runtime representation in Unity 6000.5.6f1
on Linux and Windows with both player backends. The `unity-union-core` tool command builds both
players through:

```sh
dotnet run -c Release --project tools/Spire.Tool -- unity-union-core --timeout 1200
```

Windows validation also passes native persistence, both actual player backends,
and rendered Inspector checks on `2022.3.62f3` and `2023.2.22f1`; see the
[three-version results](../../docs/reviews/2026-11-06-windows-unity-version-matrix.md).
Use a fresh disposable `--project` for Windows player builds. Reusing the native
persistence project can encounter the documented file-sharing preparation issue.

The `unity-web` command also reuses these runtime assertions and canonical
Resources in browser WebGL/IL2CPP players. See the [Web runtime commands and
three-version browser results](../../docs/unity.md).

The invoked Editor method is:

```text
Spire.UnityVariantPayloadCore.CorePlayerBuild.BuildPlayers
```

On Unity 2022, the native generic serialization fixture is inapplicable:
`SerializedUnionAsset.generic` is `[NonSerialized]`, and its persistence
assertions do not run. Generic factories, payload reflection, accessors, and
managed hydration remain tested. Unity 2023 and newer retain the serialized field
and its persistence assertions. Editor checks verify the field's presence or
absence through `SerializedObject`; reports include
`genericUnionSerializationSupported` and `genericUnionSerializationApplicability`.

The editor writes `unity-core-build-report.json` at the disposable project root.
The report contains separate executable paths:

```text
Builds/VariantPayloadCore/Mono/VariantPayloadCore.x86_64
Builds/VariantPayloadCore/IL2CPP/VariantPayloadCore.x86_64
```

The backend directories are intentionally separate. Unity writes backend runtime
files beside the executable, so placing Mono and IL2CPP outputs in one directory
causes one build to overwrite the other backend's data.
On Windows the executable suffix is `.exe`; the builder selects
`StandaloneWindows64` instead of `StandaloneLinux64`. Current Windows results
are recorded in the [Windows validation report](../../docs/reviews/2026-11-04-windows-unity-validation.md).

Before either player build, the editor method prepares the canonical serialized
union fixtures under `Assets/Resources/SerializedUnionPayloadCore`. The player
probe loads those resources after reconstruction and includes their checks in the
same backend report as the core runtime checks.

Launch either executable with a report path:

```sh
<executable> -batchmode -nographics -spire-report <absolute-report-path>
```

The runtime probe writes the selected backend, Unity version, debug-build flag,
processor bitness, assertion count, and failures to that report. Exit code zero
requires all generated-union assertions to pass.

The serialized phase uses the same disposable project and exposes these Editor
entry points:

```text
Spire.UnityVariantPayloadCore.SerializedUnionEditorTests.Write
Spire.UnityVariantPayloadCore.SerializedUnionEditorTests.Read
Spire.UnityVariantPayloadCore.SerializedUnionEditorTests.Recovery
Spire.UnityVariantPayloadCore.SerializedUnionEditorTests.AccessorAssertions
Spire.UnityVariantPayloadCore.SerializedUnionEditorTests.PreparePlayerAssets
```

`AccessorAssertions` checks active and inactive variant reads in ordinary,
generic, and nested serialized unions. It captures real Unity assertion logs
and writes `unity-union-accessor-assertions-results.json`. The Write and Read
phases also verify grouped generic payload type loading, mismatched/null payload
state recovery, and round-tripping of a private `SerializeReference`-only field.

The serialization phases persist Resources assets for fresh-process reads and write
`unity-union-serialization-write-results.json`,
`unity-union-serialization-read-results.json`, and
`unity-union-serialization-recovery-results.json` at the project root. The
`PreparePlayerAssets` performs the same canonical/malformed Resources setup for
the player builder without exiting the Editor and records
`unity-union-serialization-player-preparation-results.json`. The player probe is exposed through
`SerializedUnionPlayerProbe.RunChecks(Action<bool,string>)`; the existing core
player probe calls it after the Resources assets have been included in both
player builds.

Concrete-wrapper acceptance has separate non-generic assets and Editor methods:

```text
Spire.UnityVariantPayloadCore.ConcreteWrapperEditorTests.Write
Spire.UnityVariantPayloadCore.ConcreteWrapperEditorTests.Read
Spire.UnityVariantPayloadCore.ConcreteWrapperEditorTests.WriteLegacy
Spire.UnityVariantPayloadCore.ConcreteWrapperEditorTests.UpgradeLegacy
Spire.UnityVariantPayloadCore.ConcreteWrapperEditorTests.VerifyUpgraded
```

Run Write and Read in separate Editor processes. These checks cover inherited
payload paths, collections, native object references, shared/cyclic managed
graphs, former names, YAML stability, and generated drawer variant detection
through real `SerializedObject` properties. They do not open an Inspector window.
The reports are `unity-concrete-wrapper-{write,read,legacy-write,upgrade,verify}-results.json`.

For migration, run WriteLegacy with the old package in a newer Editor, archive
the asset/YAML, replace the package, then run UpgradeLegacy and VerifyUpgraded
in separate processes. VerifyUpgraded can also check the migrated asset in
Unity 2022. The fixture cannot create old generic-wrapper assets in Unity 2022.
Keep the broader generic cases above: this non-generic fixture does not establish
generic serialization support there. Current evidence is in the
[wrapper validation](../../docs/reviews/2026-11-04-concrete-unity-wrappers.md).

The November 5 concrete-wrapper revalidation passes both Linux x64 player builds
with High stripping on `6000.5.6f1`, then all 60 runtime checks on each actual
Mono/IL2CPP executable. The resource allowance is 12 GiB total with two CPU cores;
evidence is under `tmp/unity-concrete-wrappers-12g-20261105`.

The current [Unity 2022/2023 recheck](../../docs/reviews/2026-11-05-unity-2022-2023-current-validation.md)
also passes both Unity 2023 player backends. The probe defers quitting until the
first update because quitting from `AfterSceneLoad` reproducibly crashes an empty
Unity 2023 player during native cleanup. The actual scheduler's success and
failure exit codes are checked in the separate lifecycle control project.

Phase-3 native evidence is archived under
`tmp/union-redesign-results/phase3-native`. On Unity 6000.5.6f1, Editor
Recovery passes with 59 checks, and both standalone Mono and IL2CPP players
pass with 39 checks each. The build uses the disposable native-library path
`tmp/unity-il2cpp-native-libs` for IL2CPP linking/runtime and a local `cp`
permission shim; the package-to-source SHA-256 comparisons are recorded in
`package-hashes.txt` in that evidence directory.

The final migration gate is archived under
`tmp/union-redesign-results/phase4-final`. With the callback-bearing helper
fixture and fresh-process no-op-save coverage, Editor Write passes 92 checks,
Read passes 53 checks, and Recovery passes 59 checks. The latest player build
passes on both Mono and IL2CPP (errors 0), and the standalone reports pass all
39 runtime checks on each backend. The IL2CPP behavioral run is archived beside
the core reports and passes all 73 original behavioral facts with an IL2CPP
player marker.

The player probe records persisted malformed-payload fallback when the Resource
survives the build unchanged. Unity may canonicalize that malformed Resource
during the player build, so this evidence does not claim native malformed-asset
callback execution; the direct generated bridge fallback remains checked in
both players.

Rendered Editor checks use native UI Toolkit controls and IMGUI input. They are
separate from the headless Editor harness and require an explicit graphics run:

```sh
SPIRE_UNITY_NATIVE_INPUT=1 dotnet run -c Release --project tools/Spire.Tool -- \
  unity-union-core --graphics \
  --method Spire.UnityVariantPayloadCore.SerializedUnionInspectorTests.Run \
  --timeout 600
```

The Linux runner creates a private Xvfb display. The opt-in input helper queues
XTest events there; it does nothing without the flag and a display. This avoids
Unity 6000.5.6f1 blocking inside synchronous `EditorWindow.SendEvent(MouseDown)`.
Windows uses native pointer/keyboard events on the signed-in guest desktop,
requires foreground Unity ownership, and rejects target points belonging to
another process. Close guest firewall prompts and other overlapping dialogs
before running. Windows Unity Mono can report `UserInteractive=false` even on
this visible desktop, so availability uses actual foreground-window ownership.
The current Linux rendered runs pass 18 cases and 204 checks on both
2023.2.22f1 and 6000.5.6f1, and 17 cases with 192 checks on 2022.3.62f3. Only
the unsupported closed-generic Inspector serialization case is not applicable
on 2022. There are no failures or unverified cases; see the
[rendered validation](../../docs/reviews/2026-11-05-rendered-inspector-validation.md).
Coverage includes repeated text/numeric edits, native IMGUI `x/y/z`
without refocusing, copied previews, collections, external edits, Undo/Redo,
shared managed-reference aliases/cycles, prefab overrides, and multi-object edits.
