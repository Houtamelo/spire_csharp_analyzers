# SPIRE005: Activator.CreateInstance on [MustBeInit] struct

| Property    | Value        |
|-------------|--------------|
| **ID**      | SPIRE005     |
| **Category**| Correctness  |
| **Severity**| Error        |
| **Enabled** | Yes          |

## Description

`Activator.CreateInstance<T>()` and `Activator.CreateInstance(typeof(T))` produce default
(zeroed) instances of value types without calling any constructor. When T is a type marked
with `[MustBeInit]`, this bypasses the required initialization the attribute is meant to
enforce.

For enums marked with `[MustBeInit]`, the rule only flags when the enum has no zero-valued named member.

### Flagged patterns

- `Activator.CreateInstance<T>()` where T is a `[MustBeInit]` struct
- `Activator.CreateInstance(typeof(T))` where T is a `[MustBeInit]` struct

### Not flagged

- `Activator.CreateInstance<T>()` where T is not `[MustBeInit]`
- `Activator.CreateInstance(typeof(T))` where T is not a `[MustBeInit]` struct
- `Activator.CreateInstance` with constructor arguments (calls a real constructor)
- Fieldless `[MustBeInit]` structs (SPIRE002 handles this case)

## Examples

### Violating code

```csharp
[MustBeInit]
struct Config { public string Name; public Config(string name) => Name = name; }

var c = Activator.CreateInstance<Config>();                // SPIRE005
var c2 = (Config)Activator.CreateInstance(typeof(Config)); // SPIRE005
```

### Compliant code

```csharp
var c = new Config("name");              // explicit initialization
var plain = Activator.CreateInstance<PlainStruct>(); // not [MustBeInit]
```

## When to suppress

Suppress when the default instance is intentional (e.g., sentinel values, test stubs).

```csharp
#pragma warning disable SPIRE005
var sentinel = Activator.CreateInstance<Config>();
#pragma warning restore SPIRE005
```
