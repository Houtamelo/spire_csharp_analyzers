# SPIRE015 Test Coverage Matrix

Diagnostic message format: `Switch on '{0}' does not handle member(s): {1}`
Location: the `switch` keyword token.
One diagnostic per switch statement/expression.

---

## Category A: Switch statement — basic detection (missing arms)

Tests that `ISwitchOperation` on a `[EnforceExhaustiveness]` enum is flagged when
one or more named members are not handled.

### should_fail

| File Name | Description |
|-----------|-------------|
| `Detect_SwitchStatement_OneMemberMissing` | Ensure SPIRE015 IS triggered when a switch statement covers Red and Green but not Blue of `Color`. Error marker on the `switch` keyword line. |
| `Detect_SwitchStatement_AllMembersMissing` | Ensure SPIRE015 IS triggered when a switch statement has no arms at all (empty body) on `Color`. |
| `Detect_SwitchStatement_TwoMembersMissing` | Ensure SPIRE015 IS triggered when a switch statement covers only Red, leaving Green and Blue uncovered. |
| `Detect_SwitchStatement_OnlyDefault` | Ensure SPIRE015 IS triggered when a switch statement has only a `default:` arm with no explicit member arms on `Color`. |
| `Detect_SwitchStatement_InForLoop` | Ensure SPIRE015 IS triggered when an incomplete switch statement on `Color` appears inside a `for` loop body. |
| `Detect_SwitchStatement_InForeachLoop` | Ensure SPIRE015 IS triggered when an incomplete switch statement on `Color` appears inside a `foreach` loop body. |
| `Detect_SwitchStatement_InWhileLoop` | Ensure SPIRE015 IS triggered when an incomplete switch statement on `Color` appears inside a `while` loop body. |
| `Detect_SwitchStatement_InLambda` | Ensure SPIRE015 IS triggered when an incomplete switch statement on `Color` appears inside a lambda body. |
| `Detect_SwitchStatement_InNestedClass` | Ensure SPIRE015 IS triggered when an incomplete switch statement on `Color` appears inside a method of a nested class. |
| `Detect_SwitchStatement_PartialAndDefault` | Ensure SPIRE015 IS triggered when a switch statement covers Red and has a `default:` arm but leaves Green and Blue uncovered. |
| `Detect_SwitchStatement_SingleMember_Empty` | Ensure SPIRE015 IS triggered when a switch statement on `SingleMember` (one-member enum) has no arms. |

### should_pass

| File Name | Description |
|-----------|-------------|
| `NoReport_SwitchStatement_AllMembersCovered` | Ensure SPIRE015 is NOT triggered when a switch statement covers all three members of `Color` (Red, Green, Blue). |
| `NoReport_SwitchStatement_AllMembersCoveredWithDefault` | Ensure SPIRE015 is NOT triggered when a switch statement covers all three members of `Color` AND has a `default:` arm. |

---

## Category B: Switch expression — basic detection (missing arms)

Tests that `ISwitchExpressionOperation` on a `[EnforceExhaustiveness]` enum is flagged when
one or more named members are not handled.

### should_fail

| File Name | Description |
|-----------|-------------|
| `Detect_SwitchExpression_OneMemberMissing` | Ensure SPIRE015 IS triggered when a switch expression covers Red and Green but not Blue of `Color`. Error marker on the `switch` expression line (the `switch` keyword). |
| `Detect_SwitchExpression_AllMembersMissing` | Ensure SPIRE015 IS triggered when a switch expression has only a discard arm `_ => ...` on `Color`. |
| `Detect_SwitchExpression_TwoMembersMissing` | Ensure SPIRE015 IS triggered when a switch expression covers only Red, leaving Green and Blue uncovered. |
| `Detect_SwitchExpression_ReturnStatement` | Ensure SPIRE015 IS triggered when an incomplete switch expression appears as the value in a return statement. |
| `Detect_SwitchExpression_ExpressionBodiedMember` | Ensure SPIRE015 IS triggered when an incomplete switch expression is the body of an expression-bodied method. |
| `Detect_SwitchExpression_LocalVariableInit` | Ensure SPIRE015 IS triggered when an incomplete switch expression initializes a local variable. |
| `Detect_SwitchExpression_MethodArgument` | Ensure SPIRE015 IS triggered when an incomplete switch expression is passed directly as a method argument. |
| `Detect_SwitchExpression_InLambda` | Ensure SPIRE015 IS triggered when an incomplete switch expression appears inside a lambda body. |
| `Detect_SwitchExpression_InForeachLoop` | Ensure SPIRE015 IS triggered when an incomplete switch expression on `Color` appears inside a foreach loop. |
| `Detect_SwitchExpression_NullCoalescing` | Ensure SPIRE015 IS triggered when an incomplete switch expression on `Color` is the left-hand side of a null-coalescing operator. |
| `Detect_SwitchExpression_Ternary` | Ensure SPIRE015 IS triggered when an incomplete switch expression on `Color` appears as a branch of a ternary conditional. |

### should_pass

| File Name | Description |
|-----------|-------------|
| `NoReport_SwitchExpression_AllMembersCovered` | Ensure SPIRE015 is NOT triggered when a switch expression covers all three members of `Color`. |
| `NoReport_SwitchExpression_AllMembersCoveredWithDiscard` | Ensure SPIRE015 is NOT triggered when a switch expression covers all three members of `Color` AND has a discard `_ => ...` arm. |

---

## Category C: Multiple switches and diagnostic isolation

Tests involving multiple switch statements/expressions to confirm one diagnostic per switch.

### should_fail

| File Name | Description |
|-----------|-------------|
| `Detect_TwoSwitchStatements_BothIncomplete` | Ensure SPIRE015 IS triggered once per switch when two separate incomplete switch statements on `Color` exist in the same method. Both `switch` keyword lines should have error markers. |
| `Detect_SwitchStatement_And_SwitchExpression_BothIncomplete` | Ensure SPIRE015 IS triggered once per switch when an incomplete switch statement and an incomplete switch expression on `Color` exist in the same method. Both switch keyword lines should have error markers. |
| `Detect_NestedSwitchStatements` | Ensure SPIRE015 IS triggered on both the outer and inner switch when both have incomplete `Color` coverage (switch inside another switch). |

---

## Category D: Default and discard arms — do not count as coverage

These cases confirm that `default:`, `_`, and catch-all patterns do not count as member coverage.

### should_fail

| File Name | Description |
|-----------|-------------|
| `Detect_SwitchStatement_DefaultDoesNotCoverMembers` | Ensure SPIRE015 IS triggered when the only arm is `default:` on `Color` (all three members uncovered). |
| `Detect_SwitchExpression_DiscardDoesNotCoverMembers` | Ensure SPIRE015 IS triggered when the only arm is `_ => ...` on `Color` (all three members uncovered). |
| `Detect_SwitchStatement_MixedDefaultNotEnough` | Ensure SPIRE015 IS triggered when arms cover Red only and `default:` handles the rest — Green and Blue remain uncovered from the rule's perspective. |
| `Detect_SwitchExpression_MixedDiscardNotEnough` | Ensure SPIRE015 IS triggered when arms cover Red only and `_ => ...` handles the rest — Green and Blue remain uncovered. |

---

## Category E: Or-patterns (`case A or B:` / `A or B =>`)

Tests for `IBinaryPatternOperation` (Or) — covers the union of both sides.

### should_fail

| File Name | Description |
|-----------|-------------|
| `Detect_SwitchExpression_OrPattern_OneMemberMissing` | Ensure SPIRE015 IS triggered when an or-pattern `Color.Red or Color.Green => ...` is present but `Color.Blue` is not covered. |
| `Detect_SwitchStatement_OrPattern_OneMemberMissing` | Ensure SPIRE015 IS triggered when a switch statement uses `case Color.Red or Color.Green:` but `Color.Blue` is absent. |

### should_pass

| File Name | Description |
|-----------|-------------|
| `NoReport_SwitchExpression_OrPattern_AllCovered` | Ensure SPIRE015 is NOT triggered when `Color.Red or Color.Green => ...` is combined with a separate `Color.Blue => ...` arm to cover all members. |
| `NoReport_SwitchStatement_OrPattern_AllCovered` | Ensure SPIRE015 is NOT triggered when `case Color.Red or Color.Green:` and `case Color.Blue:` together cover all members. |
| `NoReport_SwitchExpression_OrPattern_TripleOr` | Ensure SPIRE015 is NOT triggered when a single arm `Color.Red or Color.Green or Color.Blue => ...` covers all three members at once. |

---

## Category F: Not-patterns (`case not A:` / `not A =>`)

Tests for `INegatedPatternOperation` — covers all members except those in the inner pattern.

### should_pass

| File Name | Description |
|-----------|-------------|
| `NoReport_SwitchExpression_NotPattern_CoversRemainder` | Ensure SPIRE015 is NOT triggered when a switch expression has `Color.Red => ...` and `not Color.Red => ...`, which together cover all members (`not Color.Red` covers Green and Blue). |
| `NoReport_SwitchStatement_NotPattern_CoversRemainder` | Ensure SPIRE015 is NOT triggered when a switch statement has `case Color.Red:` and `case not Color.Red:` covering all members. |

---

## Category G: Fallthrough labels in switch statements (`case A: case B: body`)

Tests that multiple `ISingleValueCaseClauseOperation` labels stacked on one section all contribute coverage.

### should_fail

| File Name | Description |
|-----------|-------------|
| `Detect_SwitchStatement_Fallthrough_OneMemberStillMissing` | Ensure SPIRE015 IS triggered when `case Color.Red: case Color.Green:` falls through to a shared body but `Color.Blue` is absent. |

### should_pass

| File Name | Description |
|-----------|-------------|
| `NoReport_SwitchStatement_Fallthrough_AllCovered` | Ensure SPIRE015 is NOT triggered when fallthrough labels `case Color.Red: case Color.Green: case Color.Blue:` share one body, covering all members. |
| `NoReport_SwitchStatement_FallthroughToDefault_AllCovered` | Ensure SPIRE015 is NOT triggered when `case Color.Red: case Color.Green: case Color.Blue: default:` all fall through to a shared body. |

---

## Category H: When guards — do not count as coverage

Tests that arms/labels with a `when` clause do NOT contribute to member coverage per MVP spec.

### should_fail

| File Name | Description |
|-----------|-------------|
| `Detect_SwitchStatement_WhenGuard_MemberNotCovered` | Ensure SPIRE015 IS triggered when `case Color.Red when condition:` is the only arm for Red — the guard disqualifies it from coverage. Green and Blue are also absent. Error marker on the switch line. |
| `Detect_SwitchExpression_WhenGuard_MemberNotCovered` | Ensure SPIRE015 IS triggered when only the Red and Blue arms are present, but the Red arm has a `when` guard — Red is not considered covered. Green and Blue (unguarded) are both present; only Red is reported missing. |
| `Detect_SwitchStatement_WhenGuard_AllArmsGuarded` | Ensure SPIRE015 IS triggered when every arm in the switch statement (Red, Green, Blue) has a `when` guard, so no member counts as covered. |

---

## Category I: Goto case / goto default — ignored for coverage

Tests that `goto case` and `goto default` control-flow statements within switch sections do not contribute member coverage.

### should_fail

| File Name | Description |
|-----------|-------------|
| `Detect_SwitchStatement_GotoCase_DoesNotCoverMember` | Ensure SPIRE015 IS triggered when a switch section for Red uses `goto case Color.Green;` rather than a `break` — `goto case` does not grant Green coverage. Blue is also missing. |
| `Detect_SwitchStatement_GotoDefault_DoesNotCoverMember` | Ensure SPIRE015 IS triggered when a switch section for Red uses `goto default;` and no other member arms are present — `goto default` does not provide coverage for Green or Blue. |

---

## Category J: Alias handling (same underlying value)

Tests for `AliasedEnum { First=0, Second=1, AlsoFirst=0 }` where First and AlsoFirst share value 0.

### should_fail

| File Name | Description |
|-----------|-------------|
| `Detect_SwitchStatement_AliasedEnum_MissingSecond` | Ensure SPIRE015 IS triggered when `case AliasedEnum.First:` is present (which also covers `AlsoFirst`) but `Second` is missing. |
| `Detect_SwitchExpression_AliasedEnum_AllMissingExceptAlias` | Ensure SPIRE015 IS triggered when neither the 0-value group nor Second are handled (empty switch body or discard only). |

### should_pass

| File Name | Description |
|-----------|-------------|
| `NoReport_SwitchStatement_AliasedEnum_FirstCoversBothAliases` | Ensure SPIRE015 is NOT triggered when `case AliasedEnum.First:` and `case AliasedEnum.Second:` cover all distinct values — `AlsoFirst` shares `First`'s value and is considered covered. |
| `NoReport_SwitchStatement_AliasedEnum_AlsoFirstCoversBothAliases` | Ensure SPIRE015 is NOT triggered when `case AliasedEnum.AlsoFirst:` and `case AliasedEnum.Second:` are present — `AlsoFirst` covers the 0-value group (which includes First). |
| `NoReport_SwitchExpression_AliasedEnum_AllCoveredViaAliasArm` | Ensure SPIRE015 is NOT triggered when `AliasedEnum.AlsoFirst => ...` and `AliasedEnum.Second => ...` cover all distinct values. |

---

## Category K: Flags enum — composite member coverage

Tests for `Permission { None=0, Read=1, Write=2, ReadWrite=Read|Write=3, Execute=4 }` with `[Flags]`.
Key rules: (1) covering `ReadWrite` also covers `Read`, `Write`, and `None` (via bitwise-subset). (2) Covering `Read` and `Write` individually does NOT cover the composite `ReadWrite`. (3) `None` (zero-valued) is covered by any case that covers a superset of 0 — i.e., every named constant case covers `None` indirectly because `(None & V) == None` for all V.

### should_fail

| File Name | Description |
|-----------|-------------|
| `Detect_SwitchStatement_Flags_ReadAndWriteDoNotCoverReadWrite` | Ensure SPIRE015 IS triggered when `Permission.Read` and `Permission.Write` are handled individually — this does NOT cover `Permission.ReadWrite` (composites are not covered by their components separately). `Execute` and `None` also missing. |
| `Detect_SwitchStatement_Flags_MissingExecute` | Ensure SPIRE015 IS triggered when `None`, `Read`, `Write`, `ReadWrite` are all handled explicitly but `Execute` is absent. |
| `Detect_SwitchExpression_Flags_OnlyReadWrite` | Ensure SPIRE015 IS triggered when only `Permission.ReadWrite` is handled (covering None, Read, Write, ReadWrite via bitwise-subset rule) but `Execute` remains uncovered. |
| `Detect_SwitchExpression_Flags_NoneNotCoveredByDiscard` | Ensure SPIRE015 IS triggered when only a discard arm `_ => ...` is present — `None` (zero-valued) is NOT covered by catch-all arms. All members missing. |

### should_pass

| File Name | Description |
|-----------|-------------|
| `NoReport_SwitchStatement_Flags_ReadWriteCoversComponents` | Ensure SPIRE015 is NOT triggered when `Permission.ReadWrite` (covering None, Read, Write via bitwise-subset rule) and `Permission.Execute` are handled — all named members covered. |
| `NoReport_SwitchStatement_Flags_AllExplicit` | Ensure SPIRE015 is NOT triggered when all five Permission members are explicitly handled (None, Read, Write, ReadWrite, Execute). |
| `NoReport_SwitchStatement_Flags_AllZeroValuedCoveredByAnySingleCase` | Ensure SPIRE015 is NOT triggered when an enum with all zero-valued members (e.g., a custom enum where every member = 0) has any single member handled — all aliases of zero are covered by that one case. |

---

## Category L: Plain enum without `[EnforceExhaustiveness]` (false positives)

Tests that switches on enums WITHOUT the attribute are never flagged.

### should_pass

| File Name | Description |
|-----------|-------------|
| `NoReport_SwitchStatement_PlainEnum_Incomplete` | Ensure SPIRE015 is NOT triggered when an incomplete switch statement on `PlainEnum` (no attribute) is present. |
| `NoReport_SwitchExpression_PlainEnum_OnlyOneArm` | Ensure SPIRE015 is NOT triggered when a switch expression on `PlainEnum` has only one arm and a discard. |
| `NoReport_SwitchStatement_PlainEnum_OnlyDefault` | Ensure SPIRE015 is NOT triggered when a switch statement on `PlainEnum` has only a `default:` arm. |
| `NoReport_SwitchStatement_BuiltinInt_Incomplete` | Ensure SPIRE015 is NOT triggered when switching on an `int` value (not an enum at all). |
| `NoReport_SwitchStatement_BuiltinString_Incomplete` | Ensure SPIRE015 is NOT triggered when switching on a `string` value (not an enum). |

---

## Category M: Empty enum (no named members)

Tests for `Empty { }` — an enum with no members, so no members can be missing.

### should_pass

| File Name | Description |
|-----------|-------------|
| `NoReport_SwitchStatement_EmptyEnum` | Ensure SPIRE015 is NOT triggered when switching on `Empty` (no members), even with an empty switch body. |
| `NoReport_SwitchExpression_EmptyEnum` | Ensure SPIRE015 is NOT triggered when switching on `Empty` in a switch expression with only a discard arm. |

---

## Category N: Nullable enum (`MyEnum?`)

Tests that `Nullable<T>` is unwrapped — the rule fires based on the underlying enum's attribute, regardless of null-handling.

### should_fail

| File Name | Description |
|-----------|-------------|
| `Detect_SwitchStatement_NullableEnum_OneMemberMissing` | Ensure SPIRE015 IS triggered when switching on `Color?` (nullable Color) with Red covered but Green and Blue missing. |
| `Detect_SwitchExpression_NullableEnum_OneMemberMissing` | Ensure SPIRE015 IS triggered when a switch expression on `Color?` covers Red but not Green or Blue. |
| `Detect_SwitchStatement_NullableEnum_NullArmDoesNotCoverMembers` | Ensure SPIRE015 IS triggered when switching on `Color?` handles only the `null` case (via pattern) — none of the enum members are covered. |

### should_pass

| File Name | Description |
|-----------|-------------|
| `NoReport_SwitchStatement_NullableEnum_AllMembersCovered` | Ensure SPIRE015 is NOT triggered when switching on `Color?` covers all three members (Red, Green, Blue) regardless of whether null is handled. |
| `NoReport_SwitchExpression_NullableEnum_AllMembersCovered` | Ensure SPIRE015 is NOT triggered when a switch expression on `Color?` has arms for Red, Green, and Blue. |

---

## Category O: Nested enum (enum declared inside another type)

Tests that the attribute is respected regardless of enum nesting depth.

### should_fail

| File Name | Description |
|-----------|-------------|
| `Detect_SwitchExpression_NestedEnum_OneMemberMissing` | Ensure SPIRE015 IS triggered when switching on an `[EnforceExhaustiveness]` enum declared inside a class, with one member missing. |
| `Detect_SwitchStatement_NestedEnum_AllMembersMissing` | Ensure SPIRE015 IS triggered when switching on a nested `[EnforceExhaustiveness]` enum with no arms. |

### should_pass

| File Name | Description |
|-----------|-------------|
| `NoReport_SwitchExpression_NestedEnum_AllCovered` | Ensure SPIRE015 is NOT triggered when switching on a nested `[EnforceExhaustiveness]` enum with all members covered. |

---

## Category P: Single-member enum edge cases

Tests for `SingleMember { Only }`.

### should_fail

| File Name | Description |
|-----------|-------------|
| `Detect_SwitchStatement_SingleMember_NoArms` | Ensure SPIRE015 IS triggered when a switch statement on `SingleMember` has an empty body (no arms). |
| `Detect_SwitchExpression_SingleMember_OnlyDiscard` | Ensure SPIRE015 IS triggered when a switch expression on `SingleMember` has only a discard arm. |

### should_pass

| File Name | Description |
|-----------|-------------|
| `NoReport_SwitchStatement_SingleMember_Covered` | Ensure SPIRE015 is NOT triggered when `SingleMember.Only` is the sole arm in a switch statement. |
| `NoReport_SwitchExpression_SingleMember_Covered` | Ensure SPIRE015 is NOT triggered when `SingleMember.Only => ...` is the sole arm in a switch expression. |

---

## Category Q: Cast away from enum type (false positives)

Tests that casting an enum to its underlying integer type before switching bypasses the rule.

### should_pass

| File Name | Description |
|-----------|-------------|
| `NoReport_SwitchStatement_CastToInt_PlainSwitch` | Ensure SPIRE015 is NOT triggered when `switch ((int)color)` is used — the switched type is `int`, not `Color`. |
| `NoReport_SwitchExpression_CastToInt_PlainSwitch` | Ensure SPIRE015 is NOT triggered when `((int)color) switch { ... }` is used — the switched type is `int`. |

---

## Category R: Async and special method contexts

Tests that the rule fires correctly inside async methods, static methods, and local functions.

### should_fail

| File Name | Description |
|-----------|-------------|
| `Detect_SwitchExpression_AsyncMethod_OneMemberMissing` | Ensure SPIRE015 IS triggered when an incomplete switch expression on `Color` appears inside an `async Task<string>` method. |
| `Detect_SwitchStatement_StaticMethod_OneMemberMissing` | Ensure SPIRE015 IS triggered when an incomplete switch statement on `Color` appears inside a `static` method. |
| `Detect_SwitchStatement_LocalFunction_OneMemberMissing` | Ensure SPIRE015 IS triggered when an incomplete switch statement on `Color` appears inside a local function. |

### should_pass

| File Name | Description |
|-----------|-------------|
| `NoReport_SwitchStatement_AsyncMethod_AllCovered` | Ensure SPIRE015 is NOT triggered when a switch statement inside an async method covers all `Color` members. |

---

## Category S: Numeric and cast constant patterns

Enum-typed cast constants (`case (Color)0:`) count as coverage — Roslyn's `IConstantPatternOperation` has an enum-typed constant value. Plain int literals (`case 0:`) in an enum switch are not enum-typed and do not count.

### should_fail

| File Name | Description |
|-----------|-------------|
| `Detect_SwitchStatement_IntLiteralCase_NoCoverage` | Ensure SPIRE015 IS triggered when the switch uses `case 0:` (int literal, not enum-typed) on `Color` — no member coverage. |

### should_pass

| File Name | Description |
|-----------|-------------|
| `NoReport_SwitchStatement_CastConstant_AllCovered` | Ensure SPIRE015 is NOT triggered when `case (Color)0:`, `case (Color)1:`, `case (Color)2:` exhaustively cover all `Color` members by underlying value (enum-typed constants). |

---

## Summary of case counts

| Category | should_fail | should_pass | Total |
|----------|-------------|-------------|-------|
| A: Switch statement — basic detection | 11 | 2 | 13 |
| B: Switch expression — basic detection | 11 | 2 | 13 |
| C: Multiple switches / diagnostic isolation | 3 | 0 | 3 |
| D: Default and discard arms | 4 | 0 | 4 |
| E: Or-patterns | 2 | 3 | 5 |
| F: Not-patterns | 0 | 2 | 2 |
| G: Fallthrough labels | 1 | 2 | 3 |
| H: When guards | 3 | 0 | 3 |
| I: Goto case / goto default | 2 | 0 | 2 |
| J: Alias handling | 2 | 3 | 5 |
| K: Flags enum coverage | 4 | 3 | 7 |
| L: Plain enum (false positives) | 0 | 5 | 5 |
| M: Empty enum | 0 | 2 | 2 |
| N: Nullable enum | 3 | 2 | 5 |
| O: Nested enum | 2 | 1 | 3 |
| P: Single-member enum | 2 | 2 | 4 |
| Q: Cast away from enum | 0 | 2 | 2 |
| R: Async and special methods | 3 | 1 | 4 |
| S: Numeric and cast constant patterns | 1 | 1 | 2 |
| **Total** | **54** | **33** | **87** |
