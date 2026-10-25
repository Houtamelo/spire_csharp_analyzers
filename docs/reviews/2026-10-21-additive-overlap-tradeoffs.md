# Type-agnostic unknown-size assignment misses same-type reuse

The original type-agnostic unit-slot proposal omitted same-type reuse. The
offset representation can apply that optimization too. The comparison below
records the omitted optimization, not an intrinsic advantage of Additive.
The design correction dated 2026-10-22 is recorded below.

Consider these variant fields, in declaration order:

```cs
A(Big big1, Small small1);
B(Small small2, Big big2);
```

Both structs are unmanaged, multi-field user types whose sizes remain unknown
under the generator's approved discovery policy. Each property has one variant
membership, so unit-slot ordering falls through to stable group indices. With
indices assigned in the order shown, placement produces:

| Helper | Overlaid fields |
|---|---|
| 0 | `A.big1` and `B.small2` |
| 1 | `A.small1` and `B.big2` |

Each helper must accommodate Big. Additive can instead reuse one Big slot and
one Small slot across the variants. An offset assignment pairing Big with Big
and Small with Small can match that representation; this is a heuristic quality
issue, not an inherent restriction of explicit layout.

## Layout probe

Measured on 2026-10-21 using Unity 6000.5.6f1's bundled Mono CLI on Linux x64,
Mono 6.13.0 (`explicit/6b095c0d`). Big contains sixteen longs; Small contains two
ints. The handwritten probe measures managed `sizeof`, with unsafe code used
only for measurement. It does not run the planned generator or establish Editor,
Player, IL2CPP, or throughput results.

| Storage | Bytes |
|---|---:|
| Big | 128 |
| Small | 8 |
| Each mixed offset-zero helper | 128 |
| Additive payload | 136 |
| Two-helper payload | 256 |
| Additive union, including byte discriminator and padding | 144 |
| Two-helper union, including byte discriminator and padding | 264 |

- [Probe source](../../tmp/unity-additive-overlap-20261021/Probe.cs)
- [Measurements, compiler arguments, runtime version and unit-slot assignments](../../tmp/unity-additive-overlap-20261021/results.json)

The compiler arguments in the JSON reproduce the executable; run it with the
same bundled Mono executable to reproduce the measurements.

## Design implications

Additive needs no layout catalog or explicit-layout helpers. No speed advantage
over optimized offset access has been measured. The size comparison above does
not justify retaining a public Additive mode: an offset assignment can reuse
the same types in exactly this example.

## Design correction, 2026-10-22

Unknown-size helper assignment now prefers an existing compatible helper that
already contains the same concrete type before cross-type best-fit placement.
Compatibility checks all groups assigned to that helper, preserving both
coexistence constraints and fixed property locations. Multiple compatible
same-type helpers are resolved by lowest stable helper index.

For this example, Small reuses the Small slot and Big reuses the Big slot, so
the oversized mixed helpers are avoided. This changes the planned heuristic;
the generator has not been implemented, and no revised Player or performance
measurement is claimed. Unknown sizes still prevent reliably comparing the
byte footprints of arbitrary assignments. Public Additive removal remains
undecided.
