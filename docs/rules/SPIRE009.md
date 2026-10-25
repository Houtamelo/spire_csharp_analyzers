# SPIRE009: Switch does not handle all variants of discriminated union

| Property    | Value        |
|-------------|--------------|
| Rule ID     | SPIRE009     |
| Category    | Correctness  |
| Severity    | Error        |
| Enabled     | Yes          |

## Description

Switch expressions and statements on discriminated union types must handle every variant explicitly. A missing variant arm means the switch silently falls through or throws at runtime — defeating the purpose of exhaustive matching.

Applies to struct unions with variant payload patterns and record unions with
type patterns. Positional deconstruction belongs to the payload accessor.

### Code fix

"Add missing variant arms" — generates arms for each uncovered variant with `throw new NotImplementedException()` as the body.

New struct arms use nested property patterns under the variant accessor.
Every field uses `field: var local`, preserving its static type and matching
null. Local captures
are uniquified against the enclosing scope. Record/class generation is unchanged.

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
    { kind: Shape.Kind.Circle, circle: { radius: var r } } => (int)(Math.PI * r * r),
    { kind: Shape.Kind.Square, square: { sideLength: var x } } => x * x,
    // SPIRE009: missing 'Rectangle'
};
```

### Compliant code

```csharp
int Area(Shape s) => s switch
{
    { kind: Shape.Kind.Circle, circle: { radius: var r } } => (int)(Math.PI * r * r),
    { kind: Shape.Kind.Square, square: { sideLength: var x } } => x * x,
    { kind: Shape.Kind.Rectangle, rectangle: { width: var width, height: var height } } => (int)(width * height),
};
```

## When to suppress

Rarely appropriate. If suppressing, ensure the missing variants are logically unreachable in context.

```csharp
#pragma warning disable SPIRE009
var result = s switch { ... };
#pragma warning restore SPIRE009
```
