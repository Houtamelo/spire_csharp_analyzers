# Corrected Skewed80 match measurements

2026-10-26. All 14 BenchmarkDotNet Short measurements completed successfully.
Each operation traverses 1,000 values. The complete statistics below retain
uncertainty; three measured iterations are insufficient for fine rankings.

`Skewed80` now selects Idle 80% of the time and distributes the remainder across
the seven other variants. Seed 42 produced `[795,25,31,24,30,30,35,30]` for
all six representations at N=1,000. N=100,000 produced
`[79946,2865,2806,2881,2884,2832,2909,2877]`. Uniform remained exactly balanced.
The [histogram data](MATCH_CORRECTED_SKEWED80_2026-10-26.histograms.json) records
both sizes for every representation; [raw CSV](MATCH_CORRECTED_SKEWED80_2026-10-26.csv)
retains the exported measurements.

Idle performs no arithmetic, so comparing Uniform against Skewed80 also changes
the amount of work performed. Earlier reports labeled Skewed80 used the incorrect
distribution and should not be compared as the same workload. All 12 historical
benchmark artifacts were checked byte-for-byte and left unchanged.

Only MatchBenchmarks consumes this distribution. Its seven methods returned
equal sums for each distribution in integration tests. Explicit no-op cases
were added to existing switches so a fresh benchmark build satisfies SPIRE015.

Reproduction uses a temporary console project targeting net10.0, LangVersion
preview, with a ProjectReference to the benchmark project. Its entry point is:

```cs
using BenchmarkDotNet.Running;
using Houtamelo.Spire.Benchmarks.Benchmarks;
BenchmarkSwitcher.FromTypes(new[] { typeof(MatchBenchmarks) }).Run(args);
```

Run it in Release with `--filter '*MatchBenchmarks*' --job Short --artifacts <private-directory>`.
This avoids the benchmark project's main entry point, which replaces its normal
combined report. The run below used the repository's existing SpireConfig.
Question marks are absent category baselines for PayloadDeconstruct, rather
than missing measurements; every case has valid statistics.

```

BenchmarkDotNet v0.15.8, Linux openSUSE Tumbleweed
AMD Ryzen 9 9900X 3.00GHz, 1 CPU, 24 logical and 12 physical cores
.NET SDK 11.0.100-preview.3.26207.106
  [Host]   : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v4
  ShortRun : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v4

Job=ShortRun  IterationCount=3  LaunchCount=1  
WarmupCount=3  

```
| Method             | Categories          | N    | Dist     | Mean       | Error       | StdDev   | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------- |-------------------- |----- |--------- |-----------:|------------:|---------:|------:|--------:|----------:|------------:|
| **PayloadDeconstruct** | **Payload Deconstruct** | **1000** | **Uniform**  |   **719.3 ns** |   **229.23 ns** | **12.57 ns** |     **?** |       **?** |         **-** |           **?** |
|                    |                     |      |          |            |             |          |       |         |           |             |
| **PayloadDeconstruct** | **Payload Deconstruct** | **1000** | **Skewed80** |   **476.2 ns** |    **29.50 ns** |  **1.62 ns** |     **?** |       **?** |         **-** |           **?** |
|                    |                     |      |          |            |             |          |       |         |           |             |
| **Auto**               | **Property**            | **1000** | **Uniform**  |   **735.1 ns** |   **211.96 ns** | **11.62 ns** |  **1.00** |    **0.02** |         **-** |          **NA** |
| ExplicitAuto       | Property            | 1000 | Uniform  |   748.4 ns |   219.57 ns | 12.04 ns |  1.02 |    0.02 |         - |          NA |
| Typed              | Property            | 1000 | Uniform  |   675.6 ns |    21.91 ns |  1.20 ns |  0.92 |    0.01 |         - |          NA |
| Shared             | Property            | 1000 | Uniform  |   662.7 ns |    66.52 ns |  3.65 ns |  0.90 |    0.01 |         - |          NA |
| Record             | Property            | 1000 | Uniform  | 1,098.9 ns |   173.37 ns |  9.50 ns |  1.50 |    0.02 |         - |          NA |
| Native             | Property            | 1000 | Uniform  | 4,152.3 ns | 1,637.62 ns | 89.76 ns |  5.65 |    0.13 |         - |          NA |
|                    |                     |      |          |            |             |          |       |         |           |             |
| **Auto**               | **Property**            | **1000** | **Skewed80** |   **472.8 ns** |    **26.27 ns** |  **1.44 ns** |  **1.00** |    **0.00** |         **-** |          **NA** |
| ExplicitAuto       | Property            | 1000 | Skewed80 |   478.1 ns |   114.05 ns |  6.25 ns |  1.01 |    0.01 |         - |          NA |
| Typed              | Property            | 1000 | Skewed80 |   475.7 ns |    36.91 ns |  2.02 ns |  1.01 |    0.00 |         - |          NA |
| Shared             | Property            | 1000 | Skewed80 |   456.4 ns |    21.59 ns |  1.18 ns |  0.97 |    0.00 |         - |          NA |
| Record             | Property            | 1000 | Skewed80 | 1,459.8 ns |   192.83 ns | 10.57 ns |  3.09 |    0.02 |         - |          NA |
| Native             | Property            | 1000 | Skewed80 | 2,341.6 ns |   337.76 ns | 18.51 ns |  4.95 |    0.04 |         - |          NA |
