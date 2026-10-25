# Cleaner variant matching with nullable accessors

Deferred API improvement: revisit nullable variant accessors when Unity provides
a CoreCLR backend. The main benefit is cleaner switch statements: matching the
payload also identifies the variant, removing the repeated explicit `kind`
pattern. The CoreCLR benchmarks make this worth revisiting, but adoption still
requires validation in Unity's actual backend. Production generation is unchanged.

## Proposed API

Return `VariantPayload?` from each variant accessor: the active variant returns
its payload, and inactive variants return null. Put an AggressiveInlining hint on
the getter, as in the tested prototype.

Illustrative matching with the proposed API:

```cs
using static Houtamelo.Spire.Showcase.GameplayCommandTypes.Variants;

switch (command) {
    case { idle: Idle idle }:
        break;
    case { teleport: Teleport teleport }:
        MoveTo(teleport.target, teleport.duration);
        break;
    // Remaining variants follow the same pattern.
}
```

This replaces the current shape:

```cs
case { kind: GameplayCommand.Kind.Teleport, teleport: var teleport }:
```

The returned value remains a struct payload. Nullable matching does not itself
box that payload. Existing storage policy, including boxing of managed-containing
structs in shared storage, is a separate concern.

## Why defer until Unity CoreCLR

Unity 6000.5.6f1 Linux x64 Mono and IL2CPP release players showed meaningful
regressions, especially for larger switches. Standalone CoreCLR handles the
nullable machinery much better, already on .NET 8. With the same Unity-compiled
matcher DLL on .NET 10, shuffled six-, twelve-, and thirty-two-variant matches
improved from 9.96 → 3.18, 8.88 → 3.03, and 8.03 → 4.56 ns respectively.
Small switches incurred a minor penalty.

This is not universally faster. Always selecting the last of 32 variants on
.NET 10 took 1.31 ns with the current pattern and 6.92 ns nullable. Native code
shows a discriminator-indexed jump table for the current pattern and sequential
checks for nullable matching. Branch prediction can make repeated destinations
cheap; it does not remove the nullable chain's executed checks. The shuffled
reversal is consistent with different prediction behavior, but hardware branch
misses were not measured.

Newer C# syntax or compiler versions are not prerequisites: Unity's C# 9 compiler
and newer Roslyn under C# 9 / C# 14 emitted identical normalized IL in all 179
checked methods. The important boundary is the runtime/JIT and its deployment
configuration. A Unity CoreCLR introduction alone does not establish that its
Editor and all player backends share these results.

## Revisit conditions

- Run the retained workloads in Unity's actual CoreCLR Editor and supported
  players, with their shipping JIT/tiering/PGO settings. Keep separate results
  for any remaining Mono or IL2CPP targets.
- Compare both predictable and shuffled inputs, small and larger switches, and
  single-variant tests. Include the hinted nonnullable control so inlining alone
  is not mistaken for a nullable-pattern benefit.
- Update exhaustiveness, field-access analysis and missing-arm code fixes to
  understand that a non-null variant accessor identifies the corresponding kind.
  Preserve invalid-kind handling and nullable payload-field semantics.
- Decide whether to replace the current accessors or offer an additional API,
  and how to select behavior across backends. Those choices remain open; the
  readability benefit is a reason to evaluate a measured cost, not a promise of
  performance parity.

Evidence and reproducible workloads:

- [Unity Mono / IL2CPP results](../reviews/2026-10-25-unity-nullable-variant-patterns.md)
- [CoreCLR results, compiler comparison and native JIT evidence](../reviews/2026-10-25-coreclr-nullable-variant-patterns.md)
- [Original experiment and request record](2026-10-25-nullable-variant-pattern-checks.md)
