# SPIRE030: Readonly record variant cannot serialize its positional data

| Property | Value |
|---|---|
| Rule ID | SPIRE030 |
| Category | Correctness |
| Severity | Warning |
| Enabled | Yes |

## Description

A readonly record-struct variant with positional data cannot persist that data
through Unity serialization. Its positional properties have readonly backing
fields, which Unity does not serialize even when annotated with
`[SerializeField]` or `[SerializeReference]`.

The warning applies to partial and non-partial variants inside mutable
`[Serializable, DiscriminatedUnion]` struct unions in Unity serialization mode.
It replaces SPIRE028 for the affected variant: adding serialization attributes
or making the record partial does not solve this restriction. Fieldless
variants, unions without `[Serializable]`, and compilations outside Unity
serialization mode do not receive SPIRE030.

## Examples

```csharp
[Serializable, DiscriminatedUnion]
public partial struct State
{
    [Variant] public readonly partial record struct Value(int amount); // SPIRE030
}
```

Remove `readonly` from the variant, or remove `[Serializable]` from the union
if Unity serialization is not intended. Readonly variants remain supported
for runtime union conversion, matching, and equality.

```csharp
[Variant] public partial record struct Value(int amount);
```

## Code fixes

The `Remove readonly from variant` code fix removes the modifier from every
authored partial declaration of that variant. Afterward, SPIRE028 may report
any serialization attributes still missing from a non-partial variant.

## When to suppress

Suppress this warning only when another serialization mechanism deliberately
owns the representation. Making the variant partial or adding field
attributes alone does not resolve SPIRE030.

```csharp
#pragma warning disable SPIRE030
[Variant] public readonly record struct Value(int amount);
#pragma warning restore SPIRE030
```
