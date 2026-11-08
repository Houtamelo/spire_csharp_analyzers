# SPIRE013 / SPIRE014 receiver-aware guard-flow coverage

This matrix is the contract for replacing the parent-chain lookup in
`FieldAccessSafetyAnalyzer` with intraprocedural Roslyn CFG flow. Cases must run
the real discriminated-union generator and then the real analyzer; hand-written
generated members or mock symbols are not acceptable. Focused test classes use
`GuardFlowTestHelper` and mark each exact diagnostic span with
`[|SPIRE013:expression|]` or `[|SPIRE014:expression|]`.

`SPIRE014` means the receiver's possible-kind state is unknown. `SPIRE013`
means a guard established a non-empty possible set, but that set still contains
a variant that does not own the accessed accessor. Diagnostic locations and the
existing message arguments remain unchanged. An unreachable branch has no
diagnostic; it must not be represented as unknown.

Use one stable `Command` preamble for most cases (`Move(int destination,
float speed)`, `Stop()`, and, where needed, `Idle()`), with a three-variant
control for set-union/exclusion cases. Payload fields after a detached copy
(`var payload = command.move`) are ordinary struct fields and receive no
SPIRE013/SPIRE014 treatment. A direct generated factory accessor such as
`Command.Stop().stop` remains safe; assigning that factory result to a local
does not silently establish a guard.

The four sections are independent writer categories. Within each category,
subgroups keep related cases together while rows remain focused enough to
become individual case files. For cases requiring multiple exact diagnostics or
emitted-assembly checks, use focused test classes and a shared
`GuardFlowTestHelper` that asserts no generator/compiler errors, emits the
generated assembly, and rejects AD0001.

## Category A: Boolean guards and possible-kind sets

### should_pass

| File Name | Description |
|---|---|
| `Pass_KindEqualsBothOperandOrders` | `command.kind == Command.Kind.Move` and reversed `Command.Kind.Move == command.kind` each establish `{Move}` before `command.move.speed`. |
| `Pass_KindNotEqualsEarlyReturnBothOrders` | `if (command.kind != Stop) return;` and reversed `if (Stop != command.kind) return;` leave the remaining path in `{Stop}` and allow `command.stop`. |
| `Pass_NegatedKindEquality` | `if (!(command.kind == Move)) return;` refines the continuing path to `{Move}`. |
| `Pass_IsVariantPositiveAndReversedBooleanComparison` | `IsMove`, `IsMove == true`, `true == IsMove`, and equivalent positive forms all allow `command.move` on the true path. |
| `Pass_IsVariantNegativeComparisonEarlyExit` | `if (IsMove == false) return;`, `if (false == IsMove) return;`, `if (IsMove != true) return;`, and `if (!IsMove) return;` all leave `{Move}` after the exit. |
| `Pass_AndShortCircuitRhsGuard` | `if (ready && command.kind == Move)` allows `command.move` in the body; the right-hand condition is evaluated only on the left-true path. |
| `Pass_ShortCircuitPredicateRhsAccessor` | Both `if (command.IsMove && command.move.speed > 0)` and `if (!command.IsMove || command.move.speed > 0)` safely read `move` in the RHS because short-circuit evaluation proves `IsMove` there. |
| `Pass_AndKindThenUnrelatedCondition` | `if (command.kind == Move && ready)` retains `{Move}` in the body and does not require the unrelated condition to be modeled as a receiver guard. |
| `Pass_OrEarlyExitComplement` | `if (command.kind != Move || !ready) return;` leaves the continuation in `{Move}`. The false edge combines both short-circuit facts. |
| `Pass_OrRhsGuardAfterFalseLeft` | `if (ready || command.kind != Move) return;` leaves `{Move}` after the condition is false, proving the RHS guard is applied only when the left operand is false. |
| `Pass_OrOfKindsThenComplement` | In a three-variant union, `if (kind == Move || kind == Stop) return;` leaves `{Idle}` and permits `command.idle`. |
| `Pass_ContradictoryAndIsUnreachable` | A body guarded by `command.kind == Move && command.kind == Stop` is unreachable and produces no SPIRE014. |
| `Pass_NestedPredicateComposition` | Parenthesized `!(IsStop || IsIdle)` and equivalent nested `&&`/`!` composition refines to `{Move}` before `command.move`. |
| `Pass_PropertyPatternTrueWithPayloadRestriction` | `{ IsMove: true, move: { speed: > 0 } }` establishes `{Move}` on success; the payload restriction does not weaken the positive discriminator proof. |
| `Pass_DirectFactoryAccessor` | `Command.Stop().stop` is safe because the receiver is the direct generated factory invocation. |
| `Pass_NameofDoesNotReadPayload` | `nameof(command.move)` does not evaluate a payload accessor and needs no discriminator proof. |
| `Pass_ExplicitGuardOnSingletonUnion` | A one-variant union still accepts `if (value.kind == Only) return value.only`; an explicit check is required even when the declared domain has one member. |
| `Pass_CompoundBooleanComparisonKeepsTrueFacts` | Comparing a captured compound condition `(command.IsMove && ready) == true` (including reversed operands) retains the facts implied by its true result after the CFG capture join. |
| `Pass_NegatedCompoundBooleanComparison` | `!((command.IsMove && ready) == false)` retains the same Move proof; Boolean wrappers cannot lose the short-circuit condition's true/false correlation. |
| `Pass_BitwiseAndTrueEdgeCombinesPureFacts` | The true body of `command.IsMove & ready` implies Move after both operands have been evaluated. Accept its payload when the operands have no receiver-mutating effects. |
| `Pass_SavedPredicateBooleanComparisons` | A saved predicate local retains its source correlation through `== true`, reversed comparison, `!= false`, and false-comparison early exit, while the source is unchanged. |
| `Pass_CompoundComparisonTracksMultipleReceivers` | A wrapped conjunction such as `(first.IsMove && second.IsMove) == true` proves both receivers. Branch facts cannot be limited to one receiver per Boolean value. |
| `Pass_LoopReevaluatesCompoundGuardsAfterMutation` | Each loop iteration evaluates a fresh compound guard after the previous iteration's receiver mutation. Capture dependencies reset at their definitions, and the finite fixed point accepts the guarded read. |

### should_fail

| File Name | Description |
|---|---|
| `Error_KindEqualsWrongVariant` | Under `kind == Move`, access `command.stop`; report `SPIRE013` at `stop` because the proven set is `{Move}`. |
| `Error_KindNotEqualsTrueBranchWrongVariant` | Inside `if (kind != Move)`, access `command.move`; report `SPIRE013` because the true path is the complement set, not unknown. |
| `Error_NegatedKindEqualityWrongVariant` | Under `if (!(kind == Move))`, access `command.move`; report `SPIRE013` on the possible non-Move path. |
| `Error_NegatedIsVariantTrueBranchWrongVariant` | Inside `if (!command.IsMove)`, access `command.move`; report `SPIRE013`, preserving negative-predicate exclusion semantics. |
| `Error_IsVariantFalseComparisonWrongVariant` | `if (command.IsMove == false) return command.move.speed;` reports `SPIRE013` in the false-predicate branch rather than treating the comparison as no guard. |
| `Error_AndGuardWrongVariant` | Under `kind == Move && ready`, access `command.stop`; report `SPIRE013`, not SPIRE014. |
| `Error_BooleanBitwiseAndDoesNotGuardRhs` | In `if (command.kind == Move & command.move.speed > 0)`, the RHS accessor is evaluated even when the left operand is false; report `SPIRE014` at `move`. Built-in boolean `&` is not short-circuit flow. |
| `Error_BooleanBitwiseOrDoesNotGuardRhs` | In `if (command.kind != Move | command.move.speed > 0)`, the RHS accessor is evaluated even when the left operand is true; report `SPIRE014`. Built-in boolean `|` is not short-circuit flow. |
| `Error_OrGuardPartialPossibleSet` | `if (kind == Move || ready) command.move` rejoins a path with no discriminator proof when `ready` is true; report `SPIRE014`, not SPIRE013. |
| `Error_OrTrueBodyDoesNotInheritRhsProof` | The RHS of `!command.IsMove || command.move.speed > 0` is safe, but its true body may execute for a non-Move value. A payload access in that body reports SPIRE014. |
| `Error_BitwiseAndFalseEdgeIsNotLeftComplement` | After `if (command.IsMove & ready) return;`, the remainder can still contain Move when ready is false. Reading Stop there reports SPIRE014 rather than accepting a false complement proof. |
| `Error_WrappedAndFalseEdgeIsNotLeftComplement` | The same rule applies to `(command.IsMove && ready) == true` followed by a Stop accessor after early exit: false includes Move and non-Move. |
| `Error_OrKindsPossibleSet` | In a three-variant union, `if (kind == Move || kind == Stop) command.move` reports `SPIRE013` because the guarded set includes `Stop`. |
| `Error_AndExclusionsPossibleSet` | A conjunction that excludes two variants but then accesses an incompatible accessor reports `SPIRE013` with the merged possible set. |
| `Error_NestedPredicatePossibleSet` | `if (IsMove || IsStop) command.move` reports `SPIRE013`; nested composition must preserve `{Move, Stop}`. |
| `Error_PayloadRestrictedPatternDoesNotProveComplement` | `if (command is { IsMove: true, move: { speed: > 0 } }) return; return command.move.speed;` and the corresponding false/else form leave an unknown remainder because the payload restriction covers only part of a variant; report `SPIRE014`, not a complement proof. |
| `Error_PayloadPatternWithoutDiscriminator` | `{ move: var payload }` without `kind`/`IsMove` proof reads an unguarded accessor and does not establish a receiver guard; both the pattern member and a subsequent direct accessor report `SPIRE014`. |
| `Error_UnguardedSingletonUnionStillUnknown` | A one-variant union accessed without an explicit kind/predicate guard still reports `SPIRE014`; cardinality alone does not prove a receiver. |
| `Error_FactoryAssignedLocalNoImplicitGuard` | `var command = Command.Move(...); return command.move.speed;` retains `SPIRE014`; factory assignment does not create an implicit proof for an unguarded local. |
| `Error_FactoryReturnedValueNoImplicitGuard` | A helper returning `Command.Move(...)` and a caller accessing `GetCommand().move` reports `SPIRE014`; arbitrary call results are unstable receivers. |
| `Error_CompoundComparisonAfterMutation` | `(command.IsMove && Replace(ref command)) == true` can be true after replacing the source. Captured Boolean truth must not recreate the stale Move proof for the changed source. |
| `Error_OverwritingSavedPredicateInvalidatesCorrelation` | A saved predicate's own Boolean storage can be overwritten directly or through ref. Its previous receiver correlation no longer applies; assigning a fresh predicate establishes a new correlation. |
| `Error_LoopMutationDoesNotReviveCapturedPredicate` | A saved compound predicate cannot guard a mutated receiver later in the loop. Reusing static capture IDs on backedges must not revive stale source projections. |
| `Error_FreshDifferentGuardCannotReviveEarlierCompoundCheck` | In a wrapped conjunction, Move can be true before a ref mutation and Stop true afterward. A fresh Stop check must not revive the old Move check or mark the real true branch unreachable; a Move accessor reports SPIRE013. |
| `Error_BitwiseRightMutationInvalidatesEarlierOperand` | A right operand can mutate a receiver after the left predicate was evaluated. The true result of `left & right` cannot transfer that old predicate to the changed receiver; report SPIRE014. |

## Category B: Control flow, patterns, and scope boundaries

### should_pass

| File Name | Description |
|---|---|
| `Pass_IfElseComplement` | `if (kind == Move) move else stop` allows the matching accessor in each branch; the else edge is the complement set. |
| `Pass_EarlyReturnAndThrowComplement` | `if (kind != Move) return; move` and `if (kind != Move) throw ...; move` both establish `{Move}` after all terminating paths. |
| `Pass_ElseIfChainTracksEachBranch` | An `if/else if/else` chain with `Move`, `Stop`, and `Idle` checks allows each matching accessor in its own branch. |
| `Pass_TernaryTrueAndFalseKindGuards` | Both `kind == Move ? command.move.speed : 0` and a false-edge form that excludes `Move` are accepted when each accessor is evaluated only on its proven branch. |
| `Pass_SwitchStatementAndExpressionMatchingArms` | Matching `switch (command.kind)` statement cases and `command switch` expression arms allow their own accessors. |
| `Pass_PropertyPatternKindGuard` | `{ kind: Command.Kind.Move, move: { speed: var speed } }` establishes the matching receiver and nested payload access is safe. |
| `Pass_PayloadPositionalPattern` | The supported generated payload form `{ IsMove: true, move: var (destination, speed) }` permits matching captures; do not require the removed union-level `Deconstruct`. |
| `Pass_SwitchDefaultComplement` | After explicit `Move` and `Stop` cases in a three-variant switch, the `default` arm is the known `{Idle}` complement and may access `command.idle`. |
| `Pass_WhenGuardMatchingAccessor` | A `Move` switch arm with a `when` condition that reads `command.move` only reads the owning accessor and remains safe. |
| `Pass_DetachedPayloadAfterPropertyPattern` | After a guarded pattern assigns `var payload = command.move`, later `payload.speed` is ordinary payload code and is not analyzed as union access. |
| `Pass_WhileGuardBreakOrContinue` | In a loop, `if (kind != Move) break/continue; command.move` is safe on the path after the guard for each iteration. |
| `Pass_ForLoopGuardEachIteration` | A `for` loop rechecks `kind == Move` before the accessor on every iteration; backedges do not widen the proven state past the guard. |
| `Pass_LambdaOwnGuard` | A lambda that receives `Command command` and performs its own kind check may access the matching payload. |
| `Pass_LocalFunctionOwnGuard` | A local function with its own receiver parameter and kind check may access the matching payload. |
| `Pass_ExceptionHandlersOwnGuards` | A catch/finally body that performs its own discriminator check accepts its matching accessor, even when incoming exceptional state is conservative. |
| `Pass_ExceptionHandlersPreserveUnchangedLocal` | A by-value local guarded before a try remains proven in catch/finally when no possible operation can change it. Exceptional flow is not a reason to discard facts about unchanged local storage. |
| `Pass_FilterGuardEstablishesVariant` | A catch filter checking command.IsMove establishes Move for that handler body; route filter true/false flow through its CFG instead of treating the filter as an unrelated scope. |
| `Pass_SingleEvaluationPropertyPattern` | `holder.Current is { IsMove: true, move: var payload }` evaluates the property once; discriminator and payload reads within that single pattern are safe. A later separate property evaluation receives no transferred proof. |

### should_fail

| File Name | Description |
|---|---|
| `Error_PartialExitDoesNotEstablishComplement` | Only one path in `if (kind != Move) { if (flag) return; }` exits; the later `command.move` rejoins an unchecked/full-domain path and reports `SPIRE014`. |
| `Error_SiblingBranchGuardDoesNotReach` | A guard in one `if` branch does not prove the receiver in a sibling branch; access there reports `SPIRE014`. |
| `Error_TernaryUnrelatedCondition` | `ready ? command.move.speed : 0` has no discriminator proof and reports `SPIRE014` at `move`. |
| `Error_SwitchGroupedKindsWrongAccessor` | A grouped `Move`/`Stop` switch arm reading `command.move` reports `SPIRE013` because the possible set includes `Stop`. |
| `Error_SwitchDefaultWrongAccessor` | After explicit `Move` and `Stop` cases, a `default` arm reading `command.move` reports `SPIRE013` because the known complement is `{Idle}`. A default with no preceding domain proof remains an unknown-state control. |
| `Error_SwitchOrPatternWrongAccessor` | An OR-pattern covering `Move or Stop` that reads `command.move` reports `SPIRE013`. |
| `Error_WhenGuardWrongAccessor` | A `Move` arm whose `when` condition or body reads `command.stop` reports `SPIRE013`; a `when` clause does not change the arm's receiver identity. |
| `Error_LoopBodyPartialGuard` | A loop body that accesses `command.move` only under a condition on an unrelated flag reports `SPIRE014`. |
| `Error_LoopBackedgeAssignment` | A valid guard followed by a loop backedge that reassigns the receiver before the next access reports `SPIRE014` on the invalidated path. |
| `Error_DoWhileFirstIterationUnknown` | A `do` body accesses the union before its discriminator guard is reached in the `while` condition; report `SPIRE014`. |
| `Error_OuterGuardDoesNotEnterLambda` | A containing-method guard does not transfer into a nested lambda; an unguarded lambda access reports `SPIRE014`. |
| `Error_OuterGuardDoesNotEnterLocalFunction` | A containing-method guard does not transfer into a local function body; the local function must guard its own receiver. |
| `Error_AwaitInvalidatesProof` | A shared/instance field path guarded before `await` is accessed after suspension without a recheck; report `SPIRE014`. An unescaped by-value local/parameter has separate passing coverage. |
| `Error_YieldInvalidatesProof` | A shared/instance field path or mutable captured receiver guarded before `yield return` is accessed after resumption without a recheck; report `SPIRE014`. |
| `Error_FinallyHasNoIncomingProof` | Access in finally does not inherit a proof that held only on some try paths, or was invalidated by a try-body write; report `SPIRE014`. |
| `Error_CatchHasNoIncomingProof` | A ref-mutating call/assignment before an exception can invalidate the pre-try guard. The catch-body accessor requires a fresh proof and reports `SPIRE014`. |
| `Error_FinallyWriteInvalidatesContinuation` | A guard before `try` permits a read inside the try, but a reassignment in finally invalidates an accessor after normal completion; report SPIRE014 only after the finally. |
| `Error_SwitchSnapshotDoesNotGuardReassignedSource` | A switch captures a union, then its `when` guard reassigns the original through ref; the old captured kind must not guard a later accessor on the reassigned original. |
| `Error_PropertyPatternGetterMutatesOriginalStorage` | A union property getter used alongside `IsMove: true` replaces the shared source field while the pattern inspects its captured value. Pattern success must not guard an accessor on the replaced original field. Do not assume an evaluation order for property subpatterns. |
| `Error_ExceptionBeforeRefRebindKeepsOldBinding` | A possible exception before `alias = ref proven` reaches catch with the old alias binding. A later rebind in the same basic block must not retroactively guard the catch accessor through the already-proven other receiver. |
| `Error_FilteredCatchBodyIsAnalyzed` | An unguarded payload accessor in catch(Exception) when(flag) still reports SPIRE014. FilterAndHandler region nesting must not cause the handler body to be skipped. |
| `Error_FilterGuardWrongVariant` | A Move catch filter followed by a Stop accessor in its handler reports SPIRE013 with the filter's proven variant. |
| `Error_FalseFilterMutationReachesNextHandler` | A filter may change the receiver then return false or throw, continuing exception search. Subsequent handlers must see those possible writes rather than inheriting the pre-filter proof. |

## Category C: Receiver identity and stability

### should_pass

| File Name | Description |
|---|---|
| `Pass_SameParameterReceiver` | Guard and accessor use the same method-parameter symbol; the matching variant is safe. |
| `Pass_SameLocalSnapshotReceiver` | `var snapshot = command; if (snapshot.kind == Move) return snapshot.move;` tracks the local receiver independently. |
| `Pass_IdentityPreservingConversion` | A semantic identity conversion around the same receiver (`(Command)command`/equivalent identity conversion) preserves the receiver key. |
| `Pass_ThisFieldPathReceiver` | `this._command.kind` guards and `this._command.move` accesses the same stable instance-field path. |
| `Pass_NestedFieldPathReceiver` | `holder.State.Command.kind` guards and `holder.State.Command.move` accesses the same complete receiver path. |
| `Pass_StaticFieldPathReceiver` | A stable static field path is guarded and accessed through that same field symbol/path. |
| `Pass_GeneratedAccessorAndPayloadProjectionsStayPure` | `if (holder.command.IsMove) return holder.command.move.destination + holder.command.move.speed;` uses the same stable field path twice; generated kind/accessor/payload projections do not behave like arbitrary user getters. |
| `Pass_ByValueCallsPreserveStableLocal` | A guarded local passed by value to an unrelated method, or an unrelated receiver mutated, does not invalidate the local proof. |
| `Pass_CapturedSnapshotLocalStable` | A captured snapshot local that is never written inside the closure retains its own guard/access proof. |
| `Pass_LocalSnapshotSurvivesUnrelatedAwaitAndYield` | An unescaped by-value local/parameter is guarded, crosses an unrelated `await` or iterator `yield`, and is then accessed; it remains stable even though shared/field receivers are conservatively invalidated across suspension. |
| `Pass_RefMutatingCallArgumentEvaluation` | After `c.IsMove` proves `Move`, both `Use(ref c, c.move.speed)` and `Use(c.move.speed, ref c)` read the payload safely during argument evaluation. Ref/out invalidation begins after all arguments have been evaluated. |
| `Pass_FactoryAssignedThenExplicitGuard` | A local initialized from a generated factory is safe only after an explicit matching kind guard; the explicit proof, not the assignment, is tested. |
| `Pass_ReadonlyInParameterSameReceiver` | A readonly/in parameter guarded and accessed through the same stable receiver is accepted when no aliasing write occurs. |
| `Pass_VolatileFieldSnapshotLocalStable` | A volatile/reference field path is copied to an unescaped local before the guard; the local snapshot is stable and its matching accessor is accepted. |
| `Pass_DirectRefAliasOwnGuard` | A direct local ref alias with a stable binding may guard and read its own union; alias support must not reject all ref receivers. |
| `Pass_RecheckAfterMutationRestoresProof` | After an assignment or ref mutation loses the previous proof, a fresh discriminator check restores access safety. |
| `Pass_UnrelatedFieldWritePreservesProof` | Writing a demonstrably separate scalar field does not invalidate a guarded union field on the same object. |
| `Pass_PayloadBeforeNestedMutationArgument` | A guarded payload read in an earlier argument is safe even if a later argument invokes a ref mutator; evaluate accesses in C# order. |
| `Pass_SetterEvaluatesValueBeforeMutation` | A guarded field's payload may be read on the RHS of an assignment whose property setter subsequently replaces that field. Do not execute getter/setter effects while merely evaluating the assignment target. |
| `Pass_ReadonlyStructGetterPreservesUnescapedValue` | A readonly user getter cannot replace its own ordinary unescaped value receiver. Preserve that receiver's proof while retaining opaque effects on unrelated shared storage. |

### should_fail

| File Name | Description |
|---|---|
| `Error_DifferentParameterReceiver` | Guard `first.kind` but access `second.move`; report `SPIRE014` because receiver keys differ. |
| `Error_LocalAssignmentInvalidatesReceiver` | Reassign a guarded local before accessing its variant field; report `SPIRE014`. |
| `Error_BoxingConversionBreaksReceiverIdentity` | Guard a union local, pass it through `object` boxing/unboxing or an arbitrary conversion, then access the result; do not treat the new receiver as identity-preserving. |
| `Error_DifferentFieldPathReceiver` | Guard `holder.Left.Command` but access `holder.Right.Command.move`; report `SPIRE014` even if terminal field symbols match. |
| `Error_FieldAndAncestorWritesInvalidatePath` | Guard `holder.State.Command`, then assign the leaf and the ancestor on separate paths before access; each invalidated accessor location reports `SPIRE014` (two exact markers). |
| `Error_StaticFieldWriteInvalidatesPath` | Guard a mutable static union field, write it through another method/path, then access it; report `SPIRE014`. |
| `Error_RefArgumentInvalidatesReceiver` | A guarded receiver passed by `ref` to a helper invalidates the proof before a later accessor. |
| `Error_OutArgumentInvalidatesReceiver` | Passing a guarded receiver as `out` invalidates the prior state; later access reports `SPIRE014`. |
| `Error_OpaqueRefAliasInvalidatesReceiver` | Passing a receiver through an opaque `ref` alias or ref-returning helper invalidates the prior proof; later access reports `SPIRE014`. A by-value/in read-only call without writable escape is covered as safe. |
| `Error_MutationCapableStructCallInvalidatesPath` | Calling a mutating instance method on a struct receiver/path after the guard invalidates the proof; report `SPIRE014`. |
| `Error_CapturedWriteOrClosureInvocationInvalidatesReceiver` | A captured lambda writes the receiver, or a call that may invoke that closure occurs between guard and access; report `SPIRE014`. |
| `Error_AccessAfterRefMutatingCall` | A payload argument may be read during `Use(ref c, c.move.speed)`, but an accessor after that invocation sees the ref mutation and reports `SPIRE014` until rechecked. |
| `Error_AwaitOrYieldInvalidatesFieldPath` | A guarded field path crosses `await` or `yield`; shared state may replace the path, so later access reports `SPIRE014`. |
| `Error_PropertyGetterReceiverIsUnstable` | `holder.Current.kind` followed by `holder.Current.move` does not reuse a property getter result; report `SPIRE014`. |
| `Error_IndexerReceiverIsUnstable` | `holder.Commands[index].kind` followed by `holder.Commands[index].move` is unstable even with the same textual index; report `SPIRE014`. |
| `Error_MethodCallReceiverIsUnstable` | `GetCommand().kind` does not guard a later `GetCommand().move`; arbitrary calls produce unstable receivers. |
| `Error_ConditionalAccessReceiverIsUnstable` | A null-conditional/property/call receiver path is not treated as a stable field path; direct accessor reports `SPIRE014`. |
| `Error_UserDefinedConversionSideEffectBreaksIdentity` | A user-defined conversion around a guarded receiver may execute arbitrary code; do not strip it as an identity conversion, and report `SPIRE014` on the later accessor. |
| `Error_VolatileFieldPathIsUnstable` | Guarding `volatile HolderRoot.Current.Command.kind` does not make a later read of that volatile reference's `Current.Command.move` stable; report `SPIRE014` because the root/path can change between reads. |
| `Error_CapturedMutableLocalWriteInvalidates` | A local captured by a lambda is reassigned, or an opaque mutator is invoked with the captured local, between guard and accessor; report `SPIRE014` even if the write is lexically inside a separate closure body. |
| `Error_SharedFieldWriteThroughHelper` | A helper can write a shared/instance field path after a guard; the later field-path accessor reports `SPIRE014`. |
| `Error_RefAliasToFieldInvalidatesPath` | A field-path receiver is changed through a ref alias, or passed through an opaque ref-returning call before access; the prior proof is invalid. Mere creation of a direct alias does not itself mutate storage. |
| `Error_AncestorReplacementInvalidatesNestedPath` | Guard `holder.Inner.Command`, replace `holder.Inner`, then access `holder.Inner.Command.move`; report `SPIRE014` because the full path changed. |
| `Error_ShortCircuitInterveningMutation` | In `c.IsMove && Replace(ref c) && c.move.speed > 0`, the invocation invalidates the first operand's proof before the final RHS accessor. |
| `Error_DirectRefAliasWriteInvalidatesOriginal` | After guarding a local, assigning a new union through its direct ref alias invalidates access on the original local. |
| `Error_HeapAliasWriteInvalidatesOtherPath` | After guarding `first.command`, a write to `second.command` may alias that field through a different holder reference; the later first-path accessor must not retain a stale proof. |
| `Error_NestedMutationArgumentBeforePayload` | A nested ref-mutating invocation in an earlier argument invalidates the proof before a payload read in a later argument. |
| `Error_DeconstructionAssignmentInvalidatesReceiver` | A tuple/deconstruction assignment that replaces the guarded local invalidates a later payload access, just like a simple assignment. |
| `Error_SetterMutationInvalidatesLaterAccess` | A property setter may replace a guarded union field after its RHS is evaluated; a payload read after that assignment requires a fresh proof. |
| `Error_FinallyRefRebindJoinsAlternatives` | Mutually exclusive ref rebindings in finally join their alternatives. A lexical walk ending at the last assignment must not give the continuation a false definite binding to the proven receiver. |
| `Error_AmbiguousRefAliasWriteInvalidatesPossibleTarget` | After a conditional rebind chooses first or second, guard first and then assign through the alias. First remains a possible target even though no definite alias binding survives the join; its later payload access reports SPIRE014. |
| `Error_AmbiguousRefAliasCallInvalidatesPossibleTarget` | The same ambiguous ref alias passed to an opaque ref mutator can change first; losing a definite binding must not turn that write into an unrelated local write. |
| `Error_AmbiguousRefAliasInstanceMutationInvalidatesPossibleTarget` | A mutation-capable struct instance call on the ambiguous alias can replace first. Apply the same may-target invalidation used for assignments and ref/out calls. |
| `Error_RefParametersMayAlias` | Two ref parameters can point to the same caller cell. Assigning through second invalidates first's proof when first and second may alias. |
| `Error_InParameterCallbackMayMutateCallerStorage` | An in parameter is a readonly view of caller-owned storage, not a frozen value copy. An opaque callback can mutate the caller's cell and invalidate that proof. |
| `Error_MutatingStructGetterInvalidatesReceiver` | A non-readonly user getter can assign this and change the union tag. Its receiver-write effect invalidates a guarded local, just like a mutation-capable instance method. |

### Additional alias and address-exposure controls

| Test | Contract |
|---|---|
| `Error_AmbiguousRefSelectionWriteInvalidatesGuardedAlias` | A guard on an ambiguous alias is invalidated by a write to any of its possible targets. |
| `Error_OpaqueRefReturnAliasWriteInvalidatesOriginal` | An opaque ref return can point to a local passed by ref. A later write through that alias invalidates the original's proof. |
| `Error_RefParameterAndStaticFieldMayAlias` | A ref parameter may refer to a static field; writes invalidate proofs in both directions. |
| `Pass_AmbiguousRefSelectionOwnGuardRemainsUsable` | Ambiguous target identity does not prevent guarding and reading the alias itself without intervening mutation. |
| `Pass_UnescapedValueSurvivesUnrelatedRefTargetWrite` | A value snapshot never exposed by address/ref retains its proof across a write through an unrelated opaque ref return. |
| `Error_AddressExposedLocalInvalidatedByPointerWrite` | Taking a local's address makes it a possible target of an indirect pointer write. Such a write invalidates its proof. |
| `Pass_AddressTakenLocalWithoutPostGuardMutationRemainsProven` | Taking an address before a fresh guard does not prevent an immediate guarded read without intervening effects. |
| `Error_IndirectWriteDoesNotForgetRefAliasBinding` | An indirect value write invalidates affected proofs but does not rebind existing managed ref locals. A later write through such an alias still invalidates its original target. |
| `Error_PointerValueCallInvalidatesAddressExposedReceiver` | A pointer passed by value can still write its address-exposed target through an opaque call. |
| `Error_PointerElementWriteInvalidatesAddressExposedReceiver` | Pointer indexing writes invalidate exposed storage just like indirect `*pointer` writes. |
| `Pass_BuiltinReferenceConversionsPreserveReceiverField` | Built-in reference-preserving casts on a holder retain its field storage identity. Boxing, unboxing, and user conversions remain opaque. |

## Category D: complete user-code effect barriers

These audit regressions use the same real generator/compile/emit helper. Calls
through constructors, operators, dynamic binding, event accessors and nested
callables must use the same mutation model as ordinary invocations. Each
negative case marks the exact stale payload access with SPIRE014.

| Test | Contract |
|---|---|
| `Error_LocalFunctionMutatesCapturedUnion` | A local function assigning the captured receiver invalidates the caller's proof on invocation. |
| `Error_DelegateMutatingInstanceInvalidatesCapture` | A delegate calling captured command.Reset() can replace the captured value; scanning only direct assignments misses the write. |
| `Error_UnknownDelegateReassignmentClearsOldEffects` | Reassigning a delegate from a local factory returning a different writer must not retain the previous delegate's smaller write summary. |
| `Error_DelegateJoinUnionsPossibleWrites` | Alternative delegates writing first or second join by union of possible writes; both later accesses require fresh proofs. |
| `Error_EscapedCallbackCanRunInOpaqueCall` | A callback stored through an earlier opaque registration call can be invoked by a later argumentless call, invalidating its writable captured receiver. |
| `Error_ConstructorRefMutationInvalidatesReceiver` | A constructor with a ref argument can replace the receiver after argument evaluation. A guarded payload argument is safe; the later payload access is not. |
| `Error_ConstructorCallbackMayMutateCapture` | A constructor may invoke its delegate argument and change an ordinary captured local. |
| `Error_DynamicRefInvocationInvalidatesReceiver` | A dynamic invocation with a ref argument has the same write capability as an ordinary ref call. |
| `Error_UserOperatorsInvalidateSharedStorage` | User binary and unary operators may change shared union fields; later payload access loses its proof. Built-in arithmetic stays pure. |
| `Error_CustomEventAccessorsMutateValueReceiver` | User event add/remove accessors on a mutable union can assign this and invalidate the receiver's proof. |
| `Error_StructSetterMutationInvalidatesReceiver` | A union-local setter can assign this after a guarded payload RHS is evaluated. Diagnose only the later access. |
| `Error_CapturedDeconstructionWriteInvalidatesReceiver` | A nested callable's tuple/deconstruction assignment can replace a captured union, not just simple assignment. |
| `Error_DynamicMemberAccessInvalidatesSharedStorage` | A dynamic member getter can execute arbitrary user code and replace shared storage without an explicit invocation node. |
| `Pass_UnknownCallbackCannotReachUnescapedValue` | An unknown callback copied/reassigned from an external delegate cannot mutate an uncaptured value local/parameter whose address was never exposed. Preserve that receiver's proof. |
| `Error_FunctionPointerRefCallInvalidatesReceiver` | Managed function-pointer calls apply the same ref mutation effects as ordinary calls, after all arguments are evaluated. Accept a guarded payload argument and diagnose a later stale read. |
| `Error_SetterCallbackCanRunImmediately` | A user setter can invoke a delegate supplied as its value and mutate that delegate's captured receiver. |
| `Error_EventCallbackCanRunImmediately` | A custom event accessor can invoke the supplied handler while adding it. |
| `Error_StoredCallbackGetterCanMutateCapture` | A getter can run a callback that escaped through an earlier store before a fresh guard. |
| `Error_StoredCallbackSetterCanMutateCapture` | A setter can run an already stored callback even when the current value is not a delegate. |
| `Error_StoredCallbackEventCanMutateCapture` | An event accessor can run an already stored callback independently of the supplied handler. |
| `Error_ClosureConstructorRefMutationInvalidatesCapture` | Closure summaries include ref writes performed by nested constructors. |
| `Error_ClosureDynamicRefMutationInvalidatesCapture` | Closure summaries include ref writes performed by nested dynamic calls. |
| `Error_ClosureFunctionPointerRefMutationInvalidatesCapture` | Closure summaries include ref writes performed by nested function-pointer calls. |
| `Error_LocalFunctionWrapperMutatesCapture` | Capturing local-function wrappers propagate possible writes transitively; cyclic wrappers reach a finite summary fixed point. |
| `Error_CyclicLocalFunctionSummaryIsIndependentOfFirstCall` | A non-writing first call must not prime an incomplete cached summary that hides a later write through a cycle. |
| `Error_ConstructorPointerArgumentInvalidatesReceiver` | Constructors receive pointer values with the same write capabilities as ordinary opaque calls. Preserve an unrelated unescaped value control. |
| `Error_ConstructorRunsPreviouslyEscapedCallback` | A constructor can run a callback stored before the guard, without receiving that callback as an argument. |
| `Error_FunctionPointerRunsPreviouslyEscapedCallback` | A function-pointer target can invoke a previously stored callback; preserve an unrelated unescaped value control. |
| `Error_OuterClosureComposesNestedCallbackAccessorEffects` | An outer closure can register a nested writer and then invoke it through an accessor. Its summary must compose registration with invocation. |
| `Pass_DeclaringNestedWriterWithoutInvokingOrEscapingDoesNotMutateCapture` | An invoked outer closure that merely declares an unused inner writer does not execute that writer. |
| `Error_ClosureRefAliasWriteInvalidatesCapture` | A closure's local ref alias can write captured storage; merely binding the alias and reading its generated kind stays pure. |
| `Error_UserDeconstructionExecutesOpaqueEffects` | User-defined deconstruction assignments and positional patterns can execute arbitrary code. Pure built-in tuples and generated payload deconstruction preserve unrelated receiver proofs. |
| `Error_UserPositionalDeconstructMutatesReceiver` | A mutable user Deconstruct in a positional pattern can change the original local receiver. Ordinary deconstruction assignment copies that struct and preserves its original guard. |
| `Error_UserOperatorsAndConversionsTransferCallbacks` | User-defined conversions and binary/unary operators can invoke previously stored callbacks or delegate-valued operands, directly or inside a closure. |
| `Error_DelegateHeapStoresEscapeCallbacks` | Delegate stores into fields, array slots, and aliases of shared storage register escaped effects; a plain store does not execute the callback. |
| `Error_ClosurePointerIndirectionWritesExposedStorage` | Closure pointer indirection/indexing writes invalidate exposed storage while preserving an unrelated unexposed value. |

## Existing controls and policy reconciliation

The implementation must retain the current generator-consumer controls while
moving their guard lookup onto CFG state:

- `FieldAccess_DetachedPayloadIsAnOrdinaryStruct` remains a no-diagnostic
  control; payload locals do not require union guards.
- `FieldAccess_MutationInvalidatesTheEarlierGuard`,
  `FieldAccess_ReassignmentAfterEarlyExitInvalidatesProof`,
  `FieldAccess_SwitchGuardDoesNotTransferToAnotherInstance`,
  `FieldAccess_SwitchExpressionGuardDoesNotTransferToAnotherInstance`, and
  `FieldAccess_ClosureReturnDoesNotExitTheContainingMethod` remain required
  invalidation/scope controls.
- Existing matching switch/property/positional patterns, generic closed
  accessors, acronym/factory-conflict accessors, readonly unions, and direct
  factory accesses remain covered by their current tests and must continue to
  compile without SPIRE013/SPIRE014.
- Update the existing `Warn_ElseBranch` expectation from `SPIRE014` to
  `SPIRE013`: under true/false CFG refinement, an `else` after `kind == Move`
  has the known complement set, and accessing `move` there is a guarded wrong
  variant (the possible set contains `Stop`).
- The former exploratory OR-pattern and `when` cases are resolved here: an
  OR-pattern yields a union possible set and a wrong owner is `SPIRE013`; a
  `when` clause retains the arm's discriminator proof but does not make a wrong
  accessor valid.
- Record/class subtype property access remains outside SPIRE013/SPIRE014's
  union-accessor contract. Keep record controls in `RecordUnionTests`; do not
  invent base-union field diagnostics for subtype properties.

## Runner and assertion requirements

Case files should use the existing generator analyzer test runner where a single
marked location is sufficient. Use focused `VariantPayloadAnalyzerTests`
helpers only for cases requiring multiple diagnostics, exact ordered possible-set
message text, or explicit CFG/receiver identity assertions. Every failing case
must assert the exact ID at the marked accessor; multi-access cases must assert
both IDs/locations. Every passing case must reject both SPIRE013 and SPIRE014,
not merely the diagnostic under discussion. Run ordinary CoreCLR generator
consumer tests and Unity-hosted structural tests through their existing lanes;
the flow rule itself must not depend on Unity metadata or native serialization.
