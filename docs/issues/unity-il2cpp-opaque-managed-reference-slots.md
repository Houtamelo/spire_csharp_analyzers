# Unity 6.5 IL2CPP scene-loading corruption

The original failure now has a Unity-only MRP. The fixture's `StabilityPlayerHost`
was declared inside `UnityPayloadStabilityTypes.cs` and saved as an embedded
`MonoScript`, without a script asset GUID. Its build warned that the attached
script was missing or invalid. Correcting that registration makes the frozen
stability fixture pass without changing its serialized values or Spire DLLs.

The separate [Unity 6.6 callback crash](unity-6.6-generic-struct-serialization-callback.md)
is unaffected by this correction.

## MRP and corrected fixture

The [repro project](../../repros/Unity65EmbeddedScriptManagedReference/README.md)
contains one runtime source file, an Editor builder, and a saved scene. Two
components are declared in `ProbePlayerHook.cs`; the second component has one
unread, null `[SerializeReference] object` field. `AddComponent<ProbeHost>()`
creates it, and scene saving embeds its `MonoScript`. There is no Spire package,
generated code, generic type, allocated payload, or serialization callback.

On Unity 6000.5.6f1, Linux x86_64, IL2CPP/High, a fresh build succeeds with the
missing-script warning and the player exits 133 with the original `level0`
corruption / position-out-of-bounds signature. Both the generated-scene probe
and a fresh project using the delivered saved scene reproduce it. Mono loads the
same source; IL2CPP loads it when the component is moved into its own correctly
named file and the scene recreated. Removing the field also passes while retaining
the embedded script and its warning. Some later builds in the same Editor session
passed, so fresh imports are required for reliable comparisons.

The frozen full stability fixture was independently copied without `Library`
and rebuilt: it still exited 133. In another fresh copy, the host was moved into
`StabilityPlayerHost.cs`, and only its scene script reference was replaced with
that asset GUID; the embedded `MonoScript` entry was removed. All four package
DLLs and every serialized field value stayed unchanged. Build and player exit
0; the report identifies Unity 6000.5.6f1 and IL2CPP and passes every required
stability and managed-reference assertion.

The tracked fixture now uses `StabilityPlayerHost.cs`. Its build entrypoint
checks that the component resolves to a persistent script asset of the expected
type, rejecting the bad registration before building a player.

A fresh project prepared from the corrected tracked fixture passes the combined
`Full` IL2CPP/High player: build and player exit 0, all three expected hosts are
present, and every main, stability, and managed-reference assertion is true. No
assertion or fixture family was removed. A separate negative control restores
the original host declaration and confirms the new guard rejects it before
player compilation. This removes the need to split the fixtures to avoid this
crash on the tested Unity version and platform.

The small MRP was subsequently imported into Unity 6000.6.5f1 (`3ff58d469c8a`).
Its C# and saved scene remained byte-identical. Unity upgraded the project
version and added `com.unity.modules.tetgen` 1.0.0 to the manifest; the two Linux
SDK/toolchain dependencies remained at 1.1.0. The IL2CPP/High build exited 0 with zero errors and one missing-script
warning, then the player exited 133 with the same `level0` corruption and
position-out-of-bounds error before its startup marker. The generic callback
Editor issue does not block this MRP. This establishes the original player-loading
signature on both tested Editor versions, with the invalid component registration
retained.

## Unity report

Submitted through Unity CLI on 2026-10-11 at 07:22:07.483 UTC:
**[Linux][IL2CPP] Player crashes loading embedded MonoScript with a null SerializeReference field**.
Unity returned `success: true`, `logsAttached: true`, and no errors or warnings.
The CLI returned no case ID. The
[receipt](unity-embedded-script-report-receipt.json) and
[submitted request](unity-embedded-script-report-request.json) preserve the result
and exact report text.

The report explicitly acknowledges the invalid component/file mapping and asks
for safe error handling or build rejection. It includes both tested versions,
the passing 6.5 controls, and the fresh-import versus warm-rebuild distinction;
overall reproducibility is marked `sometimes`. Five supplied attachments are the
source-project ZIP, validation notes, build-result excerpts, and both player logs.
The ZIP's SHA-256 is
`4e0d464d62f4cd5610050be2d3714e8831cf906417b2382657cac812fcc0df8f`.

The first sandboxed attempt failed locally with `LOCAL_STORE_UNWRITABLE` before
submission. The authorized retry outside the sandbox succeeded; only one report
was submitted.

## Evidence

Evidence under `tmp/`:

| Check | Evidence |
| --- | --- |
| Original frozen player, exit 133 | `unity65-original-recheck-player.log` |
| Fresh frozen fixture, exit 133 | `unity65-frozen-scene-control-player.log` |
| Saved-scene Unity-only MRP, exit 133 | `unity65-mrp-delivery-player.log`, `unity65-mrp-delivery-check/build-result.txt` |
| Same MRP on 6.6, build exit 0 and player exit 133 | `unity66-embedded-script-mrp-player.log`, `unity66-embedded-script-mrp-check/Unity65EmbeddedScriptManagedReference/build-result.txt` |
| Same source under Mono, exit 0 | `unity65-slim-mono-player.log` |
| Correct script asset under IL2CPP, exit 0 | `unity65-slim-fixed-player.log` |
| Embedded script with no field, exit 0 | `unity65-slim-empty-player.log` |
| Corrected frozen fixture, all required checks true | `unity65-frozen-scene-proper-script-player.json` |
| Corrected complete fixture, all required checks true | `unity65-corrected-full-fixture-player.json`, `unity65-corrected-full-fixture/unity-player-build-report.json` |
| Bad registration rejected before build, exit 1 | `unity65-script-registration-rejection/unity-player-build-report.json` |
| Four unchanged package DLL hashes | `unity65-mrp-reduction-evidence.json` |

Small-project batch-mode Editors also exited 139 during shutdown after successful
builds. That is separate from the player's scene-loading trap; build result files
and actual player launches establish the results above. All runs used Xvfb.

The following sections retain earlier narrowing work. Their direct-slot and
cache hypotheses preceded the script-registration comparison above.

## Earlier investigation

The [complete subsequent-production-release changelog audit](unity-il2cpp-release-changelog-audit.md)
finds relevant later serialization fixes, including a generic-array layout fix
on 6.6 that remains open on 6.3 LTS. None is yet a confirmed match for this crash.
The [6.6 recheck](#unity-66-recheck-on-october-10) passes the compiler and runtime
behavioral suites but encounters an earlier Editor serialization crash.

Environment:

- Unity `6000.5.6f1` (`0e0577a1a2ac`)
- Linux x86_64 standalone player
- IL2CPP backend with managed stripping set to `High`
- Null graphics device (`-nographics`)
- The package runtime assembly was the current `unity-6000` build

The full stability fixture builds successfully with IL2CPP, but its player
fails before its managed test report with:

```text
The file '.../SpireUnityIntegration_Data/level0' is corrupted! Remove it and launch unity again!
[Position out of bounds!]
```

The player exits with code 133 and does not produce its managed report. The
same failure was reproduced in an isolated copy at
`tmp/runtime-il2cpp-marker-investigation`; the build report was successful with
zero errors and 74 warnings. The player log is
`/tmp/runtime-il2cpp-typed-full-player.log`.

Within the larger disposable probe project, the smallest failing scene setup
found so far is a `MonoBehaviour` with two direct fields declared as
`[SerializeReference] object?`, both left null. Its IL2CPP build succeeds, but
the player reports the same `level0` corruption. This was not a standalone
Unity-only project. A control
with one such field starts without the error. A wrapped serializable class with
two object managed-reference fields reproduces it as well. A seven-field object
variant reproducer also reproduces it. These results are limited to this
Unity/editor/platform configuration.

The controls were not uniform across all managed-reference graphs:

| Probe | Build | Player startup |
| --- | --- | --- |
| Five generated union layouts only | succeeded | no corruption observed |
| Nested/generic/callback union graph | succeeded | no corruption observed |
| One direct `[SerializeReference] object?` field | succeeded | no corruption observed |
| Two direct null object fields | succeeded | `level0` corruption |
| Seven direct null object fields | succeeded | `level0` corruption |
| Two object fields plus a typed managed-reference graph root | succeeded | no corruption observed |
| Two `[SerializeReference] UnitySerializationPayloadBase?` fields, both null | succeeded | no corruption observed |
| Two typed base fields assigned simple serializable payload instances | succeeded | `level0` corruption |
| Two typed base fields assigned generated former-name payload instances | succeeded | `level0` corruption |

A single collection changes the result only when another managed-reference graph
is present. A disposable host with one
`[SerializeReference] UnitySerializationPayloadBase?[]` field containing two
simple assigned payloads started without corruption. The stability fixture was
then changed to one such array containing all seven former-name payloads. The
`StabilityBaselineAndManaged` player started successfully and reported all
stability checks true, including former-name reconstruction, cache lifetime,
payload collection, and managed-reference identity. The same array fixture in
`StabilityOnly` (without the separate managed-reference graph host) still
crashed, as did the combined full fixture containing the main baseline. Thus a
single collection is a supported shape in the passing graph, but it is not by
itself a workaround for the isolated stability player.

Changing the seven individual declarations from `object?` to
`UnitySerializationPayloadBase?` was insufficient: assigned former-name payloads
still reproduced the failure. The tracked fixture instead stores the seven
former-name payloads in one managed-reference array, retaining their generated
private concrete types until reconstruction after load.

At that stage, the evidence pointed to an interaction between Unity's IL2CPP player
serialization data and the shape of these direct managed-reference slots. It
does not identify the exact native trigger, and it does not implicate the
generated one-reference union payload bridge: the layout, nested, generic, and
managed-reference graph controls start successfully. The disposable probe
source is under `tmp/runtime-il2cpp-marker-investigation` and was not added to
the package or test suite.

Additional native and stripping checks narrowed the boundary without
identifying a package method that needs preservation. A coredump from the
two-null-slot reproducer was inspected at `/tmp/runtime-il2cpp-two-opaque.core`;
that temporary file is no longer present after the shutdown. The recorded usable
frames are Unity native `UnityPlayer.so` frames during player loading, with no
managed stack. The stripped `Houtamelo.Spire.dll`,
`mscorlib.dll`, and `Assembly-CSharp.dll` are byte-identical between the
failing `StabilityOnly` build and the passing `StabilityBaselineAndManaged`
build. `UnityLinkerRoots.ResolveManagedReferenceType`, including its
`Type.GetType(string)` call, is present in both stripped runtime assemblies.
The passing build produces a `SerializedTypes.xml` linker input for its managed
reference graph; the failing isolated build does not. This is evidence of a
native serialized-type/type-tree difference, not evidence that another linker
method should be retained. No `link.xml` or broad assembly root was added.

A final whole-player workaround probe used two scenes in one High-stripping
IL2CPP player. The first scene contained the main baseline and loaded the
second scene additively; the second contained the stability baseline with its
real managed-reference graph and the single former-payload array. The main
scene was also given a separate real managed-reference graph root. The player
build succeeded and loaded `level0`, but loading `level1` still failed with the
same native corruption error. The build and player logs are
`/tmp/runtime-il2cpp-probe-build-split-baselines-bothmanaged.log` and
`/tmp/runtime-il2cpp-probe-player-split-baselines-bothmanaged.log`.

The tracked harness runs the main and stability IL2CPP fixtures as two isolated
players. Each profile validates its expected hosts, actual backend, and required
assertion categories. Both include the real managed-reference graph, and
together they retain the combined fixture's assertions. A single High-stripping
IL2CPP player containing both fixture families failed during native scene loading
before the script-registration correction above.

The final October 10 package recheck also reproduced the failure in the isolated
stability profile. A never-imported project with the final package and an
independent fresh project with the earlier passing package both built
successfully, then exited 133 during native `level0` loading without producing a
managed report. This rules out an explanation based solely on the final runtime
binary or that project's import cache. The fixture had gained new Inspector-only
numeric asset types between the passing and failing runs. A third fresh project
with the final package passed every required stability and managed-reference
assertion when only `UnityDrawerNumericAsset` and `UnityDrawerNumericTypes` were
excluded from player compilation with `UNITY_EDITOR` guards. Those types exercise
rendered Inspector controls; no player host or assertion depends on them. The
tracked fixture now uses that boundary. The passing fresh player also passed a
second launch. Rebuilding the preexisting project with identical guarded source
and all four identical package DLLs still failed before producing its report. The
guarded boundary therefore does not establish a sufficient fix for every import
and build state. A separate clean project prepared from the tracked fixture also
passed with the final package: build exit 0, player exit 0, and every required
stability and managed-reference assertion true. Its report is
`tmp/lead-unity-redesign-final-il2cpp-stability-player.json`; native Editor
integration with the same guarded fixture still passes all 117 assertions,
including the numeric controls. These controls narrow the fixture/import
boundary without identifying the exact Unity native defect. Probe commands and
exact outcomes are retained in
`tmp/final-il2cpp-stability-probe-summary.md`.

A read-only comparison of the guarded cached failure and fresh passing control
found identical stripped package runtime, used-script listings, linker scene
inputs, and build settings. Their managed class listings also match. The failed
scene lists the stability host before the managed-reference host; the passing
scene lists them in the opposite order, with different generated script GUIDs.
Serialized values and payload counts match after removing generated IDs.

The generated native type inputs differ: the failing project's
`Library/PlayerDataCache/Linux642/ScriptsOnlyCache.yaml` and `SerializedTypes.xml`
omit every stability serialized class. The passing project includes 29 stability
classes. Its `level0` contains the stability payload type names; the failed file
does not. These are concrete generation differences, not a managed implementation
difference. Whether scene ordering, retained import state, or another Unity
generation detail causes the omission is still unproven. No runtime preservation
workaround was added for this discrepancy.

## GDB recheck on October 10

The existing failing `lead-unity-redesign-il2cpp-stability` player was run under
GDB on a virtual display. Normal garbage-collector `SIGPWR`/`SIGXCPU` signals were
passed through. The actual failure stops the `Loading.Preload` thread on
`SIGTRAP`, with `__pthread_kill_implementation`, `raise`, and 21 `UnityPlayer.so`
frames. The player log reports the same `level0` corruption and position error.
This is an explicit native trap following the reported scene-data error.

`GameAssembly.so` has debugger symbols, but `UnityPlayer.so` lacks debugging
information. Its relevant frames remain `??`, so this trace does not identify the
engine function, source line, or offending serialized field. There are no
`GameAssembly.so` or generated union callback frames on the captured crash stack;
that does not prove that no managed callback ran earlier during loading.

The reproducible command is `bash tmp/capture-unity-native-gdb.sh`. The backtrace
is saved in `tmp/scene-corruption-gdb-backtrace.txt`, and the matching player log
in `tmp/scene-corruption-gdb-player.log`, both under the workspace rather than
the system temporary directory.

This signature differs from the earlier, fixed High-stripping crash in
`tmp/unity-il2cpp-integration/high-stripping-backtrace.txt`: that GDB trace stopped
on `SIGSEGV` in `il2cpp::vm::Method::GetParamCount(method=0x0)`. The current trace
does not show that null-method failure.

## Committed-baseline comparison

The [package comparisons](unity-il2cpp-committed-baseline-comparison.md)
include a corrected test that keeps every current fixture file and the saved
failing scene fixed, swapping only package DLLs. Both the current and earlier
compatible snapshot reproduce the native corruption error in fresh projects.
Commit `976a91d` cannot compile those fixtures because it lacks their newer APIs
and collision support. The initial stash-and-retest comparison used older
committed fixtures and therefore did not establish a runtime verdict for the
current cases. It also adds a clean
Unity-only control with no Spire package: both one and two null object
managed-reference fields pass in IL2CPP with High stripping. Thus the earlier
two-field failure depends on the larger project's fixture/type metadata; two
such declarations alone are not a sufficient reproducer in a clean project.

## Unity 6.6 recheck on October 10

The subsequent [Unity-only callback investigation](unity-6.6-generic-struct-serialization-callback.md)
reproduces the Editor crash without Spire and verifies that the same source passes
in 6.5. It narrows the 6.6 failure to generic struct callback dispatch; the original
IL2CPP player-loading issue remains separate.

Unity `6000.6.5f1` (`3ff58d469c8a`), installed through Hub with Linux Mono and
IL2CPP modules, does not pass the complete native serialization validation.
The compiler/analyzer harness and IL2CPP behavioral tests pass; native integration
and player preparation crash in the Editor before producing their final reports.
All runs used Xvfb virtual displays and the unchanged package runtime with SHA-256
`bee22b42b2fd7563a901cb5544fa4445c04677ac6f7b89de21791c46c897ba4d`.

| Run | Unity 6000.6.5f1 result | Saved evidence under `tmp/` |
| --- | --- | --- |
| Complete Editor Mono harness | 2,266 passed; zero failures/skips; exact 2,127 portable identities and all 129 original behavioral methods matched | `unity-6.6-harness/harness-results.xml`, `unity-6.6-harness-cli.log` |
| IL2CPP behavioral player | 137 passed; zero failures/skips; all 129 originals and IL2CPP root-domain marker verified | `unity-6.6-il2cpp-behavioral/il2cpp-behavioral-results.xml` |
| Native graphics Editor integration | Editor exit 139; no final JSON report | `unity-6.6-editor/unity-integration-1.log` |
| Mono Full native player preparation | Editor exit 139; no player build report | `unity-6.6-mono-full/unity-integration-1.log` |
| IL2CPP Full native player preparation | Editor exit 139; no player build report | `unity-6.6-il2cpp-full/unity-integration-1.log` |
| IL2CPP MainBaselineAndManaged preparation | Editor exit 139; no player build report | `unity-6.6-il2cpp-main/unity-integration-1.log` |
| IL2CPP StabilityBaselineAndManaged preparation | Editor exit 139; no player build report | `unity-6.6-il2cpp-stability/unity-integration-1.log` |
| Frozen formerly failing stability scene | Editor exit 139 before completing its IL2CPP build | `unity-6.6-frozen/unity-profile-build.log`, `unity-6.6-frozen-build-stdout.log` |

The frozen control copied only Assets, Packages, and ProjectSettings into a
never-imported project, without Library or prior player builds. All 50 fixture
source/meta/scene files remained byte-identical after import and the failed build;
all package DLLs were retained. The saved scene was not regenerated. Unity updated
its project-version settings to 6000.6.5f1. Input hashes are saved in
`tmp/unity-6.6-frozen-inputs.json` and compared against
`tmp/fixed-current-fixture-comparison/fixture-before.json`.

The native Editor integration and four normal player preparation logs report:

```text
Caught fatal signal - signo:11 code:1 errno:0 addr:0x818
```

The Inspector run reaches the field-collision asset save/reload scenario before
the process crashes. Repeating the unchanged full run under GDB reproduces a
main-thread `SIGSEGV`. The fault address is Mono JIT code at `0x40001f00`; its
instruction reads `0x38(%rdi)` with `%rdi = 0x7e0`. Mono's `mono_pmip` resolves the
caller to `Spire.UnityIntegration.UnityGenericUnion<T_REF>.OnBeforeSerialize()`.
The immediate faulting method is unresolved. This locates a serialization callback
on the stack but does not establish whether generated code, Mono, or Unity's
callback invocation caused the invalid address.

The final trace and matching log are
`tmp/unity-6.6-editor-gdb-backtrace.txt` and `tmp/unity-6.6-editor-gdb.log`;
`bash tmp/capture-unity66-editor-gdb.sh` reproduces the capture. Normal Mono
`SIGPWR`, `SIGXCPU`, `SIG36`, and `SIG37` are passed through. Earlier captures
that stopped at a normal realtime GC signal are not crash evidence.

This `SIGSEGV` occurs in the Editor, whereas the 6.5 failure is a player loading
`SIGTRAP`. The 6.6 native players could not be produced, so these runs do not
establish whether 6.6 fixes the original `level0` corruption. No fixtures were
removed, assertions suppressed, or package source changed for this recheck.
