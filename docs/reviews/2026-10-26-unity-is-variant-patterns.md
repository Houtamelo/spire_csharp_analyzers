# Boolean variant patterns in Unity Mono and IL2CPP

## Request recorded before experimentation

Compare `{ IsVariant: true }` against `{ kind: Union.Kind.Variant }`, including
a third control whose boolean getter uses
`[method: MethodImpl(MethodImplOptions.AggressiveInlining)]`.

The approved analyzer design treats generated boolean variant predicates as
equivalent discriminator constraints. This experiment determines whether the
shorter pattern syntax changes runtime cost; it does not implement that analyzer
design or change production generator output.

Use the same union storage, payload extraction, arithmetic, input arrays, and
non-inlined consumer boundaries for each control. Cover small and large unions,
predictable and shuffled variants, and both complete switches and single-variant
tests. Validate per-input results and timed checksums. Record Unity backend,
version, player configuration, repeated timings, and native code evidence.

## Results

The shorter boolean property pattern is viable. IL2CPP recovers the same native
dispatch in the tested payload switches; Mono retains a chain of boolean tests,
so its performance depends on which variants occur. `AggressiveInlining` adds no
code-generation benefit here: both backends already inline these small predicates.

Unity **6000.5.6f1**, Linux x64 nondevelopment players, Ryzen 9 9900X, benchmark
thread pinned to CPU 0. IL2CPP uses Release, OptimizeSpeed, Low managed stripping.
Each value below is nanoseconds per complete operation, including the array read,
a non-inlined matcher, payload extraction/arithmetic, accumulation, and loop
overhead. It is not an isolated getter or branch latency.

The following inputs use a fixed shuffled sequence of 4,096 valid variants:

| Scenario | Mono kind | Mono bool | Mono inline bool | IL2CPP kind | IL2CPP bool | IL2CPP inline bool |
|---|---:|---:|---:|---:|---:|---:|
| 2 variants: empty / Vector2 + float | 10.818 | 11.013 | 11.034 | 3.503 | 3.496 | 3.491 |
| 6 mixed variants | 14.010 | 9.648 | 9.601 | 8.512 | 8.679 | 8.663 |
| 12 variants, four floats each | 30.741 | 24.902 | 24.837 | 9.717 | 9.599 | 9.479 |
| 32 variants, two floats each | 26.915 | 22.783 | 22.832 | 6.083 | 6.040 | 5.913 |
| Single Teleport test, including hits and misses | 10.833 | 11.016 | 11.037 | 3.504 | 3.499 | 3.499 |

The Mono improvements for shuffled inputs do not generalize to predictable inputs:

| Mono workload | kind | bool | inline bool |
|---|---:|---:|---:|
| 32-variant payload switch, cycling | 20.624 | 22.795 | 22.836 |
| 32-variant payload switch, always last | 20.716 | 26.637 | 26.653 |
| 32-variant discriminator-only switch, always last | 5.585 | 11.155 | 11.203 |
| 6-variant mixed payload switch, always last | 7.850 | 9.797 | 9.814 |

For the 32-variant payload switch, plain boolean matching is about 15% faster on
shuffled inputs but 29% slower when always selecting the last arm. The latter
adds about 5.9 ns per complete operation. Single-variant tests remain close to
their discriminator controls. Boolean matching therefore provides cleaner syntax
without guaranteeing identical throughput on Mono.

The single last-variant test on shuffled Mono inputs takes 5.131 ns with `kind`
versus 5.619 / 5.632 ns with ordinary/hinted boolean predicates: about 0.5 ns
additional cost, without the long chain needed by the complete switch.

Inlining-hinted and ordinary boolean versions have identical normalized IL2CPP
instructions in all seven scenario families. Their small timing differences,
including the discriminator-only probes, do not demonstrate an attribute benefit;
code placement, caller layout, and predictor state can affect these tiny timings.
Both source forms remain supported. After reviewing these measurements, the user
selected the shorter `IsVariant: true` form for generated **Add missing variant
arms** output. These results do not justify adding the hint to production getters.

All final probes use a `byte` discriminator, matching `VariantPayloadEmitter.KindType`
for the tested variant counts. The earlier int-discriminator controls remain
preserved separately; the measurements reported here are the final byte runs.

All four distributions, all seven scenario families, process medians/ranges, and
raw samples are retained in the [timing tables](../../tmp/unity-is-variant-byte-20261026/results/tables.md)
and [summary](../../tmp/unity-is-variant-byte-20261026/results/summary.json).

## Native code

The [native comparison](../../tmp/unity-is-variant-byte-20261026/results/native-comparison.json)
records method sizes, calls, branches, and normalized instruction comparisons.

- IL2CPP has identical normalized matcher instructions for all three controls in
  the two-, twelve-, and thirty-two-variant payload switches and both
  single-variant tests. The twelve- and thirty-two-variant switches use a single
  discriminator dispatch. No boolean getter calls remain.
- The mixed six-variant IL2CPP switch also uses a discriminator dispatch. Its
  `kind` version is 333 bytes; both boolean versions are 337 bytes, with the same
  payload calls and different block ordering/instructions. The remaining calls
  are payload extraction and a write barrier, not boolean predicates.
- The IL2CPP discriminator-only switch uses a lookup table in all controls. Its
  `kind` matcher is 32 bytes; both boolean versions are 42 bytes. Boolean lowering
  handles the final variant separately instead of using the same bounds check.
- Mono inlines both ordinary and hinted predicates but retains sequential
  compare/materialize-boolean/test sequences. The enum form has a jump-table
  dispatch. This explains the sensitivity to earlier versus later arms; branch
  prediction also affects the shuffled results.
- Ordinary and hinted boolean controls have the same reported Mono JIT extents
  and predicate-test structure in every scenario family. For the 32-variant
  payload matcher, `kind` occupies 6,564 bytes and both boolean versions 7,268.
  These extents can include inline data; they are not instruction counts.

Representative evidence:

- [IL2CPP 32-variant kind matcher](../../tmp/unity-is-variant-byte-20261026/results/il2cpp-PredicateProbes_Dense32Kind.annotated.asm)
- [IL2CPP 32-variant boolean matcher](../../tmp/unity-is-variant-byte-20261026/results/il2cpp-PredicateProbes_Dense32Plain.annotated.asm)
- [Mono discriminator-only kind matcher](../../tmp/unity-is-variant-byte-20261026/results/mono-1-PredicateProbes_Tags32Kind.asm)
- [Mono discriminator-only boolean matcher](../../tmp/unity-is-variant-byte-20261026/results/mono-1-PredicateProbes_Tags32Plain.asm)
- [Mono JIT extents](../../tmp/unity-is-variant-byte-20261026/results/mono-method-sizes.json)

Normalized IL2CPP comparisons ignore code/data addresses and do not compare jump
table contents. Per-input correctness checks independently validate the mappings.
Static call counts include all arms and cold paths; they are not calls executed
on each operation.

## Validation and boundaries

Each of six fresh player processes exited successfully and passed **123,820
per-input checks** before timing. These cover every valid variant, invalid tags,
41 payload seeds, null reference payloads, managed-containing structs, and every
active/inactive boolean predicate. Each process additionally validates one, two,
and three complete passes per kernel/distribution. All **7,560 timed checksums**
passed across the six processes.

The [final verification record](../../tmp/unity-is-variant-byte-20261026/results/verification.json)
checks report counts, checksums, backend settings, source/binary hashes, and native
comparison controls.

[IL inspection](../../tmp/unity-is-variant-byte-20261026/results/il-audit.json)
verified all 21 matcher and 104 predicate bodies: none contains a `box` instruction,
and all 52 hinted getters carry the requested method implementation flag. Managed
struct boxing in input preparation is identical across controls and outside
timing. No allocation-counter claim is made.

The probes are handwritten representations of the generated getter/storage shapes,
with all three controls on the same input structs and the same payload accessors.
They cover empty, Vector2, Vector3, Quaternion, string, managed-containing struct,
and float payloads. Serialized payload fields and editor/assertion paths are not
modeled. These are release player matching benchmarks, not end-to-end generator,
Inspector, or serialization tests.

Measurements are medians of three fresh-process medians, with 15 samples per
kernel/distribution calibrated to about 20 ms. Timing order rotates and backend
order alternates. Four independent accumulators consume results; delegates run
once per batch, not once per element. Preparation, validation, collection, and
file output remain outside timing. Shuffled tags repeat a deterministic xorshift
sequence and are not a worst-case branch predictor workload.

Results do not establish performance on ARM/mobile, other Unity releases, Burst,
CoreCLR, generic payloads, caller-inlined matchers, cold startup, or full game loops.
No production generator, analyzer, package version, or example source was changed
for this experiment. The approved analyzer work remains recorded in PENDING.md.

## Reproduction and build environment

```sh
python3 tmp/unity-is-variant-byte-20261026/make_probe.py
dotnet format whitespace tmp/unity-is-variant-byte-20261026/project/Assets --folder --verbosity quiet
python3 tmp/unity-is-variant-byte-20261026/build.py
python3 tmp/unity-is-variant-byte-20261026/run_players.py
python3 tmp/unity-is-variant-byte-20261026/extract.py
python3 tmp/unity-is-variant-byte-20261026/verify_il.py
python3 tmp/unity-is-variant-byte-20261026/finalize.py
```

The experiment reuses the established timing/build harness. Sources:
[probe generator](../../tmp/unity-is-variant-byte-20261026/make_probe.py),
[matchers and getters](../../tmp/unity-is-variant-byte-20261026/project/Assets/PredicateProbes.cs),
[timed loops and validation](../../tmp/unity-is-variant-byte-20261026/project/Assets/NullableLoops.cs),
[timing harness](../../tmp/unity-is-variant-byte-20261026/project/Assets/PredicateCost.cs).
[Hashes](../../tmp/unity-is-variant-byte-20261026/results/sha256.json) identify the
sources, shipped IL2CPP binary, and matching debug symbols.

Engine files contain an ACL entry with unmapped UID 4294967295. A probe-local
`cp --no-preserve=mode,ownership` shim avoids copying that unsupported ACL; the
engine installation remains untouched. Local player outputs receive executable
permissions before launch. IL2CPP uses the existing isolated linker-library
directory for `libxml2.so.2`.

Both final BuildPipeline reports succeeded with zero warnings/errors, and the
Editor exited cleanly (0):
[Mono](../../tmp/unity-is-variant-byte-20261026/build-Mono2x.txt),
[IL2CPP](../../tmp/unity-is-variant-byte-20261026/build-IL2CPP.txt),
[Editor log](../../tmp/unity-is-variant-byte-20261026/build-editor.log).

The initial `int`-discriminator experiment remains preserved separately under
`tmp/unity-is-variant-20261026`. Its first build reproduced the ACL copy failure;
a subsequent build completed but the Editor crashed during teardown. Those
initial environment failures do not apply to the clean final `byte` build.
