# Legacy Unity fixes and LTS follow-up

The [November 5 current-package recheck](2026-11-05-unity-2022-2023-current-validation.md)
passes all 2,689 current Editor cases on both 2022 and 2023 and completes the
Unity 2023 native player gate. The counts and package scope below are historical.

The native Unity 2022 failure recorded here precedes the
[concrete-wrapper repair](2026-11-04-concrete-unity-wrappers.md). That follow-up
passes native write/read checks for non-generic unions; generic unions remain
unsupported by Unity 2022's serializer. Results below retain their original
package and test scope.

This follows the [initial Unity 2020–2023 validation](2026-11-04-unity-2020-2023-validation.md).
The non-extended installations are `2021.3.45f2` and `2022.3.62f3`.
The full Editor harness passes on Unity 2022 and 2023 after repairs, but Unity
2022 fails native persistence and Unity 2021 cannot load Spire's Roslyn baseline.
The published minimum remains `6000.5.6f1`.

## Final results

| Gate | Unity 2021.3.45f2 | Unity 2022.3.62f3 | Unity 2023.2.22f1 |
| --- | --- | --- | --- |
| Full Editor harness | No tests executed; compiler startup timed out | **2,649/2,649 passed** across partitions and corrective reruns | **2,649/2,649 passed** across partitions and corrective reruns |
| Exact portable identities | Not executed | All 2,532 names match independent discovery | All 2,532 names match independent discovery |
| Original runtime Facts | Not executed | 100/100 passed through Unity's compiler | 100/100 passed through Unity's compiler |
| General native integration | Compilation blocked | 10/11 passed; serialized payload field missing | 11/11 passed |
| Native serialization Write | Blocked | Fails at check 41; generic managed references rejected | 126 checks passed |
| Fresh-process Read | Blocked | Diagnostic read fails at check 4; payload values did not restore | 87 checks passed |
| Recovery / accessor assertions | Blocked | Not accepted after failed Write preparation | 59 / 9 checks passed |

The 2,649 distinct Editor checks comprise 2,532 portable cases, 100 original
runtime Facts, 12 adapter contracts, and 5 compiler probes. No cases are skipped.
Portable Roslyn tests use their test-only Roslyn 4.10 dependencies; Unity's own
compiler builds the 100 runtime Facts and native Editor fixtures. A passing
portable harness does not replace native asset-persistence acceptance.
`acceptance-proof.json` merges individual results by full NUnit identity, applies
later corrective reruns, and verifies exact portable-name coverage. It does not
substitute XML root counters for individual outcomes.

The generated-source compatibility baseline was refreshed only for the shared
drawer template changed by the Unity 2022 repair. The corresponding fixture,
SHA-256, and approved-change description were updated; all three Q09 exact-output
checks pass both on .NET and in the Unity 2022 rerun.

The Unity `6000.5.6f1` control also passes all 11 native integration checks,
including the modern focus API path. Rendered Inspector and legacy player
acceptance remain separate, incomplete gates; no lower minimum is announced.

## Unity 2021 compiler boundary

The non-extended Editor starts without the previous licensing rejection. Its full
harness and native integration attempts stall in shared-compiler startup, with
bounded timeouts and no executed cases. Prepending its bundled runtime to the
process-local PATH does not resolve the Editor attempt.

Running the same installation's compiler directly, without `/shared`, reports
`3.9.0-5.21120.8 (accdcb77)`. Compiling its actual `Assembly-CSharp.rsp` fails with
`CS8032`: Spire requires `Microsoft.CodeAnalysis, Version=4.3.0.0`, which that host
cannot load. This compiler diagnostic is independent evidence, not an Editor
suite pass. See `2021-compiler-proof.json` for the exact invocation and output.
Supporting this version requires a separate Roslyn backport.

## Repairs

The first repair pass retained the four package DLL hashes from the initial
validation and repaired metadata, contents, and the development harness. Unity
2022 then exposed a separate generated Inspector API incompatibility, described
below; its correction requires a new analyzer DLL and fresh validation.

- Serialize `Any` as the platform mapping key in plugin metadata. Actual Unity
  importer checks verify that the runtime is enabled for Editor/Linux players
  and compiler-only DLLs remain excluded. With Any Platform enabled, check the
  resolved `PluginImporter.GetImporters` list and exclusion flags; a platform's
  explicit compatibility flag alone does not represent the effective policy.
- Include the showcase's authored Editor response file and its metadata; remove
  the broken packaged SPIRE009 link and update stale README expectations.
- Replace the parser tests' ignored Bee-cache dependency with captured actual
  Unity 2023 compiler/project output, retaining byte hashes and provenance.
- Locate scripting attributes in `UnityEngine.CoreModule.dll` when the Editor
  does not provide a separate `Unity.Scripting.dll`.
- Compile dynamically executed tests against the actual runtime's framework and
  JSON assemblies. Semantic-only tests retain the copied .NET reference pack.
- Reuse the existing `IsExternalInit` compatibility tree in the record conversion
  fixture when compiling against an older Mono runtime.
- Supply the copied framework references to the no-Spire-reference test instead
  of downloading a reference pack through Mono's TLS stack. Explicitly assert
  that the Spire attribute symbol remains absent from that compilation.
- Use the compiler's preview mode for test-only raw-string sources; retain the
  consumer C# 9 profile.
- Resolve the public Label payload through the union's public `label` property
  type instead of an obsolete generated top-level type name.

## Unity 2022 Inspector compatibility

The initial LTS run compiles and passes all 100 original runtime Facts, but 21
generator tests fail because the shared drawer directly calls
`FocusController.IgnoreEvent`. That public method is absent in Unity 2022;
native Editor fixture compilation consequently stops too. This is a package
compatibility defect, separate from the harness failures.

The shared drawer now discovers the public method at runtime. Older Editors use
the existing event focus-processing state instead. Both paths retain focus
suppression for consumed change events. A new headless native integration check
uses actual Unity events/controllers to cover handled and unattached events.
It does not open an Inspector window.

## Test Framework terminal failure

Unity Test Framework 1.4.5 can retain the previous yield object when its final
Task completes synchronously. It appends `EditMode test can only yield null`
to the last individual result after the aggregate result has been calculated.
Therefore an Editor exit code of zero or passing XML root is insufficient.

The identical 42-case metadata subset recorded 41 passes plus this terminal
failure with the original adapter. The framework's
`disableNestedEnumeratorBugfix` setting did not help. Yielding once after
successful adapter execution produced 42/42 passes. Both adapters now do this;
new real-Editor contracts cover deferred synchronous completion and propagation
of original exceptions. All 60 focused regression checks pass.

## Host verification

After the repairs, the normal .NET runs pass:

| Scope | Passed |
| --- | ---: |
| DevTools filesystem/process/preparation integration | 128/128 |
| Initialization global-enforcement helper regressions | 64/64 |
| Affected generator/reference helpers | 47/47 |
| Generator snapshots | 102/102 |
| Original compile-time behavioral tests | 100/100 |

The portable bundle builds successfully. Changed C# files were passed through
`dotnet format whitespace --include`. The SDK selected on this host is
`11.0.100-preview.3.26207.106`; tests still target their configured frameworks.
The final shipped-content/documentation rerun passes 29/29 checks, and the scoped
diff whitespace check passes. No owned Unity Editor, Bee backend, or Xvfb process
remains after validation.

## Resource limits and evidence

All heavy operations run sequentially. Each uses CPU affinity 22/23, a two-core
quota, Unity's two-worker setting, a 3 GiB systemd memory maximum with swap
disabled, and an independent watchdog above 2 GiB aggregate RSS. Managed build
processes additionally use a 512 MiB heap cap and disable shared compilation.

The initial combined post-fix harness hit the RSS guard before saving results.
It is incomplete, not a pass. Subsequent partitions contain at most 220 portable
cases and start a fresh Editor process. Acceptance compares their executed names
with all 2,532 independently discovered portable identities, then separately
checks all 100 original behavioral Facts, adapter contracts, and compiler probes.
No assertion is removed, ignored, or converted to an expected pass.

Artifacts are under `tmp/unity-legacy-fixes-20261104/`:

- `package-hashes.json`: frozen package inventory.
- `partitions.json`: exact expected names and filters for every partition.
- `matrix-progress.json`: individual outcomes and resource records.
- `final-progress.json`, `extra-progress.json`, and `acceptance-proof.json`:
  corrected-package reruns and the final distinct-case coverage proof.
- Per-Editor `harness/*.xml`: original individual NUnit results.
- Per-operation `*.resources.json`: actual exit, peak RSS, limits, and stop reason.
- Per-Editor `integration` and `native`: native Editor reports and logs.

The rendered Inspector memory incident remains unresolved. No graphical
Inspector run is repeated in this headless follow-up. Player results must be
reported separately from Editor results.

Unity 2022 persistence is a confirmed blocker, documented in the
[generic managed-reference issue](../issues/2026-11-04-unity-2022-generic-managed-reference-payloads.md).
For the diagnostic fresh read, the exact failed-Write asset bytes were copied to
the baseline path solely to satisfy that phase's file-existence prerequisite.
Its payload assertions were unchanged and fail immediately; this is not a
successful Write or a relaxed acceptance gate.
