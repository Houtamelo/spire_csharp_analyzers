# SPIRE015: Exhaustive Enum Switch

| Property    | Value           |
|-------------|-----------------|
| **ID**      | SPIRE015        |
| **Category**| Correctness     |
| **Severity**| Error           |
| **Enabled** | Yes             |

## Description

Switch statements and expressions on enum types marked with `[EnforceExhaustiveness]` must explicitly handle every named member — unless an unguarded catch-all arm is present.

An unguarded **catch-all** opts out of the check:

- `default:` in a switch statement
- `_ => ...` in a switch expression
- `var x => ...` / `case var x:` (var declaration pattern)

Guarded catch-alls (e.g. `_ when cond => ...`) do NOT opt out — the guard may fail, leaving members uncovered.

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
};
```

### Compliant code

Handle every member explicitly:

```csharp
int Score(Color c) => c switch
{
    Color.Red => 1,
    Color.Green => 2,
    Color.Blue => 3,
};
```

Or opt out with a catch-all:

```csharp
int Score(Color c) => c switch
{
    Color.Red => 1,
    Color.Green => 2,
    _ => 0,  // catch-all — SPIRE015 does not fire
};
```

## When to Suppress

Using an unguarded catch-all is the preferred way to opt out — no suppression needed. If you want to keep the analyzer silent without a catch-all (e.g. while refactoring), use:

```csharp
#pragma warning disable SPIRE015
// suppressed code
#pragma warning restore SPIRE015
```
