# Unity IL2CPP package comparisons

Date: October 10, 2026. Baseline commit: `976a91d896e36716ba173ec9347d07a648b7ee48`.
Environment: Unity 6000.5.6f1, Linux x64, IL2CPP, High stripping, virtual display.

## Corrected comparison: current fixtures held fixed

The comparison below preserves the entire current fixture from
`tmp/lead-unity-redesign-il2cpp-stability`, including the saved failing stability
scene and its real managed-reference host. All 50 fixture files, script GUIDs,
scene ordering, serialized values, and the scene file itself were byte-identical
across the three controls. Hash checks also confirmed that none changed during
import or build. Only the four Spire package DLLs were swapped.

A shared Editor-only build adapter selects IL2CPP/High and builds the existing
scene with `CleanBuildCache`; it does not recreate the scene, replace fixture
types, weaken assertions, or suppress diagnostics. Each project starts without
a `Library` directory.

| Package | Build | Player |
|---|---|---|
| Commit `976a91d`, runtime `f7393f3e…` | Compile failure: missing newer API and collision support | Not run |
| Current local package, runtime `bee22b42…` | Succeeded, zero errors | Exit 133; `level0` corruption / position out of bounds |
| Earlier API-compatible snapshot, runtime `4be44681…` | Succeeded, zero errors | Exit 133; same `level0` corruption / position out of bounds |

The committed package reports missing `UnitySerializeReference` and
`Variant.FormerlySerializedAs`, field-type conflicts, and the removed unsupported
Unity-field diagnostics. Those failures were retained as evidence rather than
removing the corresponding current fixtures. They prevent an unchanged committed
package from reaching native loading in this comparison.

The earlier compatible snapshot came from
`tmp/lead-unity-redesign-harness/Packages/com.houtamelo.spire`. It is an earlier
uncommitted package artifact, not a separate Git revision or proof of a
pre-redesign baseline. Its identical failure demonstrates that this frozen
fixture also fails with that artifact, without identifying an introducing local
change or exonerating the whole redesign.

Projects and fixture hashes are under `tmp/fixed-current-fixture-comparison`.
Player logs are `tmp/fixed-current-fixture-local-player.log` and
`tmp/fixed-current-fixture-earlier-player.log`. Neither player produced a managed
report. The committed compilation failures are in
`tmp/fixed-current-fixture-comparison/committed/unity-profile-build.log`.

## Initial comparison: committed fixtures

The initial comparison below also used the older committed fixtures. It
establishes their behavior and the known stripping boundary, but does not answer
whether the local implementation regressed the current fixtures. The corrected
comparison above holds those fixtures fixed.

The local tracked, staged, and untracked changes were stashed. The committed
tests were rebuilt and run, then the workspace was restored with its index.
Staged and unstaged patches, Git status, and all 87 untracked file hashes matched
their pre-stash snapshots. No product source was changed for this comparison.

| Check | Result |
|---|---|
| Committed .NET suite | 2,013 passed |
| Committed native Editor integration | 87 passed, zero failures, three observations |
| Restored local .NET suite | 2,242 passed |
| Committed fixture, committed package, IL2CPP/High | Build passed; player exited 139 with `SIGSEGV` |
| Unity-only one null managed-reference field, no Spire | Build and player passed |
| Unity-only two null managed-reference fields, no Spire | Build and player passed |
| Committed fixture and package with diagnostic type-resolution root | Build and player passed; exit 0 |
| Identical committed fixture with local package and same diagnostic root | Build and player passed; exit 0; identical runtime report |

The committed harness has a Mono build entry point. Its disposable copy was
adapted only to select IL2CPP, High stripping, and `CleanBuildCache`. The committed
assertions and package source were unchanged. GDB confirms the baseline crash is
`il2cpp::vm::Method::GetParamCount(method=0x0)` on `Loading.Preload`, matching the
previous stripping failure. It is not the current scene-corruption `SIGTRAP`.

The next two disposable controls share identical fixture source and script GUIDs.
Both have a preserved method that calls `Type.GetType(string)` to retain the
already-known native dependency. Their package DLLs differ: the committed runtime
hash is `f7393f3e34757c7044dccf2c31c1303c961e789ef76f1ac915023fe37695edd5`, and the
local runtime hash is `bee22b42b2fd7563a901cb5544fa4445c04677ac6f7b89de21791c46c897ba4d`.
These are diagnostic controls, not an unchanged committed IL2CPP test suite.
Both build reports identify IL2CPP, and both generated `GameAssembly.so`. The
original player assertions passed in each run, with byte-identical runtime JSON.

The clean Unity-only project has three runtime scripts and no package reference
or source dependency on Spire. Both players verify their backend, field count,
and null field values in a managed report. This narrows the earlier two-field
reproducer to its surrounding fixture/type metadata.

Artifacts are under `tmp/commit-baseline-check`: .NET logs, the native Editor
report, isolated projects, disposable adapters, and workspace restoration checks.
The baseline GDB trace is `tmp/committed-baseline-gdb-backtrace.txt`. The Unity-only
reports are `tmp/plain-unity-one-slot-player.json` and
`tmp/plain-unity-two-slots-player.json`.

The two normalized player reports are `tmp/committed-baseline-preserved-player.json`
and `tmp/committed-fixture-current-package-player.json`. Their corresponding
projects are `tmp/commit-baseline-check/il2cpp-preserved` and
`tmp/commit-baseline-check/il2cpp-current-package`.

## Initial comparison conclusion

The later [Unity-only MRP and script-registration comparison](unity-il2cpp-opaque-managed-reference-slots.md#mrp-and-corrected-fixture)
identifies an embedded `MonoScript` in this frozen fixture. Replacing it with a
proper script asset reference makes the same saved values and package DLLs pass.
The statements below record what the package-swap comparison established before
that reduction.

Stashing exposes the older, already-known stripping failure. Retaining its
native dependency allows both the committed and local packages to pass the same
committed fixture under IL2CPP/High. The current scene-corruption trap does not
reproduce in that fixture, and the clean Unity-only two-slot control also passes.

This does not establish that every local package change is safe. The newly added
stability/managed-reference fixtures exercise cases absent from the committed
suite and need APIs absent from the committed package. The remaining failure is
therefore narrowed to those newer fixture/type-metadata interactions; a package
regression affecting only those cases remains possible. No introducing commit
or local source change has been identified by this comparison.
