# Unity 6.5 embedded-script scene-loading repro

Validated on Unity 6000.5.6f1 (`0e0577a1a2ac`) and 6000.6.5f1 (`3ff58d469c8a`),
Linux x86_64, IL2CPP, High managed stripping. No Spire package, generated code, payload instances,
generic types, or serialization callbacks are involved.

`ProbePlayerHook.cs` declares two components. Only `ProbePlayerHook` resolves to
the script asset. `ProbeHost`, added programmatically, is saved as an embedded
`MonoScript` in `Assets/Probe.unity`. Its sole field is an unread, null
`[SerializeReference] object`.

1. Open a clean copy of this project in Unity 6000.5.6f1 or 6000.6.5f1 with Linux IL2CPP support. Accept the project-version upgrade when using 6.6.
2. Select **Tools > Unity 6.5 Repro > Build IL2CPP**.
3. Run `Builds/Probe/Probe.x86_64 -batchmode -nographics -logFile player.log`.

The build succeeds with a missing-script warning for `ProbeHost`. The player
fails before the `MRP_SCENE_LOADED` marker:

```text
The file '.../Probe_Data/level0' is corrupted! Remove it and launch unity again!
[Position out of bounds!]
```

On Linux it traps with exit 133. A fresh import of the included saved scene
reproduces the failure. Repeated builds in an existing Editor session can produce
different results; use a fresh project copy without `Library` for comparisons.

The batch-mode equivalent is:

```sh
Unity -batchmode -nographics -projectPath /absolute/project/path -executeMethod ProbeBuild.Build -logFile build.log
```

Batch-mode Editor shutdown also exited 139 after the successful build in the
6.5 runs; the 6.6 build exited 0. The target of this repro is the separate
player-loading trap, reproduced by both players. The menu command leaves the
Editor open.

Passing controls: **Build Mono** loads the scene and prints the marker; moving
`ProbeHost` into `ProbeHost.cs`, keeping its field unchanged and recreating the
scene, makes the IL2CPP player load too. Removing the field also passes, even
though the embedded script and missing-script warning remain. `VALIDATION.md`
records the controls and the original Spire-fixture comparison. This project
deliberately retains the incorrect component registration that triggers the
failure.
