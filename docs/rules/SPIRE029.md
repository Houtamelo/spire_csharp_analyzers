# SPIRE029: Authored positional member participates in payload reconstruction

| Property | Value |
|---|---|
| Rule ID | SPIRE029 |
| Category | Correctness |
| Severity | Warning |
| Enabled | Yes |

## Description

A record-struct variant explicitly replaces a compiler-synthesized positional
member. Union conversion copies positional values; payload access reconstructs
the record from them. Custom initialization or getter behavior can therefore
change the reconstructed values.

This is a warning; authored members remain supported. Spire-generated
serialization properties, helper methods, and additional computed properties do
not trigger it merely by being present.

## Examples

```csharp
[Variant]
public record struct Counter(int value)
{
    public int value { get; init; } = value + 1; // SPIRE029
}
```

`new Counter(10)` exposes 11. Conversion stores 11, and later reconstruction
through `new Counter(11)` exposes 12. Ordinary positional records preserve their
values without this extra transformation:

```csharp
[Variant]
public record struct Counter(int value);
```

## When to suppress

Suppress when the authored member's reconstruction behavior is intentional and
its effect is understood. There is no automatic code fix for this behavior.
