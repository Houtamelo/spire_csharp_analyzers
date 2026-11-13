# Reject default union comparisons

Status: deferred. Implement union equality first. This plan changes no analyzer behavior.

Reject comparisons of non-nullable struct discriminated unions with `default`.
`[DiscriminatedUnion]` inherits `[EnforceInitialization]`; comparing against an
uninitialized union should not bypass its initialization contract. The existing
comparison exemption predates generated union equality.

Current behavior is documented in [SPIRE003](../docs/rules/SPIRE003.md): equality
comparisons are treated as detection rather than creation. The exemption was
introduced with SPIRE003 in commit `4dc1b17` (2026-03-12). Preserve this history;
update the current rule documentation when the new behavior is implemented.

## Intended scope

| Expression | Future behavior |
|---|---|
| `union == default`, `union != default` | Reject |
| `default == union`, `default != union` | Reject |
| Either order with `default(Union)` | Reject |
| Parenthesized defaults and implicit conversions around these expressions | Reject |
| Ordinary `[EnforceInitialization]` structs without `[DiscriminatedUnion]` | Preserve existing comparison exemption |
| Explicitly constructed union or variant payload | Preserve normal equality |
| `default(VariantPayload)` explicitly compared with a union | Preserve payload equality; this is not `default(Union)` |
| Nullable union/reference-record comparisons with null/default | Preserve absence/null semantics |

Keep the existing rejection of default union initialization and default union
method arguments. This change does not add runtime checks or change generated
equality/hash behavior for values obtained by bypassing analyzers.

## Implementation

1. Add success/failure integration tests before changing SPIRE003. Cover both
   operand orders and operators, literal/typed defaults, readonly/generic/nested
   unions, nullable controls, and ordinary enforced-struct controls.
2. Recognize non-nullable struct union comparisons before applying the existing
   equality exemption. Reuse SPIRE003's diagnostic where its location/message fit.
3. Handle ambiguous operator binding: per-variant equality overloads can make
   `union == default` fail overload resolution. Require a diagnostic for this
   spelling even when Roslyn does not produce a valid binary operation; use a
   narrow syntax/semantic fallback if necessary.
4. Preserve false-positive coverage for explicit variant-payload defaults and
   nullable/reference-record comparisons. Do not broaden the policy to all
   `[EnforceInitialization]` types without a separate decision.
5. Update the rule docs and coverage matrix, format changed C# files, and run
   SPIRE003 plus the generator equality integration tests.

Acceptance: all rejected rows produce the intended analyzer diagnostic, allowed
rows retain their behavior, and generated equality remains independent of
serialization/cache/inactive backing fields.
