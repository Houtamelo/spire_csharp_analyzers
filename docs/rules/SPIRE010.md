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
    { IsCircle: true, circle: var (r) } => $"circle r={r}",
    _ => "other",  // cursor here → lightbulb offers "Replace wildcard with explicit variants"
};
```

### Equivalent explicit handling

```csharp
string Describe(Shape s) => s switch
{
    { IsCircle: true, circle: var (r) } => $"circle r={r}",
    { IsRectangle: true, rectangle: var (width, height) } => "other",
    { IsSquare: true, square: var (sideLength) } => "other",
};
```

The wildcard's original expression (`"other"`) is preserved for each generated arm.

The example uses the canonical predicate and payload deconstruction style.
The refactoring currently emits equivalent `kind` checks with nested
`field: var local` captures. These captures match null and preserve static
field types; local names are uniquified against the enclosing scope.
Record/class generation is unchanged.
