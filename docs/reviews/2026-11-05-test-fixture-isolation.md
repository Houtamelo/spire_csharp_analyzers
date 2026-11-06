# Tests are independent of interactive examples

Automated tests and Unity acceptance use dedicated inputs under `tests/`.
They do not read or modify the repository's live `examples/` tree. The final
Linux solution run passed all 2,701 tests across eight projects; a syscall
capture of that run recorded zero filesystem access to the live examples.
Hashes for 84 existing Showcase asset/settings files and the examples Git status
remained unchanged.

## Fixture ownership

| Input | Owner |
|---|---|
| Exported package sample assets and inventory | `tests/UnityPackageSample/Assets`, `sample-files.txt` |
| Customer project's Unity settings | `tests/UnityPackageSample/ProjectSettings` |
| Upgrade consumer's initial assets | Baseline package's own `Samples~/Showcase` |
| MSBuild policy input | `tests/DevTools.Tests/Fixtures/MSBuildPolicy/Directory.Build.props` |
| Examples-management test repositories | Owned temporary directories under `tmp/` |

The interactive showcase remains independent. Its edited assets are not test
inputs and do not determine exported sample contents. The dedicated sample's
canonical scene and preset were generated in a disposable Windows Unity project
with `ShowcaseAuthoring.CreateExamples`. Only the generated copies under
`tests/UnityPackageSample` were updated. The fixture's own persistence, scene,
and Play Mode checks now assert that canonical data.

Package export and Unity/IDE validation reject inputs or outputs under the
repository examples directory before mutation. Real filesystem regressions cover
the directory itself, child paths, normalized `..` traversal, preserved files,
missing baseline sample inputs, and allowed sibling names such as
`examples-other`. The Visual Studio runner also rejects protected paths with
`-Prepared`.

Source Link's SDK Git task was another indirect dependency: it inspected example
submodules while building test dependencies and temporary consumers. Test project
references, validation builds, and test-owned subprocess builds now suppress
those queries. Real MSBuild checks verify the test/reference settings and that
direct production builds retain their normal source-control policy.

## Verification

| Check | Result |
|---|---|
| Linux .NET solution | 2,701 passed, zero failures/skips, eight projects |
| Final Linux syscall capture | 1,973,537 entries checked; zero live-example accesses |
| Original Showcase assets/settings | 84 hashes unchanged; examples Git status unchanged |
| Windows .NET development tools | 138 passed, same 13 Linux-only failures, zero skips |
| Windows Editor Mono development tools | 138 passed, identical 13 failures, zero skips |
| Windows package installation/import/verification/configuration | Passed |
| Imported sample Editor/Play Mode suite | All 16 passed |
| Visual Studio runner path guards | Four checks passed |
| Windows storage/crash events | No new events in the post-SATA boot capture |

At that stage the remaining 13 platform-specific cases were recorded in the
[development-tool fixture report](../issues/2026-11-04-windows-development-tool-fixtures.md).
They were not excluded or reclassified as passing in this isolation run. The
subsequent [platform follow-up](2026-11-05-windows-platform-tests.md) resolved
their Windows portability and verified the full applicable Editor suite.

The initial full-solution verification also passed 2,701 tests, but its trace
found one remaining SDK Git query from a catalog reference-project build. The
catalog subprocess helper was fixed, and the final complete run above verified
zero access. An intermediate `--no-restore` attempt encountered SDK assets left
targeting the Unity portable host; the successful final run used normal restore.

Local evidence:

- Host TRX reports: `/tmp/spire-host-results/isolation-verified/`.
- Raw syscall capture: `/tmp/spire-isolation-verified-files.trace`.
- Audit summary and compressed capture: `tmp/example-isolation-evidence/`.
- Windows reports: `~/.local/share/spire-windows/virtualbox/results/example-isolation-final/`.

Windows used the existing owned source snapshot with the scoped fixture/tooling
updates. The Linux run tested the current host working tree. These are separate
source states; this report does not claim a new full Windows package-suite run.
No changes were committed.
