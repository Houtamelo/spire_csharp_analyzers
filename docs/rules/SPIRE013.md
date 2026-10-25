# SPIRE013: Accessing another variant's field

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
    { kind: Shape.Kind.Circle } => s.square.sideLength, // SPIRE013: square belongs to Square
    _ => 0,
};
```

### Compliant code

```csharp
double Ok(Shape s) => s switch
{
    { kind: Shape.Kind.Circle } => s.circle.radius,
    { kind: Shape.Kind.Square } => s.square.sideLength,
    _ => 0,
};
```

## When to suppress

Should not be suppressed — accessing the wrong variant's field is always a bug.
