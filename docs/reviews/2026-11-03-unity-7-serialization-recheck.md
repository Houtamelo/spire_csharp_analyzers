# Unity 7 serialization crash comparison

The 6000.6.5f1 crash does **not** reproduce on the installed Unity
`7000.0.0a12` (`8751f7c83ed7`), Linux x86_64. Both the Spire-free engine repro and
the preserved Spire 4.7.6 generic-reference-union probe pass. The same inputs
still reproduce the serialization crash on `6000.6.5f1` (`3ff58d469c8a`).

The comparison used the exact four DLLs from the
[November 2 investigation](2026-11-02-unity-6.6-serialization-recheck.md), without
rebuilding Spire. The original engine repro and the two generated-union probe
classes retain their source hashes. The minimal `UnityGenericUnion<T>` declaration
was extracted from the preserved integration fixture and used in both editors.
Runtime logging and fresh-process read helpers were added only to disposable
projects under `tmp/unity7-crash-recheck-20261103`.

| Check | Editor runtime | Result |
|---|---|---|
| Original Spire-free `Snapshot<string>` callback repro | 7000.0.0a12, CoreCLR | `MRP_PASS`, exit 0 |
| Same engine repro | 6000.6.5f1, Mono | `MRP_BEGIN`, then repeated SIGSEGV; crash-handler loop stopped, exit 137 |
| Preserved `UnityGenericUnion<string>`, managed preparation and native save/reload | 7000.0.0a12, CoreCLR | All stages pass, exit 0 |
| Same generic-reference-union probe and DLLs | 6000.6.5f1, Mono | Managed preparation passes; `AssetDatabase.CreateAsset` triggers SIGSEGV at `0x39`, exit 139 |
| String-union asset read in a fresh Editor process | 7000.0.0a12, CoreCLR | Variant and `"reference"` payload preserved, exit 0 |
| `UnityGenericUnion<int>`, managed preparation and native save/reload | 7000.0.0a12, CoreCLR | Variant and `42` payload preserved, exit 0 |
| Integer-union asset read in a fresh Editor process | 7000.0.0a12, CoreCLR | Variant and `42` payload preserved, exit 0 |

## Runtime boundary and interpretation

The running Unity 7 Editor identifies itself as CoreCLR, with
`System.Private.CoreLib, Version=10.0.0.0` and `Environment.Version` `10.0.10`.
The 6.6 Editor identifies itself as Mono, with `mscorlib, Version=4.0.0.0` and
`Environment.Version` `4.0.30319.42000`. These observations come from managed
runtime evidence written by the actual Editor processes and their startup logs.

This confirms that the crash is absent from this Unity 7 configuration. The
runtime change prevents attributing that result solely to a particular engine
patch. Unity's [7.0 alpha release notes](https://unity.com/cn/releases/editor/alpha/7000.0.0a7)
explicitly remove support for the Mono Editor and Mono desktop players. Its
[Unity 7 backend guide](https://docs.unity.com/en-us/engine/7000.0/manual/programming-environment/scripting-backends/intro)
lists CoreCLR and IL2CPP and confirms that Mono has been removed as a scripting
backend. Player backend settings do not select the Editor runtime.

The publisher reports Unity issue `25665` as fixed in `7000.0.0a5`. Per the agreed
decision, the passing Unity 7 probes support the working assumption that this
issue has the same underlying cause. Exact issue attribution and a `6000.7.x` backport
remain unconfirmed. Keep the measured 6000.6.5f1 limitation until a relevant 6.x
build passes the repro.

The checks cover native Editor asset serialization. They do not establish full
Unity 7 package support, rendered Inspector behavior, or Player/backend support.
The subsequent [IL2CPP validation](2026-11-03-unity-7-il2cpp-validation.md)
passes all 100 original behavioral tests and 60 native runtime/serialization
checks on Linux. Its full, unfiltered NUnit run passes 107 cases after removal
of an unrelated subprocess capability probe.

## Evidence and launch details

The [verified manifest](../../tmp/unity7-crash-recheck-20261103/manifest.json)
records seven primary results, runtime identities, source hashes, DLL hashes,
log paths, and log hashes. The four DLLs remain byte-identical after the runs:

| Assembly | SHA-256 |
|---|---|
| Houtamelo.Spire.dll | `ec840fc5b187688b814d628c038446c981c6954ea79e3f1e6e1a40c9faa21c3f` |
| Houtamelo.Spire.Analyzers.dll | `0ba0fb33f1da7604da3c6625d32e0cdb74aaf224ae750595944af8c83809c636` |
| Houtamelo.Spire.PatternAnalysis.dll | `4f5a207920bd62ad1d86ec5a7eb1c4d25fa87229d2b584e5a977e45dbaf3cd77` |
| Houtamelo.Spire.CodeFixes.dll | `90fde65a1f02a8e57efce3bc4a7ada6eaa022ff1dbc0b48f2a74dcff737450a4` |

Both main editor comparisons used the repository's existing
`UnityRunner.MethodAsync` through a
[temporary invocation bridge](../../tmp/unity7-crash-recheck-20261103/Runner/Program.cs).
This retains the established private Xvfb, process environment, GI-cache location,
and process-group management while avoiding the integration CLI's automatic
replacement of the preserved package DLLs.

Initial custom-launcher 6.6 processes exited SIGSEGV during startup, before the
probe entrypoints. Those failures are excluded from the serialization results.
The established launcher reached the intended entrypoints and reproduced the
known native failures. The standalone 6.6 repro's repeated fatal-signal handler
was stopped only after the log confirmed `MRP_BEGIN` and SIGSEGV; its exit 137 is
therefore a deliberate termination after a crash, rather than a natural exit.
The Spire 6.6 probe exited naturally with 139.

The setup, initial launcher, and evidence-checking scripts are retained beside
the projects. No production implementation or tracked C# fixture was changed.
