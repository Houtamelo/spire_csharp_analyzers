# Unity reference cast comparison

On Unity 6000.5.6f1 Linux x64, replacing `(T)value` with `(value as T)!` did
not consistently improve successful reads. Inlined string reads were effectively
tied on both Mono and IL2CPP. Inherited-class reads favored different forms on
the two backends; IL2CPP favored the explicit cast.

## Configuration and method

- AMD Ryzen 9 9900X; benchmark thread pinned to logical CPU 0.
- Real StandaloneLinux64 release players, development mode disabled.
- Both backends built by Unity 6000.5.6f1. IL2CPP used Release, OptimizeSpeed,
  and Low managed stripping. C# compilation enabled optimization.
- Three fresh player processes per backend, 21 timed samples per case per process.
  Each case was warmed twice, then calibrated toward 50 ms, capped at 67,108,864
  iterations per sample. Case order rotated and reversed between rounds.
- An array of 1,024 non-null objects supplied runtime values. String lengths
  varied; class inputs alternated between two subclasses of a nonsealed base.
- All measured conversions succeeded. Null inputs, invalid casts, `as` failures,
  dereference exceptions, reference identity, and matching checksums were checked
  separately before timing. Every player passed those checks.
- Inline cases put the conversion directly inside the loop. Getter cases called
  a `NoInlining` method returning the converted reference, then read its value.
  The sums were consumed after each loop.

The numbers below are **nanoseconds per complete loop iteration**, including the
array lookup, conversion, value read, accumulation, and loop overhead. They are
throughput measurements, not isolated cast latency. Each number is the median
of the three process medians. Parentheses give the range of process medians.

| Backend | Read | Explicit cast | `as` plus `!` |
|---|---|---:|---:|
| Mono | Inline string `.Length` | 0.543 (0.541–0.544) | 0.541 (0.539–0.543) |
| Mono | Inline inherited-class field | 0.607 (0.582–0.713) | 0.558 (0.558–0.582) |
| IL2CPP | Inline string `.Length` | 0.371 (0.371–0.371) | 0.372 (0.371–0.372) |
| IL2CPP | Inline inherited-class field | 0.464 (0.464–0.465) | 0.503 (0.503–0.504) |
| Mono | String through non-inlined getter | 0.896 (0.732–0.897) | 0.830 (0.791–0.832) |
| Mono | Class through non-inlined getter | 0.863 (0.856–0.897) | 0.914 (0.906–1.073) |
| IL2CPP | String through non-inlined getter | 0.722 (0.721–0.723) | 0.717 (0.717–0.717) |
| IL2CPP | Class through non-inlined getter | 0.849 (0.849–0.849) | 1.073 (0.987–1.076) |

Mono showed appreciable process-to-process variation in some cases. The stable
IL2CPP inherited-class inline result made `as` about 8% slower in this workload.
These results do not establish a universal timing or cover ARM, other CPUs,
other inheritance shapes, interfaces, arrays, cold metadata initialization,
or Editor Mono.

## Generated code

Mono native instructions were copied from each warmed method's
`MethodHandle.GetFunctionPointer()` and disassembled. Each raw dump is a 256-byte
window and can include adjacent methods; only the named method's instructions
were used for comparison. Both string getters test null and compare runtime type
metadata. The explicit cast branches to a cold exception helper on mismatch.
The `as` getter instead produces zero on mismatch and preserves an additional
register in the inspected method. Removing the throwing path does not remove
the type comparison or necessarily simplify the successful path.

IL2CPP's generated C++ uses `CastclassSealed` versus `IsInstSealed` for strings,
and `CastclassClass` versus `IsInstClass` for the nonsealed base class. The final
native string getter is 35 bytes for the cast and 32 bytes for `as`. The latter
uses a conditional move to select the original reference or null. For the class
getter, sizes are 92 versus 89 bytes, but the `as` version's successful path
includes a conditional move after the inheritance check.

When `.Length` follows the conversion in the same method, IL2CPP emits null
handling in both forms. The successful paths check the input, compare the type,
and load the length. The cast form has separate failure destinations for null
and incompatible type; the `as` form routes both to `NullReferenceException`.
The final methods are 40 and 35 bytes respectively. On Mono, the length load
itself supplies the null-reference fault in these inspected methods.

No successful conversion path in the inspected native helpers allocates. The
allocation counters in the JSON reports are **not measurement evidence**:
`GC.GetAllocatedBytesForCurrentThread()` also reported zero around an explicit
1,024-byte allocation on both backends, so its sanity check failed.

## Artifacts and reproduction

All probe files and raw results are in the ignored
[`tmp/unity-cast-comparison-20261020`](../../tmp/unity-cast-comparison-20261020)
directory. No Spire storage implementation was changed.

The [typed-storage follow-up](2026-10-20-unity-typed-reference-storage.md) adds
matching controls without casts and reports their cost relative to object slots.

- [Probe source](../../tmp/unity-cast-comparison-20261020/project/Assets/CastComparison.cs)
- [Release-player build entrypoint](../../tmp/unity-cast-comparison-20261020/project/Assets/Editor/CastComparisonBuild.cs)
- [Raw timings and process medians](../../tmp/unity-cast-comparison-20261020/results/summary.json)
- [Extracted IL2CPP C++](../../tmp/unity-cast-comparison-20261020/results/il2cpp-probe.cpp)
- [Mono explicit string cast](../../tmp/unity-cast-comparison-20261020/results/mono-1-CastString.asm)
- [Mono string `as`](../../tmp/unity-cast-comparison-20261020/results/mono-1-AsString.asm)
- [IL2CPP explicit string cast](../../tmp/unity-cast-comparison-20261020/results/il2cpp-CastProbe_CastString.asm)
- [IL2CPP string `as`](../../tmp/unity-cast-comparison-20261020/results/il2cpp-CastProbe_AsString.asm)

Build with the installed Editor's `-batchmode -nographics -buildTarget Linux64
-executeMethod CastComparisonBuild.Run` entrypoint and the probe's `project`
directory as `-projectPath`. The build used the existing isolated libraries in
`tmp/unity-il2cpp-native-libs` through `LD_LIBRARY_PATH` to satisfy the native
linker's libxml2 dependency. Run players only after the build has completed:

```sh
python3 tmp/unity-cast-comparison-20261020/run_players.py
python3 tmp/unity-cast-comparison-20261020/extract_il2cpp.py
```
