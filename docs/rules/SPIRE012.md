# SPIRE012: Discriminated union pattern field count mismatch

| Property    | Value        |
|-------------|--------------|
| Rule ID     | SPIRE012     |
| Category    | Correctness  |
| Severity    | Error        |
| Enabled     | Yes          |

## Description

A positional pattern inside a variant payload accessor provides the wrong
number of field bindings. Count payload fields only; the union's `kind` is a
separate property pattern.

## Examples

### Violating code

```csharp
[DiscriminatedUnion]
partial struct Shape
{
    [Variant] public static partial Shape Circle(double radius);
    [Variant] public static partial Shape Rectangle(float width, float height);
}

var result = s switch
{
    { kind: Shape.Kind.Circle, circle: { radius: var r } } => 1,
    { kind: Shape.Kind.Rectangle, rectangle: (float w, float h, float extra) } => 2, // SPIRE012
};
```

### Compliant code

```csharp
var result = s switch
{
    { kind: Shape.Kind.Circle, circle: { radius: var r } } => 1,
    { kind: Shape.Kind.Rectangle, rectangle: (float w, float h) } => 2,
};
```

## When to suppress

Should not be suppressed — a field count mismatch is always a bug.
