# Spire.Analyzers

Roslyn analyzers for C# struct correctness.

## Installation

```shell
dotnet add package Spire.Analyzers
```

## Rules

| Rule                               | Description                                                          |
|------------------------------------|----------------------------------------------------------------------|
| [SPIRE001](docs/rules/SPIRE001.md) | Non-empty array of `[MustBeInit]` struct produces default instances  |
| [SPIRE002](docs/rules/SPIRE002.md) | `[MustBeInit]` on fieldless type has no effect                       |
| [SPIRE003](docs/rules/SPIRE003.md) | `default(T)` where T is a `[MustBeInit]` struct                      |
| [SPIRE004](docs/rules/SPIRE004.md) | `new T()` on `[MustBeInit]` struct without parameterless constructor |
| [SPIRE005](docs/rules/SPIRE005.md) | `Activator.CreateInstance` on `[MustBeInit]` struct                  |
| [SPIRE006](docs/rules/SPIRE006.md) | Clearing array or span of `[MustBeInit]` struct                      |
| [SPIRE007](docs/rules/SPIRE007.md) | `Unsafe.SkipInit` on `[MustBeInit]` struct                           |
| [SPIRE008](docs/rules/SPIRE008.md) | `RuntimeHelpers.GetUninitializedObject` on `[MustBeInit]` struct     |

## Discriminated Unions — Layout Strategy Comparison

N=1000, .NET 10.0, AMD Ryzen 9 9900X. Full results in [docs/benchmark-results/](docs/benchmark-results/).

**Legend**: Ctor = Construction, GC = Garbage Collector, Alloc = Allocation, Prop = Property Pattern, Decon = Deconstruct

| Layout | Ctor Speed | Ctor Alloc | Prop Match | Decon Match | Decon Alloc | Copy | JSON Deser | Generics | Worst Case |
|---|---|---|---|---|---|---|---|---|---|
| **Additive** | Fast | Moderate (86 KB) | Fastest | Slow (boxing) | 18 KB | Slow (large struct) | Moderate | Yes | Wide types: slot explosion |
| **Overlap** | Fast | Moderate (86 KB) | Fastest | Slow (boxing) | 18 KB | Slow (large struct) | Moderate | No | Managed fields: no overlap benefit |
| **UnsafeOverlap** | Moderate | Moderate (86 KB) | Fastest | Slow (boxing) | 18 KB | Slow (large struct) | Moderate | No | Same as Overlap, needs unsafe |
| **BoxedTuple** | Fast | Low (35 KB) | Slow (cast) | Fast (no boxing) | None | Fast (16B struct) | Moderate | Yes | Uniform distribution: 1.3-1.4x |
| **BoxedFields** | Slow | High (168 KB) | Slowest | Fast (no boxing) | None | Slowest | Moderate | Yes | Most categories; wins Decon alloc |
| **Record** | Fastest | Low (36 KB) | Fast (uniform) | Fastest | None | Fastest (8B ref) | Best (46% less alloc) | Yes | Skewed: 1.2x; Many variants: 2.9x |
| **Class** | Fastest | Low (36 KB) | Fast (uniform) | Fastest | None | Fastest (8B ref) | Best (46% less alloc) | Yes | Skewed: 1.2x; Many variants: 2.9x |

### Choosing a layout

- **Default choice**: `Layout.Additive` — fastest struct match, zero-alloc property access, works with generics.
- **Need reference semantics or JSON**: `record` — fastest construction, best JSON deserialization, natural C# pattern matching. Trades GC pressure for speed.
- **No generics, unmanaged-heavy**: `Layout.Overlap` — identical match speed to Additive, true field overlap for minimal struct size. Cannot handle generic type parameters.
- **Skewed variant distribution** (80%+ one variant): `Layout.BoxedTuple` — fieldless fast-path is sub-nanosecond. Poor uniform performance.
- **Avoid**: `Layout.BoxedFields` — outperformed by Additive in most categories. Only advantage: zero-alloc Deconstruct (fields already boxed). Legacy layout.

## License

[MIT](LICENSE)
