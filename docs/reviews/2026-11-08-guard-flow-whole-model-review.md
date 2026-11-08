# SPIRE013 / SPIRE014 guard-flow hardening review

The whole-model review examined receiver identity, Boolean snapshots, joins,
exception transfers, callable summaries, effect invalidation, convergence, and
cancellation. It found concrete gaps beyond the previously reviewed changes.
Each confirmed finding has a real compiler/analyzer regression; runtime probes
also verified the wrong-discriminator behavior for the unsafe acceptances.

## Confirmed findings

| Finding | Cause | Regression coverage |
|---|---|---|
| Nested local functions can produce AD0001; nested lambdas can lose diagnostics. | A descendant executable body was looked up through a CFG that did not own it. | `GuardModelRegressionTests`: nested locals/lambdas, independent inner guards, sibling and recursive functions. |
| A fresh guard after loop-carried ref rebinding can warn incorrectly. | The proof identity and the alias view used at the read can differ after a join. | Same class: recursive field paths, fresh guards, and stale-guard controls. |
| A synthetic alias proof can survive mutation of its shared referent. | Shared invalidation considered the local view's path without its possible target dependencies. | `GuardSharedAliasRegressionTests`: definite, saved, and ambiguous shared aliases, plus unexposed-local controls. |
| Rebinding an object reference can lend a new object's proof to an alias addressing the old object. | A captured field address was subsequently resolved through a mutable lexical receiver prefix. | Same class: local/nested reference rebinding, stable field storage, and replacement of a value in the same local cell. |
| Detaching one possible target can lose another possible local write target. | Replacing an entire may-target set with an empty unknown set discarded write obligations. | Same class: mixed local/heap targets and precision after explicit rebinding. |
| Shared invalidation can forget a later callback's local writes. | Callable effect capabilities were discarded as if they were discriminator proofs. | Same class: invoked local/heap writer and unused/heap-only precision control. |
| Ref-field writes can preserve a stale guard on their referent. | Ref fields were treated as ordinary field storage rather than indirect storage. | Installed NuGet consumer: direct writes, reverse aliases, rebinding, helper effects, and stable snapshots. |
| A ref-return indexer can mutate a guarded local without invalidating it. | An indirect write through `MemoryMarshal.CreateSpan` was not connected to exposed storage. | `GuardModelRegressionTests`: span writes, stable copies, and fresh checks. |
| A pointer stored in static storage can mutate a guarded local through a later call. | Pointer escape and subsequent opaque mutation were not composed. | `GuardModelEffectRegressionTests`: escaped pointer and unrelated-pointer control. |
| An escaped callback can mutate a local between iterator resumptions. | Suspension did not cover every retained callback capability. | Same class: escaped callback and stable unescaped local. |
| A dynamic receiver supplied by the caller can invoke an escaped callback and preserve a stale local proof. | The dynamic boundary omitted escaped-callback invalidation. | Same class: dynamic parameter invocation and unrelated-storage controls. |
| An impure property pattern can accept a payload read after changing the discriminator. | Pattern-local facts could override mutation invalidation, including an earlier outer guard. | Same class: impure getter with an inner or outer guard, plus pure generated projections. |
| A literal dead branch can report SPIRE014. | Unreachable CFG edges and predicate reachability were not fully respected. | Same class: constants, contradictory conditions, and reachable negative controls. |

Frozen Roslyn 4.3.1 probes compiled and emitted successfully. Executed witnesses
observed `Stop` at the accepted ref-field, escaped-pointer, dynamic-call, and iterator payload
reads. The impure pattern matched while its observed and original discriminator
were both `Stop`, and its extracted payload was invalid for that discriminator.

Two suspected gaps were not reproduced: an unknown delegate invoking an escaped
callback and a custom awaiter invoking that callback already produced SPIRE014.
They remain useful controls; they are not reported as newly fixed bugs.

## Model invariants

The [model contract](../guard-flow.md) describes the required invariants. Review
identified the following maintenance obligations:

- Traverse nested executable bodies through their owning CFG and analyze their
  guards independently.
- Use one consistent receiver identity for refinement, reads, alias rebinding,
  and reverse invalidation. A proof cannot outlive a write to any possible
  referent.
- Join only reachable states; unknown, incompatible, and unreachable remain
  distinct diagnostic states.
- Retain evaluation-time Boolean snapshots. Never recover an invalidated proof
  by reevaluating the syntax that originally created the Boolean.
- Keep normal operation effects and callable summaries aligned, especially
  indirect writes, escapes, and suspension.
- Preserve a bounded receiver-path domain. Static operation identities and
  finite variant sets alone do not bound paths synthesized through aliases.
- Check cancellation during candidate discovery as well as CFG iteration,
  callable-summary construction, and final diagnostic projection.

The ordinary joins use conservative variant and alias unions. Local callable
summaries accumulate effects over a finite call graph. These are structural
convergence arguments, not a formal verification of every transfer function.

## Validation

Final source and validation-tool closure reviews found no confirmed blocker in
their reviewed scopes. The new coverage consists of 54 focused regression Facts,
nine generated/oracle/context Facts, and an installed NuGet consumer Fact. The generated
corpus executes 512 consumers over 12 inputs: 6,144 bounded executions, with
256 required unsafe sites. Named precision controls reject unexpected warnings;
the broader one-way oracle retains conservative warnings on bounded-safe code.

Validated on the frozen source:

- CoreCLR/NuGet: 2,716 passed, zero failures/skips; Release build had zero
  warnings/errors.
- Unity metadata: 95 passed, zero failures/skips.
- Actual Unity 6000.5.6f1 Editor Mono on Linux: 2,996 passed, zero failures/skips.
  All 2,879 applicable xUnit cases and 117 original behavioral cases executed;
  exact .NET/Unity case identities and all eight explicit platform exclusions
  matched. Report: `tmp/unity-coreclr-isolation-harness/harness-results.xml`.
- Eight isolated minimum Roslyn 4.3.1 probes compiled/emitted and produced the
  expected diagnostics, including the nested-body positive and the confirmed
  alias/effect counterexamples.
- The [performance report](../benchmark-results/guard-flow-2026-11-08.md) records
  20 full workloads, exact diagnostic controls, minimum-host validation, and
  cancellation followed by a successful clean replay. Raw measurements identify
  the actual analyzer DLL hash as well as the uncommitted worktree's base SHA.

The first Editor attempt exposed a deadlock in the new reducer harness: its
synchronous replay waited for a continuation captured by Unity's thread. The
harness now avoids that context capture. A real non-pumping-context regression
fails deterministically without the fix and passes with it, using bounded
cleanup. The analyzer binary and its performance measurements were unchanged.

Counts overlap across host lanes and must not be added as independent semantic cases.
These checks provide bounded integration evidence, not formal verification of
all C# programs or a universal IDE latency guarantee.
