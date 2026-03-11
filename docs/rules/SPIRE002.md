# SPIRE002: [MustBeInit] on fieldless type has no effect

| Property    | Value        |
|-------------|--------------|
| Rule ID     | SPIRE002     |
| Category    | Correctness  |
| Severity    | Warning      |
| Enabled     | Yes          |

## Description

The `[MustBeInit]` attribute marks struct types whose default value should be considered uninitialized. However, a struct with no instance fields has only one possible value — the default — so the attribute serves no purpose. This warning helps avoid false confidence that `[MustBeInit]` is providing protection when there is nothing to protect.

**What counts as an instance field:**
- Explicit instance fields (`public int X;`)
- Auto-property backing fields (`public int X { get; set; }`, `public int X { get; }`, `public int X { get; init; }`)
- Record struct positional parameters (`record struct S(int X)`)

**What does NOT count:**
- Non-auto (computed) properties (`int X => 42;`, `int X { get => 42; }`)
- Static fields and static auto-properties
- Constants (`const int X = 1;`)
- Methods, indexers, constructors

## Examples

### Violating code

```csharp
[MustBeInit]                              // SPIRE002
struct Empty { }

[MustBeInit]                              // SPIRE002
record struct EmptyRecord;

[MustBeInit]                              // SPIRE002
struct OnlyComputed
{
    public int Value => 42;
}

[MustBeInit]                              // SPIRE002
struct OnlyStatic
{
    public static int Count;
    public const int Max = 100;
}
```

### Compliant code

```csharp
[MustBeInit]
struct Config
{
    public string Name;
}

[MustBeInit]
struct WithAutoProperty
{
    public int Value { get; set; }
}

[MustBeInit]
record struct Point(int X, int Y);
```

## When to suppress

Suppress if you intentionally use `[MustBeInit]` as a documentation marker regardless of whether the type has fields — for example, to signal intent for a type that will gain fields in the future.

```csharp
#pragma warning disable SPIRE002
[MustBeInit]
struct PlaceholderForFutureFields { }
#pragma warning restore SPIRE002
```
