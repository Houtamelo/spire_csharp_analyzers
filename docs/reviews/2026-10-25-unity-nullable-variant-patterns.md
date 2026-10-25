# Nullable variant patterns in Unity Mono and IL2CPP

The proposed nullable accessor works correctly, but it introduces measurable
performance regressions on both tested backends. `AggressiveInlining` does not
make it equivalent to the current discriminator plus payload pattern. Keep the
current pattern as the performance-oriented path; replacing it with nullable
accessors would be an explicit ergonomics/performance tradeoff.

No production generator or package source was changed for this experiment.

The idea is retained as a [future API improvement for Unity CoreCLR](../designs/2026-10-25-future-coreclr-nullable-variant-accessors.md),
primarily for cleaner matching syntax, subject to backend performance validation.

A [CoreCLR follow-up](2026-10-25-coreclr-nullable-variant-patterns.md) runs the
same Unity-compiled matchers on .NET 8, .NET 10 and a .NET 11 preview. Its larger
shuffled nullable cases improve substantially, so the Unity results here must
not be generalized to CoreCLR.

## Measurements

Unity **6000.5.6f1**, Linux x64 nondevelopment players, Ryzen 9 9900X, benchmark
thread pinned to CPU 0. IL2CPP uses Release, OptimizeSpeed, Low managed stripping.
Both player builds completed with zero warnings and zero errors.

These are nanoseconds per complete operation: array access, a non-inlined match
function, payload extraction and arithmetic, accumulation, and loop overhead.
Values are medians of three fresh-process medians, each containing 15 samples.
Each sample is calibrated to approximately 20 ms. The input array has 4,096
elements and remains warm. Shuffled tags use a fixed xorshift sequence and repeat;
they are not a worst-case branch-predictor workload.

| Shuffled variants | Mono current | Mono nullable | IL2CPP current | IL2CPP nullable |
|---|---:|---:|---:|---:|
| 2: empty Idle / Vector2 + float Teleport | 10.473 | 11.617 | 3.450 | 6.116 |
| 6: mixed unmanaged, references, managed struct | 13.741 | 18.476 | 8.436 | 8.466 |
| 12: four floats per variant | 31.951 | 52.748 | 9.734 | 7.453 |
| 32: two floats per variant | 27.120 | 59.731 | 5.790 | 25.688 |
| Single Teleport test, including hits and misses | 10.425 | 11.592 | 3.453 | 6.052 |

The small case directly models the request: `Idle?` and `Teleport?`, with a
`Vector2` and `float` in Teleport. Nullable getters have the requested aggressive
inlining hint. The current-pattern control has the existing plain getter; a
second control adds the same hint to its nonnullable getter. The hinted control
does not remove the nullable regressions: the shuffled 32-variant hinted results
are 24.302 ns on Mono and 5.769 ns on IL2CPP, versus 59.731 / 25.688 ns nullable.

Earlier and later arms behave differently. With every tag selecting the last
of 32 variants, current versus nullable is **20.725 → 72.881 ns** on Mono and
**0.951 → 28.405 ns** on IL2CPP. The latter is roughly 30× but an absolute
difference of 27.454 ns. With the first variant selected, the corresponding
IL2CPP result is 0.952 → 5.760 ns. These comparisons include complete operations,
not isolated branch latencies.

The shuffled 12-variant IL2CPP example improves by about 23%, whereas its
constant-tag and cycling cases regress. That benefit is specific to this
workload and generated control flow. The measurements do not support claiming
that nullable matching is always slower, or that it is performance-neutral.

All distributions, inlining controls, individual process medians and raw timing
samples are retained in the [tables](../../tmp/unity-nullable-variants-20261025/results/tables.md)
and [summary](../../tmp/unity-nullable-variants-20261025/results/summary.json).

## What survives optimization

C# lowers a nullable payload type pattern to a getter call, `HasValue`, and
`GetValueOrDefault`. The getter first compares the discriminator, then returns
either a constructed `Nullable<TPayload>` or its zero/default value. There is
no compiler-visible enum switch over the union in the nullable consumer.

In the final IL2CPP native code:

- The small current-pattern method is 79 bytes. Its nullable counterpart is
  420 bytes and retains calls to the specialized `Nullable<T>` constructors,
  nullable-state tests, and metadata-initialization guards.
- The 12-variant current method has a single discriminator dispatch. Its
  nullable counterpart retains sequential checks and some out-of-line getters,
  despite their inlining hints.
- The 32-variant current method is 699 bytes and uses one discriminator
  dispatch. Its nullable counterpart is 3,382 bytes, with constructor calls and
  numerous getter calls for later arms. The hint is not a guarantee that all
  this work disappears.

Metadata initialization occurs on cold paths, but its guards remain in warm
code. Static call/branch counts include those cold paths and must not be read
as counts executed on every operation. Method sizes also exclude callees.

Mono retains more temporary initialization, payload copying and nullable-state
handling. The small method grows from a reported JIT extent of 372 to 1,188
bytes; the 32-variant method grows from 6,564 to 24,052 bytes. Mono-reported JIT
extents can include inline data. Size differences alone are not timing results.

The practical issue is broader than one extra discriminator comparison:
optimization does not reliably collapse the entire nullable construction and
test back into the current direct discriminator dispatch.

See the [native comparison](../../tmp/unity-nullable-variants-20261025/results/native-comparison.json),
[small current IL2CPP method](../../tmp/unity-nullable-variants-20261025/results/il2cpp-NullableProbes_Small2Baseline.annotated.asm),
[small nullable method](../../tmp/unity-nullable-variants-20261025/results/il2cpp-NullableProbes_Small2Nullable.annotated.asm),
[32-variant nullable method](../../tmp/unity-nullable-variants-20261025/results/il2cpp-NullableProbes_Dense32Nullable.annotated.asm),
and [Mono method sizes](../../tmp/unity-nullable-variants-20261025/results/mono-method-sizes.json).

## Correctness, storage, and allocation boundaries

The benchmark uses handwritten prototypes of the current and proposed accessor
shapes. Each scenario exposes all three access styles on the **same input struct**,
with identical offset-zero unmanaged helpers, reference storage, payload types,
and arithmetic. The experiment isolates access/matching behavior; it is not a
generator implementation or an end-to-end Inspector benchmark.

The six-variant scenario includes empty, Vector2, Vector3, Quaternion, string,
and a struct containing string + int payloads. It uses one shared object slot
for the two managed cases. Reference casts and nullable unboxing are identical
in all getter controls. Boxing of the managed-containing struct happens during
input preparation, outside timing, for every access style.

Every fresh player passed **61,664 per-input correctness checks** before timing.
Checks include every valid tag, two invalid tags, negative/zero/positive payloads,
null strings, managed-containing structs, and `HasValue` on every active/inactive
nullable accessor. Each loop is also checked for one, two, and three complete
array passes; all 5,400 timed checksums across the six processes were verified.

IL inspection found **no `box` instructions in all 71 matcher/accessor bodies**.
Nullable construction and matching here introduce value temporaries, not boxed
payload objects. No allocation-counter claim is made. Preparation already boxes
the managed struct, and metadata/runtime initialization is outside the warmed
steady-state comparison. The
[IL audit](../../tmp/unity-nullable-variants-20261025/results/il-boxing-check.json)
records the checked methods and their calls.

This does not cover ARM/mobile CPUs, other Unity releases, Burst, caller-inlined
switches, guarded or reordered arms, generic payloads, cold startup, streaming
memory pressure, or a full game update loop. The explicit non-inlined matcher
boundary is identical for all controls and makes native inspection reproducible;
different caller contexts can change optimization and timings. The demonstrated
regressions are sufficient to reject a general performance-parity assumption.

## Reproduction and evidence

The [source generator for the experiment](../../tmp/unity-nullable-variants-20261025/make_probe.py)
reuses the established calibration, CPU pinning, timing-order rotation, checksum,
and Mono native-code capture harness. It generates 15 kernels and four input
distributions, with four independent accumulators and no delegate call per
element. Delegates run only once per timed batch. Preparation, validation,
collection and file output are outside timing. Backend order alternates across
fresh-process repetitions.

```sh
python3 tmp/unity-nullable-variants-20261025/make_probe.py
dotnet format whitespace tmp/unity-nullable-variants-20261025/project/Assets --folder --verbosity quiet
python3 tmp/unity-nullable-variants-20261025/build.py
python3 tmp/unity-nullable-variants-20261025/run_players.py
python3 tmp/unity-nullable-variants-20261025/extract.py
python3 tmp/unity-nullable-variants-20261025/verify_il.py
```

The build uses the installed Unity editor and isolated IL2CPP linker libraries.
IL inspection uses the locally installed ilspycmd, rolling its .NET 6 runtime
requirement forward to an installed runtime. Native extraction uses the shipped
`GameAssembly.so` and its matching debug symbols; Mono bodies are captured after
timing from runtime-reported native addresses and sizes. File
[hashes](../../tmp/unity-nullable-variants-20261025/results/sha256.json)
tie the generated C# and native binary to the retained evidence.
