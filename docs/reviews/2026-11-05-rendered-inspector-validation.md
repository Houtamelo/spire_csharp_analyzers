# Rendered Inspector validation

The dedicated rendered Inspector suite passes on Linux x64 in all three tested
Editors. These checks open actual UI Toolkit and IMGUI controls; they are a
separate process from the 2,689-case headless Editor harness.

| Editor | Rendered cases passed | Assertions | Not applicable | Native wrapper assertions |
| --- | ---: | ---: | --- | ---: |
| 2022.3.62f3 | 17 | 192 | ClosedGenericKindControl | 79 |
| 2023.2.22f1 | 18 | 204 | None | 79 |
| 6000.5.6f1 | 18 | 204 | None | 79 |

All accepted runs exit 0 with no failures or unverified cases. Unity 2022's
unsupported generic-union serialization is explicitly not applicable; every
other rendered case runs. Generic Inspector serialization remains covered on
2023 and 6000.

Coverage includes opening without mutating data, field layout, repeated typing
and focus, variant changes, fieldless variants, copied values, arrays/lists,
managed-reference aliases and cycles, Unity object identity, Undo/Redo, external
edits, prefab overrides, multi-object editing, and actual IMGUI input.

## Corrections found by these runs

The first 2022/2023 rendered runs time out. A direct headless native probe then
isolates an infinite traversal in the generated drawer's
`HasSharedManagedReference`: `NextVisible` repeatedly descends a cyclic graph.
The fixed traversal tracks visited managed-reference IDs while checking for
wrapper aliases before skipping repeated descent. ID 0 remains valid; negative
IDs represent invalid/null references. The native regression uses real
`SerializedObject` instances and generated wrappers, with both a cycle at ID 0
and a shared wrapper at ID 0. It checks unshared and shared outcomes.

Unity 2023 also exposes a multi-object data overwrite during initial binding.
Native `SerializedPropertyChangeEvent` notifications enter the edit path and
broadcast the first target's destination/speed to the second target before any
user edit. Native synchronization now ignores mixed and unchanged notifications;
real mixed-field edits remain accepted, and commits refresh the binding snapshot.
The rendered test checks distinct per-object data before and after editing speed.

The fixture now waits for finite, editable field geometry and prepares text focus
across updates before requesting append input. This fixes premature assertions
against NaN bounds and Unity 2022's initial caret placement. Append, focus,
Undo/Redo, alias, cycle, and data-isolation assertions remain active. The cyclic
back-link remains visible. Reports are checkpointed during execution so a future
timeout retains completed cases and diagnostics.

The final source-generator project passes **1,051/1,051** tests, including the
updated exact-output contract. The compile-time behavioral project passes
**100/100** tests. Temporary product diagnostics have been removed.

## Evidence and scope

Evidence root: `tmp/unity-rendered-inspector-20261105/`.

- `rendered-acceptance-proof.json` verifies case identities, applicability,
  individual reports, clean exits, package hashes, and resource limits.
- `focus-final-ui-{2022,2023,6000}-report.json` contains the accepted rendered
  reports; corresponding logs and run records are in each Editor's project.
- `final-native-{2022,2023,6000}-report.json` contains the native regressions.
- `dotnet-results/final-source-generators.trx` and `final-behavioral.trx`
  contain the .NET results.
- `cycle-traversal-headless` preserves the original direct-probe timeout;
  `cycle-traversal-fixed` records its successful correction. Earlier failed
  rendered reports and diagnostic logs are retained alongside accepted results.

The accepted package uses analyzer SHA-256
`4b25aebd188ab3af9a485ca59dd4c26ac685a02f2e6cfc55b1d1f9b897757063`;
the other three DLLs match the preceding wrapper/player validation. Full hashes
are in `final-package-sha256.json`.

All heavy operations run sequentially on CPU affinity `22,23`, quota 200%, two
Unity workers, `MemoryMax=12G`, `MemoryHigh=10G`, no swap, and an aggregate RSS
watchdog. Rendered runs use private Xvfb displays and peak at 1.22, 2.50, and
2.89 GiB sampled aggregate RSS respectively.

The earlier [headless Editor and player results](2026-11-05-unity-2022-2023-current-validation.md)
remain separately recorded. This follow-up rebuilds the Editor-only drawer and
reruns native, rendered, source-generator, and behavioral checks; it does not
claim another full 2,689-case Editor or player run with this analyzer DLL. The
package's declared minimum version is unchanged.
