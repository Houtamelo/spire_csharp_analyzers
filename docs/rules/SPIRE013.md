# SPIRE013: Attempting to access mismatched variant data

| Property    | Value        |
|-------------|--------------|
| Rule ID     | SPIRE013     |
| Category    | Correctness  |
| Severity    | Error        |
| Enabled     | Yes          |

## Description

Code reads a variant accessor (for example, `shape.circle`) while the kind guard
indicates a different active variant. Inactive unmanaged payload views may read
overlaid values; shared managed slots may fail a cast or unbox. Always guard
the accessor with its own variant.

Generated `IsVariant` checks are equivalent to comparisons on `kind`, including
negation, boolean comparisons, and property patterns. A negative check can leave
multiple possible variants and does not prove a particular alternative.
Nested conditions combine those constraints. Branches that rejoin contribute
all remaining possibilities; an accessor is safe only when every possibility
owns it. A payload restriction such as `speed: > 0` can fail while the variant
still matches, so that pattern's failure does not exclude the whole variant.

Receiver identity and proof invalidation follow the same rules as
[SPIRE014](SPIRE014.md). An invalidated proof produces SPIRE014; a surviving
proof that permits the wrong variant produces SPIRE013.

The diagnostic names the accessed field, its required `kind`, and the kind known
within the current scope. It also identifies the valid accessor when one is known.

## Examples

### Violating code

```csharp
[DiscriminatedUnion]
partial struct Shape
{
    [Variant] public static partial Shape Circle(double radius);
    [Variant] public static partial Shape Square(int sideLength);
}

double Bad(Shape s) => s switch
{
    { IsCircle: true, circle: var (radius) } => s.square.sideLength, // SPIRE013: square belongs to Square
    _ => 0,
};
```

### Compliant code

```csharp
double Ok(Shape s) => s switch
{
    { IsCircle: true, circle: var (radius) } => radius,
    { IsSquare: true, square: var (sideLength) } => sideLength,
    _ => 0,
};
```

## When to suppress

Should not be suppressed — accessing the wrong variant's field is always a bug.
