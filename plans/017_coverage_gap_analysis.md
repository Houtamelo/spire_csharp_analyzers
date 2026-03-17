# 017: Default-Initialization Coverage Gap Analysis

Research into all C# patterns that produce default-initialized `[MustBeInit]` structs,
compared against SPIRE001-SPIRE004.

## Current Coverage

| Rule    | Detects |
|---------|---------|
| SPIRE001 | Array/collection allocation: `new T[n]`, `stackalloc`, `Array.CreateInstance`, `Array.Resize`, `GC.AllocateArray`, `GC.AllocateUninitializedArray`, `ArrayPool.Rent`, `ImmutableArray.Builder.Count` |
| SPIRE003 | `default(T)` and `default` literal (excluding equality comparisons) |
| SPIRE004 | `new T()` without user-defined parameterless ctor or incomplete field initializers |

SPIRE002 is a warning about `[MustBeInit]` on fieldless types — not a detection rule.

## Gaps

### P0 — Uninitialized fields of `[MustBeInit]` type in containing types

```csharp
class Container {
    MustBeInitStruct _field; // zero-initialized by CLR, never explicitly set
}

struct Outer {
    MustBeInitStruct _inner; // same problem
}
```

The CLR zero-initializes all fields. If a class/struct has a field of a `[MustBeInit]` type
and no constructor assigns it (and no field initializer exists), the field silently gets
`default`. This is the most common and dangerous gap — it's the implicit default that
`[MustBeInit]` exists to prevent.

**Detection strategy:** For every field/auto-property whose type is `[MustBeInit]`:
- Check if it has a field/property initializer → OK
- Check if ALL constructors assign it → OK
- Otherwise → flag the field declaration

**Complexity:** Medium-High. Requires tracking constructor assignment paths, handling
constructor chaining, and dealing with partial initialization.

**Considerations:**
- Classes with no explicit constructor have an implicit parameterless ctor that assigns nothing
- Structs with no explicit constructor have an implicit default ctor that zeros everything
- Constructor chaining: `Ctor(int x) : this(x, "default")` — if the chained ctor assigns it, OK
- Partial classes: all constructors across all parts must assign the field
- Record types: primary constructor parameters generate assignments automatically

---

### P1 — `Activator.CreateInstance`

```csharp
Activator.CreateInstance<Config>();           // default struct
Activator.CreateInstance(typeof(Config));     // default struct (boxed)
```

Creates a default instance for value types. Common in generic utility code, serializers,
and DI containers.

**Detection strategy:** Register on `IInvocationOperation` for:
- `Activator.CreateInstance<T>()` where T is `[MustBeInit]`
- `Activator.CreateInstance(Type)` where the Type argument resolves to a `[MustBeInit]` struct

**Complexity:** Low. Straightforward method signature matching.

---

### P1 — `CollectionsMarshal.SetCount`

```csharp
var list = new List<Config>();
CollectionsMarshal.SetCount(list, 10); // 10 default Config items
```

.NET 8+ API. Extends a `List<T>` by exposing default-initialized slots directly.
Semantically identical to array allocation for our purposes.

**Detection strategy:** Register on `IInvocationOperation` for
`CollectionsMarshal.SetCount<T>(List<T>, int)` where T is `[MustBeInit]`.

**Complexity:** Low.

---

### P2 — `Array.Clear` / `Span<T>.Clear()`

```csharp
Array.Clear(configs, 0, configs.Length); // zeros all elements
configs.AsSpan().Clear();
```

Resets existing elements to `default`. Semantically "destruction of initialized data" rather
than "creation of new defaults", but the end result is the same: `[MustBeInit]` instances
in the default state.

**Detection strategy:** Register on `IInvocationOperation` for:
- `Array.Clear(Array)` and `Array.Clear(Array, int, int)` where element type is `[MustBeInit]`
- `Span<T>.Clear()` / `Memory<T>.Span` + `.Clear()` where T is `[MustBeInit]`

**Complexity:** Low-Medium. Need to resolve the element type from the array/span.

---

### P2 — `Unsafe.SkipInit<T>`

```csharp
Unsafe.SkipInit(out Config c); // c is uninitialized garbage
```

Worse than default — leaves memory completely uninitialized. Niche but dangerous.

**Detection strategy:** Register on `IInvocationOperation` for
`Unsafe.SkipInit<T>(out T)` where T is `[MustBeInit]`.

**Complexity:** Low.

---

### P3 — Generic `new T()` resolving to `[MustBeInit]`

```csharp
T Create<T>() where T : new() => new T();
var cfg = Create<Config>(); // default Config at runtime
```

When `new T()` appears in generic code, the compiler emits `Activator.CreateInstance<T>()`.
The definition site only sees the type parameter. To detect this:
- Option A: Analyze at instantiation sites (complex, requires whole-program analysis)
- Option B: Flag all generic `new T()` where T could be a struct (too noisy)
- Option C: Accept this as out-of-scope for a static analyzer

**Complexity:** Very High. Likely out-of-scope.

---

### P3 — Constructor chaining with `: this()`

```csharp
[MustBeInit]
struct Config {
    public string Name;
    public Config(string name) : this() { Name = name; }
}
```

`: this()` on a struct without a user-defined parameterless ctor zero-initializes all fields.
The body typically overwrites them, making this benign in practice. Flagging it would be noisy
since `: this()` is the standard C# pattern for satisfying definite assignment in struct
constructors.

**Verdict:** Likely not worth flagging. The body must assign all fields anyway (compiler
enforces definite assignment for structs in older C# versions). In newer versions with field
initializers, the semantics are different.

---

### P3 — `RuntimeHelpers.GetUninitializedObject`

```csharp
var obj = RuntimeHelpers.GetUninitializedObject(typeof(Config));
```

Bypasses all constructors. Used internally by serialization frameworks. Very rare in
application code.

**Detection strategy:** Same as Activator — match method signature.

**Complexity:** Low, but low value.

---

### P3 — Inline arrays containing `[MustBeInit]` elements

```csharp
[InlineArray(10)]
struct Buffer { Config _element; }
var buf = new Buffer(); // 10 default Config instances
```

If only the element type `Config` has `[MustBeInit]` (not `Buffer` itself), this slips
through all existing rules. Detecting this requires understanding inline array semantics
and checking whether the single field's type is `[MustBeInit]`.

**Complexity:** Medium. Niche feature.

---

### P3 — `Nullable<T>.GetValueOrDefault()`

```csharp
Config? maybe = null;
Config c = maybe.GetValueOrDefault(); // returns default(Config)
```

Returns `default(T)` when `HasValue` is false. Could be flagged but may be noisy —
`GetValueOrDefault()` is extremely common and often guarded by `HasValue` checks.

**Verdict:** Probably not worth a dedicated rule. The caller typically checks `HasValue` first.

---

## Patterns Already Covered Indirectly

These patterns produce defaults but are caught by existing rules:

| Pattern | Caught by |
|---------|-----------|
| `Config c = default;` | SPIRE003 |
| `return default;` | SPIRE003 |
| `Foo(default)` | SPIRE003 |
| `condition ? x : default` | SPIRE003 |
| `void Process(Config c = default)` | SPIRE003 (default literal) |
| `Enumerable.Repeat(default(Config), n)` | SPIRE003 (default expression) |
| `Array.Fill(arr, default(Config))` | SPIRE003 (default expression) |
| `new Config[n]` | SPIRE001 |
| `stackalloc Config[n]` | SPIRE001 |
| `new Config()` (no parameterless ctor) | SPIRE004 |
| `new Config { }` | SPIRE004 |

## Recommended Rule Assignments

| New Rule | Gap | Severity |
|----------|-----|----------|
| SPIRE005 | Uninitialized fields of `[MustBeInit]` type in containing types | Error |
| SPIRE006 | `Activator.CreateInstance` on `[MustBeInit]` struct | Error |
| SPIRE007 | `CollectionsMarshal.SetCount` on `List<[MustBeInit]>` | Error |
| SPIRE008 | `Array.Clear` / `Span.Clear` on `[MustBeInit]` elements | Warning |
| SPIRE009 | `Unsafe.SkipInit` on `[MustBeInit]` struct | Error |

P3 items (generic `new T()`, ctor chaining, inline arrays, `Nullable.GetValueOrDefault`)
are either out-of-scope, too noisy, or too niche to warrant dedicated rules at this time.
