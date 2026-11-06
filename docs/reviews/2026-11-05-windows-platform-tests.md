# Windows platform test coverage

The 13 Windows failures came from fixtures that assumed Linux facilities.
The shared child-cleanup fixture now launches real Windows processes. Eight
Linux process-group cases and four Xvfb cases retain their Linux coverage and
declare that requirement explicitly. Eight new Windows process cases cover
deadlines, inherited stdout/stderr, live descendant termination, and captured
output. These checks also exposed a Windows Mono timeout-cleanup bug in the
development-tool runner: synchronously closing a reader could block behind a
descendant's inherited pipe. Reader closure now runs within the existing bounded
cleanup wait. The five real inherited-pipe cases reproduce that failure under
Editor Mono; their EOF and deadline assertions were retained. No package runtime
behavior was changed.

## Verification

| Check | Result |
|---|---|
| Linux .NET solution, eight projects | 2,704 passed, zero failures; eight Windows cases explicitly inapplicable |
| Windows .NET development tools | 150 passed, zero failures; 12 Linux cases explicitly inapplicable |
| Focused Windows process/platform checks | 12 passed, zero failures/skips |
| Windows Unity discovery contracts | 10 passed, zero failures/skips |
| Full Windows Editor Mono harness | 2,671 passed, zero failures/skips; all 2,554 applicable xUnit identities matched |
| Focused checks after the cleanup fix, Windows .NET and Editor Mono | 12 passed in each runtime, zero failures/skips |
| Linux filesystem audit | 1,976,656 trace entries; zero access to live `examples/` |
| Existing Showcase assets/settings | 84 hashes unchanged; examples Git status unchanged |
| Scoped host/guest source and prepared Unity adapter | All 12 source hashes and the adapter match |
| Windows storage/crash capture | No new events |

The Windows cases use actual PowerShell parent/child processes and PID/readiness
files. Orphan-pipe cases deliberately leave a non-owned descendant alive until
the test's cleanup, proving that completion honors its deadline while a pipe
remains open. They do not claim Linux process-group ownership on Windows.
The live-parent case separately verifies recursive descendant termination.
If a non-owned descendant keeps a pipe open after timeout, best-effort reader
closure can remain pending in the background until that pipe closes; the public
operation still returns its timeout. The test owns and terminates that child.

The fixture restricts inherited handles with `STARTUPINFOEX` and
`PROC_THREAD_ATTRIBUTE_HANDLE_LIST`. This allows stdout-only, stderr-only, and
both-stream cases to assert that the unused stream reaches EOF. The earlier
managed-spawn probes failed those assertions because additional pipe handles
were inherited. All EOF and deadline assertions were retained.
[Microsoft handle-inheritance documentation](https://learn.microsoft.com/en-us/windows/win32/api/processthreadsapi/nf-processthreadsapi-updateprocthreadattribute)

Platform exclusions are separately recorded, with exact case identities and
canonical reasons. Independent xUnit discovery and the Unity reflection bridge
must agree on the active cases and exclusions. Every applicable case and all
100 original compile-time behavioral facts executed. Ordinary skipped facts,
altered reasons, and unexpected ignored NUnit cases still fail validation.
See the [platform contract](../test-platforms.md).

## Evidence and reproduction

Run the ordinary solution tests on each host; attributes select its applicable
process/display cases. Run the full Editor suite with:

```sh
dotnet run -c Release --project tools/Spire.Tool -- unity-harness --timeout 1800
```

Host evidence is under `tmp/windows-platform-evidence/`, with Linux TRX reports
under `/tmp/spire-host-results/platform-cleanup-final/`. Windows evidence is under
`~/.local/share/spire-windows/virtualbox/results/platform-cleanup-final/`.
Intermediate failed fixture probes remain in their own result directories.
The final audit is `tmp/windows-platform-evidence/summary.json` and a copy is
stored with the Windows reports, together with the scoped source archive.

The earlier broad Editor rerun recorded nine repository-root lookup failures
and 2,662 passes; the fixture was corrected to use the configured repository
root. A subsequent focused Mono run reproduced the five cleanup failures before
the runner fix. One simultaneous host/guest build run also expired an existing
five-second command-capture deadline; serial repeats passed with that budget
unchanged. Those intermediate failures remain preserved.

Windows uses the existing owned source snapshot with scoped updates. Linux
uses the host working tree. These are uncommitted working-tree validations,
not certification of a clean release commit. Existing Windows player, Inspector,
customer-installation, and Visual Studio gates are recorded separately in the
[environment report](2026-11-04-windows-unity-validation.md).

Before committing, the selected changes were exported onto base `1637f1e`
without the unrelated analyzer/demo edits or the interactive examples directory.
That isolated tree passed 2,480 tests across all eight .NET projects, with eight
explicit Windows-only exclusions and zero failures. The larger counts above
describe the previously validated working-tree snapshots.
