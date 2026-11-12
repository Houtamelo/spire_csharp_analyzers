# Unity `.unitypackage` acceptance fixture

This fixture validates the legacy Asset Package import path separately from the
UPM package. The initial project has only built-in Unity modules and the
reflection-based Editor verifier; it does not reference Spire at compile time.

The exporter stages an archive under `Assets/Spire`. Import the generated
`.unitypackage` with **Assets → Import Package → Custom Package**, then copy
`Probe.cs.txt` to `Assets/Probe.cs` and `Probe.csc.rsp` to `Assets/csc.rsp`.
After Unity recompiles, use **Spire → Verify imported unitypackage**, or run
Unity with `-batchmode -projectPath PATH -executeMethod
Spire.UnityAssetPackageAcceptance.AssetPackageAcceptance.Verify -logFile LOG`.
The method writes `unitypackage-verify.json` and exits with its result.
The bundle contains five DLLs: the Spire runtime, the Functional runtime, and
three compiler extensions. The verifier checks runtime/compiler plugin settings
and labels, generated APIs, the Functional `Result`/`Option` source probe, the
`[Serializable]` Unity drawer, wrong-variant assertions, and native
`EditorJsonUtility` persistence. The source archive and staging directory can be
removed before verification; the imported `Assets/Spire` tree is self-contained.

To exercise the analyzer failure path, remove `Assets/Probe.cs`, copy
`Probe.Invalid.cs.txt` to `Assets/Probe.cs`, and keep `Assets/csc.rsp` with
`-warnaserror:SPIRE014`. Unity compilation must fail with SPIRE014 for the
unguarded `InvalidCommand.move` access.

On November 12, 2026, Unity `6000.5.6f1` on Linux exported and freshly imported
the five-DLL bundle, passed all six acceptance groups, and exited with code 0.
The compiled Functional probe exercised result mapping, exhaustive matching,
inferred `then`, and option mapping in Editor Mono. The negative consumer failed
with SPIRE014 as expected. This run does not add Functional player or other-Editor
coverage. Portable DevTools tests cover staging, metadata preservation, repeat
exports, and rejected inputs separately.

In a restricted Linux sandbox, use a writable `XDG_CONFIG_HOME` with access to the
existing Unity license when running the native gate.
