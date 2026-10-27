# SPIRE014: Attempting to access variant data without checking 'kind'

| Property    | Value        |
|-------------|--------------|
| Rule ID     | SPIRE014     |
| Category    | Correctness  |
| Severity    | Warning      |
| Enabled     | Yes          |

## Description

Code reads a variant accessor (for example, `shape.circle`) without checking
the active variant through a switch, if-pattern, equality check on `kind`, or
generated `IsVariant` predicate.
An inactive accessor can read unrelated unmanaged storage or fail a shared-slot
cast. A captured payload's fields are ordinary fields and need no further guard.

Accessing `kind` or a generated `IsVariant` predicate is always safe and does not
trigger this diagnostic. Checks must refer to the same union value; assigning a
new value invalidates a previous guard.

The diagnostic names the required kind check and shows how to suppress SPIRE014
when that check is guaranteed by external logic.

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
    if (s.IsCircle)
    {
        var r = s.circle.radius;  // equivalent to checking s.kind == Shape.Kind.Circle
    }
}

// Also fine: switch expression/statement arms
double Area(Shape s) => s switch
{
    { IsCircle: true, circle: var (r) } => Math.PI * r * r,
    { IsSquare: true, square: var (x) } => x * x,
};
```

## When to suppress

Suppress when you've verified the variant through external logic not visible to the analyzer.

```csharp
#pragma warning disable SPIRE014
var r = shapes[knownCircleIndex].circle.radius;
#pragma warning restore SPIRE014
```
