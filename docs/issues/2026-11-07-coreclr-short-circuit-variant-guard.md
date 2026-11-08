# SPIRE014 short-circuit variant guards

Resolved on 2026-11-08.

Observed while restoring the CoreCLR/NuGet suite on the .NET 10.0.401 SDK.
The packed generator/analyzer reported SPIRE014 for this consumer:

```csharp
[Serializable, DiscriminatedUnion]
public readonly partial struct ReadOnlyCommand
{
    [Variant] public static partial ReadOnlyCommand Hold(int duration);
}

var readOnly = ReadOnlyCommand.Hold(11);
if (readOnly.kind != ReadOnlyCommand.Kind.Hold || readOnly.hold.duration != 11)
    return 11;
```

C# evaluates the payload access only after the left operand is false, which
establishes `kind == Hold` for this unchanged local. The observed diagnostic
instead says the kind is unknown. Evidence: `/tmp/spire-coreclr-isolation.log`.

This limitation was separate from Unity environment detection. The packed NuGet
consumer now uses the original expression above without a workaround or
diagnostic suppression.

The replacement uses receiver-aware Roslyn CFG analysis for SPIRE013/SPIRE014.
It tracks possible variants on true/false edges and merges reachable alternatives
at joins. Boolean values retain evaluation-time snapshots; writes detach facts
about the changed source rather than reconstructing old checks from its syntax.
The same mutation model covers ref aliases, shared/exposed storage, callbacks,
accessors, constructors, operators, dynamic/function-pointer calls, suspension,
exception filters, and finally blocks. Unescaped value locals remain stable
across unrelated calls. State and callable summaries have finite domains.

The [coverage matrix](../../tests/Houtamelo.Spire.SourceGenerators.Tests/FieldAccess/GuardFlow.coverage-matrix.md)
records 192 new integration cases using the real generator, compiler, assembly
emission, and analyzer. They assert exact diagnostic IDs, severities, and spans;
analyzer exceptions fail the tests. Loop cases also exercise convergence.

Validation on the completed implementation:

- CoreCLR/NuGet: 2,652 passed via `tools/test-coreclr.sh`, including the original
  packed-consumer expression.
- Unity metadata: 95 passed via `tools/test-unity-metadata.sh`.
- Actual Unity 6000.5.6f1 Editor Mono on Linux: 2,933 passed, no failures or
  skips; exact discovery identities, platform exclusions, and execution checks
  passed. Report: `tmp/unity-coreclr-isolation-harness/harness-results.xml`.
- Targeted Roslyn 4.3.1 probes confirm condition snapshots, saved predicates,
  multiple receivers, ref aliases, and mutation ordering on the minimum host.

The final independent audit found no remaining blocker in the reviewed flow and
effect-model changes, including receiver-copy, callback-storage, operator, and
pointer-write controls.

Evidence: `/tmp/spire-guard-flow-v4-coreclr.log`,
`/tmp/spire-guard-flow-v4-unity-metadata.log`,
`/tmp/spire-guard-flow-v4-unity-native.log`, and
`tmp/variant-guard-flow-v4-review.md`.

The subsequent [whole-model hardening review](../reviews/2026-11-08-guard-flow-whole-model-review.md)
records broader composition findings, their regressions, and generated/runtime
and performance validation beyond that original audit's scope.
