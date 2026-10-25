# SPIRE014: Accessing variant field without kind guard

| Property    | Value        |
|-------------|--------------|
| Rule ID     | SPIRE014     |
| Category    | Correctness  |
| Severity    | Warning      |
| Enabled     | Yes          |

## Description

Code reads a variant accessor (for example, `shape.circle`) without checking
the active variant through a switch, if-pattern, or equality check on `kind`.
An inactive accessor can read unrelated unmanaged storage or fail a shared-slot
cast. A captured payload's fields are ordinary fields and need no further guard.

Accessing the `kind` field itself is always safe and does not trigger this diagnostic.

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
    var r = s.circle.radius;  // SPIRE014: no kind guard
}
```

### Compliant code

```csharp
void Ok(Shape s)
{
    if (s.kind == Shape.Kind.Circle)
    {
        var r = s.circle.radius;  // guarded by kind check
    }
}

// Also fine: switch expression/statement arms
double Area(Shape s) => s switch
{
    { kind: Shape.Kind.Circle, circle: { radius: var r } } => Math.PI * r * r,
    { kind: Shape.Kind.Square, square: { sideLength: var x } } => x * x,
};
```

## When to suppress

Suppress when you've verified the variant through external logic not visible to the analyzer.

```csharp
#pragma warning disable SPIRE014
var r = shapes[knownCircleIndex].circle.radius;
#pragma warning restore SPIRE014
```
