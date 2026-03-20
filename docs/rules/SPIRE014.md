# SPIRE014: Accessing variant field without tag guard

| Property    | Value        |
|-------------|--------------|
| Rule ID     | SPIRE014     |
| Category    | Correctness  |
| Severity    | Warning      |
| Enabled     | Yes          |

## Description

Code accesses a variant-specific field (e.g., `shape.circle_radius`) without first checking which variant is active via a switch, if-pattern, or equality check on `tag`. Without a guard, the field read may return garbage data from a different variant's overlapping memory.

Accessing the `tag` field itself is always safe and does not trigger this diagnostic.

## Examples

### Violating code

```csharp
[DiscriminatedUnion]
partial struct Shape
{
    [Variant] public static partial Shape Circle(double radius);
    [Variant] public static partial Shape Square(int sideLength);
}

void Bad(Shape s)
{
    var r = s.circle_radius;  // SPIRE014: no tag guard
}
```

### Compliant code

```csharp
void Ok(Shape s)
{
    if (s.tag == Shape.Kind.Circle)
    {
        var r = s.circle_radius;  // guarded by tag check
    }
}

// Also fine: switch expression/statement arms
double Area(Shape s) => s switch
{
    (Shape.Kind.Circle, double r) => Math.PI * r * r,
    (Shape.Kind.Square, int x) => x * x,
};
```

## When to suppress

Suppress when you've verified the variant through external logic not visible to the analyzer.

```csharp
#pragma warning disable SPIRE014
var r = shapes[knownCircleIndex].circle_radius;
#pragma warning restore SPIRE014
```
