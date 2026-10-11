# SPIRE010: Expand wildcard to explicit variant arms (Refactoring)

| Property    | Value                  |
|-------------|------------------------|
| Rule ID     | SPIRE010               |
| Type        | Code Refactoring       |
| Trigger     | Cursor on wildcard arm |

## Description

A code refactoring (not a diagnostic) that offers to replace a wildcard (`_`) arm in a switch expression on a discriminated union with explicit arms for each variant the wildcard covers.

No diagnostic is reported — wildcards are valid. The refactoring appears in the IDE lightbulb menu when the cursor is on a wildcard arm.

## Example

### Before refactoring

```csharp
string Describe(Shape s) => s switch
{
    (Shape.Kind.Circle, double r) => $"circle r={r}",
    _ => "other",  // cursor here → lightbulb offers "Replace wildcard with explicit variants"
};
```

### After refactoring

```csharp
string Describe(Shape s) => s switch
{
    (Shape.Kind.Circle, double r) => $"circle r={r}",
    { kind: Shape.Kind.Rectangle, width: var width, height: var height } => "other",
    { kind: Shape.Kind.Square, sideLength: var sideLength } => "other",
};
```

The wildcard's original expression (`"other"`) is preserved for each generated arm.

Every new struct arm uses property patterns with `property: var local` captures.
This matches null and preserves the property's static type, including when a
positional `Deconstruct` overload would expose `object?`. Qualified property names
do not add the variant prefix to the local capture. Record/class generation is
unchanged.
