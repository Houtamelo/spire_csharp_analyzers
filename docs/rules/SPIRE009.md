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
Generated `IsVariant: true` property patterns and the corresponding `kind`
patterns are equivalent. `IsVariant: false` excludes that variant and covers the
remaining variants; payload restrictions still limit the arm's coverage.

### Code fix

"Add missing variant arms" — generates arms for each uncovered variant with `throw new NotImplementedException()` as the body.

New struct arms use the shorter predicate and deconstruct the variant payload:
`{ IsRectangle: true, rectangle: var (width, height) }`. Fieldless arms use
`{ IsPoint: true }`. Captures preserve factory parameter order and accept null
field values. Local names are uniquified against the enclosing scope. Existing
arms and record/class generation are unchanged.

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
    { IsRectangle: true, rectangle: var (width, height) } => (int)(width * height),
};
```

## When to suppress

Rarely appropriate. If suppressing, ensure the missing variants are logically unreachable in context.

```csharp
#pragma warning disable SPIRE009
var result = s switch { ... };
#pragma warning restore SPIRE009
```
