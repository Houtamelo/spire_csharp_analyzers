# SPIRE013/SPIRE014 pending diagnostic-message coverage

Implemented and verified: the full solution passes 2,363 tests, zero skipped.
The 77 generated-source controls are unchanged. Evidence:
`tmp/pending-requests-20261026/full-suite/` and `verification.json` in its parent.

This matrix covers only the two ready diagnostic-message requests in
`docs/PENDING.md`. Each case should run the real discriminated-union generator
and then `FieldAccessSafetyAnalyzer`; generator output must compile before the
analyzer diagnostics are inspected. The smallest useful home is the existing
`VariantPayloadAnalyzerTests` integration suite, with the existing
`SingleValueCaseRegressionTests` and `FieldCollisionsTests` facts strengthened
where they already exercise the relevant source.

This is a message-contract change, not a guard-recognition change. Keep the
current `SPIRE013`/`SPIRE014` behavior and do not add recognition for
`Is{Variant}`/`Is{Variant} == false`; that item remains in the pending
discussion and is deliberately absent from the cases below.

## Descriptor contract

Every `should_fail` case below asserts exactly one `SPIRE013` or `SPIRE014`
diagnostic, the expected `DiagnosticDescriptor.Title`, `GetMessage()`,
`DiagnosticSeverity`, and source location. The old title and old message must
not remain in the result.

For the representative single-owner/single-guard fixtures, the exact strings
are:

| ID | Title | Severity |
|---|---|---|
| `SPIRE013` | `Attempting to access mismatched variant data` | `Error` |
| `SPIRE014` | `Attempting to access variant data without checking 'kind'` | `Warning` |

`SPIRE013` exact message for the `ApplyEffect` accessor inside a `Move` guard:

```text
The field 'applyEffect' only contains valid data when 'kind=ApplyEffect', but within this scope, it is statically known that 'kind=Move'.
Help: Within this scope, only the field 'move' contains valid data.
```

`SPIRE014` exact message for the same accessor without a guard:

```text
The field 'applyEffect' only contains valid data when 'kind=ApplyEffect', but within this scope, the value of 'kind' is unknown.
Help 1: Check if 'kind=ApplyEffect' before accessing the field 'applyEffect'.
Help 2: If you are absolutely sure that 'kind=ApplyEffect', you can suppress this diagnostic with `#pragma warning disable SPIRE014`.
```

For the three-variant negative-guard fixture, pin the multi-kind extension as
well. Declared order is `Move`, `Stop`, `Idle`, so the exact message is:

```text
The field 'move' only contains valid data when 'kind=Move', but within this scope, it is statically known that 'kind=Move' or 'kind=Stop'.
Help: Check 'kind' again before accessing variant data; this scope can contain more than one variant.
```

The spelling `mismatched` and the spelling `suppress` above are intentional
corrections to the typos in the pending note. Other wording and punctuation is
part of the contract. The diagnostic location is the generated accessor
reference itself (for example, the source span text `command.applyEffect`),
not the `kind` guard, the containing statement, or the nested payload field.
Also assert that the location belongs to the consumer source tree rather than
generated source (`variant-payload-consumer.cs`, `single-value-case.cs`, or
`field-collision.cs`, according to the existing helper).

## Category A: SPIRE013 wrong-variant message contract

### should_fail

| File/Fact | Description |
|---|---|
| `FieldAccess_WrongVariantDiagnosticContract` | Generate a struct union with `Move(int destination)` and `ApplyEffect(int effect)`. Under `if (command.kind == Command.Kind.Move)`, read `command.applyEffect.effect`. Assert the exact SPIRE013 title/message/severity above and that the location span is exactly `command.applyEffect`. |
| `SingleValueCase_WrongVariantDiagnosticContract` | Keep the single-value `switch (command.kind)` path from `SingleValueCaseRegressionTests`: in the `Move` case read the `ApplyEffect` accessor. Assert the same exact contract and accessor location, proving the message change does not depend on an `if` operation. |
| `FieldAccess_MultipleGuardedKindsNamesAreDeterministic` | Use declared variants `Move`, `Stop`, `Idle`; under `if (command.kind != Command.Kind.Idle)`, read `command.move` (the owner is one of the possible kinds, but `Stop` is also possible). Assert the exact multi-kind message above, SPIRE013 at `command.move`, `Error` severity, and `Move` then `Stop` in declared order. The help must use the general narrowing text and must not claim that `move` is valid for the whole `Move`/`Stop` scope. |

## Category B: SPIRE014 unguarded message and generated-name contract

### should_fail

| File/Fact | Description |
|---|---|
| `FieldAccess_UnguardedDiagnosticContract` | Generate the same `Move`/`ApplyEffect` union and read `command.applyEffect.effect` in an ordinary expression-bodied or local-variable expression with no kind guard. Assert the exact SPIRE014 title/message/severity above and location span `command.applyEffect`. |
| `FieldAccess_FactoryConflictAccessorUsesQualifiedNameInDiagnostic` | Generate the existing collision source with factories `run()` and `HTTPRequest(string value)`, whose generated accessor is `run_value`. Read `naming.run_value` without a guard. Assert exact SPIRE014 severity/title and a message naming `run_value` (not `run`), `kind=run`, and the corrected `suppress` pragma; the location span must be `naming.run_value`. |

## Category C: valid generated access remains diagnostic-free

### should_pass

| File/Fact | Description |
|---|---|
| `FieldAccess_MatchingKindGuard_NoDiagnostic` | Retain/strengthen `FieldAccess_MatchingKindGuardAllowsTheAccessor`: a `Move` equality guard followed by `command.move.speed` produces no SPIRE013 or SPIRE014. This is the positive counterpart to the exact wrong-variant message case. |
| `SingleValueCase_MatchingKindGuard_NoDiagnostic` | Retain/strengthen `SingleValueCase_GuardsMatchingField`: a matching single-value `switch` arm reads its own accessor and produces no SPIRE013 or SPIRE014. |
| `FieldAccess_SharedPayloadFieldNamesRemainOrdinary` | Retain/strengthen `MultipleSingleValueLabels_GuardSharedField`: `Move(int destination)` and `Rotate(int destination)` each read their own accessor and nested `destination` field under the matching guard. Assert no diagnostics; the shared nested payload name must not be treated as a second union-level owner or require a new `VariantFieldMap` rule. |
| `FieldAccess_MatchingPropertyPattern_NoDiagnostic` | A real generated property pattern such as `{ kind: Command.Kind.Move, move: { speed: var speed } }` binds the selected payload and produces no SPIRE013 or SPIRE014. This keeps the valid pattern path separate from direct accessor reads. |
| `FieldAccess_FactoryConflictAccessorGuardedAndFactoryRead_NoDiagnostic` | Retain/strengthen `FieldAccess_FactoryConflictAccessorUsesRunValue`: a matching `kind=run` guard may read `naming.run_value`, and `Naming.run().run_value` may read the accessor through its own `[Variant]` factory result. Both remain diagnostic-free and the generated accessor suffix remains exactly `run_value`. |

## Scope and assertion notes

- The tests are real generator/analyzer integration facts, not descriptor-only
  unit tests or hand-authored generated members. Reuse the existing helpers and
  source paths in the three named test classes.
- Positive diagnostics assert ID, exact title, exact message, severity, source
  tree, and accessor-only span. Negative cases assert no `SPIRE013`/`SPIRE014`
  after generator and compilation diagnostics have already been checked.
- Nested payload fields such as `speed` and `destination` are ordinary fields;
  only the union-level accessor (`move`, `applyEffect`, `run_value`, and so on)
  is the diagnostic location.
- Do not add `IsMove`, `IsApplyEffect`, or other `Is{Variant}` guard cases. Do
  not broaden this matrix to records/classes, layouts, or new ownership rules;
  those are unrelated to the two pending message requests.
