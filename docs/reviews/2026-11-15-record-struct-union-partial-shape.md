# Outer record-struct union emits an incompatible partial declaration

While testing SPIRE030's unsupported-union exemption, the real generator and
compiler exposed an existing declaration-shape problem:

```cs
[Serializable, DiscriminatedUnion]
public partial record struct Customization
{
    [Variant] public readonly record struct Slider(int amount);
}
```

Unity validation correctly reports SPIRE_DU013, because an outer record struct
is outside Unity serialization mode. However, core generation emits a
`partial struct Customization` alongside the authored `partial record struct`.
The compiler reports CS0261, followed by conversion/operator/interface errors.

Resolved by [SPIRE_DU020](../rules/SPIRE_DU020.md): outer record-struct union
declarations are explicitly rejected. Generation stops before variant, Unity,
and JSON validation, avoiding incompatible generated partial types. The
`Remove record from union` fix converts authored partial declarations to plain
structs while preserving nested record variants.

The regression
`ReadonlyRecordVariantsTests.RecordStructOuterUnion_IsRejectedWithoutNestedReadonlyWarning`
now expects SPIRE_DU020 without the old CS0261 cascade or SPIRE030. Record-struct
variant payloads and record-class unions remain supported. See the
[implementation plan](../../plans/2026-11-15-reject-record-struct-unions.md).
