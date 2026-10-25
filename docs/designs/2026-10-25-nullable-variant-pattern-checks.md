# Nullable variant accessor experiment

Request recorded before implementation: evaluate the proposed nullable variant
accessors, where an active variant returns its payload and an inactive variant
returns null. Consumers match `case { idle: Idle idle }` instead of separately
matching `kind`. Check for performance regressions before changing the generator.

The getter carries `MethodImplOptions.AggressiveInlining`, as in the requested
prototype. Compare it with the existing nonnullable payload accessor plus an
explicit kind pattern, using identical storage and arithmetic.

Run actual Unity 6000.5.6f1 Linux x64 Mono and IL2CPP release players. Include
small and larger switches, managed payloads, early/late/cycling/shuffled matches,
single-variant extraction, correctness checks, repeated fresh-process timings,
and native-code inspection. Keep production generation unchanged during this
experiment. Save the reproducible source, raw samples, and findings.

Completed: both actual release backends passed correctness checks. Repeated
measurements demonstrate regressions; the performance-parity assumption does
not hold. Production generation remains unchanged. See
[results and limitations](../reviews/2026-10-25-unity-nullable-variant-patterns.md).

Follow-up request, recorded before implementation: run the same tests on CoreCLR
to investigate whether newer C# compilers or runtimes optimize the nullable
pattern differently. Preserve the original Unity-compiled matcher assembly to
isolate runtime changes; compare compiler output separately. Use installed
.NET 8, .NET 10, and .NET 11 preview runtimes and retain native JIT evidence.

Follow-up completed: twelve fresh CoreCLR processes passed all correctness and
timed checksum checks. Larger shuffled nullable switches win on all three
versions, with predictable-input regressions still present. Newer Roslyn under
C# 9 / C# 14 emits the same normalized IL as Unity's compiler. See the
[CoreCLR results](../reviews/2026-10-25-coreclr-nullable-variant-patterns.md).

Documentation request, recorded before changes: retain this idea for future
consideration when Unity introduces CoreCLR. The primary benefit is a cleaner
switch statement, not just performance optimization. Record the proposed API,
runtime evidence, and conditions for revisiting it without changing generation.

Recorded as a [deferred API improvement for Unity CoreCLR](2026-10-25-future-coreclr-nullable-variant-accessors.md),
with cleaner matching as its primary motivation and backend performance as an
adoption constraint.
