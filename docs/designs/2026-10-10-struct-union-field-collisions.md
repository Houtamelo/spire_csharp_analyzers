# Struct union field-name collisions

Status: implemented on October 10, 2026. Struct field-name and generated-arm
regressions pass in the .NET and Unity Editor Mono harnesses.

This incorporates the code-fix format task in [the TODO](../todo.md), reconciled
with the subsequently approved property-pattern and capture-name rules.

## Scope

Apply these changes only to struct-based unions. Preserve record/class-based
unions' existing generated APIs and behavior.

## Compatible fields

Same-name fields with the same type identity share one property. Differences in
reference-nullability annotations do not make their types incompatible. Combine
their nullability contracts conservatively and independently of variant order.

For `ApplyEffect(Effect? effect)` and `RemoveEffect(Effect effect)`, generate a
shared `Effect? effect` property. Preserve each factory parameter and each
variant's serialized payload field with its original type annotation.

Compare semantic type identity, not formatted type strings. Nullable value types
remain distinct types: `int` and `int?` are an incompatible collision.

## Incompatible fields

When a field-name group contains genuinely different types, generate separate
typed properties qualified by variant for that group. Use the naming convention
`camelCaseVariantName_fieldName`, preserving the declared field name.

For `Count(int value)` and `Text(string value)`, generate `int count_value` and
`string text_value`. Noncolliding field names remain unchanged.

Factory parameter names and serialized payload field names remain unchanged.
The qualification affects the runtime property API. Storage mapping, field-access
analysis, pattern analysis, and code fixes must use the same resolved names.

Adding an incompatible collision changes the previously shared property's name.
This is an API compatibility consequence of qualification only on collisions.

## Agreed generated-arm format

Generate struct-union variant arms as property patterns. Each captured field uses
`property_name: var var_name`; do not emit typed declaration patterns or choose
positional patterns for new struct variant arms. Apply this policy to missing-arm
code fixes and wildcard expansion. Record/class-based union behavior is unchanged.

Use the original variant parameter name as the capture name, without the variant
prefix from a qualified property. Make the local name unique when its surrounding
scope requires it. For example:

```cs
case { kind: Result.Kind.Count, count_value: var value }:
```

The discriminator selects the variant. Field captures match null as well as
non-null values and infer the generated property's static type, including its
combined nullable contract. Generated captures do not enforce non-null factory
parameter contracts through runtime type filtering.

## Pattern-matching semantics

`#nullable enable` and `#nullable disable` do not change runtime pattern matching.
An `Effect effect` declaration pattern rejects null in either context. A `var`
pattern accepts null and non-null values. Nullable reference annotations are
compile-time contracts, not runtime null checks.

The agreed generated-arm format matches a variant without rejecting null field
values. It must not assume that disabling nullable analysis makes typed
declaration patterns match null.

The TODO's typed nullable examples (`effect: Effect? effect`, `string? name`)
are not valid declaration patterns. Its struct examples are superseded by the
agreed `var` captures. Its record example is illustrative rather than compilable
declaration syntax; do not treat it as a request to change record declarations,
serialization support, or generated-arm behavior. Record/class-based matching
retains its existing behavior. Repairing nullable record-arm generation would
be a separate change outside this struct redesign.

Property patterns avoid the boxing introduced by shared `object?` Deconstruct
outputs. This does not promise allocation-free construction or serialization
for layouts that deliberately allocate, such as BoxedFields and BoxedTuple.

## Implementation defaults and verification

- Use conventional acronym-aware camel casing: `HTTPResponse` becomes
  `httpResponse`, and `URL` becomes `url`. Preserve the field-name portion.
- Resolve incompatible original field names with the agreed variant-qualified
  names; those collisions must not produce a field-type-conflict diagnostic.
- Do not add detection or custom diagnostics for secondary naming conflicts,
  such as a user-defined `count_value` member already occupying the resolved
  property name. Leave these to ordinary C# compiler diagnostics and keep the
  agreed naming convention.
- Choose unique local captures and escape C# keywords as ordinary code-fix
  name-generation details.
- Verify conservative merging of nested reference-nullability annotations and
  generic field types, all applicable struct layouts, and native payload schema
  preservation.
