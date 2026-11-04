# Unity 6.6 generic struct serialization callback regression

November 3 comparison: the original engine repro and the preserved Spire
generic-reference-union probe pass on `7000.0.0a12` with CoreCLR, including a
fresh-process union asset read. The same inputs still crash on 6000.6.5f1 with
Mono. See the [Unity 7 results](../reviews/2026-11-03-unity-7-serialization-recheck.md).

November 2 recheck: the current payload representation passes 126 native Editor
write checks and 87 fresh-process read checks on `6000.6.5f1`. The isolated
`UnityGenericUnion<int>` round trip also passes, but `UnityGenericUnion<string>`
still crashes during `AssetDatabase.CreateAsset`, after managed preparation
succeeds. The identical string probe passes on `6000.5.6f1`. See the
[current-package results](../reviews/2026-11-02-unity-6.6-serialization-recheck.md).
The October investigation below records the original engine repro; its outcome
does not mean that every current Spire serialization case fails on 6.6.

Investigated October 10, 2026, on Linux x86_64. A project without Spire reproduces
the Editor serialization crash in `6000.6.5f1` (`3ff58d469c8a`), while identical
callback source saves and reloads successfully in `6000.5.6f1` (`0e0577a1a2ac`).
The failure concerns managed-reference serialization of a generic struct
instantiated with a reference type. A 26-line, single-file project now reproduces
it with a null `[SerializeReference] object` field and a callback that only calls
`Debug.Log("callback")`. No payload allocation, reflection, helper method, or Spire
code is necessary. No safe package workaround is established.

This is separate from the [6.5 IL2CPP player-loading failure](unity-il2cpp-opaque-managed-reference-slots.md).
The 6.6 crash prevents the complete native serialization fixture from building;
it does not establish a 6.6 verdict for the earlier player corruption.

## Minimal reproducible project

The project is [repros/UnityGenericStructCallback](../../repros/UnityGenericStructCallback).
Its Unity inputs are exactly three files:

- [Assets/Editor/ProbeAsset.cs](../../repros/UnityGenericStructCallback/Assets/Editor/ProbeAsset.cs): the asset host, generic struct, and Menu/batch entry point.
- [Packages/manifest.json](../../repros/UnityGenericStructCallback/Packages/manifest.json): empty dependencies; Unity adds its builtin packages on import.
- [ProjectSettings/ProjectVersion.txt](../../repros/UnityGenericStructCallback/ProjectSettings/ProjectVersion.txt): `6000.6.5f1`.

The local `.gitignore` excludes generated project files. No scene, saved asset,
payload class, custom drawer, unsafe code, plugin DLL, or player build is included.
The managed-reference field stays null; its serialization metadata is enough to
select the failing path.

1. Open a fresh copy with Unity `6000.6.5f1` and wait for script compilation.
2. Select **Repro > Create asset**. The batch entry point is `ProbeAsset.Reproduce`.
3. Expected: `callback`, then `MRP_PASS`, and a new `Assets/Probe.asset`.
   Actual on Linux: `MRP_BEGIN` followed by native `SIGSEGV`; no `MRP_PASS`.

`Assets/Probe.asset` is the output path, not an input asset. `CreateInstance`
constructs the host in memory; `AssetDatabase.CreateAsset` serializes it into a
new file. The 6.6 fault prevents that file from being created. The passing 6.5
control creates it; the project ZIP intentionally starts without it.

The final source also passes on `6000.5.6f1` with only `ProjectVersion.txt` changed
to that version. All recorded runs use batch mode under Xvfb. Some failing logging
callbacks repeatedly enter Unity's fatal-signal handler; the runner uses
`timeout --kill-after=10s 120` so that failure cannot leave a test Editor running.
A timeout after a recorded SIGSEGV is distinguished from a timeout before the
reproduction begins.

| Reduction/control | Result | Evidence under `tmp/` |
| --- | --- | --- |
| Final literal logging callback, `Snapshot<string>`, 6.6.5 | SIGSEGV after BEGIN; fatal handler repeats until timeout (exit 124) | `unity-mrp-single-file-log-only/probe.log` |
| Fresh extracted final ZIP, 6.6.5 | Same SIGSEGV after BEGIN, no PASS; exit 124 after timeout | `unity-mrp-archive-66/probe.log` |
| Identical final C# source, 6.5.6 | `callback`, PASS, exit 0 | `unity-mrp-final-65/probe.log` |
| Fresh extracted final ZIP with only project version changed, 6.5.6 | `callback`, PASS, exit 0 | `unity-mrp-archive-65/probe.log` |
| Final source with `Snapshot<int>`, 6.6.5 | `callback`, PASS, exit 0 | `unity-mrp-final-int/probe.log` |
| Earlier single-file `Debug.Log(typeof(T))` callback, 6.6.5 | SIGSEGV, exit 139 | `unity-mrp-single-file-66/probe.log` |
| Same type-logging source, 6.5.6 | `System.String`, PASS, exit 0 | `unity-mrp-single-file-65/probe.log` |
| Remove the managed-reference field from type-logging source, 6.6.5 | Callback still logs `System.String`; PASS, exit 0 | `unity-mrp-single-file-no-field/probe.log` |
| Replace the callback body with `this = default`, 6.6.5 | Asset creation succeeds, exit 0 | `unity-mrp-single-file-default/probe.log` |
| Replace `typeof(T)` with `typeof(string)`, 6.6.5 | SIGSEGV; fatal handler repeats; test process stopped (exit 137) | `unity-mrp-single-file-fixed-type/probe.log` |
| Remove unused fields and all other variants from original reproducer, 6.6.5 | SIGSEGV, exit 139 | `unity-mrp-no-unused-fields/probe.log` |
| Remove `[SerializeReference]` from that reduced custom-payload field, 6.6.5 | Payload populated, PASS, exit 0 | `unity-mrp-inline-payload/probe.log` |

These controls establish that the null managed-reference field and reference-type
generic instantiation matter. They disprove a trigger limited to helper methods,
self-returning methods, custom payload types, or reflection of the type parameter.
They do not prove every possible callback body fails: resetting the struct succeeds.

`tmp/unity66-generic-struct-callback-mrp.zip` packages the three inputs plus
`Reproduction.txt`. It supersedes the earlier archive with four control types.
The final C# SHA-256 is
`a20c40ff10819e8de1688e6e542410182dd56a4f676d8f39cd525b69252907f5`.
The archive SHA-256 is
`22c8e48f3b12d97ebc58505719fcaa5fac0b9928c55b6cf9bd4700c0f29a8253`.
Fresh extracted copies reproduce the 6.6 native fault and create the asset on 6.5.
Their C# hashes
match the repository source. The 6.5 asset records `payload: {rid: -2}` (null).

## Controlled probes

The original asset probes use real Unity asset creation, saving, unloading,
importing, and reloading on Xvfb. Passing asset controls verify the reconstructed
managed payload; the ordinary managed-call control deliberately avoids serialization.
None imports Spire or third-party DLLs. Each source-changing control uses its own
project. The script files match their ScriptableObject class names.

| Control | Editor | Result | Evidence under `tmp/` |
| --- | --- | --- | --- |
| Reference-instantiated struct, callback calls a self-returning helper | 6.5.6 | Pass | `unity-generic-probe-return-reference-65-clean/probe.log` |
| Same struct and callback | 6.6.5 | Exit 139, SIGSEGV during CreateAsset | `unity-generic-probe-return-reference/probe.log` |
| Same returning struct instantiated with `int` | 6.6.5 | Pass | `unity-generic-probe-return-value/probe.log` |
| Reference-instantiated struct updates a nongeneric payload directly | 6.6.5 | Pass | `unity-generic-probe-direct-reference/probe.log` |
| Reference-instantiated struct calls a void instance helper | 6.6.5 | Exit 139, SIGSEGV | `unity-generic-probe-void-reference/probe.log` |
| Explicit interface callback calling the returning helper | 6.6.5 | Exit 139, SIGSEGV | `unity-generic-probe-explicit-reference/probe.log` |
| Callback marked NoInlining and NoOptimization | 6.6.5 | Exit 139, SIGSEGV | `unity-generic-probe-noopt-reference/probe.log` |
| Callback directly allocates a nested generic payload | 6.6.5 | Exit 139 | `unity-generic-probe-generic-payload-reference/probe.log` |
| Generic class callback calling a helper | 6.6.5 | Pass | `unity-generic-probe-class-reference/probe.log` |
| Normal managed call to the failing struct callback, without asset serialization | 6.6.5 | Pass; payload allocation verified | `unity-generic-probe-managed-only/probe.log` |
| Normal managed call followed by CreateAsset, under GDB | 6.6.5 | Managed call passes, then the serialization callback crashes | `unity-generic-probe-managed-first-reference/gdb.log`, `gdb-backtrace.txt` |

The void-helper failure disproves a trigger limited to returning a large struct.
The generic-payload failure shows that merely inlining the generated callback's
preparation code is insufficient: constructing its generic payload still needs
the context. Explicit callbacks and optimizer attributes did not avoid the failure.

An initial 6.5 control copied an already upgraded package manifest and failed UPM
resolution on the 6.6-only `com.unity.modules.tetgen`; it supplies no runtime verdict.
The passing 6.5 control instead uses a fresh project with the same C# and its
matching builtin package manifest. An initial managed-first launch also died before
the probe began; its GDB repeat reached and completed the managed call before
reproducing the serialization failure. Debugger stops at normal GC/realtime or pipe
signals are not treated as crash evidence.

## Invocation evidence

The full integration GDB trace resolves its caller to
`UnityGenericUnion<T_REF>.OnBeforeSerialize`. At entry, the shared method saves
`%r10`, then passes that value to a Mono generic-context lookup trampoline.
The captured value is `0x7e0`, which is not a valid context address. The trampoline
reads `0x38(%rdi)`, producing the observed `0x818` fault. The earlier independent
project likewise faults in the callback's context lookup; normal C# invocation
of that same callback succeeds.

The relevant disassembly is in `tmp/unity-6.6-editor-detailed-backtrace.txt`;
the independent trace is in
`tmp/unity-generic-probe-managed-first-reference/gdb-backtrace.txt`.

The installed 6.6 `UnityEngine.CoreModule.dll` contains
`UnityEngine.Serialization.SerializationBackendManagedCommands`. Its
`ProcessGatherInvokeOnBeforeSerializeStruct` invokes a stored method address as
`delegate*<ref byte, void>`. Its other before/after struct callback paths use
the same pointer signature. Class callbacks instead invoke
`ISerializationCallbackReceiver` through the managed interface. This class is
absent from the inspected 6.5 runtime modules. The decompiled diagnostic copy is
`tmp/unity66-serialization-managed-commands.cs`; it is not a shipped replacement DLL.
The inspected CoreModule SHA-256 is
`84cdbeefed4ba71c5577b3240acc34ff8f61b6fdb31bde8a33c1a9b78dd62bfc`.

These observations strongly indicate that 6.6's struct callback path calls a
shared generic entry without supplying its required context. The native code
constructing the stored pointer is not available here, so the exact faulty
pointer-construction step remains an inference. Mono documents the context and
calling-convention requirements of shared generic code in its
[runtime explanation](https://www.mono-project.com/docs/advanced/runtime/docs/gsharedvt/).

## Earlier archive and remaining boundary

`tmp/unity66-generic-callback-repro.zip` contains an 11 KB Unity-only project,
the four original controls, instructions, and input hashes. It contains no
Library, player builds, Unity DLLs, or Spire package. Use a fresh extracted copy
for each Editor/case because upgrading rewrites the builtin package manifest.
The launcher used locally is `bash tmp/run-generic-callback-probe.sh`; the
archive includes a portable command in its README. Windows/macOS were not tested.
The extracted archive passes its reference control on 6.5. The 6.6 archive
verification encountered an additional failure before the probe began: its first
launch exited 139 during startup, and a repeat stalled at ModeService loading.
That stalled test Editor was terminated. Linux ptrace policy prevented attaching
GDB to that already running process; the callback traces above used GDB as the
Editor's parent and were captured successfully. These startup outcomes do not
replace the independently reproduced CreateAsset callback failures or establish
an archive-run verdict for the callback itself on 6.6.

No package implementation, tracked test fixture, approved serialization layout,
or per-union storage was changed during these probes. There is no proven safe
generator workaround yet. Engine patching or restricting generic serialization
would require a separate decision; this evidence does not justify either change.
## Submission to Unity

Submitted through Unity CLI `1.0.0-beta.13` using the existing signed-in account
after explicit authorization. The command used `--area editor`, reproducibility
`always`, and attached the MRP ZIP and validation notes. The CLI also attached its
standard diagnostics/logs. It exited 0 and returned:

```json
{
  "success": true,
  "command": "bug",
  "data": {
    "submittedAt": "2026-10-11T01:53:18.993Z",
    "logsAttached": true
  },
  "errors": [],
  "warnings": []
}
```

The exact description, validation attachment, and receipt are saved as
`tmp/unity66-bug-report-description.txt`, `tmp/unity66-mrp-validation.txt`, and
`tmp/unity66-bug-report-submission.json`. Neither the CLI response nor its recent
local logs contained a report ID. Unity documents delivery of the ID in its
[confirmation email](https://unity.com/releases/editor/qa). No confirmation email
was inspected, and no public issue-tracker entry or QA verdict is established yet.
