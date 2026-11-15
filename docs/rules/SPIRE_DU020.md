# SPIRE_DU020: Struct-based discriminated unions cannot be records

| Property | Value |
|---|---|
| Rule ID | SPIRE_DU020 |
| Category | SourceGeneration |
| Severity | Error |
| Enabled | Yes |

## Description

A struct-based `[DiscriminatedUnion]` must be declared as a `partial struct`,
optionally `readonly`. The generator provides the union's equality and other
members; the outer type must not use `record struct`.

This error applies in Unity and ordinary .NET compilations, including generic,
readonly and serializable unions. Generation stops for that declaration to
avoid incompatible partial types and secondary compiler errors. Nested
`[Variant] record struct` payloads and record-class unions remain supported.

## Examples

```csharp
[DiscriminatedUnion]
public partial record struct State // SPIRE_DU020
{
    [Variant] public record struct Value(int amount);
}
```

Remove `record` from the union declaration:

```csharp
[DiscriminatedUnion]
public partial struct State
{
    [Variant] public record struct Value(int amount);
}
```

## Code fixes

`Remove record from union` updates every authored partial declaration of the
selected union and preserves nested record variants. It supports empty
positional lists and bodyless declarations. A nonempty positional list must
be refactored manually; the fix does not discard its parameters or synthesized
properties.

## When to suppress

Use a supported declaration instead of suppressing the error. Suppression
does not cause the generator to emit an implementation for a record-struct
union.
