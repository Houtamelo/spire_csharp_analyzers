# Repeated discriminator checks in property patterns

Unity 6000.5.6f1 IL2CPP eliminated the repeated getter discriminator checks in
the tested switch. Unity Mono retained them, even with `AggressiveInlining`.
CoreCLR .NET 10.0.12 eliminated them for a by-value argument, but retained them
for the tested `in` argument.

These are generated-code observations on Linux x64, not timing measurements or
a guarantee for other targets, larger unions, or different getter bodies.

The [larger IL2CPP follow-up](2026-10-20-unity-kind-dispatch-complexity.md) found
that ordinary larger getters retain calls, while aggressive inlining restores
elimination for kind-first patterns. Field-first patterns can retain checks even
after inlining.

## Probe

This is hand-written code modeling a proposed storage design, not current Spire
generator output. A readonly struct contains a byte discriminator and two float
slots. Its properties select slots using the discriminator:

| Variant | Slot 0 | Slot 1 |
|---|---|---|
| A | x | y |
| B | y | z |
| C | z | x |

The primary getters use switch expressions with a zero fallback when the active
variant does not contain that property. For example:

```cs
public float x
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    get => tag switch
    {
        CommandKind.A => slot0,
        CommandKind.C => slot1,
        _ => 0,
    };
}
```

The caller follows the discussed property-pattern shape, with observable case
bodies to retain the work:

```cs
switch (command)
{
    case { kind: CommandKind.A, x: float x, y: float y }: return x + y;
    case { kind: CommandKind.B, y: float by, z: float z }: return by - z;
    case { kind: CommandKind.C, z: float cz, x: float cx }: return cz * cx;
    default: return -1;
}
```

Controls cover ordinary getter inlining, simpler conditional getters that leave
inactive properties unspecified, switching on `kind` directly, direct slot
access, three fixed property slots, an `in` parameter, and empty case bodies.
Only the enclosing probe methods are marked `NoInlining`; their getters remain
available for optimization into those callers.

Both Unity players are nondevelopment Linux64 builds from the installed
6000.5.6f1 Editor. IL2CPP uses Release, OptimizeSpeed, and Low managed stripping.
The Unity player compiler response file contains `/optimize+`. CoreCLR uses a
Release net10.0 build of the same probe source, with tiered compilation disabled
to capture fully optimized code without warmup/tier ambiguity. CoreCLR uses the
installed SDK compiler; the Unity IL was captured separately from its player.

Each backend passed 1,435 result checks, covering all three variants, two unknown
discriminator values, and varied negative, zero, and positive payload values.
The payload design uses two floats rather than three: the complete tested
structs occupy 12 and 16 bytes respectively, including discriminator/padding.
The Unity size observations here use `Marshal.SizeOf` on these simple types.

## What survives compilation

The Unity-compiled IL reads `kind` once and dispatches to the matching case.
Each case then calls its two property getters. The compiler has not replaced
those calls with variant-specific slot reads; that optimization is left to the
runtime/native compiler.

| Backend | By-value pattern | `in` pattern |
|---|---|---|
| Unity Mono | Getter checks remain after aggressive inlining | Getter checks remain |
| Unity IL2CPP | Getter checks eliminated | Getter checks eliminated |
| CoreCLR 10.0.12, full optimization | Getter checks eliminated | Getter checks remain in this probe |

IL2CPP's by-value pattern, ordinary-inlining pattern, conditional-getter pattern,
explicit kind switch, and direct slot control all have the same instruction
sequence after normalizing addresses. Each is 57 bytes. The remaining tag
comparisons implement only the outer dispatch; there are no checks inside a
selected case. Its arithmetic paths reduce to:

```asm
; xmm0 and xmm1 already contain the two slots.
case_A:
    addss xmm0, xmm1
    ret
case_B:
    subss xmm0, xmm1
    ret
case_C:
    mulss xmm0, xmm1
    ret
```

Mono retains both inlined getter decision trees inside each selected case.
Removing the inlining hints leaves calls to the getter bodies instead. Simpler
ternary getters reduce the amount of emitted code but still retain their tag
checks. Switching on `command.kind` directly also retains the getter checks.

For reference, the captured sizes are:

| Probe method | Mono | IL2CPP | CoreCLR |
|---|---:|---:|---:|
| Pattern, aggressive getters | 1,188 | 57 | 101 |
| Pattern, ordinary getters | 404 | 57 | 101 |
| Pattern, conditional getters | 756 | 57 | 101 |
| Pattern, `in` argument | 1,236 | 51 | 224 |
| Kind switch, aggressive getters | 1,060 | 57 | 101 |
| Direct slot control | 196 | 57 | 101 |
| Pattern, three fixed property slots | 500 | 71 | 106 |
| Empty pattern cases | 1,045 | 1 | 1 |

Sizes are not latency estimates. Mono's values are the exact JIT-reported method
extents, which can include inline constants/padding and exclude separately
allocated jump tables. Its ordinary-getter size excludes the separate getter
bodies. IL2CPP values come from native symbol sizes; CoreCLR values come from
its JIT disassembly report. Compare behavior rather than treating these figures
as directly comparable code-size accounting across runtimes.

The exact empty-body shape disappears completely on IL2CPP and CoreCLR; Mono
still retains getter work. The primary results therefore use the arithmetic
case bodies above.

## Design implication

Repeated checks can disappear, and they do on the tested IL2CPP build. A compact
layout that requires property getters to dispatch on `kind` still has a real
generated-code tradeoff on the tested Mono backend. Inlining hints alone do not
remove it. The redesign should not assume discriminator-based getters have
zero cost on all supported Unity backends. No storage policy is selected by
this investigation, and no Spire implementation was changed.

## Reproduction and artifacts

- [Probe types and switches](../../tmp/unity-kind-branching-20261020/project/Assets/BranchProbe.cs)
- [Unity build entrypoint](../../tmp/unity-kind-branching-20261020/project/Assets/Editor/BranchBuild.cs)
- [Unity-compiled IL](../../tmp/unity-kind-branching-20261020/results/unity-branchprobe.il)
- [Mono pattern disassembly](../../tmp/unity-kind-branching-20261020/results/mono-Pattern.asm)
- [Mono conditional-getter disassembly](../../tmp/unity-kind-branching-20261020/results/mono-PatternConditional.asm)
- [IL2CPP pattern disassembly](../../tmp/unity-kind-branching-20261020/results/il2cpp-BranchProbe_Pattern.asm)
- [IL2CPP direct-slot disassembly](../../tmp/unity-kind-branching-20261020/results/il2cpp-BranchProbe_Direct.asm)
- [IL2CPP normalized equivalence check](../../tmp/unity-kind-branching-20261020/results/il2cpp-equivalence.txt)
- [IL2CPP generated C++](../../tmp/unity-kind-branching-20261020/results/il2cpp-probe.cpp)
- [CoreCLR by-value disassembly](../../tmp/unity-kind-branching-20261020/results/coreclr-Pattern.asm)
- [CoreCLR `in` disassembly](../../tmp/unity-kind-branching-20261020/results/coreclr-PatternIn.asm)
- [Captured sizes](../../tmp/unity-kind-branching-20261020/results/method-sizes.json)

The isolated build script starts a virtual display and supplies the existing
`tmp/unity-il2cpp-native-libs` directory through its local `LD_LIBRARY_PATH` to
satisfy the installed linker's libxml2 dependency. Unity builds and players need
their normal access to user caches/preferences.

```sh
python3 tmp/unity-kind-branching-20261020/build.py
python3 tmp/unity-kind-branching-20261020/run_players.py
python3 tmp/unity-kind-branching-20261020/extract_il2cpp.py
```

Mono method boundaries are read from its exported JIT-info API before copying
native bytes. IL2CPP boundaries are read from the generated debug symbol file,
with instructions disassembled from the shipped `GameAssembly.so`.
