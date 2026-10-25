# Typed reference storage versus casts in Unity

Typed storage removed the runtime type checks and improved the inline read loops
on both Unity backends. This follow-up used Unity 6000.5.6f1 release Linux x64
players on the same Ryzen 9 9900X, with the benchmark thread pinned to CPU 0.

Three fresh player processes per backend each collected 21 samples per case.
The values below are medians of the three process medians, in nanoseconds per
complete read-loop iteration. The workload includes an array lookup, reference
conversion where needed, string length or class field read, accumulation, and
loop overhead. It measures throughput rather than isolated cast latency.

| Backend | Inline read | Typed storage | Explicit cast | Extra time with cast | Relative increase |
|---|---|---:|---:|---:|---:|
| Mono | String length | 0.435 | 0.541 | 0.106 | 24% |
| Mono | Inherited-class field | 0.391 | 0.583 | 0.192 | 49% |
| IL2CPP | String length | 0.319 | 0.368 | 0.049 | 15% |
| IL2CPP | Inherited-class field | 0.356 | 0.466 | 0.110 | 31% |

The string typed-storage process medians ranged from 0.435–0.440 ns on Mono and
0.318–0.343 ns on IL2CPP. Class typed-storage medians ranged from 0.390–0.391 ns
on Mono and 0.356–0.358 ns on IL2CPP. Percentages describe these tight, cache-hot
loops; they are not application-wide slowdowns or fixed per-cast latencies.
Code layout and additional optimization opportunities can contribute to the
difference. Compare the measurements within this build rather than subtracting
numbers from the earlier probe's different binary.

The same run also included `as`:

| Backend | Inline read | Typed storage | Explicit cast | `as` plus `!` |
|---|---|---:|---:|---:|
| Mono | String length | 0.435 | 0.541 | 0.539 |
| Mono | Inherited-class field | 0.391 | 0.583 | 0.559 |
| IL2CPP | String length | 0.319 | 0.368 | 0.368 |
| IL2CPP | Inherited-class field | 0.356 | 0.466 | 0.713 |

## Getter controls

To match actual backing storage, the non-inlined getters in this follow-up read
a field from a struct passed by reference. The wrappers each contain one field:
`object`, `string`, or `CastPayload`. The typed controls do not cast. All wrappers
are populated with references to the same runtime objects.

| Backend | Non-inlined getter and read | Typed field | Object field and cast | Object field and `as` |
|---|---|---:|---:|---:|
| Mono | String length | 0.716 | 0.732 | 0.904 |
| Mono | Inherited-class field | 0.716 | 0.776 | 0.884 |
| IL2CPP | String length | 0.715 | 0.721 | 0.718 |
| IL2CPP | Inherited-class field | 0.892 | 0.899 | 1.080 |

Call overhead and execution overlap mask much of the type-check cost in these
getter cases. The small IL2CPP typed/cast differences overlap the observed
process ranges, so they do not establish a meaningful difference there.

IL2CPP's typed string and class getters both compile to four bytes:

```asm
mov rax, [rdi]
ret
```

The calling loops retain calls to those getters; the compiler did not eliminate
the controls. Mono also emits a plain field load, surrounded by its normal
register save/restore sequence. Neither typed getter checks runtime type.
Subsequent dereferences still have the normal null-reference behavior.

All six player processes passed the reference-conversion and checksum
validations before timing. The allocation-counter limitation from the original
comparison still applies: its sanity check reports zero around an explicit
allocation, so allocation counts are not used as evidence.

## Reproduction and artifacts

The [earlier comparison](2026-10-20-unity-reference-casts.md) describes the shared
build configuration and sampling method. This follow-up adds typed getter
controls and changes the non-inlined cast getters to read backing fields.
The original probe and results are preserved separately.

- [Follow-up source](../../tmp/unity-typed-storage-comparison-20261020/project/Assets/CastComparison.cs)
- [Raw timing summary and process ranges](../../tmp/unity-typed-storage-comparison-20261020/results/summary.json)
- [IL2CPP typed string getter](../../tmp/unity-typed-storage-comparison-20261020/results/il2cpp-CastProbe_TypedStringSlot.asm)
- [IL2CPP typed class getter](../../tmp/unity-typed-storage-comparison-20261020/results/il2cpp-CastProbe_TypedClassSlot.asm)
- [Mono typed string getter](../../tmp/unity-typed-storage-comparison-20261020/results/mono-1-TypedStringSlot.asm)
- [Extracted IL2CPP C++](../../tmp/unity-typed-storage-comparison-20261020/results/il2cpp-probe.cpp)

Rebuild the follow-up `project` with `CastComparisonBuild.Run` using the same
Editor and isolated linker dependencies as the earlier comparison. Run the
players and extract the assembly after the build has finished:

```sh
python3 tmp/unity-typed-storage-comparison-20261020/run_players.py
python3 tmp/unity-typed-storage-comparison-20261020/extract_il2cpp.py
```

No Spire storage implementation was changed by either comparison.
