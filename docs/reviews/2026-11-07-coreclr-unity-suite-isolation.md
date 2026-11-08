# CoreCLR/NuGet and Unity suite isolation

The five original .NET test projects from `main` remain: analyzers, source
generators, PatternAnalysis, compile-time behavioral tests, and MSBuild global
configuration. Retired layout-specific cases describe removed APIs; their
replacement coverage uses the current variant-payload representation. Restoring
obsolete layout implementations is not needed to restore the CoreCLR suite.

The portability regression was Unity-reference tests being mixed into the
generator project. The new CoreCLR entry point keeps portable cases active and
filters only tests explicitly requiring installed Unity metadata. Mixed
snapshot/formatting tests retain separate portable and Unity assertions. The
Unity Mono discovery/execution contract remains intact.

## Environment contract

`System.Serializable` propagates ordinary payload markers in every target.
Unity serialization and validation require the target compilation's
`UNITY_5_3_OR_NEWER` symbol; serialization output also requires accessible runtime
APIs. Drawers and shared Editor support additionally require `UNITY_EDITOR` and
accessible Editor APIs. Merely referencing Unity metadata activates none of
these features. Assertions follow the same environment boundary. Former-name
payload alias validation remains active outside Unity to prevent corrupt output.

Tests cover ordinary, readonly, ref-struct, and record declarations; missing
runtime references; metadata without symbols; runtime without Editor mode;
actual Editor output; and one generator driver reused across CoreCLR, Editor,
and CoreCLR compilations.

## Validation

- `bash tools/test-coreclr.sh`: 2,460 passed, zero failed/skipped, .NET 10.0.401
  SDK. `SPIRE_UNITY_EDITOR` pointed to a nonexistent path. Counts: 926 analyzers,
  987 generator tests, 428 pattern tests, 100 behavioral tests, 11 configuration
  tests, and eight packed-package/publishing checks. Repeated successfully after
  the native Unity run, confirming restore/build switches back from the Unity
  test target without stale-framework failures.
- `bash tools/test-unity-metadata.sh`: 85 passed, zero failed/skipped, using real
  Unity `6000.5.6f1` runtime and Editor assemblies with the .NET 10.0.401 SDK.
- Native Unity `6000.5.6f1` Editor Mono harness on Linux: 2,731 passed, zero
  failed/skipped. All 2,614 applicable shared xUnit identities and all original
  behavioral cases executed; eight Windows-only identities were independently
  excluded. Preparation used the installed .NET 11 preview SDK; execution ran
  in Unity's Mono runtime.
- Development-tool suite on CoreCLR: 180 passed, eight Windows-only cases
  skipped on Linux, zero failed, .NET 10.0.401 SDK.

The first complete Mono run executed all 2,731 cases and exposed ten tooling
failures: a JsonArray span-constructor call unavailable in the loaded runtime,
the discovery assertion for the deliberately split no-support theory/fact, and
repository discovery in the new WebGL preparation tests. The fixes retain the
old JSON array API, explicitly verify both discovery shapes, and honor the
configured repository root inside Unity. No test is removed or newly skipped.

The next complete run passed 2,730 cases and exposed one Xvfb cleanup race:
after a successful owned process-group kill, a redundant root kill could throw
on Mono and mask the callback's original exception. Successful group termination
now avoids that second kill; callers still wait for exit. The real Xvfb
exception/cleanup regression exercises 16 independent server lifecycles without
retries or mocks. The complete CoreCLR tooling suite passes after this change.

The NuGet checks pack all three packages, inspect their DLL references without
loading them, and install the bundle/standalone analyzers into isolated ordinary
.NET consumers. Complete consumers compile and run; incomplete matches fail with
SPIRE009. No Unity references, callbacks, wrappers, drawers, or Unity-only
diagnostics leak into the ordinary consumer. Real publishing fixtures also prove
that a failed portable test prevents packaging.

CI now runs the portable suite on pushes/pull requests and before NuGet
publication. No remote workflow, package publication, or branch promotion was
performed in this change. Native player and Inspector workflows remain separate
gates; metadata compilation alone does not validate their runtime behavior.

Evidence: `/tmp/spire-coreclr-isolation-final.log`,
`/tmp/spire-coreclr-isolation-after-unity.log`,
`/tmp/spire-unity-isolation-metadata.log`, and
`/tmp/spire-unity-isolation-harness-clean.log`. The complete Mono report is
`tmp/unity-coreclr-isolation-harness/harness-results.xml`; exact discovery,
platform exclusions, and execution checks all passed. The independently discovered
[SPIRE014 short-circuit guard limitation](../issues/2026-11-07-coreclr-short-circuit-variant-guard.md)
was recorded for follow-up without suppressing the diagnostic. It was resolved
on 2026-11-08 by the receiver-aware CFG implementation; the NuGet consumer again
uses the original short-circuit expression. The linked issue records the new
CoreCLR and Unity validation results.
