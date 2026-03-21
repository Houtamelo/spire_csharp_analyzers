# SPIRE015: Exhaustive Enum Switch

| Property    | Value           |
|-------------|-----------------|
| **ID**      | SPIRE015        |
| **Category**| Correctness     |
| **Severity**| Error           |
| **Enabled** | Yes             |

## Description

Switch statements and expressions on enum types marked with `[EnforceExhaustiveness]` must explicitly handle every named member. A `default` or discard (`_`) arm does not count as coverage.

For `[Flags]` enums, handling a composite value covers its constituent bit members. Alias members (same underlying value) share coverage.

## Examples

### Violating code

```csharp
[EnforceExhaustiveness]
enum Color { Red, Green, Blue }

int Score(Color c) => c switch  // SPIRE015: does not handle member(s): Blue
{
    Color.Red => 1,
    Color.Green => 2,
    _ => 0,
};
```

### Compliant code

```csharp
int Score(Color c) => c switch
{
    Color.Red => 1,
    Color.Green => 2,
    Color.Blue => 3,
    _ => 0,  // allowed, just doesn't count as coverage
};
```

## When to Suppress

Suppress when you intentionally handle a subset of members and rely on `default` for the rest, accepting that new members added to the enum won't trigger a compile-time error.

```csharp
#pragma warning disable SPIRE015
// suppressed code
#pragma warning restore SPIRE015
```
