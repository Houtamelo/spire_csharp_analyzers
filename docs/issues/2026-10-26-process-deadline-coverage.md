# Process deadline and output-draining coverage

This is the research and coverage matrix for the approved ProcessRunner batch.
The current implementation starts two `PumpAsync` tasks at
`tools/Spire.Tool.Core/ProcessRunner.cs:57-58`, waits for the parent at lines
63-68, then awaits both pumps at line 69. The timeout is checked only while the
parent is alive. A child that inherits either redirected pipe can therefore
make a parent that already exited successfully wait past the deadline forever.
The existing timeout test (`UnityHarnessRunnerTests.RunUnity_Timeout...`) keeps
the parent alive and therefore does not cover this boundary.

## Ownership and platform boundary

On Linux, `ownProcessGroup: true` launches through `setsid` (ProcessRunner:25-37)
and `Kill` sends SIGKILL to the negative process-group ID (89-92). This is the
strongest ownership boundary available to the current API and must remain the
primary timeout cleanup path. The subsequent `KillTree` call is still needed
for races and non-group platforms.

On non-Linux platforms, `ownProcessGroup` currently falls back to
`ProcessCompatibility.KillTree`; this is a process-tree snapshot, not a durable
job/object ownership boundary. The fix should not claim Windows descendant
ownership without introducing a native job object. It must still enforce the
deadline for the parent and reader tasks on every target.

`ProcessCompatibility.KillTree` under the Unity host recursively scans immediate
`/proc` children (52-78), so a reparented child can escape after its parent
exits. Group cleanup avoids this race on Linux when callers opt into ownership;
tests must distinguish that guarantee from the fallback tree behavior.

## Recommended implementation shape

Use one deadline for process exit, stdout completion, and stderr completion. A
small internal completion state can expose the following operations without
changing the public `ProcessResult` shape:

1. Start both pumps and a process-exit observation task against one monotonic
   deadline. Successful completion requires the process and both readers to
   finish before the deadline.
2. When the deadline expires, mark timeout, kill the owned process group/tree,
   and close/dispose the parent-side stdout/stderr streams so a reader blocked
   behind an inherited pipe cannot keep the operation alive.
3. Await both reader tasks with a short cleanup deadline. Observe every task
   exception; never leave a pump writing to a disposed `TextWriter` or an
   unobserved fault behind the returned/raised operation.
4. Preserve all bytes/lines already captured and flush the optional log before
   disposing it. Throw the existing `TimeoutException` after cleanup, with the
   existing executable/timeout message. A process that exits nonzero before the
   deadline still returns `ProcessResult`; timeout remains exceptional.

`ReadLineAsync` is useful for the current line-oriented log, but the pump needs
an explicit cancellation/close path. If the target framework cannot cancel a
`StreamReader.ReadLineAsync` directly, close the parent stream on timeout and
bound the await; do not wait indefinitely for a descendant holding the pipe.
The normal success path must continue draining to EOF and preserve the current
line-oriented log behavior.

Do not make the timeout longer to accommodate inherited pipes, and do not return
success merely because the parent has exited. The timeout budget covers output
draining by design.

## Coverage matrix

All cases use real `/bin/sh` descendants and actual redirected pipes on Linux.
They should use short, bounded durations and a unique temporary fixture root;
the child scripts must publish PIDs through files so cleanup can be asserted.
The tests should tolerate ordinary scheduling jitter with a generous upper
bound while proving that the observed operation does not exceed a bounded
cleanup window.

### Category A: normal completion and output capture

#### should_pass

| Test | Description |
|---|---|
| `RunAsync_ReturnsExitCodeAndCapturesStdoutAndStderr` | A process writes multiple newline-terminated stdout/stderr lines and exits 37; result preserves both streams and exit code. |
| `RunAsync_CapturesPartialFinalLines` | A process writes stdout/stderr without a final newline and exits normally; the partial lines are retained and logs can be reopened. |
| `RunAsync_WritesCombinedLogBeforeReturning` | With `logFile`, both streams are observed, the file is readable after return, and no pump writes after disposal. |
| `RunAsync_SuccessWithNoOutput` | A quiet process exits zero; empty streams and an empty/reopenable log are valid. |

### Category B: parent exits while descendants retain pipes

#### should_fail

| Test | Description |
|---|---|
| `RunAsync_TimeoutCoversStdoutHeldByExitedParent` | Shell backgrounds a long-lived child inheriting stdout, parent exits immediately; operation throws within the requested timeout plus bounded cleanup and leaves no owned child. |
| `RunAsync_TimeoutCoversStderrHeldByExitedParent` | Same using stderr only; catches the independent second pump. |
| `RunAsync_TimeoutCoversBothPipesHeldByExitedParent` | Child retains both descriptors; timeout cleanup closes readers and kills the owned process group. |
| `RunAsync_ParentExitDoesNotPrematurelyReturnWhilePipeIsOpen` | Parent returns zero but descendant keeps a pipe open beyond the deadline; operation must time out rather than return a successful `ProcessResult`. |

### Category C: timeout while the parent remains alive

#### should_fail

| Test | Description |
|---|---|
| `RunAsync_TimeoutKillsLiveOwnedProcessGroup` | Parent and child sleep; with `ownProcessGroup: true`, timeout kills both and throws the existing `TimeoutException`. |
| `RunAsync_TimeoutPreservesOutputCapturedBeforeKill` | Parent writes lines, then blocks; timeout preserves already captured stdout/stderr and produces a reopenable log. |
| `RunAsync_TimeoutDoesNotLeaveReaderTasksOrChildren` | After timeout, owned child PID is gone and no delayed write/fault occurs after the test observes completion. |

### Category D: cleanup and ownership boundaries

#### should_pass

| Test | Description |
|---|---|
| `RunAsync_GroupCleanupKillsGrandchildAfterParentExit` | Linux-owned group includes shell → child → grandchild; parent exits first, and group cleanup removes the grandchild. |
| `RunAsync_NonOwnedTreeUsesDocumentedFallback` | Exercise `ownProcessGroup: false` with a live direct child; assert the current tree cleanup behavior without claiming reparented-child guarantees. Mark the reparented case as a separate platform limitation. |
| `RunAsync_ExistingProcessCanBeCompletedThroughCompleteAsync` | Start a real process through `ProcessRunner.Start`, call `CompleteAsync`, and verify identical output/deadline behavior to `RunAsync`. |
| `RunAsync_LogRemainsReadableWhenCleanupReportsChildPipeClosure` | A timeout with an inherited pipe yields a readable flushed log and no unobserved reader exception. |

### Category E: argument, environment, and platform compatibility

#### should_pass

| Test | Description |
|---|---|
| `RunAsync_PreservesArgumentsThroughOwnedGroupLauncher` | Existing spaces/quotes/backslashes argument contract also passes when `ownProcessGroup: true` and `setsid` is used. |
| `RunAsync_AppliesEnvironmentBeforeTimeout` | Child writes a configured environment value before blocking; captured value survives timeout cleanup. |
| `RunAsync_RejectsInvalidWorkingDirectoryBeforeStarting` | Existing start error behavior remains immediate and does not create reader tasks or logs. |

## Acceptance checks

The focused process suite must include Categories A-D on the real host and run
the host-compatible subset under `SpireUnityTestHost=true` if the ProcessRunner
is consumed there. No test may use a mocked process, a fake stream, an ignored
timeout, or an unconditional platform skip. Windows should run the normal
completion and parent timeout cases, while Linux additionally proves durable
owned-group cleanup. All tests must bound their own child lifetimes in `finally`
blocks even after an assertion failure.

The final full SDK suite should report zero failures/skips. Native Unity and
Rider workflows should be smoke-tested after the change because both depend on
ProcessRunner output draining and process-group cleanup. Existing CLI messages,
exit-code handling, log paths, and public method signatures remain unchanged.

