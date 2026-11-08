# SPIRE013 / SPIRE014 guard-flow hardening coverage matrix

This matrix defines the follow-up work after the receiver-aware CFG rewrite. It
has three goals: test combinations that are difficult to enumerate manually,
measure analysis cost and cancellation behavior, and make the abstract-model
invariants reviewable. The generated cases are real integration tests: generate
the union, compile and emit the consumer, run a small bounded probe, and then
run `FieldAccessSafetyAnalyzer` against the same compilation.

The generated probe is an independent semantic oracle. It constructs a finite
set of `Command` values (`Move`, `Stop`, and, where needed, `Idle`), executes the
guard/mutation program, and observes the actual discriminator immediately before
each payload read. Expected ownership is declared independently of the analyzer;
equivalent Boolean/receiver rewrites also compare runtime observations. The oracle is
one-way: it rejects an analyzer acceptance when a reached payload site can see
the wrong kind. A safe bounded run does not by itself require the analyzer to
accept; conservative warnings remain valid unless a row names an existing
precision/equivalence contract. Unsafe controls mark the exact stale access
with the existing `[|SPIRE014:...|]` or `[|SPIRE013:...|]` syntax and do not use
runtime behavior to excuse a required diagnostic.

Generated cases use deterministic recipe identities and a finite input domain.
Each emitted assembly contains a batch of probe methods, invoked by their stable
case identities; the harness does not load one assembly per trial. Inline trace
stores record `(siteId, actualKind, reached)` immediately before every payload read,
so accepted cases prove that every reached site had the owning kind and that
every intended site was actually exercised. Unique assembly identities avoid
load collisions. Each failing generated trial reports the recipe/case identity,
input sequence, failure category/site, and reduced source so it can become
a permanent focused regression. The harness must keep the existing
generator/compiler/emit checks, reject AD0001, preserve exact diagnostic spans,
and run under both the portable CoreCLR lane and the Unity Editor Mono lane.
The matrix rows below are research families, not individual implemented test
names or a claim that every family is independently generated. The implementation
mapping at the end distinguishes the generated corpus from existing integration
coverage and records measurement limitations.

## Category A: Boolean-equivalence generated probes

### should_fail

| File Name | Description |
|---|---|
| `Generated_Error_AndMutationBetweenGuardAndPayload` | Generate `IsMove && Mutate(ref command) && command.move.speed`; the final read is stale after the mutation and must retain a `SPIRE014` marker. The runtime probe confirms the mutation can select `Stop`. |
| `Generated_Error_OrGuardDoesNotProveLeftVariant` | Generate `command.IsMove || ready` followed by `command.move`; the body executes when the left side is false, so the access is unknown and must report `SPIRE014`. |
| `Generated_Error_BitwiseAndEvaluatesPayloadWithoutGuard` | Generate `command.IsMove & ReadMove(command)` where `ReadMove` reads `move`; both operands execute and the RHS cannot inherit a short-circuit proof. |
| `Generated_Error_BitwiseOrEvaluatesPayloadWithoutGuard` | Generate `!command.IsMove | ReadMove(command)` and require a warning on the RHS access even when the left operand is true for some inputs. |
| `Generated_Error_SavedPredicateAfterReceiverOverwrite` | Save `var move = command.IsMove`, replace `command`, then branch on `move`; the old Boolean snapshot must not guard the new value. |
| `Generated_Error_NegatedPredicateDoesNotNameAlternative` | For a three-variant union, generate `if (!command.IsMove) command.move`; the possible set contains `Stop`/`Idle`, so this is SPIRE013 rather than a silently accepted read. |
| `Generated_Error_PayloadRestrictionComplement` | Generate `{ IsMove: true, move: { speed: > 0 } }` and read `stop` on the failed pattern path; a failed payload restriction does not prove `Stop` and must remain SPIRE014 or SPIRE013 according to the known discriminator state. |
| `Generated_Error_ContradictoryBooleanMutationPath` | Generate a nested condition whose first branch proves Move, invokes a mutator, and then reaches a second Boolean check through `||`; no stale first-branch proof may survive the join. |

### should_pass

| File Name | Description |
|---|---|
| `Generated_Pass_AndOrRewritesHaveSameTruthTable` | For every bounded `ready` value, compare equivalent forms of `IsMove && ready`, `!(!IsMove || !ready)`, and their parenthesized/reversed Boolean comparisons. Matching true paths may read only `move`; all forms must compile, execute equivalently, and remain diagnostic-free. |
| `Generated_Pass_ShortCircuitRhsGuard` | Generate `IsMove && command.move.speed > 0` and `!IsMove || command.move.speed > 0`; execute both variants over all kinds and confirm the payload expression is reached only when Move is active. |
| `Generated_Pass_OrOfKindsThenComplement` | For a three-variant union, generate `kind == Move || kind == Stop` with an early return and read `idle` on the continuation. Exhaustive execution must agree with the complement set and produce no diagnostic. |
| `Generated_Pass_NestedDeMorganForms` | Generate nested `!`, `&&`, and `||` expressions that are truth-table equivalent for two receivers. Verify that both receiver proofs are present only on the shared true path. |
| `Generated_Pass_SavedPredicateUnchangedReceiver` | Save `IsMove`, compare it through `== true`, `true ==`, `!= false`, and an early-exit negation without writing the receiver. All accepted reads must observe Move at runtime. |
| `Generated_Pass_UnreachableContradiction` | Generate contradictory nested guards (`Move && Stop`) around a payload read. The runtime probe confirms that the site is never reached; retain the existing explicit unreachable-branch diagnostic expectation separately rather than inferring it from runtime reachability. |
| `Generated_Pass_PureBooleanAndBitwiseFacts` | Generate bitwise `&`/`|` only over pure Boolean locals after discriminator checks have already been evaluated. The analyzer may preserve facts only where both operands have completed; the emitted probe must confirm those paths. |
| `Generated_Pass_MultipleReceiverConjunction` | Generate `(first.IsMove && second.IsMove) == true` and read both `first.move` and `second.move`; all bounded pair inputs must agree with the conjunction and no cross-receiver proof may leak. |

## Category B: nested control flow, joins, and loops

### should_fail

| File Name | Description |
|---|---|
| `Generated_Error_NestedElseJoinWidensPossibleKinds` | In nested `if/else` branches, guard Move on one path and Stop on another, then join before reading `move`; require SPIRE013 because the join permits both kinds. |
| `Generated_Error_SavedPredicateLoopBackedge` | Save a predicate before a loop, mutate the receiver at the end of one iteration, and reuse the saved Boolean on the next iteration. The backedge must not revive the old correlation. |
| `Generated_Error_LoopPartialGuard` | Guard only one path of a loop body and read the payload on the other path. Exhaustive bounded execution must include the unsafe iteration and retain SPIRE014. |
| `Generated_Error_BreakContinueJoin` | Use `continue` from a mutating branch and `break` from a guarded branch before a shared payload read. The merged state must include the invalidated path. |
| `Generated_Error_ExceptionFilterMutation` | Guard a field path, mutate it in an exception filter or catch path, and read after the handler join. The filter/finally route must invalidate the proof. |
| `Generated_Error_FinallyAlternativeRebind` | Rebind a ref alias to different union cells in mutually exclusive branches, then read through the original path after `finally`; possible targets must be joined conservatively. |
| `Generated_Error_NestedCallbackComposition` | A local function calls another local function that may mutate a captured command; invoke the outer callback after a guard and require SPIRE014. |
| `Generated_Error_AwaitSharedReceiver` | Guard a field-held command, suspend, allow the generated probe to mutate the field, and read afterward. Shared storage must be invalidated across suspension. |

### should_pass

| File Name | Description |
|---|---|
| `Generated_Pass_NestedIfAndSwitchJoin` | Every reachable path reaching a payload read proves the same variant, even when nested `if` and switch constructs are reordered. Runtime execution must cover all paths. |
| `Generated_Pass_LoopRechecksFreshGuard` | Mutate a command at the end of each iteration, then re-evaluate `IsMove` at the top before reading `move`. The finite fixed point must converge and accept the fresh proof. |
| `Generated_Pass_BreakContinueGuardedRead` | A loop reads `move` only after a per-iteration guard; `break` and `continue` paths must not create false unknown states. |
| `Generated_Pass_UnescapedValueAcrossAwait` | Copy a command by value, guard the local, await an unrelated operation, and read the local. The local remains stable; a separate field receiver in the same probe is invalidated. |
| `Generated_Pass_UnreachableCatchRead` | A catch body is unreachable for the bounded probe but has a complete matching guard when considered by the analyzer. No unrelated path may poison the safe read. |
| `Generated_Pass_ThreeVariantExhaustiveSwitch` | Generate a switch that narrows each variant, including a nested guard in one arm. All payload reads must match their active kind. |
| `Generated_Pass_EquivalentEarlyExitShapes` | Compare `if (!IsMove) return`, `if (IsMove == false) return`, and `if (!(kind == Move)) return`; all continuation reads must execute only for Move. |
| `Generated_Pass_RecursiveLocalSummaryConverges` | Generate a finite mutually recursive local-call graph whose eventual path rechecks the discriminator before reading. It must terminate, converge, and preserve the fresh proof. |

## Category C: receiver identity, aliases, and effects

### should_fail

| File Name | Description |
|---|---|
| `Generated_Error_PropertyReceiverReevaluated` | Guard `holder.Current.IsMove`, then read `holder.Current.move` where the getter can return a different command on the second call. Textually identical paths must not be treated as one receiver. |
| `Generated_Error_IndexerReceiverReevaluated` | Guard `holder.Commands[index].kind`, mutate or replace the indexed slot, then read the same index's payload. The path is unstable and must warn. |
| `Generated_Error_RefAliasMayTargetJoin` | Select a ref alias from two possible command cells, guard one cell, write through the alias, and read the guarded cell. Any possible target invalidates the proof. |
| `Generated_Error_RefParameterMayAlias` | Pass two ref parameters that may point at the same storage; write through one after guarding the other and require SPIRE014. |
| `Generated_Error_AddressExposedPointerWrite` | Take a command address, guard the local, perform an opaque pointer write through `*pointer` or `pointer[0]`, and read the payload afterward. |
| `Generated_Error_UserConversionBreaksIdentity` | Wrap a guarded receiver in a user-defined conversion that can mutate shared state, then read the payload through the converted expression. |
| `Generated_Error_ConstructorOrOperatorMutation` | Invoke a constructor, unary/binary operator, or dynamic operation that may write the receiver or a shared field between guard and access. All use the same opaque effect model. |
| `Generated_Error_EscapedCallbackThroughFieldOrArray` | Store a callback that writes a guarded command into a field or array, invoke it through an opaque helper, then read the old payload. |

### should_pass

| File Name | Description |
|---|---|
| `Generated_Pass_StableFieldPath` | Guard and read the exact same stable field path without intervening writes. Runtime probes must exercise both matching and nonmatching initial kinds. |
| `Generated_Pass_ByValueUnrelatedCall` | Guard an unescaped value local, pass an unrelated value to an opaque helper, and read the original payload. The helper cannot mutate the unescaped local. |
| `Generated_Pass_DirectAliasWithoutWrite` | Bind a direct ref alias to a command, guard and read through that alias without mutation. Alias identity is accepted when the target is definite. |
| `Generated_Pass_FreshGuardAfterMutation` | Replace a command, re-check its discriminator, and read the matching payload. The fresh guard restores safety. |
| `Generated_Pass_PayloadProjectionIsPure` | Read generated kind/accessor/payload projections from the same stable receiver after a positive guard. Generated projections do not act like arbitrary user getters. |
| `Generated_Pass_ReadonlyValueAcrossUnrelatedEffects` | A readonly/in view of an unescaped value is guarded and read while unrelated shared storage changes. The value snapshot remains semantically stable. |
| `Generated_Pass_SetterRhsEvaluationOrder` | Read a guarded payload in an assignment RHS before a setter replaces the receiver. C# argument/assignment evaluation order must be reflected in both runtime and flow state. |
| `Generated_Pass_BuiltinReferenceConversionPreservesField` | Apply only a built-in reference-preserving conversion to a stable holder, guard the field, and read through the converted holder. User-defined conversions remain in the failing category. |

## Category D: generated semantic oracle and minimized regressions

### should_fail

| File Name | Description |
|---|---|
| `Generated_Error_MinimizedSeedIsStable` | When a generated run fails, rerun the reducer and persist the smallest source/seed pair that still demonstrates the wrong acceptance or wrong diagnostic. Replay must reproduce the same failure and include the original seed, case id, and reduced source. |
| `Generated_Error_AnalyzerMustNotAcceptUnsafeExecution` | Inject a mutation into an otherwise safe generated template and assert that at least one bounded input reaches a mismatched payload read; the analyzer must report SPIRE014/SPIRE013 at that access. |
| `Generated_Error_DiagnosticDomainMatchesState` | Generate cases where the receiver is unknown versus known-but-incompatible. Assert SPIRE014 for unknown and SPIRE013 for a surviving incompatible possible-kind set. |

### should_pass

| File Name | Description |
|---|---|
| `Generated_Pass_ReferenceInterpreterMatchesEmittedProbe` | For all fixed seeds and bounded inputs, the emitted consumer's final kind/result equals the independent reference interpreter. This validates the oracle before using it to judge analyzer acceptance. |
| `Generated_Pass_PureRewriteMetamorphism` | Replace each generated guard with a truth-table-equivalent rewrite (`kind` comparison, `IsVariant`, negated comparison, De Morgan form). Safe execution and analyzer result must remain equivalent. |
| `Generated_Pass_ReceiverRenamingMetamorphism` | Rename locals/parameters/holders while preserving the access graph. Diagnostics and runtime observations must remain equivalent, proving identity is symbol/path based rather than textual-name based. |
| `Generated_Pass_DeadBranchEliminationMetamorphism` | Add or remove a branch proven unreachable by contradictory conditions. The reachable runtime result and diagnostics must not change. |
| `Generated_Pass_TrialBudgetAndDeterministicReplay` | Run the configured finite trial budget under CoreCLR and Unity Mono, then replay the same seed set and require byte-for-byte stable case identifiers and outcomes. |

## Category E: performance and cancellation workloads

These rows are harness outcomes rather than SPIRE diagnostics. The durable
runner should be a standalone `tools/Spire.GuardFlow.Performance` net10 project
with a machine-readable JSON result (schema version, commit, SDK/Roslyn/host,
workload dimensions, warm-up count, wall time, process allocation deltas,
syntax-node count as a complexity proxy, and exact diagnostic identities/hash).
It must be separate from ordinary xUnit timing and from the unrelated
BenchmarkDotNet project. Workloads are generated in-memory from fixed
templates and measured after one warm-up run. Do not assert a machine-specific
absolute time in ordinary unit tests; use a generous CI budget and retain raw
measurements as an artifact. Cancellation starts after generation, compilation,
and emission, once the analyzer API has been entered. The runner requires
cancellation and a clean successful subsequent run, and records request-to-return
latency. Internal phase/summary-count instrumentation is not exposed.

### should_fail

| File Name | Description |
|---|---|
| `Performance_Error_CancellationIsIgnored` | Cancel during a deliberately large CFG/call-summary workload. The analyzer must observe the token and stop; a completed result after cancellation is a harness failure. |
| `Performance_Error_NonConvergentRecursiveSummary` | Feed a bounded mutually recursive local-call graph with repeated joins. Analysis must terminate within the budget; timeout or monotonically growing summaries is a failure. |
| `Performance_Error_UnexpectedSuperlinearGrowth` | Record growth across workload sizes for comparison. The implemented gate enforces a generous per-analysis budget; it does not assert an uncalibrated machine-independent growth envelope. |

### should_pass

| File Name | Description |
|---|---|
| `Performance_Pass_SmallBaseline` | Measure a 2-variant, 2-branch consumer as the baseline for generator, compilation, and analyzer phases separately. |
| `Performance_Pass_BranchScaling` | Scale independent `&&`/`||` branches from 8 to 256 while keeping receiver count fixed. Record diagnostics and analysis time; verify fixed-point termination at every size. |
| `Performance_Pass_ReceiverScaling` | Scale stable receivers and conjunctions from 1 to 64. Summary size and time must remain bounded by the finite receiver/effect domains. |
| `Performance_Pass_AliasAndEffectScaling` | Scale ref aliases, callback summaries, and join points independently. Confirm no stale proof acceptance and record the first workload where conservative widening occurs. |
| `Performance_Pass_RecursiveCallGraphScaling` | Scale a finite local call graph from 1 to 32 nodes with cycles and summary joins. Require convergence, cancellation checks, and stable diagnostics. |
| `Performance_Pass_CancellationAtPhaseBoundaries` | Measure cancellation at the prepared analyzer API boundary. Candidate/CFG/summary/report checkpoint coverage is source-reviewed; the tool does not claim to observe which internal phase consumed the token. |
| `Performance_Pass_Repeatability` | Run each workload three times after warm-up. The diagnostic set and source spans must be identical; timing variance is reported, not used to alter results. |

## Whole-model review checklist

The reviewer should inspect the model and tests against these invariants and
record evidence or a follow-up issue for every exception:

1. **State meaning is explicit.** A receiver state distinguishes unreachable,
   unknown, and a non-empty possible-kind set. SPIRE014 is reserved for unknown;
   SPIRE013 is used when a reachable set contains a kind that cannot own the
   accessor.
2. **Facts are edge-specific.** `&&`, `||`, `!`, comparisons, switches, filters,
   loops, and early exits apply true/false facts on the same evaluation edge.
   Boolean locals hold evaluation-time snapshots and are not reconstructed from
   their original syntax after a write.
3. **Joins are conservative and monotone.** A proof survives a join only when
   every incoming reachable state supports it. Possible alias targets and
   callable effects use finite unions; recursive summaries reach a fixed point
   without a provisional empty-cache order bug.
4. **Receiver identity is semantic.** Stable locals, parameters, fields, and
   direct ref aliases compare symbols and path segments. Repeated properties,
   indexers, calls, conditional access, volatile paths, boxing, and user-defined
   conversions are not silently treated as the same value.
5. **Effects follow C# evaluation order.** Arguments and assignment RHS values
   are evaluated before later ref/out writes or setters. Generated pure
   projections stay pure, while user code, callbacks, constructors, operators,
   accessors, dynamic/function-pointer calls, suspension, and exposed storage
   invalidate the affected paths.
6. **Unrelated storage remains precise.** An opaque write invalidates shared or
   address-exposed targets that may alias the receiver, but an unescaped value
   local survives unrelated calls, suspension, and writes.
7. **Diagnostics remain contractual.** IDs, severities, locations, and message
   arguments do not change; unreachable code emits nothing; analyzer exceptions
   are test failures; minimum Roslyn 4.3.1 remains supported.
8. **Cancellation is pervasive.** Candidate scan, CFG work, callable-summary
   fixed points, and final projection all check the provided token. Cancellation
   cannot leave a globally cached partial summary that contaminates a later
   analysis.

## Ownership and validation commands

The generated semantic test writer owns new test/helper files under
`tests/Houtamelo.Spire.SourceGenerators.Tests/FieldAccess/` and any focused
regression source. The performance writer owns the performance harness and its
artifact/documentation files; it must not relax ordinary test assertions to
make a workload pass. The review owner updates this matrix only when an
invariant or workload is added, removed, or disproven.

Minimum validation for each handoff:

```text
dotnet test tests/Houtamelo.Spire.SourceGenerators.Tests/Houtamelo.Spire.SourceGenerators.Tests.csproj --filter FullyQualifiedName~FieldAccess
bash tools/test-coreclr.sh
bash tools/test-unity-metadata.sh
```

The generated trial suite should additionally run through the existing Unity
6000 Editor Mono harness. Performance reports should include SDK, Roslyn, host,
configuration, warm-up policy, workload sizes, and raw measurements so a later
comparison is meaningful.

## Implementation mapping and limits

| Contract | Implementation |
|---|---|
| Generated combinations | `GuardGeneratedFlowTests`: 512 recipe consumers, emitted in batches of 64, with 12 inputs crossing three variants and two independent Boolean flags. Guard/comparison syntax, alias/value receivers, saved/direct predicates, nesting/loops/early exits, and direct/callback mutation are crossed. Equivalent syntax is not counted as an additional semantic dimension. |
| Runtime oracle and failure replay | `GuardGeneratedFlowHarness`: per-site tag observations, source-span diagnostic matching, explicit unsafe-site obligations, fault-injected missing-diagnostic controls, and failure-only reduction through the real pipeline. |
| Host synchronization context | The real reducer pipeline runs under a non-pumping context with bounded cleanup. The test fails without context-independent awaits; it does not rely on an Editor hang as its only regression signal. |
| Scope, aliases, and indirect references | `GuardModelRegressionTests`: nested functions/lambdas, loop-carried ref rebinding, and portable ref-return span writes. |
| Shared alias dependencies and capability lifetimes | `GuardSharedAliasRegressionTests`: shared/saved/ambiguous views, explicit and opaque reference-prefix rebinding, retained mixed-target write obligations, same-cell value identity, and callback effects surviving shared invalidation. |
| Effects and reachability | `GuardModelEffectRegressionTests`: escaped pointers/callbacks, iterator suspension, impure patterns, constants, and pure/unrelated-storage controls. |
| CoreCLR runtime capability | `NuGetConsumerTests`: actual packed/installed consumers with C# 11 ref fields, exact warning locations, and executed positive controls. These consumers are outside the Mono suite. |
| Remaining research families | The existing `GuardBooleanFlowTests`, `GuardControlFlowTests`, `GuardReceiverStabilityTests`, and `GuardEffectSafetyTests` retain the prior 192 handwritten integration cases. They cover additional language shapes; they are not 192 extra generated dimensions. |
| Measurements | `tools/Spire.GuardFlow.Performance`: independent scaling dimensions, exact diagnostic controls, warm-up plus three samples, cancellation and clean replay. Raw reports identify the SDK, Roslyn, host, and analyzer binary. |
| Model review | `docs/guard-flow.md` and the linked whole-model review document invariants, verified findings, and validation boundaries. |

This work uses a runtime-tag oracle rather than a separate handwritten language
interpreter. It does not implement unrestricted random C# generation, internal
phase telemetry, or a formal proof. Bounded execution is evidence within the
generated corpus; it cannot certify every possible execution of arbitrary code.
