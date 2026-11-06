# Windows validation file lifetime

Windows validation exposed two distinct file-sharing failures in the tooling.
Neither failure is a Unity test assertion or a generated-package compilation error.

## Log reading: fixed

`IntegrationWorkflow` and `CustomerWorkflow` opened Unity logs with
`File.ReadAllText`. Windows rejects that reader while an existing writer remains
open because the new reader does not share writes. Editor completion can precede
the final release of its log's writer handle.

The workflows now read through `FileOps.ReadSharedText`, which permits concurrent
writers and deletion. It preserves encoding detection, missing-file errors, and
exclusive-lock errors. Six real-filesystem regression cases cover those contracts.

The Windows red check executes the old reader against the same tests: five pass
and the real open-writer case fails with a sharing violation. After rebuilding
the corrected reader, all six pass. The complete Windows .NET suite passes
2,707 tests with 12 explicit Linux exclusions. Linux DevTools passes 161 tests
with eight explicit Windows exclusions.

## Reusing the native project: investigation remains

The final Unity 6000 native Write, fresh-process Read, Recovery, accessor, and
wrapper phases pass. The immediately following player-build command fails during
preparation with:

```text
The process cannot access the file because it is being used by another process.
```

That attempt produces no `unity-core-build-report.json`. The CLI prints only the
exception message, so the exact failing operation and handle owner are not yet
proven. Archiving the previous Editor's files is a likely source; it must not be
reported as a confirmed stack trace.

The validation pipeline runs player builds in fresh disposable projects,
independent of native persistence projects. Those projects still run the original
`CorePlayerBuild.BuildPlayers` method, High stripping, and actual Mono/IL2CPP
executables. This does not change or bypass player assertions. Accepted results
include a clean build/process exit and a fresh runtime report. Both backends
pass on all three Editors: 59 checks each on Unity 2022, 60 on 2023 and 6000.
The [complete matrix](../reviews/2026-11-06-windows-unity-version-matrix.md)
also records passing independent native persistence runs using distinct logs.

To repair repeated-project orchestration, first retain a complete exception stack
and identify the held file. Reproduce the specific read/move/copy conflict with a
real held handle, then test successful release and a permanently locked failure
before introducing any bounded retry.

Evidence root on the validation host:
`~/.local/share/spire-windows/virtualbox/results/windows-matrix-20261106/`.
The failed command is under `final/6000.5.6f1/core-build/`; independent player
attempts are under `final/players-clean/`. Initial failures and runner attempts
remain alongside the final results.
