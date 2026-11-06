# SPIRE015 enum-switch code-fix coverage matrix

The code fix is provided by `AddMissingArmsCodeFix` and is
named **Add missing enum members**. It consumes the `MissingMembers` diagnostic
property emitted by `SPIRE015ExhaustiveEnumSwitchAnalyzer` and appends one arm
or case section for every reported member. Generated bodies are deliberately
compilable placeholders:

```csharp
throw new System.NotImplementedException();
```

The fix never adds a catch-all. For a switch statement it adds `case
Type.Member:` sections; for a switch expression it adds `Type.Member =>`
arms. A reported nullable value is emitted as `case null:` / `null =>`.

Snapshot cases below belong in `CodeFix/cases/{case-name}/before.cs` and
`after.cs`. The existing snapshot runner compares normalized syntax trees;
the integration checks in Category F are required for behavior that the
runner cannot observe (diagnostic properties, code-action absence, comments,
and fix-all).

## Category A: basic switch statement and expression forms

| Case name | Before / after contract |
|-----------|-------------------------|
| `SPIRE015AddMissingEnumMembers_SwitchStatement_MixedBodies` | Before: the supplied `State` switch, with `Idle` and `Moving` sections (including a block-bodied section), omits `Attacking`. After: append a separate `case State.Attacking:` section whose only statement throws `System.NotImplementedException`; do not put it inside the preceding block. |
| `SPIRE015AddMissingEnumMembers_SwitchStatement_AllMembersMissing` | Before: an empty switch statement on a three-member enforced enum. After: append all three member sections in enum declaration order, each throwing the placeholder exception. |
| `SPIRE015AddMissingEnumMembers_SwitchExpression_OneMemberMissing` | Before: `Color.Red` and `Color.Green` arms only. After: append `Color.Blue => throw new System.NotImplementedException()`. |
| `SPIRE015AddMissingEnumMembers_SwitchExpression_MultipleMembersMissing` | Before: only the first member arm. After: append every missing arm in the order listed by the diagnostic (`Green`, then `Blue`), with no reordering or deduplication beyond the analyzer’s list. |
| `SPIRE015AddMissingEnumMembers_ExpressionBodiedMember` | Before: an expression-bodied method or property whose switch expression is incomplete. After: only that switch expression receives the missing arm; its surrounding expression-bodied syntax remains intact. |
| `SPIRE015AddMissingEnumMembers_SwitchStatement_CommentsAndIndentation` | Before: a statement switch with comments between sections and non-default indentation. After: the new section is structurally separate, uses the surrounding indentation/line ending, and existing comments remain attached to their original sections. A text-level assertion is needed because the snapshot runner normalizes trivia. |

## Category B: nullable, alias, flags, and escaped member names

| Case name | Before / after contract |
|-----------|-------------------------|
| `SPIRE015AddMissingEnumMembers_NullableEnum_MissingNull` | Before: all named `Color` members are covered in a `Color?` switch, but no null arm exists. After: append `case null:` or `null => throw ...`; do not add a duplicate enum-member arm. |
| `SPIRE015AddMissingEnumMembers_NullableEnum_MembersAndNullMissing` | Before: a `Color?` switch covers only `Red`. After: append `Green`, `Blue`, and `null` in diagnostic order, each with the placeholder body. |
| `SPIRE015AddMissingEnumMembers_AliasedEnum_RepresentativeOnly` | Before: an enum has `First = 0`, `AlsoFirst = 0`, and `Second = 1`; the switch covers `Second` only. After: add exactly `AliasedEnum.First` (the analyzer’s first declared representative for the zero-valued group), never a redundant `AlsoFirst` arm. |
| `SPIRE015AddMissingEnumMembers_FlagsEnum_MissingCompositeComponent` | Before: a `[Flags]` enum handles `None`, `Read`, `Write`, and `ReadWrite`, omitting `Execute`. After: add only `Permission.Execute`; components already covered through the composite value must not be added again. |
| `SPIRE015AddMissingEnumMembers_FlagsEnum_MultipleMembersMissing` | Before: a flags switch handles only `Read`. After: add the distinct missing named values in the analyzer’s declaration order: `Write`, `ReadWrite`, and `Execute`. `None` is already covered because it is the zero-bit subset of `Read`; do not add it redundantly. |
| `SPIRE015AddMissingEnumMembers_EnumMember_AtNull` | Before: `[EnforceExhaustiveness] enum Token { @null = 0, Done = 1 }` with only `Token.Done` handled. After: add `case Token.@null:` / `Token.@null => ...`, not a literal `null` pattern. This is a required semantic-identifier test because the diagnostic text is the same spelling as nullable null coverage. |

## Category C: symbol qualification and nesting

| Case name | Before / after contract |
|-----------|-------------------------|
| `SPIRE015AddMissingEnumMembers_NestedEnum` | Before: an enforced enum nested in an outer type and switched from a nested/peer consumer. After: the generated member access binds to the nested enum (`State.Attacking` or the minimally qualified equivalent) and compiles without resolving to an unrelated `State`. |
| `SPIRE015AddMissingEnumMembers_QualifiedEnum` | Before: the switch uses an enum imported from another namespace with an existing qualified member arm. After: append the same qualified enum type/member form (`Game.Model.State.Attacking`) and preserve the source’s binding. |
| `SPIRE015AddMissingEnumMembers_GenericContainingType` | Before: `Container<T>.State` is switched inside or outside the generic containing type with one member missing. After: append the member through the constructed/nested enum type so the result compiles for the generic context. |

## Category D: guarded patterns and context boundaries

| Case name | Before / after contract |
|-----------|-------------------------|
| `SPIRE015AddMissingEnumMembers_GuardedMember` | Before: a member arm exists only with a `when` guard, so SPIRE015 reports that member as uncovered. After: add an unguarded explicit member arm; retain the guarded arm and its body. |
| `SPIRE015AddMissingEnumMembers_GuardedCatchAll` | Before: a discard arm has a `when` guard and explicit members are incomplete. After: add the missing explicit members; do not treat the guarded discard as an opt-out and do not add an unguarded discard. |
| `SPIRE015AddMissingEnumMembers_LocalFunctionOrLambda` | Before: an incomplete switch is inside a local function or lambda. After: locate and update that switch only, preserving the containing function/lambda body. |
| `SPIRE015AddMissingEnumMembers_NestedSwitch` | Before: two nested incomplete switches exist; the selected diagnostic targets the inner switch. After: only the inner switch changes in the single-diagnostic snapshot. |

## Category E: no-action and diagnostic-safety checks

These are real AdhocWorkspace assertions rather than ordinary before/after
snapshots, because the snapshot runner requires a fixable diagnostic.

| Check | Required result |
|-------|-----------------|
| `SPIRE015_NoAction_UnguardedCatchAll` | An enforced enum switch containing `default:`, `_ =>`, or an unguarded `var` pattern produces no SPIRE015 diagnostic and therefore no code action. |
| `SPIRE015_NoAction_UnmarkedEnumWithoutGlobalPolicy` | An incomplete switch on an unmarked enum produces no SPIRE015 diagnostic and no action. |
| `SPIRE015_NoAction_UnrelatedDiagnostic` | Calling `RegisterCodeFixesAsync` with a diagnostic whose ID is not SPIRE015 registers no actions; the provider must not claim SPIRE009, compiler exhaustiveness, or arbitrary diagnostics. |
| `SPIRE015_NoAction_MissingDiagnosticProperty` | A SPIRE015 diagnostic without `MissingMembers` registers no action rather than throwing or changing the document. |

## Category F: integration and fix-all checks

Use the real `SPIRE015ExhaustiveEnumSwitchAnalyzer`, `AdhocWorkspace`, and
`SPIRE015AddMissingEnumMembersCodeFix`. These checks should compile the changed
document after applying the action so invalid member qualification, duplicate
aliases, and `@null`/null confusion are caught.

| Check | Required result |
|-------|-----------------|
| `SPIRE015_GlobalEnforcement_CodeFixesUnmarkedEnum` | With analyzer option `build_property.Spire_EnforceExhaustivenessOnAllEnumTypes=true`, an otherwise unmarked enum receives SPIRE015 and the code action adds the missing member. With the option absent/false, the same source has no action. |
| `SPIRE015_FixAll_TwoSwitches` | Two incomplete switches in one document each produce SPIRE015. Applying the provider’s `BatchFixer` fix-all updates both switches, adds the right members to each, and leaves unrelated code unchanged. |
| `SPIRE015_FixAll_MultipleDocuments` | In a project with two documents containing incomplete switches, fix-all updates both documents and does not apply a member from one enum to the other. |
| `SPIRE015_ExistingTriviaPreserved` | Apply the action to a switch containing comments, blank lines, CRLF input, and mixed inline/block sections; assert comments and line-ending/indentation policy remain stable while the new section is inserted at the switch end. |
| `SPIRE015_CodeActionTitleAndEquivalence` | The provider advertises exactly one action titled `Add missing enum members` with a stable equivalence key, and the action result is compilable with no new syntax or binding diagnostics. |

## Contract risks to keep visible during implementation

- `MissingMembers` is a comma-separated display string. A member named `@null`
  and nullable null both appear as `null`. The analyzer therefore exports
  separate complete patterns through the shared metadata contract in
  [exact-shapes-coverage-matrix.md](exact-shapes-coverage-matrix.md), retaining
  `MissingMembers` for display. The provider never reconstructs null identity
  or coverage from names.
- Alias and flags coverage is already collapsed by SPIRE015. The code fix must
  add only the representative names reported by the analyzer rather than
  attempting to reconstruct every enum declaration.
- The diagnostic location is the `switch` keyword. Both statement and
  expression code paths must walk from that location to their containing
  syntax node and preserve the surrounding switch’s trivia.
- A guarded catch-all is not an opt-out; an unguarded catch-all is. The fix must
  never manufacture a catch-all arm as a way to silence SPIRE015.
