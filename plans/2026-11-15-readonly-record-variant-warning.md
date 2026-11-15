# SPIRE030: Readonly record variant cannot serialize its positional data

Status: Implemented and validated.

Report a default-enabled Correctness warning for public nested readonly
record-struct variants with positional data inside mutable, non-ref
`[Serializable, DiscriminatedUnion]` struct unions, when Unity serialization is
active. Preserve runtime support and existing generated representation.

Flag partial and non-partial readonly variants, including variants that already
have serialization attributes. Emit one SPIRE030 per variant, preferably on its
authored `readonly` keyword. Suppress SPIRE028 for that variant because adding
attributes or making it partial cannot solve the readonly restriction.
SPIRE029 remains independent.

Do not flag fieldless variants (with or without an empty positional parameter
list), mutable variants, unions without `[Serializable]`, compilations outside
Unity serialization mode, or outer readonly/ref unions already rejected by
SPIRE_DU013. Attribute recognition must use semantic identity.

Extend `RecordVariantDeclarationAnalyzer` and its existing compilation/symbol
registration. Add the descriptor to the generator-coupled descriptor registry
and the unshipped release table. No new attribute is required.

Provide a dedicated SPIRE030 code fix titled `Remove readonly from variant`.
Remove the modifier from every authored partial declaration of the same record
symbol, including declarations in other documents. Preserve unrelated syntax,
attributes, comments, and modifiers. Do not edit generated declarations. Offer
no action for unrelated diagnostics or stale/indeterminate targets. Re-run the
generator after editing; SPIRE028 may then legitimately identify remaining
missing attributes on a non-partial variant.

Tests must precede implementation. Use the existing real Unity-reference
generator harness and Roslyn workspace code-action pipeline. Cover detection,
diagnostic severity/message/location, fieldless and non-Unity exemptions,
SPIRE028 replacement, SPIRE029 coexistence, semantic attribute identity,
partial declarations, multi-document fixes, trivia, and unaffected sibling
variants. Compile and emit representative before/after results. Classify every
Unity-dependent test with `Environment=Unity`. Run focused red/green checks,
then the relevant source-generator suite. Native Unity serialization behavior
is unchanged by this warning and is not an acceptance claim for this change.
