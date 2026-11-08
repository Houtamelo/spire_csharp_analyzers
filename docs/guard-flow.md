# Variant guard flow analysis

SPIRE013 and SPIRE014 check whether a payload accessor belongs to every variant
that can reach that access. The analysis follows the receiver's storage and C#
evaluation order, including mutations between a guard and a read. It does not
infer guards from factory assignments or propagate caller guards into a nested
function's body.

Start with `FieldAccessSafetyAnalyzer`, then
`SourceGenerators/Analyzers/GuardFlow/GuardFlowAnalyzer.cs` for transfers and
`GuardFlowModel.cs` for state, joins, and invalidation. Both files live under
`src/Houtamelo.Spire.Analyzers/`. The
[hardening matrix](../tests/Houtamelo.Spire.SourceGenerators.Tests/FieldAccess/GuardHardening.coverage-matrix.md)
describes the integration contracts.

## Proofs and snapshots

| State | Meaning at a payload access |
|---|---|
| Unreachable | No incoming execution; do not report an access diagnostic. |
| Unknown | The receiver has no surviving useful guard proof; report SPIRE014. A set containing every declared variant provides no narrowing. |
| Known narrowed variants | Accept only if every possible variant owns the accessor; otherwise report SPIRE013. |

Receiver paths use symbol identity for locals, parameters, `this`, and field
segments. A repeated property, indexer, or method call is not a stable storage
identity. Ref aliases track possible targets; a definite target can identify a
receiver, while a write through an ambiguous alias must affect every possible
target.

A ref field or ref-return expression denotes indirect storage. Its textual
field/indexer path is not proof that it is independent of an exposed local.
Writes and ref rebinding must account for all possible referents, including
reverse invalidation when the original local changes. An arbitrary constructor
does not establish a precise alias relationship merely because it received a
ref argument. Guarding the same stable ref-field view can still prove its read.

A ref alias to `holder.Command` retains the original field's address if `holder`
is later reassigned. A guard on the replacement holder cannot prove that old
alias. Changing a reference-valued prefix must detach any target relationship
that depended on the old reference; an unknown target is a conservative result.
Replacing a union value in a local cell is different: an alias to that cell still
observes the same storage. Shared invalidation must follow alias dependencies,
including saved Boolean facts, even when a proof's immediate key is a local view.

Boolean locals and CFG captures retain facts from their evaluation. A later
write detaches the facts that depended on the changed storage. Reading the old
Boolean must never reconstruct a guard from its original expression:

```csharp
bool wasMove = command.IsMove;
command = Command.Stop();
if (wasMove)
    Use(command.move); // SPIRE014: the Boolean describes the old value.
```

Short-circuit edges carry facts only after the operand that established them
has executed. Built-in `&` and `|` evaluate both operands; their right operand
cannot borrow a short-circuit proof. A failed payload-restricted pattern does
not necessarily exclude that variant.

## Joins and effects

Only reachable predecessors contribute to a join. A receiver proof survives
only when every incoming predecessor has one; its possible-variant sets are
united. Alias targets and possible callable effects are also united. Unknown
must not be confused with an empty, unreachable alternative.

Evaluate receivers, arguments, and assignment values in C# order before
applying the corresponding call, ref/out write, or setter effect. Direct writes
invalidate dependent paths, saved predicates, captures, and alias views. User
code may affect shared or exposed storage and invoke escaped callbacks;
generated discriminator and payload projections have explicit purity rules.
An unescaped value local remains stable across unrelated user code.
Pointer escape through a field or array and callback escape must remain visible
to later opaque calls and suspension. Address exposure alone is not a reason to
forget a fresh guard; a subsequent effect that can reach that address is.

Runtime transfers and callable summaries must describe the same effects. When
adding an operation, check both `EvaluateOperation` and `CollectFunctionEffect`,
including their shared helpers. A local function that calls another function
must include the latter's effects even when their call graph contains a cycle.
A callback store records escape; it does not imply immediate invocation.
Discriminator proofs and callable capabilities have different lifetimes.
Invalidating a shared value must not erase a known callback's possible writes;
those writes still apply when the unchanged delegate is invoked later.
An impure property or deconstruction pattern cannot lend stale discriminator
facts to payload subpatterns. This applies to both pattern-local facts and a
guard established before entering the pattern.

Exception paths need state at the throwing operation, including effects already
performed. Filter failure can preserve mutations for later handlers. A normal
edge leaving a protected region must apply its `finally` regions before joining
the destination. Suspension invalidates potentially changed shared storage,
while stable value snapshots retain their proofs.

## Convergence and cancellation

CFG inputs are merged until their structural states stop changing. Diagnostics
are projected in a separate pass from those converged inputs. Local callable
summaries similarly accumulate effects over a finite local call graph; a
provisional empty summary must never be treated as a completed cyclic result.

The intended domains are finite sets of variants, operation/capture identities,
receiver/alias paths, and effect flags. New transfers must preserve a bounded
path domain and monotone joins; finite variant sets alone do not establish
termination. Avoid runtime mutation counters or permanently growing history.
When a target becomes uncertain, retain known possible write targets alongside
the unknown capability. Losing the ability to prove a read does not remove the
obligation to invalidate an unexposed local that an alias may still write.
Cancellation must be checked during scans, CFG iteration, summary construction,
and final reporting. Per-body partial results must not escape into a global
cache after cancellation.

## Validation boundaries

Handwritten integration cases assert diagnostic IDs, severities, and exact
source spans with the real generator, compiler, emitted assembly, and analyzer.
Generated semantic checks additionally execute bounded inputs and observe the
actual discriminator at each read. An accepted read observing an incompatible
variant is a failure. A safe bounded execution alone does not require the
analyzer to accept the program: its proof may be deliberately conservative.

Generated checks and measured workloads increase confidence within their
corpus; they do not prove all C# programs safe or establish IDE latency on every
host. Retain failing seeds as small regression cases and compare performance
under the same SDK, Roslyn version, host, and measurement conditions.

The [whole-model review](reviews/2026-11-08-guard-flow-whole-model-review.md)
records concrete findings and regression results. The
[performance report](benchmark-results/guard-flow-2026-11-08.md) links the raw
measurements and states the cancellation and allocation measurement boundaries.
