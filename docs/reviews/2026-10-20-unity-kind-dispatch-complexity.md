# IL2CPP discriminator-check elimination in larger examples

IL2CPP's successful optimization in the [initial three-variant probe](2026-10-20-unity-kind-dispatch.md)
extends to larger examples, but it depends on getter inlining and pattern shape.
Larger getters without hints retained calls. `AggressiveInlining` removed those
calls and their checks when `kind` came first. Reading all fields before `kind`
retained six getter selection dispatches even after aggressive inlining.

This follow-up used Unity 6000.5.6f1, nondevelopment Linux x64, IL2CPP Release,
OptimizeSpeed, and Low managed stripping. Results describe native code in the
shipped `GameAssembly.so`; they are not timing measurements. The probe code is
a hand-written model of the proposed design, not current Spire generator output.

## Examples and results

The dense cases give every variant a distinct permutation of the physical float
slots. Weighted arithmetic consumes every property and distinguishes the
permutations. Each getter has a discriminator switch and a zero fallback.
Every pattern has a direct-slot control using the same layout and arithmetic.

| Example | Getter configuration | Native result |
|---|---|---|
| 12 variants, 6 properties per variant | Ordinary | 6 getter call sites remain |
| Same 12-variant example | AggressiveInlining | Same instruction shape as direct slots; no getter calls/checks |
| 32 variants, 8 properties per variant | Ordinary | 8 getter call sites remain |
| Same 32-variant example | AggressiveInlining | Same instruction shape as direct slots; no getter calls/checks |
| 12 variants, fields before `kind` | AggressiveInlining | Calls disappear, but 6 getter dispatches remain |
| 12 variants, guarded cases and same-kind fallthrough | AggressiveInlining | Getter checks disappear; explicit guard tests/calls remain |
| 12 variants grouped into 3 `or` cases | AggressiveInlining | Selection combines into one tag dispatch |
| 5 variants with Vector3, Quaternion, double, int, float | Ordinary | Getter calls remain, both by value and through `in` |
| Same mixed-payload example | AggressiveInlining | Getter calls/checks disappear, both by value and through `in` |
| 12-variant negative control | NoInlining | Getter calls remain as expected |

Call-site counts are static counts in the whole method. The mixed-payload
method contains 18 getter call sites across five cases; a selected case executes
three to five of them. They are not 18 calls on every invocation.

All 24 methods, including controls, passed 14,760 result checks in a real IL2CPP
player. Inputs cover each variant, two invalid discriminator values per union,
and varied negative, zero, and positive payloads. The guarded example exercises
both guard outcomes. Static validation state is kept in a separate class so it
does not introduce unrelated class-initialization checks into the probes.

## Inlining is necessary but does not settle branch elimination

In the ordinary dense cases, the compiler combines the common arithmetic case
bodies, but retains calls to the discriminator-based getters. For example, the
12-variant method still calls `Dense12_get_p0` through `Dense12_get_p5`. Each
getter retains its own tag dispatch.

With `AggressiveInlining`, IL2CPP emits `IL2CPP_MANAGED_FORCE_INLINE` getter
declarations in the generated C++. The kind-first dense pattern methods then
have the same normalized instruction sequences as their direct-slot controls:
676 bytes for the 12-variant case and 2,378 bytes for the 32-variant case.
Each has one bounds check and one indirect tag dispatch, followed by the
variant-specific loads and arithmetic. No getter calls or subsequent tag checks
remain.

The field-first example demonstrates a separate limit:

```cs
case { p0: var a, p1: var b, p2: var c,
       p3: var d, p4: var e, p5: var f, kind: 0 }:
    return a + b * 2 + c * 3 + d * 4 + e * 5 + f * 6;
```

The other eleven cases have the same property-first shape. The captured player
IL evaluates the properties before dispatching on `kind`. Its native method
has seven bounds-check/indirect-dispatch pairs: one for each of six inlined
getters and one for the outer switch. The matching kind-first method has one
pair. There are no getter calls in either aggressively inlined method, so this
is a failure to eliminate the selection logic after inlining.

## Guards, grouped variants, and mixed payloads

The guarded example has two cases for each kind. The first captures six fields
and calls a non-inlined scalar predicate in a `when` clause. If it fails, a
second case with the same kind uses two fields. Native code retains one tag
dispatch and the explicit guard calls/tests, with no getter dispatches. Its
spills and arithmetic layout differ from the direct control; elimination of
tag checks does not imply identical instructions or identical performance.

The grouped example uses `kind: 0 or 1 or 2 or 3`, and two other four-tag groups.
Although no source case identifies one variant uniquely, the native code
combines the selection into a single twelve-way tag dispatch. Each destination
selects the six field addresses and then enters shared arithmetic. It does not
retain a separate tag dispatch per getter.

The mixed-payload example embeds a separate nongeneric, explicit-layout helper
with a 40-byte overlapping region. Five variants use different combinations and
offsets for Unity `Vector3` and `Quaternion`, a double, an int, and floats. These
include position at offsets 0, 12, 16, or 24 and weight at offsets 24, 28, or 36.

Without hints, its struct-returning and scalar getters retain calls. With
aggressive inlining, both by-value and `in` pattern methods contain only one tag
bounds check and one tag dispatch. Struct extraction, arithmetic, and—in the
`in` case—some copies/spills remain. Their instruction sequences are not
identical to the controls, despite the removal of getter checks. This validates
this nongeneric helper arrangement in IL2CPP; it does not validate embedding it
in a generic union.

## Implication for the redesign

The optimization is not restricted to tiny unions: it works here with 32
distinct layouts, eight properties per case, guarded fallthrough, and composite
Unity payloads. It is also not automatic. The proposed emitter should consider
aggressive inlining for discriminator-based getters, and generated property
patterns should keep `kind` first. These are recommendations from this probe,
not newly selected storage policies. The earlier Mono result still applies.

No timings were collected. Smaller native bodies with out-of-line getters
should not be interpreted as faster, nor should larger inlined methods be
interpreted as a proportional slowdown. Different platforms, Unity versions,
compiler settings, aliasing, and getter bodies still need their own evidence.

## Source and evidence

- [Dense probe generator and runtime validation](../../tmp/unity-kind-complexity-20261020/make_probe.py)
- [Generated dense cases](../../tmp/unity-kind-complexity-20261020/project/Assets/DenseProbe.cs)
- [Mixed payload and cases](../../tmp/unity-kind-complexity-20261020/project/Assets/NestedProbe.cs)
- [Mixed payload with inlining hints](../../tmp/unity-kind-complexity-20261020/project/Assets/NestedInlineProbe.cs)
- [Player results](../../tmp/unity-kind-complexity-20261020/results/il2cpp.json)
- [Native comparison](../../tmp/unity-kind-complexity-20261020/results/comparison.json)
- [Ordinary dense caller](../../tmp/unity-kind-complexity-20261020/results/ComplexProbe_Dense12Pattern.annotated.asm)
- [Ordinary dense getter](../../tmp/unity-kind-complexity-20261020/results/Dense12_get_p0.annotated.asm)
- [32-variant inlined caller](../../tmp/unity-kind-complexity-20261020/results/ComplexProbe_Dense32InlinePattern.annotated.asm)
- [Field-first counterexample](../../tmp/unity-kind-complexity-20261020/results/ComplexProbe_FieldsFirst.annotated.asm)
- [Guarded cases](../../tmp/unity-kind-complexity-20261020/results/ComplexProbe_Guarded.annotated.asm)
- [Grouped variants](../../tmp/unity-kind-complexity-20261020/results/ComplexProbe_Grouped.annotated.asm)
- [Inlined mixed payload](../../tmp/unity-kind-complexity-20261020/results/ComplexProbe_MixedInlinePattern.annotated.asm)
- [Inlined mixed payload through `in`](../../tmp/unity-kind-complexity-20261020/results/ComplexProbe_MixedInlinePatternIn.annotated.asm)
- [Captured player IL](../../tmp/unity-kind-complexity-20261020/results/player.il)
- [Generated C++ callers](../../tmp/unity-kind-complexity-20261020/results/probes.cpp)

Annotated disassembly names direct call targets using the native debug symbols.
Instruction comparisons normalize code addresses and RIP-relative data locations;
they do not compare relocation bytes or jump-table data. Native control flow was
also inspected, rather than inferring check elimination solely from matching
method sizes or the absence of getter calls.

Recreate the source, build the release player, validate, and extract native code:

```sh
python3 tmp/unity-kind-complexity-20261020/make_probe.py
dotnet format whitespace tmp/unity-kind-complexity-20261020/project/Assets --folder --verbosity quiet
python3 tmp/unity-kind-complexity-20261020/build.py
python3 tmp/unity-kind-complexity-20261020/run_player.py
python3 tmp/unity-kind-complexity-20261020/extract.py
```

The build uses the same installed Editor and isolated linker dependencies as the
initial probe. No Spire implementation or package was changed.
