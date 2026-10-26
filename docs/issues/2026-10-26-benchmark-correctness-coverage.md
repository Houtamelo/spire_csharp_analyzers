# Benchmark distribution correctness and validation plan

This is the read-only research note for batch 4 of
`docs/issues/2026-10-26-correctness-fixes.md`. The approved change is to make
`Distribution.Skewed80` select variant zero 80% of the time and distribute the
remaining 20% uniformly among variants one through `count - 1`, while preserving
the current deterministic cycling behavior of `Distribution.Uniform`.

## Confirmed defect

`benchmarks/Houtamelo.Spire.Benchmarks/Helpers/ArrayFiller.cs:213-216`
currently does this for `Skewed80`:

```csharp
random.NextDouble() >= 0.8 ? random.Next(1, count) : index % count
```

The `index % count` branch is taken 80% of the time, so variant zero appears
only once per cycle inside that branch. With eight variants and an array length
that is a multiple of eight, the resulting asymptotic frequencies are:

| Variant | Current frequency |
|---|---:|
| 0 | 10% |
| 1–7 | 12.857% each |

The intended frequencies are:

| Variant | Correct frequency |
|---|---:|
| 0 | 80% |
| 1–7 | 2.857% each |

The current implementation also calls `Random.Next(1, count)` when `count == 1`
and the random branch is selected, returning index one outside the only valid
index zero. The chooser should return zero for `count == 1`
and reject non-positive counts with an explicit argument error.

The corrected chooser should be the single implementation used by every real
`ArrayFiller.Fill` path:

```text
if count <= 0: throw ArgumentOutOfRangeException
if count == 1: return 0
if distribution == Uniform: return index % count
return random.NextDouble() < 0.8 ? 0 : random.Next(1, count)
```

The exact `Random` sequence changes for the corrected distribution by design.
Uniform must remain byte-for-byte deterministic for a fixed array length and
seed, with kinds cycling `0, 1, ..., count - 1`.

## Actual benchmark impact

All 17 public `ArrayFiller.Fill` overloads accept `Distribution`:

- Event: `EventBench`, `EventAuto`, `EventTyped`, `EventShared`,
  `EventJsonBench`, `EventJsonAuto`, `EventJsonTyped`, `EventJsonShared`,
  `EventJsonRecord`, `EventRecord`, and `EventNative`.
- Physics: `PhysicsBench`, `PhysicsAuto`, `PhysicsTyped`, `PhysicsShared`,
  `PhysicsRecord`, and `PhysicsNative`.

They use either `FillEvents`, `FillPhysics`, or their equivalent concrete
loops, so the shared chooser affects all of them. The current benchmark suite
uses `Skewed80` only through `MatchBenchmarks.Dist`:

```text
MatchBenchmarks.Setup -> ArrayFiller.Fill(Physics*, new Random(42), Dist)
```

`ConstructBenchmarks`, `UpdateLoopBenchmarks`, `JsonNsjBenchmarks`, and
`JsonStjBenchmarks` pass `Distribution.Uniform`; their benchmark inputs are
unchanged by this correction. `MicroMatchBenchmarks` constructs a fixed
single value and is also unaffected.

The affected `MatchBenchmarks` methods are `Auto`, `ExplicitAuto`, `Typed`,
`Shared`, `Record`, `Native`, and `PayloadDeconstruct`, for both `Uniform` and
`Skewed80`. Existing `docs/benchmark-results/RESULTS_default.md` and
`RESULTS_dry.md` are historical artifacts and must not be overwritten while
rerunning the corrected benchmark.

## Regression coverage

The benchmark project currently has no test project and `ArrayFiller` is
internal. Add a dedicated `tests/Houtamelo.Spire.Benchmarks.Tests` net10.0
xUnit project to the solution and grant it internal access through the
benchmark assembly. The tests must call the real `ArrayFiller.Fill` overloads;
they must not reimplement the selection formula.

### Chooser edge cases

Test the internal chooser used by the real fill paths:

| Case | Contract |
|---|---|
| `Uniform_CountEight` | Kinds cycle exactly `0..7`, independent of random values. |
| `Uniform_CountOne` | Every result is zero and the random object is not required to choose a range. |
| `Skewed80_CountOne` | Every result is zero; no out-of-range index one. |
| `InvalidCountZero` | Throws `ArgumentOutOfRangeException` with a clear parameter name. |
| `InvalidCountNegative` | Throws the same boundary error. |
| `Skewed80_FixedSeedIsRepeatable` | Two chooser runs with identical seed/input produce identical sequences. |
| `Uniform_FixedSeedIsRepeatable` | Existing deterministic cycle remains identical for two seeds/runs. |

### Real fill overloads

Use fixed seeds and classify the generated union kind through each type's real
`kind` member. Cover the six physics representations used by
`MatchBenchmarks` (`PhysicsBench`, `PhysicsAuto`, `PhysicsTyped`,
`PhysicsShared`, `PhysicsRecord`, `PhysicsNative`) and representative event
representations from the event/JSON overload groups. Every tested array must
prove:

- all kinds are within `[0, count)`;
- the same seed yields the same kind sequence for every representation;
- `Uniform` remains an exact cycle;
- `Skewed80` has variant zero in a statistically expected range and every
  nonzero kind appears.

For a 100,000-element `Skewed80` sample with eight variants, use a deterministic
seed and a binomial tolerance rather than asserting one exact random histogram.
A practical acceptance band is variant zero in `[79%, 81%]` and each nonzero
variant in `[2%, 4%]`; additionally assert that the sum is exactly the sample
length. For a small 1,000-element benchmark-shaped sample, use wider bounds
because the expected binomial standard deviation is about 12.6 elements for
variant zero.

The tests should compare the actual generated kind sequence across all six
physics representations, which catches an overload accidentally bypassing the
shared chooser. Payload values need not be compared because this batch changes
only variant selection; existing constructor and behavioral tests cover payload
construction.

## Benchmark rerun without destroying history

Do not invoke the benchmark executable's top-level `Program` for the corrected
measurement: it deletes `BenchmarkDotNet.Artifacts/results` and rewrites
`docs/benchmark-results/RESULTS_<job>.md` before running. Instead, create an
ignored temporary net10.0 runner under `tmp/benchmark-skewed80-fix/` that
references the benchmark project and calls:

```csharp
BenchmarkSwitcher
    .FromTypes(typeof(MatchBenchmarks))
    .Run(args);
```

Run it with a private artifact directory, for example:

```text
dotnet run -c Release --project tmp/benchmark-skewed80-fix/ -- \
  --filter '*MatchBenchmarks*' --job Short \
  --artifacts tmp/benchmark-skewed80-fix/artifacts
```

This runs the actual public benchmark class and its setup/benchmark methods,
but does not execute the repository `Program.Main` cleanup or report writer.
Copy the resulting `MatchBenchmarks` markdown and summary into
`tmp/benchmark-skewed80-fix/results/`, recording commit, SDK/runtime, OS,
BenchmarkDotNet version, seed, N, job, and the corrected histogram. Preserve
the old documents unchanged. The affected results should contain both
`Uniform` and `Skewed80` rows for Property and Payload Deconstruct; no other
benchmark workload needs a new distribution comparison.

The final batch gate should include the dedicated benchmark tests, the full SDK
solution, and the focused `MatchBenchmarks` Short run. No benchmark result
should be described as comparable to historical `Skewed80` numbers without
calling out that the historical input generator was incorrect.
