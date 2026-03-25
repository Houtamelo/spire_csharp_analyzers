# SPIRE006: Clearing array or span of [EnforceInitialization] struct

| Property    | Value        |
|-------------|--------------|
| **ID**      | SPIRE006     |
| **Category**| Correctness  |
| **Severity**| Error        |
| **Enabled** | Yes          |

## Description

`Array.Clear` and `Span<T>.Clear()` reset elements to `default(T)`. When T is a type
marked with `[EnforceInitialization]`, this produces uninitialized values — the same state the
attribute exists to prevent.

For enums marked with `[EnforceInitialization]`, the rule only flags when the enum has no zero-valued named member.

### Flagged patterns

- `Array.Clear(array)` where the array's element type is a `[EnforceInitialization]` struct
- `Array.Clear(array, index, length)` — same check
- `Span<T>.Clear()` where T is a `[EnforceInitialization]` struct

### Not flagged

- Arrays/spans of types not marked `[EnforceInitialization]`
- Fieldless `[EnforceInitialization]` structs (SPIRE002 handles this)
- `Array` typed variable where element type can't be resolved
- Generic `Span<T>.Clear()` where T is a type parameter

## Examples

### Violating code

```csharp
[EnforceInitialization]
struct Config { public string Name; public Config(string name) => Name = name; }

var configs = new[] { new Config("a"), new Config("b") };
Array.Clear(configs);              // SPIRE006
configs.AsSpan().Clear();          // SPIRE006
```

### Compliant code

```csharp
var plainArray = new int[] { 1, 2, 3 };
Array.Clear(plainArray);           // not [EnforceInitialization]
```

## When to suppress

Suppress when clearing is intentional (e.g., security-sensitive data that must be zeroed).

```csharp
#pragma warning disable SPIRE006
Array.Clear(configs);
#pragma warning restore SPIRE006
```
