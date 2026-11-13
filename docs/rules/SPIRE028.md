# SPIRE028: Record variant needs serialization attributes

| Property | Value |
|---|---|
| Rule ID | SPIRE028 |
| Category | Correctness |
| Severity | Warning |
| Enabled | Yes |

## Description

A Unity-serializable struct union has a record-struct variant missing
`[Serializable]` or serialization attributes on its positional backing fields.
The existing internal serialization wrapper contains this record as its value.

Partial record variants receive the missing type attribute and generated
positional properties automatically. Authored properties remain unchanged and
need their own field annotations. Non-partial records must supply the attributes
themselves. Fully annotated records and unions outside Unity serialization mode
do not receive this warning.

A positional `[UnitySerializeReference]` marker requires `[SerializeReference]`
on its backing field. `[SerializeField]` alone does not satisfy that requirement;
the field-annotation fix upgrades it while preserving the marker.

## Examples

```csharp
[Serializable, DiscriminatedUnion]
public partial struct State
{
    [Variant] public record struct Value(int amount); // SPIRE028
}
```

Make the record partial to enable generation:

```csharp
[Variant] public partial record struct Value(int amount);
```

Or annotate it explicitly:

```csharp
[Serializable, Variant]
public record struct Value([field: SerializeField] int amount);
```

## Code fixes

- Add `[Serializable]` when missing.
- Add missing positional backing-field annotations, preserving authored properties
  and existing attributes. Managed-reference fields use `[SerializeReference]`.
- Make the record `partial` so generation can supply missing synthesized members.

Fieldless variants need only the type attribute. Helper methods and additional
computed properties do not need serialization annotations for the union schema.

## When to suppress

Prefer adding the required attributes or removing `[Serializable]` from a union
that was not intended for Unity serialization. Suppress only when another
serialization mechanism deliberately owns the representation.
