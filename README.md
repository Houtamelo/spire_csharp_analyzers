# Houtamelo.Spire

Roslyn analyzers and source generators for C# type safety — struct initialization enforcement, discriminated unions, and exhaustive switch checking.

## Installation

```shell
dotnet add package Houtamelo.Spire
```

## Rules

| Rule                               | Description                                                                     |
|------------------------------------|---------------------------------------------------------------------------------|
| [SPIRE001](docs/rules/SPIRE001.md) | Non-empty array of `[EnforceInitialization]` struct produces default instances  |
| [SPIRE002](docs/rules/SPIRE002.md) | `[EnforceInitialization]` on fieldless type has no effect                       |
| [SPIRE003](docs/rules/SPIRE003.md) | `default(T)` where T is an `[EnforceInitialization]` type                       |
| [SPIRE004](docs/rules/SPIRE004.md) | `new T()` on `[EnforceInitialization]` struct without parameterless constructor |
| [SPIRE005](docs/rules/SPIRE005.md) | `Activator.CreateInstance` on `[EnforceInitialization]` struct                  |
| [SPIRE006](docs/rules/SPIRE006.md) | Clearing array or span of `[EnforceInitialization]` type                        |
| [SPIRE007](docs/rules/SPIRE007.md) | `Unsafe.SkipInit` on `[EnforceInitialization]` struct                           |
| [SPIRE008](docs/rules/SPIRE008.md) | `RuntimeHelpers.GetUninitializedObject` on `[EnforceInitialization]` struct     |
| [SPIRE009](docs/rules/SPIRE009.md) | Switch does not handle all variants of discriminated union                      |
| [SPIRE010](docs/rules/SPIRE010.md) | Expand wildcard to explicit variant arms (refactoring)                          |
| [SPIRE011](docs/rules/SPIRE011.md) | Discriminated union pattern field type mismatch                                 |
| [SPIRE012](docs/rules/SPIRE012.md) | Discriminated union pattern field count mismatch                                |
| [SPIRE013](docs/rules/SPIRE013.md) | Accessing another variant's field                                               |
| [SPIRE014](docs/rules/SPIRE014.md) | Accessing variant field without kind guard                                      |
| [SPIRE015](docs/rules/SPIRE015.md) | Exhaustive enum switch                                                          |
| [SPIRE016](docs/rules/SPIRE016.md) | Cast to `[EnforceInitialization]` enum may produce invalid variant              |

## Discriminated Unions

The source generator provides:

- `Kind` enum and `kind` property for variant discrimination.
- Factory methods (`Shape.Circle(5.0)`) and `IsVariant` properties (`shape.IsCircle`).
- Property-pattern and `Deconstruct` support for `switch`.
- Optional JSON serialization (System.Text.Json, Newtonsoft.Json).
- Exhaustive switch checking (SPIRE009) and field access safety (SPIRE011-014).
- 5 struct memory layouts + record unions.

Define a struct union with `[DiscriminatedUnion]`:

```csharp
using Houtamelo.Spire;

[DiscriminatedUnion(Layout.Additive)]
partial struct Shape
{
    [Variant] public static partial Shape Circle(double radius);
    [Variant] public static partial Shape Square(int side);
    [Variant] public static partial Shape Point();
}
```

Construct and match:

```csharp
Shape shape = Shape.Circle(5.0);

// Property pattern
var area = shape switch
{
    { kind: Shape.Kind.Circle, radius: var r } => Math.PI * r * r,
    { kind: Shape.Kind.Square, side: var s }   => s * s,
    { kind: Shape.Kind.Point }                 => 0,
};

// Deconstruct pattern
area = shape switch 
{
    (Shape.Kind.Circle, radius: var r) => Math.PI * r * r,
    (Shape.Kind.Square, side: var s)   => s * s,
    (Shape.Kind.Point, _)              => 0,
};
```

Record types are also supported:

```csharp
[DiscriminatedUnion]
partial abstract record Shape
{
    public partial record Circle(double radius) : Shape;
    public partial record Square(int side) : Shape;
    public partial record Point() : Shape;
}
```

See [docs/discriminated-unions.md](docs/discriminated-unions.md) for the full guide.

## EnforceExhaustiveness

The analyzer provides:

- Exhaustive switch checking over enum members or sealed subtypes (SPIRE015).
- Invalid enum cast detection (SPIRE016).
- Inherits all `[EnforceInitialization]` rules (SPIRE001-008) — prevents `default(T)`, uninitialized arrays, etc.
- Works on enums, abstract classes, and interfaces.

Apply `[EnforceExhaustiveness]` to a type to require exhaustive switches:

```csharp
using Houtamelo.Spire.Core;

[EnforceExhaustiveness]
public abstract class Animal { }
public sealed class Dog : Animal { }
public sealed class Cat : Animal { }

// SPIRE015: switch does not handle 'Cat'
string Describe(Animal a) => a switch
{
    Dog => "dog",
};
```

See [docs/exhaustiveness.md](docs/exhaustiveness.md) for the full guide.

## Discriminated Unions — Layout Strategy Comparison

N=1000, .NET 11.0, AMD Ryzen 9 9900X. Full results in [docs/benchmark-results/](docs/benchmark-results/).

**Legend**: Ctor = Construction, Prop = Property Pattern, Decon = Deconstruct

| Layout             | Ctor Speed | Ctor Alloc | Prop Match  | Decon Match   | Decon Alloc | Copy         | Generics |
|--------------------|------------|------------|-------------|---------------|-------------|--------------|----------|
| **Additive**       | Fast       | Moderate   | Fastest     | Slow (boxing) | Yes         | Slow (large) | Yes      |
| **Overlap**        | Fast       | Moderate   | Fastest     | Slow (boxing) | Yes         | Slow (large) | No       |
| **UnsafeOverlap**  | Moderate   | Moderate   | Fastest     | Slow (boxing) | Yes         | Slow (large) | No       |
| **BoxedTuple**     | Fast       | Low        | Slow (cast) | Fast          | None        | Fast (16B)   | Yes      |
| **BoxedFields**    | Slow       | High       | Slowest     | Fast          | None        | Slowest      | Yes      |
| **Record**         | Fastest    | Low        | Fast        | Fastest       | None        | Fastest (8B) | Yes      |
| **Native (C# 15)** | Fast       | Low (heap) | Moderate    | Moderate      | None        | Fast (ref)   | Yes      |

### Choosing a layout

- **Default choice**: `Layout.Additive` — fastest struct match, zero-alloc property access, works with generics.
- **Reference semantics or JSON**: `record` — fastest construction, best JSON deserialization, natural C# pattern matching. Trades GC pressure for
  speed.
- **No generics, unmanaged-heavy**: `Layout.Overlap` — true field overlap for minimal struct size. Cannot handle generic type parameters.
- **Skewed variant distribution** (80%+ one variant): `Layout.BoxedTuple` — fieldless fast-path is sub-nanosecond. Poor uniform performance.
- **C# 15 native unions**: comparable to Additive/Record depending on usage; avoids source generator overhead.
- **Avoid**: `Layout.BoxedFields` — outperformed by Additive in most categories. Only advantage: zero-alloc Deconstruct (fields already boxed).

## License

[MIT](LICENSE)
