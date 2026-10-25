# SPIRE011: Discriminated union pattern field type mismatch

| Property    | Value        |
|-------------|--------------|
| Rule ID     | SPIRE011     |
| Category    | Correctness  |
| Severity    | Error        |
| Enabled     | Yes          |

## Description

A pattern inside a variant payload accessor uses a typed binding that does not
match the declared field type. Payload `Deconstruct` outputs retain their field
types. The analyzer checks nested property and positional patterns.

### Code fix

"Fix field type" uses a `var` capture in a property pattern, preserving nulls,
or the declared field type in a positional pattern.

## Examples

### Violating code

```csharp
[DiscriminatedUnion]
partial struct Shape
{
    [Variant] public static partial Shape Circle(double radius);
    [Variant] public static partial Shape Square(int sideLength);
}

var result = s switch
{
    { kind: Shape.Kind.Circle, circle: { radius: string bad } } => 1, // SPIRE011
    { kind: Shape.Kind.Square, square: { sideLength: var x } } => 2,
};
```

### Compliant code

```csharp
var result = s switch
{
    { kind: Shape.Kind.Circle, circle: { radius: var r } } => 1,
    { kind: Shape.Kind.Square, square: { sideLength: var x } } => 2,
};
```

## When to suppress

Should not be suppressed — a type mismatch is always a bug.
