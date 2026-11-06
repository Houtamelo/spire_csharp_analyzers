# SPIRE009 nullable reference payload coverage

Regression coverage for the resolved nullable-reference payload request from
`docs/PENDING.md`. The existing recursive property matrix already handles
these cases; no production change was needed.

`Effect` is an ordinary abstract class without `[EnforceExhaustiveness]`.
`[UnitySerializeReference] Effect? effect` has separate null and non-null
regions. The analysis must preserve their correlation with `ApplyEffect`
without enumerating `Effect` subclasses.

| Scenario | Expected result | Coverage |
|----------|-----------------|----------|
| `effect: null` only | SPIRE009 for missing non-null values | Struct `kind` and `IsApplyEffect` cases |
| `effect: not null` only | SPIRE009 for missing null | Struct `kind` and `IsApplyEffect` cases |
| Missing entire `ApplyEffect` variant | SPIRE009 | Struct case |
| Both partitions or unrestricted payload capture | No SPIRE009 | Both discriminator forms and mixed forms |
| Non-nullable `Effect` under enabled annotations | No null arm required | Both discriminator forms, explicit non-null pattern and captures |
| Original five-variant statement with other payload captures | SPIRE009 for either omitted complement | Two complete reproductions of the pending sample |
| Explicit `Effect?` under disabled nullable context | SPIRE009 for omitted null | Analyzer case and integration test |
| Missing-pattern metadata | Exactly one correlated witness for `ApplyEffect` | `NullablePayloadIntegrationTests` |
| Apply missing-arm code fix | Generated source compiles, emits, and clears SPIRE009 | Null and non-null complements, both discriminator forms |
| Compiler exhaustiveness warnings | Incomplete partitions remain unsuppressed; complete partitions permit suppression | Real generator/analyzer/suppressor integration |

The 17 case files live in `Exhaustiveness/cases/`; six integration tests live
in `CodeFix/NullablePayloadIntegrationTests.cs`. This does not change the
existing policy for oblivious references or ordinary class subtype matching.
