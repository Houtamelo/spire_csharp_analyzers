# Discriminated Unions

The `[DiscriminatedUnion]` source generator turns a `partial struct` or `abstract partial record` into a closed, exhaustively-matched discriminated union type. The generator lives in `Houtamelo.Spire.Analyzers`; all attributes are in the `Houtamelo.Spire` namespace.

## Quick Start

```csharp
using Houtamelo.Spire;

[DiscriminatedUnion(Layout.Additive)]
partial struct Shape
{
    [Variant] public static partial Shape Circle(double radius);
    [Variant] public static partial Shape Square(int side);
    [Variant] public static partial Shape Point();
}

// Construction
Shape s = Shape.Circle(5.0);

// Pattern match (property pattern)
double area = s switch
{
    { kind: Shape.Kind.Circle, radius: var r } => Math.PI * r * r,
    { kind: Shape.Kind.Square, side: var x }   => x * x,
    { kind: Shape.Kind.Point }                 => 0,
};
```

SPIRE009 (error) fires if any variant arm is missing. The "Add missing variant arms" code fix (SPIRE010) fills them in automatically.

## Layout Strategies

All layouts produce identical external APIs. The difference is how variant data is stored in memory. For most use cases, omit the parameter — `Auto` picks Additive.

**Additive** — each variant's fields are stored independently (no aliasing). Size = sum of all variant sizes. Safe for generics, value types, and managed types. Best general-purpose choice. Zero undefined behavior.

**Overlap** — all variants share the same memory region. Size = largest variant. Requires all fields to be unmanaged types. Uses `[FieldOffset]`. Fastest match; smallest footprint for wide unions with value-type fields. Not usable with generic structs (SPIRE_DU005).

**UnsafeOverlap** — like Overlap but uses `Unsafe.AsRef` instead of `[FieldOffset]`, allowing managed types in overlapping positions. Requires `<AllowUnsafeBlocks>true</AllowUnsafeBlocks>` (SPIRE_DU009). Use only when Overlap doesn't fit and you can guarantee no GC-visible aliasing of managed refs.

**BoxedFields** — wraps each variant's fields in a heap-allocated object. Size = one object reference per variant. Useful when field types prevent all inline layouts (e.g., ref structs as fields). Allocates on construction.

**BoxedTuple** — like BoxedFields, but packs all fields into a single `ValueTuple` object per variant. Slightly fewer fields on the union struct at the cost of an extra indirection. Rarely preferable to BoxedFields.

Benchmark comparisons are in [docs/benchmark-results/](benchmark-results/).

## Record Unions

Use `abstract partial record` for heap-allocated unions with C# record semantics. Variants are `sealed partial record` nested types. The `Layout` parameter is ignored for record unions (SPIRE_DU004).

```csharp
using Houtamelo.Spire;

[DiscriminatedUnion]
public abstract partial record Shape
{
    public sealed partial record Circle(double Radius) : Shape;
    public sealed partial record Square(int Side) : Shape;
    public sealed partial record Point() : Shape;
}

// Construction
Shape s = new Shape.Circle(5.0);

// Pattern match (type pattern)
double area = s switch
{
    Shape.Circle { Radius: var r } => Math.PI * r * r,
    Shape.Square { Side: var x }   => x * x,
    Shape.Point                    => 0,
};
```

Record unions box on every construction. Use struct unions when allocation matters.

## Pattern Matching

**Property pattern** (struct unions):

```csharp
s switch
{
    { kind: Shape.Kind.Circle, radius: var r } => ...,
    { kind: Shape.Kind.Square, side: var x }   => ...,
    { kind: Shape.Kind.Point }                 => ...,
};
```

**Deconstruct pattern** (struct unions, enabled by default via `GenerateDeconstruct = true`):

```csharp
s switch
{
    (Shape.Kind.Circle, var r) => ...,
    (Shape.Kind.Square, var x) => ...,
    (Shape.Kind.Point)         => ...,
};
```

`Deconstruct` signature: `void Deconstruct(out Shape.Kind kind, out double radius, out int side)` — all variant fields are out-parameters at every arm; unneeded ones are conventionally discarded with `_`.

## JSON Serialization

```csharp
[DiscriminatedUnion(Layout.Additive, Json = JsonLibrary.SystemTextJson)]
partial struct Shape
{
    [Variant] public static partial Shape Circle(double radius);
    [Variant] public static partial Shape Square(int side);
}
```

Serializes as `{"kind":"Circle","radius":5.0}`. The discriminator field name defaults to `"kind"`; override with `JsonDiscriminator = "type"`. The `[JsonName]` attribute overrides variant and field names in the JSON output:

```csharp
[JsonName("sq")] public static partial Shape Square([JsonName("s")] int side);
// serializes as {"kind":"sq","s":3}
```

Both `SystemTextJson` and `NewtonsoftJson` can be enabled simultaneously:

```csharp
Json = JsonLibrary.SystemTextJson | JsonLibrary.NewtonsoftJson
```

Generator diagnostics: SPIRE_DU006 if `System.Text.Json` is not referenced; SPIRE_DU007 if `Newtonsoft.Json` is not referenced; SPIRE_DU008 if the union is a `ref struct`.

## Generated API

For every struct union the generator produces:

- `Shape.Kind` — enum with one member per variant (`Circle`, `Square`, `Point`)
- `shape.kind` — property returning `Shape.Kind`
- `shape.radius`, `shape.side` — per-variant field properties (valid only when kind matches; SPIRE013/014 guard incorrect access)
- `shape.IsCircle`, `shape.IsSquare`, `shape.IsPoint` — convenience bool properties
- `IDiscriminatedUnion<Shape.Kind>` — implemented unconditionally; exposes `kind` through the interface
- `Deconstruct(out Shape.Kind, ...)` — present when `GenerateDeconstruct = true` (default)
- Factory methods: `Shape.Circle(5.0)`, `Shape.Square(3)`, `Shape.Point()`

`IDiscriminatedUnion<TKind>` and `SpireLINQ.OfKind` live in `Houtamelo.Spire.Core`:

```csharp
using Houtamelo.Spire.Core;

IEnumerable<Shape> shapes = ...;
IEnumerable<Shape> circles = shapes.OfKind(Shape.Kind.Circle);
```

## Analyzer Rules

| Rule | Severity | Description |
|------|----------|-------------|
| [SPIRE009](rules/SPIRE009.md) | Error | Switch does not handle all variants |
| [SPIRE010](rules/SPIRE010.md) | Info | Code fix: expand wildcard to explicit variant arms |
| [SPIRE011](rules/SPIRE011.md) | Error | Pattern field type mismatch |
| [SPIRE012](rules/SPIRE012.md) | Error | Pattern field count mismatch |
| [SPIRE013](rules/SPIRE013.md) | Error | Accessing another variant's field |
| [SPIRE014](rules/SPIRE014.md) | Error | Accessing variant field without kind guard |

## Comparison with C# 15 Native Unions

C# 15 `union` types store all variants in a shared memory slot but box value types into `object? Value`. Case types are record classes — every construction heap-allocates. Spire struct unions store all data inline: no allocations for value-type variants.

Trade-off: Spire requires a source generator and NuGet reference; native unions are language-native and need no tooling.

See [docs/benchmark-results/](benchmark-results/) for measured throughput comparisons across all strategies and native unions.

## Limitations

- **No class support** — applying `[DiscriminatedUnion]` to a non-record class emits SPIRE_DU011 (error). Use `abstract partial record` for reference-type unions.
- **No generic Overlap** — generic structs cannot use `Layout.Overlap` (SPIRE_DU005). Use `Layout.Additive` or `Layout.BoxedFields` instead.
- **No ref struct support** — `ref struct` types cannot be decorated with `[DiscriminatedUnion]` (SPIRE_DU002).
- **No ref struct JSON** — `ref struct` unions cannot use JSON serialization (SPIRE_DU008).
- **No generic UnsafeOverlap** — same restriction as Overlap (SPIRE_DU005).
