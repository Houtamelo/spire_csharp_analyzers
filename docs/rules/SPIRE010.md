# SPIRE010: Switch uses wildcard instead of exhaustive variant matching

| Property    | Value        |
|-------------|--------------|
| Rule ID     | SPIRE010     |
| Category    | Correctness  |
| Severity    | Info         |
| Enabled     | Yes          |

## Description

A switch on a discriminated union covers all variants only because a wildcard (`_` or `var`) catches the unmatched ones. This hides missing arms — if a new variant is added later, the wildcard silently absorbs it instead of producing a compile-time error.

### Code fix

"Expand wildcard to explicit arms" — replaces the wildcard with typed arms for each variant it was covering.

## Examples

### Violating code

```csharp
[DiscriminatedUnion]
partial struct Shape
{
    [Variant] public static partial Shape Circle(double radius);
    [Variant] public static partial Shape Square(int sideLength);
}

string Describe(Shape s) => s switch
{
    (Shape.Kind.Circle, double r) => $"circle r={r}",
    _ => "other",  // SPIRE010: wildcard hides Square
};
```

### Compliant code

```csharp
string Describe(Shape s) => s switch
{
    (Shape.Kind.Circle, double r) => $"circle r={r}",
    (Shape.Kind.Square, int x) => $"square side={x}",
};
```

## When to suppress

Suppress when intentionally using a catch-all for a subset of variants that share the same handling.

```csharp
#pragma warning disable SPIRE010
string Describe(Shape s) => s switch
{
    (Shape.Kind.Circle, double r) => $"circle r={r}",
    _ => "polygon",
};
#pragma warning restore SPIRE010
```
