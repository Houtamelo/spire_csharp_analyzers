# SPIRE007: Unsafe.SkipInit on [MustBeInit] struct

| Property    | Value        |
|-------------|--------------|
| **ID**      | SPIRE007     |
| **Category**| Correctness  |
| **Severity**| Error        |
| **Enabled** | Yes          |

## Description

`Unsafe.SkipInit<T>(out T)` bypasses zero-initialization entirely, leaving the value as
whatever was previously in that memory location. When T is a type marked with `[MustBeInit]`,
this is worse than default — the value contains garbage data.

For enums, this is always flagged regardless of whether a zero-valued member exists — garbage data is never a valid variant.

### Flagged patterns

- `Unsafe.SkipInit(out MustInitStruct s)` where `MustInitStruct` is a `[MustBeInit]` struct with fields

### Not flagged

- Types not marked `[MustBeInit]`
- Fieldless `[MustBeInit]` structs (SPIRE002 handles this)
- Generic `Unsafe.SkipInit<T>(out T)` where T is a type parameter

## Examples

### Violating code

```csharp
[MustBeInit]
struct Config { public string Name; public Config(string name) => Name = name; }

Unsafe.SkipInit(out Config c); // SPIRE007
```

### Compliant code

```csharp
var c = new Config("default");  // proper initialization
```

## When to suppress

Suppress only in performance-critical code where you guarantee the struct will be fully
initialized before any read.

```csharp
#pragma warning disable SPIRE007
Unsafe.SkipInit(out Config c);
#pragma warning restore SPIRE007
c = new Config("value"); // initialized immediately after
```
