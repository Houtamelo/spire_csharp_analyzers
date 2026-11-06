# SPIRE009 Typed Generated-Payload Deconstruction Coverage Matrix

Generated payload positional patterns must preserve the variant constraint and
map simple positional restrictions to the existing value domains. A typed
capture such as `moving: (Point destination, _)` is total for that nonnullable
value slot. A declaration such as `speaking: (string text, var timer)` is not
total for `string?`: it covers only non-null text, and its `null` complement
must remain visible to exhaustiveness analysis.

The implementation may reuse the existing bool, nullable, enum, and numeric
domain operations; this change does not introduce new range or enum math.
Ordinary open reference hierarchies and unsupported nested/user-defined
deconstruction remain indeterminate and must leave the compiler warning
unsuppressed rather than being broadened into a whole-variant proof.

All case files belong in
`tests/Houtamelo.Spire.SourceGenerators.Tests/Exhaustiveness/cases/`.

---

## Category A: Flat positional payloads and missing sibling variants

### should_fail

| File Name | Description |
|-----------|-------------|
| `GeneratedState_TypedMoving_MissingSpeaking_Expression` | Switch expression covers `Idle` and `Moving` with `{ IsMoving: true, moving: (Point destination, _) }`, but omits `Speaking`; SPIRE009 reports the missing sibling. |
| `GeneratedState_TypedMoving_MissingSpeaking_Kind` | Equivalent `kind: State.Kind.Moving` form with the typed positional payload omits `Speaking`; SPIRE009 reports. |
| `GeneratedState_TypedMoving_MissingSpeaking_Statement` | Switch statement with the typed `Moving` payload omits `Speaking`; SPIRE009 reports at the statement. |
| `GeneratedState_TypedValueMoving_MissingSpeaking` | Nonnullable value payload captures both slots exactly (`(Point destination, float speed)`) while omitting `Speaking`; the exact row remains analyzable. |
| `GeneratedState_DiscardMoving_MissingSpeaking` | Discard-only `Moving` payload (`(_, _)`) is total for the value payload, but omitting `Speaking` still reports. |
| `GeneratedState_BoolTrue_MissingFalse` | A generated `A(bool flag)` payload is matched positionally with `flag: true`; the `false` path is missing and SPIRE009 reports. |
| `GeneratedState_BoolFalse_MissingTrue` | The inverse positional bool arm (`flag: false`) omits `true`; SPIRE009 reports. |
| `GeneratedState_NullableString_TypedNonNull_MissingNull` | A generated `Speaking(string? text, float timer)` payload is matched with `(string text, var timer)`; the `text: null` path is missing and SPIRE009 reports. |
| `GeneratedState_NullableString_Null_MissingNonNull` | A nullable string payload is matched with an explicit `text: null` positional/property arm but omits the non-null partition; SPIRE009 reports. |

### should_pass

| File Name | Description |
|-----------|-------------|
| `Pass_GeneratedState_TypedMoving_Complete` | `Idle`, typed positional `Moving`, and unrestricted `Speaking` cover every variant; no SPIRE009. |
| `Pass_GeneratedState_BoolFullPartition` | Positional `flag: true` and `flag: false` arms cover a bool payload; no SPIRE009. |
| `Pass_GeneratedState_NullableStringFullPartition` | Positional non-null string and explicit null arms cover `string?`; no SPIRE009. |

## Category B: Correlation across payload outputs and mixed pattern forms

### should_fail

| File Name | Description |
|-----------|-------------|
| `GeneratedState_TwoPositionalOutputs_MissingValidPair` | Two generated variants each have a bool payload. Existing arms cover `A(true)` and `B(false)`; the valid correlated pairs `A(false)` and `B(true)` remain missing and must be reported separately. |
| `GeneratedState_MixedPositionalProperty_MissingComplement` | One variant is constrained through a positional bool arm and another through an equivalent nested property arm; a missing complement in either path reports without losing the parent-variant correlation. |
| `GeneratedState_PositionalEnum_MissingMember` | A generated enum payload is matched positionally for one member while another member is omitted; reuse the existing enum domain and report the exact missing member path. |

### should_pass

| File Name | Description |
|-----------|-------------|
| `Pass_GeneratedState_TwoPositionalOutputs_FullCorrelation` | All correlated combinations across the two positional payload outputs are present; no Cartesian-product false positive or SPIRE009. |
| `Pass_GeneratedState_MixedPositionalProperty_Full` | Positional and nested property arms together cover every variant and payload value; no SPIRE009. |
| `Pass_GeneratedState_PositionalEnum_Full` | All enum members are covered through positional payload constraints; no SPIRE009. |

## Category C: Unsupported positional controls and compiler-warning behavior

### should_fail

| File Name | Description |
|-----------|-------------|
| `GeneratedState_TypedSubtypeCapture_RemainsIndeterminate` | A concrete subtype declaration against an ordinary nullable base reference does not enumerate open-class subtypes; compiler exhaustiveness remains visible. |
| `GeneratedState_UserDefinedDeconstruct_RemainsIndeterminate` | A non-generated payload with a user-defined deconstruction method is outside the trusted generated-position mapping; compiler exhaustiveness remains visible rather than being inferred as a finite domain. |

### should_pass

| File Name | Description |
|-----------|-------------|
| `Pass_GeneratedState_TypedSubtypeCapture_RemainsConservative` | The unsupported open hierarchy does not invent a finite subtype domain or a Spire diagnostic; the control remains compiler-warning-driven. |
| `Pass_GeneratedState_UserDefinedDeconstruct_RemainsConservative` | Unsupported user-defined deconstruction does not invent a generated payload mapping or suppress the compiler warning. |

## Code-fix snapshots

These before/after cases belong under
`tests/Houtamelo.Spire.SourceGenerators.Tests/CodeFix/cases/` and must use
`MissingPatternCount`/`MissingPatternN` complete patterns.

| Case Folder | Required behavior |
|---|---|
| `SPIRE009_Exact_TypedValuePayloadMissingSibling` | Existing control: add only the missing `Speaking` arm and preserve the typed `Moving` arm. |
| `SPIRE009_Exact_TypedPositionalBoolMissingFalse` | Add `{ IsA: true, a: { flag: false } }` (or the equivalent `kind` root) after an existing `flag: true` positional arm. |
| `SPIRE009_Exact_TypedPositionalNullableStringMissingNull` | Add the exact nested `text: null` arm after a typed non-null positional arm; do not add a broad whole-variant arm. |
| `SPIRE009_Exact_TypedPositionalCorrelation` | Add each missing correlated root-plus-payload pattern for two variants; do not form an independent Cartesian product from flattened names. |

## Integration contract

`TypedPayloadIntegrationTests` and the code-fix harness must run the real
generator, `ExhaustivenessAnalyzer`, and `CS8509Suppressor`:

- Missing bool, nullable-string, and correlated positional regions produce
  SPIRE009 with complete exact missing-pattern metadata.
- A missing region leaves the original compiler warning unsuppressed; complete
  true/false and null/non-null partitions suppress any original compiler
  warning.
- Applying each positional code fix recompiles the generated source, clears
  SPIRE009, and preserves the existing positional/property arms.
- Typed subtype and unsupported user-defined deconstruction cases that remain
  outside the trusted mapping stay indeterminate and compiler-visible; they
  must not be treated as exhaustive merely because a root variant name was
  recognized.

No new record or tuple domain is required unless the same generated
deconstruction-to-property mapping directly supports it; ordinary user-defined
deconstruction remains outside this scoped contract.
