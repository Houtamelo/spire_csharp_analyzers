# Guard-flow performance harness

This standalone `net10.0` tool measures the real source generator, Roslyn
compilation, and `FieldAccessSafetyAnalyzer` on deterministic generated
consumers. It is separate from xUnit timing and from the runtime
BenchmarkDotNet project because its subject is compiler analysis.

Run a short local profile:

```sh
dotnet run -c Release --project tools/Spire.GuardFlow.Performance -- --quick \
  --output tmp/guard-flow-performance.json
```

The default profile uses branch, receiver, alias, recursive-summary, and
irrelevant-body dimensions. Each workload is warmed once and measured three
times. The JSON report records generator/compilation/emit/analyzer timings,
process-wide allocation deltas, exact diagnostic hashes, workload dimensions,
host/runtime metadata, and cancellation observations. Timing is evidence for
comparison, not a machine-independent pass/fail threshold.

Cancellation is tested in two ways: a pre-cancelled token must be observed,
and a prepared, warmed compilation enters `GetAnalyzerDiagnosticsAsync` before
the token is requested. The report records the request timestamp, return
latency, completed analyzer baseline, and clean post-cancellation hash. The
Roslyn API does not expose an internal phase checkpoint, so the report does not
claim which analyzer phase consumed cancellation. Every analyzer workload has
a 30-second cancellation budget; overrunning it fails the process.
