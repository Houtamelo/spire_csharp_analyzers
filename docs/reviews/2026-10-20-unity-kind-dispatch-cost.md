# Cost of selecting union storage by discriminator in Unity

Simple discriminator-based getters can be very cheap. In this probe, replacing
two fixed-location float getters with small ternary selectors added effectively
no time for constant or cycling variants on Mono, and 0.26 ns per complete match
for shuffled variants. Larger switch getters had measurable costs on both
backends. IL2CPP eliminated their repeated selection checks in the aggressively
inlined, kind-first examples; Mono did not, and forcing large getters to inline
made some cases substantially worse.

These are timings of hand-written storage-design prototypes, not current Spire
generator output. Results support distinguishing small selectors from large
getter switches. They do not establish a universal cost per branch or settle the
storage-policy decision.

## Matching property-pattern comparisons

The following numbers are nanoseconds per complete operation, including an array
read, a non-inlined match function, its payload reads and arithmetic, accumulation,
and loop overhead. They are medians of three fresh-process medians. Both sides
use matching property patterns; fixed getters read one location, while branching
getters select storage using `kind`. The large ordinary getters have no inlining
attribute. Each workload uses a repeating, deterministically shuffled sequence
of 4,096 valid variants.

| Workload | Mono fixed | Mono branching | Difference | IL2CPP fixed | IL2CPP branching | Difference |
|---|---:|---:|---:|---:|---:|---:|
| 3 variants, 2 float reads, ordinary switch getters | 21.247 | 24.344 | +3.097 | 0.944 | 0.988 | +0.044 |
| Same small union, ternary getters | 21.247 | 21.511 | +0.264 | 0.944 | 0.990 | +0.045 |
| 12 variants, 6 float reads, ordinary switch getters | 38.077 | 46.495 | +8.418 | 8.261 | 19.641 | +11.380 |
| 32 variants, 8 float reads, ordinary switch getters | 45.864 | 56.878 | +11.014 | 6.565 | 25.588 | +19.024 |
| 5 variants, 3–5 mixed payload reads, ordinary getters | 13.733 | 17.831 | +4.098 | 12.879 | 14.080 | +1.201 |

The small example is the earlier `A(x,y), B(y,z), C(z,x)` arrangement, with two
physical float slots versus three fixed slots. The ordinary and inlining-hinted
switch getters return zero for inactive properties. The ternary version only
selects between its two physical slots; inactive-property values are unspecified,
matching the fixed overlapping-storage control. Every timed property access is
valid for its selected variant.

The dense examples deliberately give variants distinct permutations of all six
or eight payload slots. Canonical fixed storage has the same size and logical
property values. These stress dispatch complexity; the permutations themselves
provide no size saving over fixed storage in these examples. The mixed example
uses `Vector3`, `Quaternion`, `double`, `int`, and `float` and a separate explicit
value helper. Its fixed counterpart also uses a 40-byte value helper, overlapping
only fields that never occur together.

| Representation | Branching struct bytes | Fixed struct bytes |
|---|---:|---:|
| Small, two versus three floats | 12 | 16 |
| Dense, six floats | 28 | 28 |
| Dense, eight floats | 36 | 36 |
| Mixed payloads | 48 | 48 |

These sizes include the discriminator and padding. They were checked in each
player; all benchmark structs are blittable. The timing workload stays in cache
after warmup and does not establish the benefit of the smaller small-union
representation in a memory-bandwidth-bound workload.

## Predictability and inlining

| Getter workload | Mono extra over matching fixed pattern, constant / cycling / shuffled | IL2CPP extra, constant / cycling / shuffled |
|---|---|---|
| Small ternary selectors | +0.003 / -0.114 / +0.264 ns | +0.044 / +0.045 / +0.045 ns |
| Dense 12, ordinary | +6.664 / +6.035 / +8.418 ns | +0.290 / +0.679 / +11.380 ns |
| Dense 32, ordinary | +8.776 / +8.464 / +11.014 ns | +1.644 / +4.508 / +19.024 ns |

These differences subtract the displayed aggregate medians. Raw results also
include paired process differences, process ranges, and every timing sample.
The small differences are workload effects, not isolated selector latencies.
Constant means kind zero for every element. Cycling visits kinds in order.
Shuffled uses a fixed xorshift-generated tag sequence, which repeats; a branch
predictor may learn that sequence. It is not a worst-case unpredictability test.

There is a second control for each packed pattern: a direct-slot function with
the exact same packed input representation, one outer kind dispatch, and no
getter storage selection. This isolates repeated selection more closely than
changing the storage layout, although Mono's treatment of property-pattern
locals still contributes to the difference.

| Packed workload, shuffled | Mono ordinary / hinted / direct | IL2CPP ordinary / hinted / direct |
|---|---|---|
| Small | 24.344 / 27.857 / 11.488 ns | 0.988 / 0.992 / 0.990 ns |
| Dense 12 | 46.495 / 73.929 / 16.830 ns | 19.641 / 10.755 / 10.127 ns |
| Dense 32 | 56.878 / 175.787 / 18.019 ns | 25.588 / 11.501 / 11.185 ns |
| Mixed | 17.831 / 16.495 / 11.286 ns | 14.080 / 10.450 / 9.916 ns |

The dense direct columns use the controls for the inlining-hinted input types.
Separate ordinary-type direct controls are included in the full results.
Small IL2CPP pattern, ordinary, ternary, and direct functions have identical
normalized native instructions. Dense 12 and 32 hinted functions also match
their respective direct controls. Their small timing differences therefore do
not establish residual getter-branch cost. Code placement, calling-loop layout,
and branch-predictor state can affect the measurements.

The field-first, six-property pattern retains six getter dispatches plus the
outer dispatch in IL2CPP. It takes 16.465 ns for shuffled variants versus
10.127 ns for its direct control. With constant variants it takes 7.065 ns versus
8.245 ns: retaining more branches does not necessarily make a particular workload
slower. Six common properties read without an outer switch take 15.603 ns versus
8.144 ns for canonical fixed slots on IL2CPP, and 40.361 versus 9.033 ns on Mono.
Those common properties exist in every variant, so these accesses are valid.

## What the native code explains

- IL2CPP removes getter calls and repeated selection in the small and hinted
  kind-first examples. Dense ordinary getters retain six or eight calls per
  operation; their getters each contain a tag dispatch.
- The ordinary mixed pattern and the matching fixed mixed pattern both retain
  their getter calls, including the simple fixed getters. The hinted mixed
  pattern removes those calls. Its advantage over the unhinted fixed pattern is
  therefore not evidence that branching storage beats equally inlined fixed
  storage. A selected mixed case executes three to five getters; the 18 call
  sites in the method are distributed across five cases.
- Mono retains storage-selection work. Its property-pattern captures also cause
  substantial local initialization, floating-point conversions, and spills,
  including in fixed patterns. Comparing only against manual direct-slot code
  would overstate the cost attributable to `kind` selection.
- Forcing the large getters to inline produces a particularly poor Mono result.
  The captured Dense 12 hinted method occupies 47,428 bytes versus 6,500 for
  ordinary getters; Dense 32 occupies 348,452 versus 23,684. The hinted method
  prologues initialize many additional temporaries. Their timings measure that
  generated work as well as retained branches. These are Mono-reported JIT
  extents, which can include inline data; they are not just instruction counts.

The implication is narrower than "branching is expensive": small selectors can
be negligible, and large getters need measurement. A blanket inlining hint is
also unsuitable across these two backends. For scale, the ordinary dense IL2CPP
differences above would correspond to about 1.14–1.90 ms for 100,000 complete
operations, if an application reproduced this workload and throughput. This is
arithmetic extrapolation, not a measured frame-time result.

## Configuration, checks, and reproduction

Unity 6000.5.6f1, nondevelopment Linux x64 players, bundled Mono and IL2CPP
Release/OptimizeSpeed, Low managed stripping, Ryzen 9 9900X. The benchmark thread
is pinned to CPU 0. Builds reported zero errors and warnings. Three fresh player
processes per backend collect 15 samples for each of 23 kernels and three
distributions. Each timed sample is calibrated to approximately 20 ms. Kernel
and backend order are varied to distribute drift. Four independent accumulators
consume the results; no delegate call occurs per element, only once per timed
batch. Preparation, allocation, validation, and report writing are outside timing.
Payloads are exactly representable for the checked arithmetic and checksums.

Each process passed 18,450 per-input result checks, covering valid and invalid
tags, varied negative/zero/positive payloads, and both guard outcomes in reused
probes. It also checks each benchmark checksum for one, two, and three array
passes before timing, then verifies every timed checksum. All six final player
processes passed. The separate initial run and source snapshot are retained in
`results-initial` and `sources-initial`; the report uses the final matching-control
run under `results`.

Native inspection uses the shipped IL2CPP `GameAssembly.so` and matching debug
symbols. Mono code is captured after timing using the runtime's reported JIT
start addresses and sizes. Normalized comparisons ignore code/data addresses
and do not compare jump-table contents; control flow was also inspected.
These results do not cover ARM, mobile CPUs, other Unity versions, cold caches,
streaming-memory throughput, or a full game update loop.

- [Benchmark generator](../../tmp/unity-kind-cost-20261020/make_probe.py)
- [Timing and validation harness](../../tmp/unity-kind-cost-20261020/KindCost.cs)
- [Generated kernels](../../tmp/unity-kind-cost-20261020/project/Assets/CostLoops.cs)
- [Matching fixed property patterns](../../tmp/unity-kind-cost-20261020/project/Assets/FixedProbes.cs)
- [Full timing tables](../../tmp/unity-kind-cost-20261020/results/tables.md)
- [Raw summary, process ranges, and paired comparisons](../../tmp/unity-kind-cost-20261020/results/summary.json)
- [Source and player artifact hashes](../../tmp/unity-kind-cost-20261020/results/sha256.json)
- [Native packed-control comparison](../../tmp/unity-kind-cost-20261020/results/native-comparison.json)
- [Native fixed-pattern comparison](../../tmp/unity-kind-cost-20261020/results/native-fixed-comparison.json)
- [Mono large inlined getter pattern](../../tmp/unity-kind-cost-20261020/results/mono-1-ComplexProbe_Dense12InlinePattern.asm)
- [IL2CPP field-first pattern](../../tmp/unity-kind-cost-20261020/results/il2cpp-ComplexProbe_FieldsFirst.annotated.asm)
- [Earlier instruction-only investigation](2026-10-20-unity-kind-dispatch-complexity.md)

```sh
python3 tmp/unity-kind-cost-20261020/make_probe.py
dotnet format whitespace tmp/unity-kind-cost-20261020/project/Assets --folder --verbosity quiet
python3 tmp/unity-kind-cost-20261020/build.py
python3 tmp/unity-kind-cost-20261020/run_players.py
python3 tmp/unity-kind-cost-20261020/summarize.py
python3 tmp/unity-kind-cost-20261020/extract.py
```

The generator reuses the preserved sources from the earlier probes. Build scripts
use the installed Editor and the existing isolated IL2CPP linker dependencies.
No Spire storage implementation, package version, or installed example package
was changed.
