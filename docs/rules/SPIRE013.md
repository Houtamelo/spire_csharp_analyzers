# SPIRE013: Accessing another variant's field

| Property    | Value        |
|-------------|--------------|
| Rule ID     | SPIRE013     |
| Category    | Correctness  |
| Severity    | Error        |
| Enabled     | Yes          |

## Description

Code accesses a variant-specific field (e.g., `shape.circle_radius`) while the kind guard indicates a different variant is active. Reading memory from the wrong variant's field slot is undefined behavior for overlapping layouts and a logic error for all layouts.

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
    (Shape.Kind.Circle, _) => s.square_sideLength,  // SPIRE013: 'square_sideLength' belongs to 'Square', not 'Circle'
    _ => 0,
};
```

### Compliant code

```csharp
double Ok(Shape s) => s switch
{
    (Shape.Kind.Circle, _) => s.circle_radius,
    (Shape.Kind.Square, _) => s.square_sideLength,
    _ => 0,
};
```

## When to suppress

Should not be suppressed — accessing the wrong variant's field is always a bug.
