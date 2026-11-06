# Current Unity 2022/2023 validation

The current full Editor harness passes on both Linux x64 installations:
`2022.3.62f3` and `2023.2.22f1`. Unity 2023 also passes actual Mono and IL2CPP
player checks after repairing the test harness's startup quit timing. Unity 2022
also passes both player backends with the unsupported native generic serialization
fixture correctly excluded. Generic runtime coverage remains active.

The package version and supported minimum are unchanged. This updates the
[earlier LTS results](2026-11-04-unity-lts-followup.md) and extends the
[concrete-wrapper validation](2026-11-04-concrete-unity-wrappers.md).

The final three-version audit also reruns the same complete Editor harness on
`6000.5.6f1`: all 2,689 cases pass, with exact independent identity, execution,
original Fact, and platform-exclusion matching. Evidence is in
`tmp/unity-all-three-12g-20261105/6000-editor-proof.json` and the corresponding
`6000.5.6f1/harness/full-editor.xml`. The run exits 0 in approximately 416 seconds
and peaks at 3.12 GiB sampled aggregate RSS under the same two-core/12 GiB limits.

Thus the headless Editor harness passes on all three exact tested
versions. Actual native Mono/IL2CPP players also pass within each version's
documented scope: 59 checks per backend on 2022, 60 on 2023 and 6000.5.6f1.
Those runs did not include rendered Inspector interaction. The subsequent
[rendered validation](2026-11-05-rendered-inspector-validation.md) finds and fixes
two Editor drawer bugs and passes on all three Editors. It records the later
analyzer DLL and exact revalidation scope; the package minimum is unchanged.

## Results

| Gate | Unity 2022.3.62f3 | Unity 2023.2.22f1 |
| --- | --- | --- |
| Current full Editor harness | **2,689/2,689 passed** | **2,689/2,689 passed** |
| Independent portable discovery/execution | All 2,567 cases match | All 2,567 cases match |
| Original runtime Facts, compiled by Unity | 100/100 passed | 100/100 passed |
| New non-generic native serialization Write / fresh Read | 67 / 44 passed | 67 / 44 passed |
| Applicable broader native Write / Read / Recovery / Accessors | 125 / 86 / 59 / 9 passed | 128 / 89 / 59 / 9 passed |
| Applicable native Mono and IL2CPP player builds | Both pass with High stripping, zero build errors | Both pass with High stripping, zero build errors |
| Actual native Mono / IL2CPP players | **59 checks each, clean exit 0** | **60 checks each, clean exit 0** |

The full Editor count comprises 2,567 portable cases, 100 original runtime Facts,
17 adapter contracts, and 5 compiler probes. Both runs execute without a test
filter in one Editor invocation. Every runnable case passes; eight Windows-only
cases are explicitly platform-inapplicable and match independent discovery.
The increased count replaces the previous 2,649-case inventory; it includes
new wrapper and test-infrastructure coverage.

Proof checks compare exact case identities, portable execution names, original
Fact names, and platform exclusions. Portable compiler tests use their test-only
Roslyn 4.10 dependencies; Unity's compiler builds the runtime Facts and native
fixtures. Editor harness results do not substitute for native persistence or
player execution.

## Player exit repair

The first Unity 2023 Mono and IL2CPP players wrote passing 60-check reports but
then crashed with SIGSEGV. Neither run was accepted as a clean player pass.
A matching-symbol native backtrace locates the crash in
`PhysicsManager::GetDefaultPhysicsSceneHandle`, called by `UnityScene` and
`RuntimeSceneManager` destructors during `RuntimeCleanup`. Providing Xvfb did
not resolve it.

A minimal project with no Spire package or union types reproduces the failure.
The same Mono binary crashes when `Application.Quit(0)` runs synchronously from
`AfterSceneLoad`, but exits normally when quitting is deferred to `Update`.
This isolates an engine startup/cleanup lifecycle problem in the test launcher.

The native test fixture now schedules its original success/failure exit code on
the first update. All assertions, report generation, and serialization cases
remain intact. No product code or Unity-version branch was added.
After rebuilding, both actual Unity 2023 players pass all 60 checks and exit 0.
The Unity `6000.5.6f1` regression control likewise rebuilds both backends with
High stripping and passes all 60 checks on each executable with clean exits.
The minimal control also runs the actual scheduler with requested codes 0 and 1;
both process exits match, preserving failure reporting as well as success.

## Unity 2022 boundary

The original unrestricted native player builds failed for both backends. Unity logged that
`SerializedGenericTypes1.SerializedPayloads.ValueSerializedPayload<System.Int32>`
could not be stored through `SerializeReference`, then reported player build errors.
Those failed builds were not recorded as runtime passes.

The fixture now marks its generic field `[NonSerialized]` only on Unity 2022 and
omits the corresponding native persistence assertions. Real `SerializedObject`
checks verify that the field is absent there and remains present on newer
Editors. YAML checks likewise prove that its field and generic payload records
are absent on 2022 and present on 2023/6000. Reports identify the case as
`not-applicable-unity-2022` through explicit applicability fields.

In-memory generic factory, type-loading, hydration, and accessor tests remain
active. Fresh native write/read, recovery, accessors, and both High-stripping
players pass within the documented support boundary. Modern controls retain
generic persistence and all 60 player checks. Rendered Inspector acceptance
passes in the separate follow-up linked above; the package minimum is unchanged.

One modern Mono control build encountered a native UnityLinker exit 139. An
unchanged Mono-only retry through the same private build method succeeded;
the resulting player passed all 60 checks with exit 0. The first IL2CPP build
and actual player passed. The failed attempt is preserved in the evidence.

## Resource limits and artifacts

All heavy operations run sequentially on CPU affinity `22,23` with quota 200%,
two Unity workers, `MemoryMax=12G`, `MemoryHigh=10G`, swap disabled, and a 12 GiB
aggregate RSS watchdog. The blanket per-process managed-heap limit is removed.
The complete Editor runs take approximately 426 and 434 seconds, with sampled
aggregate RSS peaks of 1.76 and 1.83 GiB respectively.

Evidence root: `tmp/unity-lts-current-12g-20261105/`.

- `2022-editor-proof.json`, `2023-editor-proof.json`, and the per-Editor
  `harness/full-editor.xml`: exact coverage and individual outcomes.
- `package-provenance.json`: both prepared harnesses use the same four frozen
  product DLLs as the wrapper validation.
- `acceptance-summary.json`: accepted Editor and player outcomes.
- `*-fixed-report.json`, `*-fixed.resources.json`, and `*-fixed-build-report.json`:
  actual backend reports, process exits, build results, and limits.
- `2022-build-report.json`: the failed unrestricted player build.
- Original `2023-*-player-report.json` and logs preserve the passing assertions
  followed by native crashes; they are not accepted player results.
- `2023-mono-native-backtrace.limited.log`, `quit-control/`, and
  `quit-control-results.json`: native stack and the package-free timing control.
- `scheduler-code-results.json`: actual scheduler success/failure exit propagation.

A separate quoting error in `WindowsProcessFixture` initially blocked the shared
bundle build. Its embedded C# source now uses a valid raw string literal; the
portable build and both complete Editor runs pass afterward. Windows-only test
execution remains outside this Linux recheck.

The applicability follow-up is archived separately under
`tmp/unity-2022-applicable-20261105/`: `editor-applicability-proof.json` records
the native reports and YAML field/type checks; `acceptance-summary.json` records
both actual backends on each Editor; per-version build reports and resource
records retain the exact commands, limits, outcomes, and the Mono retry.
