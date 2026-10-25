# Nullable variant patterns on CoreCLR

CoreCLR handles the proposed nullable accessors substantially better than Unity
Mono or IL2CPP. The shuffled larger-switch cases consistently favor nullable
matching on .NET 8, .NET 10, and the installed .NET 11 preview. There is still no
general performance-parity guarantee: small cases have minor penalties, and
predictable later matches can strongly favor direct discriminator dispatch.

The user approved retaining this as a
[future API improvement for Unity CoreCLR](../designs/2026-10-25-future-coreclr-nullable-variant-accessors.md).
Cleaner switch syntax is the main motivation; performance is a constraint to
revalidate in Unity's eventual backend.

These differences are runtime/JIT behavior, not a C# language-version benefit.
The experiment runs the original Unity-compiled matcher assembly unchanged.
Recompiling its source with newer Roslyn, under both C# 9 and C# 14, produces
identical normalized instructions and local types in all 179 checked methods.

## Controlled runtime comparison

Same Linux x64 Ryzen 9 9900X, CPU 0 affinity, input arrays, storage, payload
arithmetic, validation, 15 match kernels and four distributions as the
[Unity experiment](2026-10-25-unity-nullable-variant-patterns.md). Three fresh
processes per configuration, 15 timing samples per kernel/distribution, each
calibrated to approximately 20 ms. Values are medians of process medians.

The primary comparison disables tiered compilation and dynamic PGO so every
matcher is compiled once with full optimization. This isolates runtime versions
from profile-training and tier-promotion effects. It is an optimized JIT
comparison, not CoreCLR's default tiering configuration; the latter is checked
separately below. Match functions retain the same explicit NoInlining boundary
as the Unity test.

Shuffled tags, **nanoseconds per complete operation**, current → nullable:

| Workload | .NET 8.0.22 | .NET 10.0.12 | .NET 11 preview 3 |
|---|---:|---:|---:|
| 2: Idle / Vector2 + float Teleport | 1.224 → 1.288 | 1.032 → 1.128 | 1.050 → 1.131 |
| 6: mixed unmanaged/reference/managed struct | 10.439 → 4.138 | 9.961 → 3.177 | 9.930 → 3.233 |
| 12: four floats per variant | 8.567 → 3.082 | 8.876 → 3.025 | 8.971 → 2.977 |
| 32: two floats per variant | 8.965 → 6.463 | 8.028 → 4.557 | 8.191 → 4.624 |
| Single Teleport test, hits and misses | 1.223 → 1.359 | 1.010 → 1.181 | 1.001 → 1.190 |

The exact preview runtime is `11.0.0-preview.3.26207.106`. Its FrameworkDescription
retains that identifier; Environment.Version reports `11.0.0`. This is preview
evidence, not a result for a released .NET 11 runtime.

The .NET 10 nullable penalties in the small complete switch and single test are
about 0.10 and 0.17 ns per operation respectively. Shuffled larger switches
improve by about 68%, 66%, and 43% for six, twelve, and thirty-two variants. The
same wins persist against aggressively inlined nonnullable getters: those
controls take 9.951, 9.084, and 7.967 ns respectively.

This does **not** hold for every distribution. For .NET 10 with every input
selecting the last variant:

| Workload | Current | Nullable |
|---|---:|---:|
| 2 variants | 1.249 | 1.272 |
| 6 variants | 2.898 | 3.505 |
| 12 variants | 1.498 | 4.696 |
| 32 variants | 1.310 | 6.924 |

The twelve- and thirty-two-variant cycling cases also favor the direct switch.
The current native methods use discriminator jump tables; nullable methods use
conditional chains. Their contrasting shuffled/predictable behavior is consistent
with branch-prediction effects, but this experiment does not collect hardware
branch-miss counters and does not establish that as the exclusive cause.

See [all tables](../../tmp/unity-nullable-variants-20261025/coreclr/results/tables.md)
and the [raw process medians and samples](../../tmp/unity-nullable-variants-20261025/coreclr/results/summary.json).

## What CoreCLR optimizes

Native JIT inspection shows:

- Nullable constructors, HasValue accessors and GetValueOrDefault calls are
  inlined already on .NET 8. The unmanaged nullable match methods contain no
  calls. This avoids the retained constructor/getter calls seen in IL2CPP.
- Nullable payload fields and state are largely handled as scalar register
  values; the entire operation does not reliably collapse into the current
  discriminator switch. Extra flag tests, zeroing and branches still remain.
- On .NET 10, Small2 grows from 81 bytes for the current pattern to 106 bytes
  nullable. Dense12 grows from 633 to 1,239 bytes; Dense32 from 834 to 2,011 bytes.
  The faster shuffled nullable timings therefore do not imply smaller code.
- One directly observable runtime improvement: the mixed nullable matcher on
  .NET 8 retains a `CORINFO_HELP_UNBOX_NULLABLE` call when reading the boxed
  managed-containing struct. On .NET 10 and the .NET 11 preview, the same matcher
  has no calls. Its input IL is unchanged.
- All fifteen .NET 10 and .NET 11 preview match methods have the same reported
  native sizes. Twelve also have identical textual instruction listings after
  stripping JIT comments/labels. The sampled preview does not show a broad new
  optimization that erases nullable-state checks.

The evidence supports a clear boundary: CoreCLR already optimizes nullable
construction well in .NET 8, and newer runtimes can further improve generated
code. It does not identify a specific first runtime release introducing those
optimizations; versions earlier than .NET 8 were not tested.

Captured JIT output and summaries:

- [.NET 8](../../tmp/unity-nullable-variants-20261025/coreclr/results/net8-jit.asm)
- [.NET 10](../../tmp/unity-nullable-variants-20261025/coreclr/results/net10-jit.asm)
- [.NET 11 preview](../../tmp/unity-nullable-variants-20261025/coreclr/results/net11preview-jit.asm)
- [Method sizes, calls and branches](../../tmp/unity-nullable-variants-20261025/coreclr/results/jit-methods.json)
- [Runtime code comparison](../../tmp/unity-nullable-variants-20261025/coreclr/results/jit-comparison.json)

## Compiler comparison

Unity's saved response file specifies `-langversion:9.0`. Its bundled compiler
reports Roslyn `4.10.0-3.25064.8 (85262f5f)`. The installed .NET SDK 10.0.401
compiler reports `5.9.0-1.26423.113`.

The original source was compiled under the newer compiler with LangVersion 9
and 14, Release optimization, the same UnityEngine.CoreModule reference, and
net8.0 as the target framework for both controls. This keeps runtime API targets
constant. Metadata tokens are resolved to member/type identities before comparing
IL; assembly-qualified generic type spelling is normalized.

All 179 matcher/accessor method instruction sequences and local type lists match
across the original Unity assembly and both newer compiler controls. The
benchmark does not need a new language feature or modern compiler to obtain
CoreCLR's improved results. A hash check also verifies that the Unity matcher
DLL copied beside the CoreCLR host is byte-identical to the actual Unity player
DLL used previously.

[Compiler comparison and method fingerprints](../../tmp/unity-nullable-variants-20261025/coreclr/results/compiler-comparison.json)
and [binary/source hashes](../../tmp/unity-nullable-variants-20261025/coreclr/results/sha256.json)
are retained.

## Tiered compilation and PGO check

Three additional .NET 10 processes enable tiered compilation and dynamic PGO.
The captured output confirms promotion through instrumented Tier0 to Tier1.
Extra warmup runs precede calibration. Shuffled nullable timings are 1.094 ns
(Small2), 3.819 ns (Mixed6), 3.528 ns (Dense12), and 6.303 ns (Dense32).

The workload order remains first, last, cycling, shuffled, in one process.
Consequently early constant-tag traffic can influence Tier1 profiles for later
distributions. Some ordinary current-pattern getters remain as cold-path calls;
the explicitly hinted nonnullable control helps distinguish this from nullable
matching itself. For Small2, the shuffled ordinary control takes 4.676 ns, but
the hinted control takes 1.043 ns, versus 1.094 ns nullable. The larger nullable
shuffled wins remain against the hinted control. These results should not be
interpreted as independently trained profiles for each distribution.

[PGO JIT output](../../tmp/unity-nullable-variants-20261025/coreclr/results/net10pgo-jit.asm)
and all hinted-control timings are retained. The controlled non-tiered comparison
above is the basis for runtime-version comparisons.

## Validation and reproduction

All twelve fresh processes passed 61,664 per-input correctness checks each,
including inactive nullable accessors, invalid tags, null references, and boxed
managed structs. Every sample checksum was validated: **10,800 timed checksums**
plus one/two/three-pass loop checks. Preparation and boxing of test inputs remain
outside timing. The host and both compiler-control projects build with zero
warnings and errors. Production generator/package code was not changed.

The CoreCLR host links the original Unity-compiled matcher DLL and its original
UnityEngine.CoreModule dependency. Vector constructors and field accesses used
in these kernels execute successfully without invoking Unity native engine APIs.
The timing/checksum loop source is reused with the host class renamed; Unity
startup, logging and JSON output are replaced by console/System.Text.Json code.

```sh
python3 tmp/unity-nullable-variants-20261025/coreclr/make_probe.py
```

Build `CoreCost.csproj` in that directory with the pinned .NET SDK 8.0.319.
Build the `compiler9/Modern9.csproj` and `compiler14/Modern14.csproj` controls
from their directories, which pin SDK 10.0.401. A writable DOTNET_CLI_HOME can be
used for first-run SDK sentinels. Then run:

```sh
python3 tmp/unity-nullable-variants-20261025/coreclr/run.py
python3 tmp/unity-nullable-variants-20261025/coreclr/analyze.py
```

The runner uses `dotnet --fx-version` to select each exact runtime and resumes
from existing result JSON files; remove those files to obtain fresh runs.
The analyzer independently checks compiler IL, original DLL identity and native
JIT output. ARM, NativeAOT, different CPUs, caller-inlined switches, independently
trained PGO distributions and full applications remain outside this experiment.
