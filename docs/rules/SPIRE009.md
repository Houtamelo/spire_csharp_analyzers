# SPIRE009: Switch does not handle all variants of discriminated union

| Property    | Value        |
|-------------|--------------|
| Rule ID     | SPIRE009     |
| Category    | Correctness  |
| Severity    | Error        |
| Enabled     | Yes          |

## Description

Switch expressions and statements on discriminated union types must handle every variant explicitly. A missing variant arm means the switch silently falls through or throws at runtime — defeating the purpose of exhaustive matching.

Applies to struct unions (positional patterns via `Deconstruct`), record unions, and class unions (type patterns).

### Code fix

"Add missing variant arms" — generates typed arms for each uncovered variant with `throw new NotImplementedException()` as the body.

## Examples

### Violating code

```csharp
[DiscriminatedUnion]
partial struct Shape
{
    [Variant] public static partial Shape Circle(double radius);
    [Variant] public static partial Shape Square(int sideLength);
    [Variant] public static partial Shape Rectangle(float width, float height);
}

int Area(Shape s) => s switch
{
    (Shape.Kind.Circle, double r) => (int)(Math.PI * r * r),
    (Shape.Kind.Square, int x) => x * x,
    // SPIRE009: missing 'Rectangle'
};
```

### Compliant code

```csharp
int Area(Shape s) => s switch
{
    (Shape.Kind.Circle, double r) => (int)(Math.PI * r * r),
    (Shape.Kind.Square, int x) => x * x,
    (Shape.Kind.Rectangle, float w, float h) => (int)(w * h),
};
```

## When to suppress

Rarely appropriate. If suppressing, ensure the missing variants are logically unreachable in context.

```csharp
#pragma warning disable SPIRE009
var result = s switch { ... };
#pragma warning restore SPIRE009
```
