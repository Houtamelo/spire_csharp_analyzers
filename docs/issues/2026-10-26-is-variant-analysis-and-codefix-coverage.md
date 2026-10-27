# Generated boolean variant analysis and codefix coverage

Request recorded before implementation: support generated `Is{Variant}` predicates
as equivalent discriminator constraints, and make `Add missing variant arms`
generate `{ IsVariant: true, payload: var (...) }` for struct unions. Existing
`kind` patterns remain supported. Do not add an inlining hint or change generator
output. The nullable-payload entry in PENDING.md remains deferred.

The approved analyzer scope includes direct boolean checks, negation, comparisons
with true/false, and property patterns, including negative predicates. Recognition
must identify actual generated predicate properties and the guarded receiver.
Negative checks exclude the named variant. Preserve existing mutation/closure
boundaries, diagnostics, severity, and generated payload naming.

Implementation and validation complete. Integration coverage exercises generated
unions, actual diagnostics, applied codefix compilation, post-fix exhaustiveness,
and both successful and rejected guards. The full SDK suite passes **2,417 tests**
with zero failures/skips; all 77 generated-source controls remain byte-identical.

Integration coverage must
exercise generated unions, actual diagnostics, applied codefix compilation,
post-fix exhaustiveness, and both successful and rejected guards. Existing arms,
record/class patterns, payload deconstruction, indentation, and line endings must
remain intact.

## Coverage matrix

This matrix is the implementation contract. It intentionally separates analyzer
semantics from codefix formatting. The tests should use the existing real generator
consumer harness (`VariantPayloadAnalyzerTests` and
`VariantPayloadCodeFixTests`) rather than hand-authored stand-ins. Analyzer tests
must run the generator first, then assert only the relevant SPIRE diagnostics;
codefix tests must apply the action, compare the complete source, compile and emit
the fixed source, then rerun exhaustiveness and field-access analysis.

### Category A: field-access safety — direct boolean guards

Generated `Is{Variant}` properties are boolean projections of `kind`. The analyzer
must associate a guard with the same union receiver and preserve the current
SPIRE013/SPIRE014 severity and source location behavior.

| Case | Expected result |
|---|---|
| `FieldAccess_IsTrueMatchingAccessor` | `if (command.IsMove) return command.move.speed;` has no SPIRE013/014. |
| `FieldAccess_IsTrueWrongAccessor` | The same guard followed by `command.stop` reports SPIRE013. |
| `FieldAccess_IsFalseEarlyExitThenMatchingAccessor` | `if (!command.IsMove) return 0; return command.move.speed;` has no SPIRE013/014. The negative check excludes Move and the surviving path is Move. |
| `FieldAccess_IsFalseWrongAccessor` | `if (!command.IsMove) return 0; return command.stop.speed;` reports SPIRE013 because the surviving path is statically known to be Move. |
| `FieldAccess_IsEqualsTrue` | `command.IsMove == true` proves Move. |
| `FieldAccess_IsNotEqualsFalse` | `command.IsMove != false` proves Move. |
| `FieldAccess_IsEqualsFalseEarlyExit` | `command.IsMove == false` excludes Move; an early exit followed by `command.move` is valid. |
| `FieldAccess_IsNotEqualsTrueEarlyExit` | `command.IsMove != true` excludes Move; an early exit followed by `command.move` is valid. |
| `FieldAccess_IsUnknownUnguarded` | Direct `command.move.speed` still reports SPIRE014. Adding generated predicates must not weaken unguarded access. |
| `FieldAccess_IsPropertyPatternMatchingAccessor` | A switch arm `{ IsMove: true, move: var move }` permits the `move` payload read without SPIRE013/014. |
| `FieldAccess_IsPropertyPatternWrongAccessor` | A switch arm `{ IsMove: true, stop: var stop }` reports SPIRE013. |

### Category B: field-access safety — control-flow and receiver boundaries

The predicate must follow the same flow rules as `kind` guards: mutation invalidates
the proof, a guard on another receiver does not transfer, and closures do not
terminate the containing method.

| Case | Expected result |
|---|---|
| `FieldAccess_IsGuardInvalidatedByAssignment` | Assigning `command = other` after `if (command.IsMove)` causes the later `command.move` read to report SPIRE014. |
| `FieldAccess_IsGuardOnOtherReceiver` | `if (first.IsMove) return second.move.speed;` reports SPIRE014. |
| Instance field receivers | `first.command.IsMove` must not guard `second.command.move`; the same holder and `this.command` remain valid guards. |
| Changing property getters | Repeated reads of a property or a property in a field's receiver chain cannot establish stable variable identity. |
| Restricted-pattern complements | Failing `{ IsMove: true, move: { speed: 0 } }` does not exclude Move; both `else` and a following early-exit path retain SPIRE014. |
| `FieldAccess_IsGuardedLambda` | A lambda that checks `value.IsMove` before reading `value.move` is valid. |
| `FieldAccess_IsUnguardedLambda` | A lambda that reads `value.move` without a predicate reports SPIRE014. |
| `FieldAccess_IsGuardedLocalFunction` | A local function parameter guarded by `value.IsMove` is valid. |
| `FieldAccess_IsGuardedAsyncMethod` | An async method with a direct predicate guard preserves the same result; the `await` must not cause a false positive or false negative in the guarded block. |
| `FieldAccess_IsGuardedTernary` | `command.IsMove ? command.move.speed : 0f` is valid. |
| `FieldAccess_IsGuardedMethodArgument` | Passing `command.move` to a method inside the true branch is valid; passing it before the guard reports SPIRE014. |
| `FieldAccess_IsCustomPredicateNameNotVariant` | A generated union with no `Foo` variant and an unrelated `IsFoo` boolean member does not establish a variant guard. |
| `FieldAccess_RecordControlUnaffected` | Record/class union behavior remains unchanged; a user-defined `IsMove` property on a record variant is not interpreted as the struct discriminator. |

The implementation should identify the actual generated property symbol by the
union and variant metadata, not merely any boolean property whose text happens to
match. A duplicate user declaration with the generated member name is a normal
C# conflict; the test suite should not rely on an invalid duplicate declaration.

### Category C: exhaustiveness — positive and negative boolean property patterns

These cases exercise `ExhaustivenessAnalyzer`, `CS8509Suppressor`, and the pattern
matrix together. Use both switch expressions and switch statements where the
existing harness supports them.

| Case | Expected result |
|---|---|
| `Exhaustiveness_AllVariantsUseIsTrue` | Every struct-union variant is covered by `{ IsVariant: true, ... }`; no SPIRE009 and no unsuppressed CS8509. |
| `Exhaustiveness_MissingIsTrueVariant` | Omitting one positive predicate arm reports SPIRE009 naming the missing variant. |
| `Exhaustiveness_TwoVariantsUseIsFalseAndIsTrue` | For a two-variant union, `{ IsMove: false }` plus `{ IsMove: true, move: var (...) }` is exhaustive. |
| `Exhaustiveness_ThreeVariantsNegativePredicateComplements` | `{ IsMove: false }` covers the two non-Move variants, and a Move arm completes coverage. |
| `Exhaustiveness_OnlyNegativePredicateIsIncomplete` | `{ IsMove: false }` alone leaves Move uncovered and reports SPIRE009. |
| `Exhaustiveness_IsEqualsTruePattern` | A boolean property pattern using `IsMove: true` is recognized as Move. |
| `Exhaustiveness_IsEqualsFalsePattern` | A boolean property pattern using `IsMove: false` contributes the complement of Move. |
| `Exhaustiveness_IsAndKindMixed` | Mixing `{ IsMove: true }` with existing `{ kind: ... }` arms has no duplicate or lost coverage. |
| `Exhaustiveness_IsPayloadDeconstruction` | Positive `IsVariant` arms with `payload: var (...)` retain payload capture ordering and remain exhaustive. |
| `Exhaustiveness_IsRestrictedPayloadRemainsIncomplete` | A positive Is predicate with a guarded/restricted payload pattern does not falsely cover the entire variant. |
| Nested union predicate path | A predicate reached through `move.box.command.IsMove` constrains that nested union, not the root union. |
| `Exhaustiveness_GenericUnionUsesIsPredicate` | A closed generic struct union recognizes `IsSome: true` and reports missing variants correctly. |
| `Exhaustiveness_RecordControlRemainsKindIndependent` | Record/class union switches remain governed by record type patterns; no Is predicate interpretation is added there. |

The negative-property policy is deliberately set-based: `IsMove: false` excludes
Move and does not claim which remaining variant is active. A complete switch may
combine that complement with one or more positive arms, while a field read after a
negative guard must still be rejected unless control flow narrows the remaining set
to a single owner.

### Category D: Add missing variant arms — positive Is-pattern output

`AddMissingArmsCodeFix` must request a separate builder mode from
`StructPropertyPatternBuilder`. The existing `ExpandWildcardRefactoring` call path
must continue emitting `kind: Union.Kind.Variant`; sharing the builder does not
mean sharing this output choice.

| Case | Expected result |
|---|---|
| `AddMissingArms_Is_FieldedExpression` | A missing fielded arm is `{ IsMove: true, move: var (destination, speed) }` with existing inline-brace formatting. |
| `AddMissingArms_Is_FieldedStatement` | A switch statement gets `case { IsMove: true, move: var (...) }:` with correct body indentation. |
| `AddMissingArms_Is_FieldlessVariant` | A fieldless missing arm is `{ IsIdle: true }` with no payload subpattern. |
| `AddMissingArms_Is_MultiFieldOrdering` | Capture names follow factory parameter order, including three or more fields. |
| `AddMissingArms_Is_GenericUnion` | Closed generic consumers use `IsSome: true` and compile through generation. |
| `AddMissingArms_Is_KeywordVariant` | Keyword-escaped variant names produce the actual generated predicate name and a compilable accessor pattern. |
| `AddMissingArms_Is_AcronymVariant` | Acronym casing matches the generated `Is{VariantName}` member exactly. |
| `AddMissingArms_Is_FactoryConflictAccessor` | The discriminator is `IsRun: true` while the payload accessor remains `run_value`. |
| `AddMissingArms_Is_UnderscoreParameter` | `_` capture names use the existing `_1` uniqueness rule inside the Is arm. |
| `AddMissingArms_Is_LocalCaptureCollision` | Existing locals/parameters are respected and generated captures are suffixed deterministically. |
| `AddMissingArms_Is_AllVariantsMissing` | An empty switch receives all positive Is arms in declaration order. |
| `AddMissingArms_Is_ExistingUserArmPreserved` | Existing user-authored kind/property arms remain byte-for-byte/structurally unchanged; only generated arms use Is predicates. |
| `AddMissingArms_Is_IndentationAndLineEndings` | Existing indentation, tabs, CRLF, trailing comments, and one-line property-clause formatting remain unchanged. |
| `AddMissingArms_Is_ExpressionAndStatementBodies` | Both insertion contexts compile and preserve the existing throw/body style. |

For every applied AddMissing case, the test must assert all of the following:

1. The changed source contains the expected `Is...: true` arms and no newly
   inserted `kind:` discriminator for those arms.
2. The complete changed source compiles through the real source generator.
3. Re-running exhaustiveness reports neither SPIRE009 nor unsuppressed CS8509.
4. Re-running field-access analysis reports neither SPIRE013 nor SPIRE014 for the
   generated payload captures.

### Category E: Expand wildcard and compatibility controls

These tests protect the explicit scope boundary and prevent a shared-builder
refactor from changing unrelated behavior.

| Case | Expected result |
|---|---|
| `ExpandWildcard_StructUnionStillUsesKind` | Existing struct wildcard expansion continues to emit `{ kind: Union.Kind.Variant, ... }`. |
| `ExpandWildcard_StructFieldlessStillUsesKind` | Fieldless wildcard arms remain kind-only. |
| `ExpandWildcard_RecordUnionUnchanged` | Record wildcard expansion remains type-pattern based. |
| `AddMissingArms_RecordUnionUnchanged` | AddMissing for record/class unions remains type-pattern based; no generated Is property is assumed. |
| `ExistingKindArmsRemainAnalyzable` | User-written kind patterns continue to satisfy exhaustiveness and field-access analysis after Is support is added. |
| `ExistingDeconstructionOutputRemainsReadable` | The earlier `var (...)` payload capture shape remains intact for generated arms; only the discriminator property changes. |

### Recommended writer split

The implementation can be covered by three focused test batches:

| Batch | Primary files/areas | Scope |
|---|---|---|
| Analyzer guard batch | `VariantPayloadAnalyzerTests.cs` or a new `IsVariantAnalyzerTests.cs` | Categories A and B: direct guards, boolean operators, receiver/flow boundaries, and actual SPIRE013/014 diagnostics. |
| Exhaustiveness batch | `VariantPayloadAnalyzerTests.cs` or a new `IsVariantExhaustivenessTests.cs` | Category C: positive/negative property patterns, incomplete switches, generic unions, mixed kind/Is arms, and compiler-warning suppression. |
| Codefix/regression batch | `VariantPayloadCodeFixTests.cs`, `ExpandWildcardRefactoringTests.cs`, and fixture pairs under `VariantPayloadConsumers/cases` / `ReviewRegressions/cases` | Categories D and E: applied output, compile/reanalysis gates, formatting, collision cases, and the unchanged wildcard/record controls. |

The existing `VariantPayloadCodeFixTests` already has real source application,
generator compilation, exhaustiveness, indentation, CRLF, tabs, and collision
helpers. Extend those controls instead of creating a second mock workspace. The
existing `ExpandWildcardRefactoringTests` is the compatibility control for the
shared builder path.

## Validation evidence

- The initial guard tests produced 17 expected failures out of 21, and the
  initial exhaustiveness tests produced 10 expected failures out of 12.
- The codefix red run captured the old `kind` output against the new `Is` fixtures.
- Final focused integration coverage passed **156/156**, including 35 predicate
  guard tests, 16 predicate exhaustiveness tests, 53 payload/codefix tests, and
  52 existing compatibility controls. PatternAnalysis separately passed 428/428.
- The full Release SDK gate passed **2,417/2,417** tests across eight projects,
  without failures or skips, in an isolated checkout with 2,051 input files
  hash-matched to the live source/test/tool scopes. Existing user edits in the
  showcase were preserved outside that checkout.
- All 77 generated source controls are byte-identical to the accepted baseline.
  The generator and generated runtime API were not modified.
- Independent review found no remaining correctness, convention, or material
  coverage issues. It checked conservative complements, receiver identity,
  nested predicate paths, and unchanged record/kind/wildcard handling.
- The local package used by UnityShowcase was rebuilt from the validated Release
  outputs. Package versions remain 4.7.6; no publishing or Unity player run was
  required for this analyzer/codefix change.

Raw TRX files, the full-suite log/summary, input hashes, and package verification
are retained under `tmp/is-variant-arms-20261026/`.
