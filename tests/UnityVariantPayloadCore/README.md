# Unity variant-payload core fixture

This fixture validates the generated runtime representation in Unity 6000.5.6f1
on both Linux player backends. The `unity-union-core` tool command builds both
players through:

```sh
dotnet run -c Release --project tools/Spire.Tool -- unity-union-core --timeout 1200
```

The invoked Editor method is:

```text
Spire.UnityVariantPayloadCore.CorePlayerBuild.BuildPlayers
```

The editor writes `unity-core-build-report.json` at the disposable project root.
The report contains separate executable paths:

```text
Builds/VariantPayloadCore/Mono/VariantPayloadCore.x86_64
Builds/VariantPayloadCore/IL2CPP/VariantPayloadCore.x86_64
```

The backend directories are intentionally separate. Unity writes backend runtime
files beside the executable, so placing Mono and IL2CPP outputs in one directory
causes one build to overwrite the other backend's data.

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

Rendered Editor checks use native UI Toolkit controls and IMGUI input:

```sh
SPIRE_UNITY_NATIVE_INPUT=1 dotnet run -c Release --project tools/Spire.Tool -- \
  unity-union-core --graphics \
  --method Spire.UnityVariantPayloadCore.SerializedUnionInspectorTests.Run \
  --timeout 600
```

The Linux runner creates a private Xvfb display. The opt-in input helper queues
XTest events there; it does nothing without the flag and a display. This avoids
Unity 6000.5.6f1 blocking inside synchronous `EditorWindow.SendEvent(MouseDown)`.
The final rendered run passes 16 cases and 147 checks with no failures or
unverified cases. It covers repeated text/numeric edits, native IMGUI `x/y/z`
without refocusing, copied previews, collections, external edits, Undo/Redo,
shared managed-reference aliases/cycles, prefab overrides, and multi-object edits.
