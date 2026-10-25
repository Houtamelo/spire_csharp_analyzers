# Unknown-size unmanaged fields can share offset-zero helpers

The proposed storage shape works in Unity 6000.5.6f1 release Linux x64 Mono and
IL2CPP players. Nongeneric explicit-layout helpers place different concrete
unmanaged types at offset zero without declaring a `Size`. Each runtime supplies
the helper's size and padding and places the helpers inside an ordinary outer
struct. Neither the storage fields nor their getters require unsafe accesses.
The probe uses unsafe code only to measure managed sizes and field addresses.

The test models `A(Foo foo1, Foo foo2, float duration)`, `B(Baz baz, Bar bar)`,
and `C(int count)`. `Foo`, `Baz`, and `Bar` are multi-field unmanaged structs,
outside the proposed size-discovery catalog. The first helper overlays `Foo`
and `Baz`; the second overlays `Foo` and `Bar`; a sized helper overlays float
and int. All helper fields start at zero.

| Measurement | Mono | IL2CPP |
|---|---:|---:|
| Foo: three floats | 12 | 12 |
| Baz: two longs | 16 | 16 |
| Bar: three doubles | 24 | 24 |
| First unsized helper | 16 | 16 |
| Second unsized helper | 24 | 24 |
| Float/int helper | 4 | 4 |
| Outer command, including tag and padding | 56 | 56 |
| Managed command array stride | 56 | 56 |

Both runtimes placed the first, second, and sized helpers at outer offsets 8,
24, and 48. A separate helper overlaying the 12-byte Foo and an 8-byte long
occupied 16 bytes, demonstrating that automatic size includes required tail
padding rather than always equaling the largest field's size.

Each player passed 821 checks. Coverage includes field offsets, alignment,
nonoverlap between simultaneously used slots, all three variant factories and
property patterns, positive/zero/negative payloads, default and invalid tags,
array storage, and value copies. It also verifies:

- Concrete nongeneric helpers embedded directly in `GenericCarrier<string>`.
- A nongeneric explicit helper containing the closed generic unmanaged type
  `Pair<int>` alongside Foo.

The generic-carrier result validates a previously untested IL2CPP arrangement.
It does not permit explicit-layout helper types that themselves capture generic
parameters. No open generic explicit helper was introduced in this probe.
Types containing managed references are outside this optimization.

The maximum number of fields in a variant is a lower bound on the necessary
slot count when each property must retain one location. For example,
`A(Foo x, Foo y)`, `B(Foo y, Foo z)`, `C(Foo z, Foo x)` needs three stable slots:
every property conflicts with both others. The proposed A/B/C example above
needs only two. Unknown field sizes also prevent choosing the byte-smallest
slot grouping using size comparisons; the runtime determines the resulting
footprint after slot assignment.

This is a representation-validation probe, not a benchmark or an implemented
Spire generator change. It does not establish support on other architectures
or Unity versions.

- [Probe source and checks](../../tmp/unity-unsized-overlap-20261020/UnsizedProbe.cs)
- [Mono results](../../tmp/unity-unsized-overlap-20261020/results/mono.json)
- [IL2CPP results](../../tmp/unity-unsized-overlap-20261020/results/il2cpp.json)

```sh
python3 tmp/unity-unsized-overlap-20261020/prepare.py
dotnet format whitespace tmp/unity-unsized-overlap-20261020/project/Assets --folder --verbosity quiet
python3 tmp/unity-unsized-overlap-20261020/build.py
python3 tmp/unity-unsized-overlap-20261020/run_players.py
```

The isolated project uses nondevelopment players, IL2CPP Release/OptimizeSpeed,
and Low managed stripping, with the installed Editor and the existing isolated
IL2CPP linker dependencies. The preparation script reuses the earlier probe's
project settings and build runner.
