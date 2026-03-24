# SPIRE003: default(T) where T is a [MustBeInit] struct

| Property    | Value        |
|-------------|--------------|
| Rule ID     | SPIRE003     |
| Category    | Correctness  |
| Severity    | Error        |
| Enabled     | Yes          |

## Description

Types marked with `[MustBeInit]` are expected to be explicitly initialized before use. Using `default(T)` or the `default` literal produces an uninitialized value, defeating the purpose of the attribute. This rule flags all locations where a `[MustBeInit]` type is produced via `default`.

For enums marked with `[MustBeInit]`, the rule only flags when the enum has no zero-valued named member. When a zero member exists (e.g., `None = 0`), `default(T)` produces that valid variant and is not flagged.

### Flagged patterns

- `default(T)` where T is a `[MustBeInit]` struct
- `default` literal in contexts where the target type is a `[MustBeInit]` struct:
  - Variable declaration: `Config c = default;`
  - Return statement: `return default;` (when return type is `[MustBeInit]`)
  - Ternary/null-coalescing producing `default`
  - Method argument: `Foo(default)` where parameter type is `[MustBeInit]`
  - Assignment: `c = default;`

### Not flagged

- `default` for non-`[MustBeInit]` types
- `[MustBeInit]` types that have no instance fields (fieldless types)
- `default` used in equality comparisons (`x == default`) — this is a detection/comparison pattern, not a creation pattern

## Examples

### Violating code

```csharp
[MustBeInit]
struct Config { public string Name; public Config(string name) => Name = name; }

Config c = default;                          // SPIRE003
var c2 = default(Config);                    // SPIRE003
Config GetDefault() => default;              // SPIRE003
void Use(Config c) { }
Use(default);                                // SPIRE003
```

### Compliant code

```csharp
Config c = new Config("name");               // explicit initialization
var c2 = new Config("name");                 // explicit initialization
bool isDefault = c == default;               // comparison, not creation

// suppress when the default is intentional
#pragma warning disable SPIRE003
Config sentinel = default;
#pragma warning restore SPIRE003
```

## When to suppress

Suppress when you intentionally need a default instance as a sentinel value or placeholder, and you understand that the struct will be in an uninitialized state.
