# Legacy struct exhaustiveness migration

The removed cases in this directory used the former union-level positional
`Deconstruct` shape, for example `(Shape.Kind.Circle, value)`. The redesign
places deconstruction on the sibling payload, so those exact source forms are
no longer valid C# consumers. Their exhaustiveness behavior is covered by the
variant-payload analyzer suite, including fieldless missing arms, guarded arms,
generic closed accessors, full qualified payload coverage, and record controls.

The remaining struct cases use property patterns and remain active. Record,
class, interface, enum, nullable-reference, suppressor, and compiler behavior
cases remain active in this directory.
