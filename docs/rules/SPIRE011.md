# SPIRE011: Discriminated union pattern field type mismatch

| Property    | Value        |
|-------------|--------------|
| Rule ID     | SPIRE011     |
| Category    | Correctness  |
| Severity    | Error        |
| Enabled     | Yes          |

## Description

A positional pattern on a struct discriminated union uses a typed binding that doesn't match the variant's actual field type. The `Deconstruct` method returns `object?` for shared-arity overloads, so the C# compiler won't catch type mismatches — this analyzer does.

### Code fix

"Fix field type" — replaces the incorrect type with the variant's declared field type.

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
    (Shape.Kind.Circle, string bad) => 1,  // SPIRE011: field is 'double', not 'string'
    (Shape.Kind.Square, int x) => 2,
};
```

### Compliant code

```csharp
var result = s switch
{
    (Shape.Kind.Circle, double r) => 1,
    (Shape.Kind.Square, int x) => 2,
};
```

## When to suppress

Should not be suppressed — a type mismatch is always a bug.
