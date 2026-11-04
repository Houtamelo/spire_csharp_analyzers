# Unity 7 subprocess capability probe stalls the IL2CPP harness

Resolved by removing the subprocess capability test and its helper from the
bridge. It did not exercise a Spire feature, and explicit backend reporting
already identifies IL2CPP. The subsequent full, unfiltered run passes all 107
remaining tests with zero failures or skips. The process-runtime behavior itself
was not changed or further investigated.

The earlier unfiltered Unity `7000.0.0a12` Linux IL2CPP behavioral harness stalled in
`XunitFactBridgeContractTests.Il2Cpp_ProcessStartReportsUnsupportedRuntime`.
This contract checks an older IL2CPP runtime limitation: starting `/bin/true`
should throw a particular `Win32Exception(0)`.

The method calls `Process.Start`, then unbounded `WaitForExit` when start succeeds,
and performs process cleanup before its assertions. A defunct `/bin/true` child
was observed under each stalled player. The precise blocking call was not
instrumented; the evidence does not establish whether it is startup, waiting,
or cleanup. The capability assumption needs revalidation for Unity 7's runtime.

Evidence is preserved under
[`tmp/unity7-il2cpp-validation-20261103`](../../tmp/unity7-il2cpp-validation-20261103):

- `behavioral/result-tree-player.log` shows the controller active with its NUnit
  runner initialized, before completed cases propagate into the root result.
- `behavioral/direct-player.log` records all 100 original Spire Facts passing
  before reaching the subprocess contract.
- `behavioral/direct-player.run.json` records the isolated diagnostic's 120-second
  timeout followed by forced termination at approximately 130 seconds.
- The ordinary NUnit run, selecting all 100 Facts and the other seven bridge
  contracts, passes all 107 cases and exits 0. The Reflection.Emit unsupported
  capability contract passes in that NUnit context.

The temporary direct-invocation diagnostic also recorded a Reflection.Emit
contract failure outside NUnit's execution context. That diagnostic is not the
acceptance result for the contract; the subsequent normal NUnit result is.

During diagnosis, the subprocess probe was isolated using a test filter and
recorded as a timeout. It was subsequently deleted because it was unrelated to
Spire's behavior; all 100 original Spire tests and all retained bridge assertions
remain unchanged. Earlier player processes blocked after SIGTERM and required SIGKILL;
those owned processes were cleaned up.

Future Spire validation should exercise Spire's generated code and retain direct
backend identity and test-discovery checks. There is no subprocess-support gate
for this package. See the
[IL2CPP validation report](../reviews/2026-11-03-unity-7-il2cpp-validation.md).
