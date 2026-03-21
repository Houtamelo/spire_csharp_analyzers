# SPIRE010: Expand wildcard to explicit variant arms (Refactoring)

| Property    | Value                  |
|-------------|------------------------|
| Rule ID     | SPIRE010               |
| Type        | Code Refactoring       |
| Trigger     | Cursor on wildcard arm |

## Description

A code refactoring (not a diagnostic) that offers to replace a wildcard (`_`) or `default:` arm in a switch on a discriminated union with explicit typed arms for each variant the wildcard covers.

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
    (Shape.Kind.Rectangle, float width, float height) => "other",
    (Shape.Kind.Square, int sideLength) => "other",
};
```

The wildcard's original expression (`"other"`) is preserved for each generated arm.
