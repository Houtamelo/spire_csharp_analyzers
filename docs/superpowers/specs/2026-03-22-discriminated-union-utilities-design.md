# Discriminated Union Utility Methods

Generated utility methods for discriminated unions produced by `[DiscriminatedUnion]`.

## 1. IsVariant Properties

Generated per variant on the union type. Always visible in IntelliSense (no `EditorBrowsable(Never)`).

**Struct unions:**
```csharp
public bool IsCircle => this.kind == Kind.Circle;
public bool IsSquare => this.kind == Kind.Square;
```

**Record unions:**
```csharp
// Generated in the base type's partial declaration, where Circle resolves to the nested type.
public bool IsCircle => this is Circle;
public bool IsSquare => this is Square;
```

- Naming: `Is{VariantName}`, PascalCase matching variant name
- Return type: `bool`
- Always generated, no opt-out attribute
- Applies to all strategies (struct, record)

## 2. IDiscriminatedUnion Interface + OfKind LINQ Extension

**Struct unions only.** Record unions already have `OfType<TVariant>()` for collection filtering.

### Interface (Spire.Core, namespace `Spire`)

```csharp
public interface IDiscriminatedUnion<TEnum> where TEnum : Enum
{
    TEnum kind { get; }
}
```

Source generators make all struct `[DiscriminatedUnion]` types implement this interface.

**Breaking change:** struct emitters currently generate `kind` as a `public readonly` field. Fields don't satisfy interface property requirements. `kind` must become a public non-auto property with a getter (see Section 3). Each strategy decides its own backing storage.

**Runtime dependency:** implementing `IDiscriminatedUnion<TEnum>` introduces a runtime dependency on `Spire.Core`. Acceptable — `Spire.Core` is already a compile-time dependency for attributes like `[MustBeInit]`.

### LINQ Extension (Spire.Core, namespace `Spire`)

```csharp
public static class SpireLINQ
{
    public static IEnumerable<TDU> OfKind<TDU, TEnum>(
        this IEnumerable<TDU> source, TEnum kind)
        where TDU : IDiscriminatedUnion<TEnum>
        where TEnum : Enum
    {
        foreach (var item in source)
        {
            if (EqualityComparer<TEnum>.Default.Equals(item.kind, kind))
                yield return item;
        }
    }
}
```

- Type inference verified — `shapes.OfKind(Shape.Kind.Circle)` compiles without explicit generic arguments
- Lazy evaluation via `yield return`, no allocation
- Single `TEnum kind` parameter (no `params` overload)
- Enables future LINQ extensions via the interface (`GroupBy(x => x.kind)`, etc.)
- Boxing note: `EqualityComparer<TEnum>.Default` may box on pre-.NET 6 runtimes. Acceptable tradeoff for a convenience method.

### Call Site

```csharp
List<Shape> shapes = ...;
IEnumerable<Shape> circles = shapes.OfKind(Shape.Kind.Circle);
```

## 3. Init-Settable Properties

Struct union properties and backing fields change from read-only to `{ get; init; }`. Enables `with` expressions for copy-with semantics. `EditorBrowsable(Never)` retained on public properties. Before/after examples representative of Additive strategy — current `readonly` usage varies by strategy.

### Backing Fields

Backing fields change from `internal readonly` fields to `internal` auto-properties with `{ get; init; }`. This preserves the `readonly struct` contract — `init` properties are allowed in `readonly struct` because they're only assignable during construction.

**Before (Additive):**
```csharp
internal readonly double _s0;
```

**After (Additive):**
```csharp
internal double _s0 { get; init; }
```

**Overlap strategy:** uses `[StructLayout(LayoutKind.Explicit)]` with `[FieldOffset]`. Apply via `field:` attribute target on auto-properties:

```csharp
[field: FieldOffset(1)]
internal double _s0 { get; init; }
```

This works because `[field: FieldOffset(X)]` applies the attribute to the compiler-generated backing field, satisfying the CLR's explicit layout requirement while still exposing `{ get; init; }` semantics.

**UnsafeOverlap strategy:** stores unmanaged fields via byte buffer. Public properties use strategy-specific read/write — no named backing auto-property needed:

```csharp
[EditorBrowsable(EditorBrowsableState.Never)]
public double radius
{
    get => Unsafe.ReadUnaligned<double>(ref this._data[offset]);
    init => Unsafe.WriteUnaligned(ref this._data[offset], value);
}
```

**BoxedTuple strategy:** stores fields in a single `object? _payload`. Single-field variants store directly, multi-field variants box a `ValueTuple`:

```csharp
// Single-field variant
[EditorBrowsable(EditorBrowsableState.Never)]
public double radius
{
    get => (double)this._payload!;
    init => this._payload = value;
}
```

For all strategies, `init` accessors can contain arbitrary code — they are not limited to setting auto-property backing fields.

### Public Properties (Additive/Overlap/BoxedFields)

**Before:**
```csharp
[EditorBrowsable(EditorBrowsableState.Never)]
public double radius => this._s0;
```

**After:**
```csharp
[EditorBrowsable(EditorBrowsableState.Never)]
public double radius
{
    get => this._s0;
    init => this._s0 = value;
}
```

C# allows `init` accessors to set other `init`-only members on `this`, so the delegation from `radius.init` to `_s0.init` is valid.

### The `kind` Discriminator

`kind` becomes a public non-auto property with a getter only. Each strategy implements the backing storage and getter independently:

```csharp
// Additive, Overlap, BoxedFields, BoxedTuple
public Kind kind => this._kind;

// UnsafeOverlap (reads from byte buffer)
public Kind kind => (Kind)this._data[0];
```

No `init` setter — users cannot write `shape with { kind = Kind.Square }`. `with` expressions copy the backing storage via memberwise copy, so `kind` is preserved automatically.

`kind` does **not** get `EditorBrowsable(Never)` — it remains visible.

### Usage

```csharp
if (shape.kind == Shape.Kind.Circle)
{
    var bigger = shape with { radius = 5.0 };
}
```

### Safety

Slot aliasing: different variants may share backing storage. Setting `radius` on a Square corrupts `sideLength` if both map to `_s0`. This is **not** guarded by the generated code — a companion analyzer rule is required.

**Companion analyzer rule** (separate spec): flags `with { property = value }` usage when not inside a `kind` guard for the correct variant. Exact rule design deferred. (`kind` mutation is prevented at the language level via `private init` — no analyzer rule needed for that case.)

### Constraints

- `init` requires C# 9+. Users on C# 8 or below see properties as read-only.
- `IsExternalInit` polyfill needed for netstandard2.0 targets. PolySharp (already in Spire.Core) provides this.
- Record unions: no change needed — records already support `with` natively.
- Applies to all struct strategies (Additive, Overlap, BoxedFields, BoxedTuple, UnsafeOverlap).

### Implementation Note

Changing backing fields to `{ get; init; }` and adding `IsVariant` properties will update all existing snapshot test output files. Behavioral tests may also require updates.

## Out of Scope

- Dynamic IntelliSense filtering based on variant context (requires VSIX/Rider plugin)
- `IDiscriminatedUnion` for record unions — use `OfType<TVariant>()` instead
- `params Kind[]` overload for `OfKind`
- Functional combinators (Map, Bind, Fold) — covered by C# pattern matching
- `UnwrapVariant` / `TryGetVariant` — covered by C# casting / pattern matching
- `Match` lambdas — covered by switch expressions with deconstruction
