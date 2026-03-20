# SPIRE012: Discriminated union pattern field count mismatch

| Property    | Value        |
|-------------|--------------|
| Rule ID     | SPIRE012     |
| Category    | Correctness  |
| Severity    | Error        |
| Enabled     | Yes          |

## Description

A positional pattern on a struct discriminated union provides the wrong number of field bindings for the matched variant. For example, matching a 2-field variant with only 1 binding, or a 1-field variant with 3 bindings. The first element (the `Kind` constant) is excluded from the count.

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
    (Shape.Kind.Circle, double r) => 1,
    (Shape.Kind.Rectangle, float w) => 2,  // SPIRE012: Rectangle has 2 field(s), not 1
};
```

### Compliant code

```csharp
var result = s switch
{
    (Shape.Kind.Circle, double r) => 1,
    (Shape.Kind.Rectangle, float w, float h) => 2,
};
```

## When to suppress

Should not be suppressed — a field count mismatch is always a bug.
