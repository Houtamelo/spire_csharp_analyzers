# SPIRE001: Non-empty array of [MustBeInit] struct produces default instances

| Property    | Value        |
|-------------|--------------|
| Rule ID     | SPIRE001     |
| Category    | Correctness  |
| Severity    | Error        |
| Enabled     | Yes          |

## Description

Types marked with `[MustBeInit]` are expected to be explicitly initialized before use. This rule detects all ways of creating arrays (or array-like collections) of such types where elements would be `default(T)` — the uninitialized state the attribute is meant to prevent.

For enums marked with `[MustBeInit]`, the rule only flags when the enum has no zero-valued named member. When a zero member exists (e.g., `None = 0`), `default(T)` produces that valid variant and is not flagged.

### Flagged patterns

**Array creation syntax:**
- `new T[n]` where `n` is not known to be zero
- `new T[n, m, ...]` where no dimension is known to be zero
- `stackalloc T[n]` where `n` is not known to be zero

**API methods:**
- `Array.CreateInstance(typeof(T), n, ...)` — runtime array creation
- `Array.Resize<T>(ref arr, n)` — may expand with default elements
- `GC.AllocateArray<T>(n)` — zero-initialized allocation
- `GC.AllocateUninitializedArray<T>(n)` — uninitialized allocation
- `ArrayPool<T>.Shared.Rent(n)` / `pool.Rent(n)` — pooled array (may contain stale data)

**Property assignments:**
- `ImmutableArray<T>.Builder.Count = n` — expanding count exposes default slots

### Not flagged

- `new T[0]`, `new T[0, n]` — empty arrays (any dimension known zero)
- `new T[] { ... }`, `new T[n] { ... }` — arrays with explicit initializers
- `stackalloc T[0]`, `stackalloc T[] { ... }` — empty or initialized stackallocs
- `Array.Empty<T>()` — empty array (method call, no array creation)
- API calls with size known to be zero
- Arrays of structs without `[MustBeInit]`

## Examples

### Violating code

```csharp
[MustBeInit]
struct Config { public string Name; public Config(string name) => Name = name; }

var arr = new Config[3];                           // SPIRE001
Array.CreateInstance(typeof(Config), 5);           // SPIRE001
GC.AllocateArray<Config>(10);                      // SPIRE001
ArrayPool<Config>.Shared.Rent(8);                  // SPIRE001

var builder = ImmutableArray.CreateBuilder<Config>();
builder.Count = 5;                                 // SPIRE001
```

### Compliant code

```csharp
var arr = new Config[] { new("a"), new("b") };     // explicit initializer
var empty = new Config[0];                         // empty array
var empty2 = Array.Empty<Config>();                 // empty array

// suppress when initializing immediately after
#pragma warning disable SPIRE001
var buf = new Config[count];
#pragma warning restore SPIRE001
for (int i = 0; i < count; i++)
    buf[i] = new Config($"item-{i}");
```

## When to suppress

Suppress when you intend to immediately initialize all elements after creation (e.g., in a loop) and the default state is acceptable as a transient value.
