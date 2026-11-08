# Guard-flow analysis measurements

Final measurements were generated on 2026-11-08 from base commit
`18a8e0794abc3fda426dcfbbee41ea0c062c82ca`. The hardening changes were
uncommitted in the measured worktree (`GIT_WORKTREE_STATE=dirty`); this report
therefore identifies the base SHA and the exact analyzer binary instead of
presenting the result as a committed release measurement.

The measured analyzer DLL was
`src/Houtamelo.Spire.Analyzers/bin/Release/netstandard2.0/Houtamelo.Spire.Analyzers.dll`
with SHA-256
`B9FEB4901341CA60993EC4C21681FC862E69C4B77B6460FFA5E85A1ED24675F1`.
The raw full-profile report is [guard-flow-2026-11-08.json](guard-flow-2026-11-08.json).

The runner uses the real discriminated-union generator, Roslyn compilation and
emit, and `FieldAccessSafetyAnalyzer`. Each workload has one warmup run and
three measured runs. Reported analyzer allocations are process-wide
`GC.GetTotalAllocatedBytes` deltas around the analyzer call. The per-call
analysis budget is 30 seconds; an overrun fails the runner. Timings are host
comparison data, not machine-independent thresholds.

The full profile used .NET 10.0.12, SDK 10.0.401, Roslyn 4.10.0.0, x64
openSUSE Tumbleweed, and 24 logical processors.

| Workload dimension | Largest input | Median generation ms | Median emit ms | Median analyzer ms | Median analyzer bytes |
|---|---:|---:|---:|---:|---:|
| Boolean/CFG branch joins | 256 branches | 34.56 | 65.89 | 99.04 | 13,935,528 |
| Simultaneous stable receivers | 64 receivers | 26.91 | 9.81 | 32.75 | 9,323,232 |
| Direct ref aliases | 32 aliases | 27.48 | 6.00 | 56.26 | 27,882,576 |
| Mutually recursive local functions | 32 functions | 24.81 | 4.58 | 12.93 | 4,621,288 |
| Irrelevant candidate scan | 128 branches, no accessor | 28.22 | 7.71 | 8.78 | 1,349,104 |
| Diagnostic controls | one SPIRE014 + one SPIRE013 | 21.43 | 3.14 | 3.44 | 956,520 |

All repeated diagnostic hashes were stable. The diagnostic-control workload
validated exact source sites, spans, severities, source text, and message
fragments for `SPIRE014` followed by `SPIRE013`; safe workloads produced no
SPIRE013/SPIRE014 diagnostics.

Cancellation used a prepared and warmed compilation. The worker signalled entry
immediately before `GetAnalyzerDiagnosticsAsync`, then the token was requested.
The full-profile cancellation workload had a clean analyzer baseline of
325.87 ms and 223,674,848 allocated bytes. The request-to-return latency was
34.46 ms. Pre-cancelled and in-flight cancellation were both observed, and the
clean post-cancellation run took 289.34 ms with the same diagnostics hash.
The report records the UTC request timestamp and the entry confirmation. The
Roslyn API does not expose which internal analyzer phase consumed the token, so
the result makes no phase-level claim.

The minimum-host profile was also run with `GuardFlowRoslynVersion=4.3.1`; the
loaded package reported Roslyn 4.3.0.0. Its quick corpus passed all 14
workloads, including exact diagnostic controls and cancellation, and produced
the same analyzer DLL SHA-256. The tool assets were restored and rebuilt with
the default Roslyn 4.10.0 configuration afterward.
