# 022 — Discriminated Unions: Design Spec

## Overview

Rust-style discriminated unions for C# via source generation + analyzers.
Package: `Spire.SourceGenerators`. Struct-based, immutable, positional pattern matching.

---

## Declaration

```csharp
// Struct — default Auto picks best layout
[DiscriminatedUnion]
partial struct Shape {
    [Variant] static partial Shape Circle(double radius);
    [Variant] static partial Shape Rectangle(float width, float height);
    [Variant] static partial Shape Square(int sideLength);
}

// Struct — explicit layout choice
[DiscriminatedUnion(Layout.BoxedFields)]
partial struct Event {
    [Variant] static partial Event Click(int x, int y, string target);
    ...
}

// Record — always generates abstract sealed record hierarchy
[DiscriminatedUnion]
partial record Option<T> {
    [Variant] static partial Option<T> Some(T value);
    [Variant] static partial Option<T> None();
}
```

- `[DiscriminatedUnion]` inherits `[EnforceInitialization]` (struct path only)
- `[Variant]` marks static partial methods as variant constructors
- Source generator reads `struct` vs `record` keyword to decide shape
- `Layout` enum controls struct storage strategy (ignored for records)

### Layout Enum

```csharp
public enum Layout {
    /// Picks Overlap if possible (non-generic, all region offsets computable),
    /// falls back to BoxedFields otherwise.
    Auto,

    /// Three-region explicit layout: unmanaged fields overlap, reference fields
    /// overlap, remaining fields boxed into object? slots.
    /// Best matching performance (zero alloc). Larger struct size.
    /// ERROR if struct is generic (CLR restriction).
    Overlap,

    /// N × object? fields (one per max field count across variants).
    /// Fields boxed at construction, zero alloc at match time.
    /// Good matching, worst construction and copy.
    BoxedFields,

    /// Single object? field holding a boxed ValueTuple per variant.
    /// Smallest struct (tag + one object?). Worst matching performance
    /// (tuple type-checks are expensive). Best construction after Overlap.
    BoxedTuple,
}
```

### Diagnostics on Layout choice

| Scenario | Diagnostic |
|---|---|
| `Layout` specified on a `record` or `class` | **Warning**: Layout only applies to structs, ignored for record/class |
| `Layout.Overlap` on a generic struct | **Error**: Generic structs cannot use Overlap layout (CLR restriction). Use BoxedFields or BoxedTuple |
| `Layout.Auto` on a generic struct | Silently picks BoxedFields (no diagnostic) |

## Generated Code

For the `Shape` example above, the generator produces:

```csharp
[EnforceInitialization]
[StructLayout(LayoutKind.Explicit)]
readonly partial struct Shape {
    // Nested tag enum (public, visibility controlled by containing struct)
    public enum Kind {
        Circle,
        Square,
        Rectangle,
    }

    // Constants for pattern matching (same name as factory minus "New" prefix)
    public const Kind Circle = Kind.Circle;
    public const Kind Square = Kind.Square;
    public const Kind Rectangle = Kind.Rectangle;

    // Tag
    [FieldOffset(0)]
    public readonly Kind tag;

    // Overlapping variant data — all at sizeof(Kind) offset
    // 1-field variants: raw type, N-field variants: ValueTuple
    // Public for property-pattern access, hidden from IntelliSense
    [FieldOffset(sizeof(Kind))]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public readonly double circle_radius;

    [FieldOffset(sizeof(Kind))]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public readonly int square_sideLength;

    [FieldOffset(sizeof(Kind))]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public readonly (float width, float height) rect;

    // Private constructors
    Shape(Kind tag) { _tag = tag; }

    // Factory methods (New-prefixed to avoid collision with constants)
    public static Shape NewCircle(double radius) { ... }
    public static Shape NewSquare(int sideLength) { ... }
    public static Shape NewRectangle(float width, float height) { ... }

    // Deconstruct overloads
    // Shared arity (Circle, Square all have 1 field) → object?
    public void Deconstruct(out Kind _kind, out object? _first) { ... }
    // Unique arity (only Rectangle has 2 fields) → typed, no boxing
    public void Deconstruct(out Kind _kind, out float _width, out float _height) { ... }
}
```

## Construction

```csharp
Shape s = Shape.NewCircle(5.0);
Shape s = Shape.NewRectangle(3.0f, 4.0f);
Shape s = Shape.NewSquare(10);
```

## Pattern Matching

```csharp
// Positional via Deconstruct
var area = shape switch {
    (Shape.Circle, double r)              => Math.PI * r * r,   // object? → unbox
    (Shape.Square, int s)                 => s * s,             // object? → unbox
    (Shape.Rectangle, var w, var h)       => w * h,             // typed! no boxing
    (Shape.None, _)                       => 0,                 // discard null
};
```

Fieldless variants use `(Shape.None, _)` — C# has no 1-element deconstruction
syntax, so the 2-param Deconstruct is used with a discard.

### Deconstruct Generation Rules

For each field-count group among all variants:
- **Unique arity** (only one variant has N fields) → generate typed
  `Deconstruct(out Kind, out T0, out T1, ...)` — zero boxing
- **Shared arity** (multiple variants have N fields) → generate
  `Deconstruct(out Kind, out object?, ...)` — boxes at match site

The tag param (`out Kind`) is always first in every overload.

### Property Patterns (alternative, no boxing)

```csharp
var area = shape switch {
    { tag: Shape.Circle, circle_radius: var r }        => Math.PI * r * r,
    { tag: Shape.Rectangle, rect: (var w, var h) }     => w * h,
    { tag: Shape.Square, square_sideLength: var s }    => s * s,
    ...
};
```

Available for hot paths. Verbose but zero-alloc for unmanaged layout.

## Layout Strategy

Strategy depends on the declaration keyword and `Layout` enum:

| Declaration | Layout | Strategy |
|---|---|---|
| `partial struct` | `Auto` (default) | Overlap if non-generic, BoxedFields if generic |
| `partial struct` | `Overlap` | Three-region explicit layout (error if generic) |
| `partial struct` | `BoxedFields` | N × `object?` fields struct |
| `partial struct` | `BoxedTuple` | Single `object?` tuple payload struct |
| `partial record` | any/omitted | `abstract record` + sealed record variants |
| `partial class` | any/omitted | `abstract class` + sealed class variants |

### Non-generic: Three-Region Explicit Layout

All non-generic unions use a single `readonly struct` with `StructLayout.Explicit`.
The struct is divided into three regions, each allowing overlap within itself:

```
[Region 0: Kind tag]
[Region 1: Unmanaged overlap — sizeof-computable types]
[Region 2: Reference overlap — pointer-sized ref slots]
[Region 3: Object? slots — managed structs, unknown-size types]
```

#### Field classification

For each variant, every field is classified into one of three categories:

1. **Region 1** — `IsUnmanagedType == true` AND `sizeof` computable at gen time
   (primitives, enums, user-defined structs composed entirely of sizeof-able types).
   Fields overlap across variants. Sorted by alignment descending within each
   variant to minimize padding.

2. **Region 2** — `IsReferenceType == true` (string, classes, arrays, etc.).
   All pointer-sized. Overlap across variants at shared ref slots. Different
   reference types at the same slot is valid (CLR only cares that the slot
   holds a managed reference, not which type).

3. **Region 3** — Everything else: managed value types (structs containing
   references), types with unknown sizeof. Each stored as `object?` (boxed).
   Boxing cost only for these exotic cases.

#### Offset computation

```
region1_size  = max across variants of (sum of region1 field sizes, with alignment)
region2_slots = max across variants of (count of region2 fields)
region3_slots = max across variants of (count of region3 fields)

Kind:    offset 0
Region1: offset sizeof(Kind)
Region2: offset sizeof(Kind) + region1_size, aligned to pointer size
Region3: offset Region2_start + region2_slots × pointer_size

Total = sizeof(Kind) + region1_size + padding + region2_slots × 8 + region3_slots × 8
```

#### Generated fields

For each variant:
- 0 region1 fields: no unmanaged field
- 1 region1 field: raw typed field at Region1 base
- N region1 fields: `ValueTuple<...>` at Region1 base (all overlap at same offset)

For Region2, per-variant typed ref fields overlap at shared slots:
```csharp
[FieldOffset(R2_START)]     public readonly string? label_text;       // slot 0
[FieldOffset(R2_START)]     public readonly string? error_message;    // slot 0 (overlaps)
[FieldOffset(R2_START)]     public readonly string? richtext_text;    // slot 0 (overlaps)
[FieldOffset(R2_START + 8)] public readonly string? richtext_font;    // slot 1
```

For Region3, shared `object?` fields with typed property accessors:
```csharp
[FieldOffset(R3_START)]     readonly object? _obj_0;
[FieldOffset(R3_START + 8)] readonly object? _obj_1;

// Generated typed accessor (cast, no alloc on read)
public PlayerInfo RichText_Player => (PlayerInfo)_obj_0!;
```

All Region2/Region3 fields are `[EditorBrowsable(Never)]` — hidden from
IntelliSense, accessible for property patterns, guarded by the variant field
access analyzer.

#### Layout example: mixed union with 8 variants

```
Point()                                          — nothing
Circle(double radius)                            — R1: double
Label(string text)                               — R2: ref_0
Rectangle(float width, float height)             — R1: (float, float)
ColoredLine(int x1, int y1, string color)        — R1: (int, int), R2: ref_0
Transform(float x, float y, float z, float w)    — R1: (float, float, float, float)
RichText(string text, int size, bool bold,
         string font, double spacing)            — R1: (double, int, bool), R2: ref_0, ref_1
Error(string message)                            — R2: ref_0

region1_size  = 16 bytes (Transform: 4 floats)
region2_slots = 2 (RichText: 2 strings)
region3_slots = 0

Layout: [Kind:4][R1:16][pad:4][R2:16] = 40 bytes
```

Matching via property patterns: zero boxing, zero alloc for regions 1 and 2.
Region 3 fields box at construction, unbox at access (cheap, no alloc).

### Record / Class Hierarchy

Used when the user declares `partial record` or `partial class`, or when a
generic `partial struct` uses `Layout.Auto` (falls back to BoxedFields struct,
but user can choose record/class instead).

Records provide `Equals`, `GetHashCode`, `ToString`, and `Deconstruct` for free.
Classes give full manual control. Both use sealed variants for exhaustive switch.

```csharp
// Record declaration
[DiscriminatedUnion]
partial record Option<T> {
    [Variant] static partial Option<T> Some(T value);
    [Variant] static partial Option<T> None();
}

// Generated
public abstract partial record Option<T> {
    public sealed record Some(T Value) : Option<T>;
    public sealed record None : Option<T>;

    public static Option<T> NewSome(T value) => new Some(value);
    public static Option<T> NewNone() => new None();
}

// Usage — native switch with destructuring
option switch {
    Option<int>.Some(var v) => v,
    Option<int>.None => 0,
};

// Class declaration
[DiscriminatedUnion]
partial class Result<T, E> {
    [Variant] static partial Result<T, E> Ok(T value);
    [Variant] static partial Result<T, E> Err(E error);
}

// Generated
public abstract partial class Result<T, E> {
    public sealed class Ok(T Value) : Result<T, E> { public T Value { get; } = Value; }
    public sealed class Err(E Error) : Result<T, E> { public E Error { get; } = Error; }

    public static Result<T, E> NewOk(T value) => new Ok(value);
    public static Result<T, E> NewErr(E error) => new Err(error);
}
```

## Generics

Generic `partial struct` with `Layout.Auto` falls back to BoxedFields (CLR
forbids explicit layout on generic types). Users can also declare generic unions
as `partial record` or `partial class` for the class hierarchy path.

## Default Value

**Struct path**: `[DiscriminatedUnion]` inherits `[EnforceInitialization]`, so existing
SPIRE rules catch:
- `default(Shape)` — SPIRE003
- `new Shape()` — SPIRE004
- `new Shape[10]` — SPIRE001
- `Activator.CreateInstance<Shape>()` — SPIRE005

**Record / class path**: `default` is `null`. Standard nullable reference type
analysis applies — no special handling needed.

---

## Analyzer Rules

### Exhaustiveness Checking

**Scope**: `switch` expressions and `switch` statements on `[DiscriminatedUnion]`
types.

| Scenario | Diagnostic |
|---|---|
| Missing variant(s) | **Error**: "Switch does not handle variant(s): 'Square', 'Rectangle'" |
| `when` guard on arm | Does NOT count as full coverage for that variant |
| Discard/wildcard arm (`_`) covers missing variants | **Warning**: "Switch uses wildcard instead of exhaustive variant matching" |
| All variants explicitly covered | Suppress CS8509 via `DiagnosticSuppressor` |
| Discard arm + all variants explicit | Suppress CS8509, no diagnostic (discard is redundant) |

### Type Safety

**Scope**: any pattern matching context (`switch`, `if`/`is`, etc.)

| Scenario | Diagnostic |
|---|---|
| Field type mismatch: `(Shape.Circle, string bad)` | **Error**: "Variant 'Circle' field 0 is 'double', not 'string'" |
| Field type mismatch with discard: `(Shape.Circle, string _)` | **Error**: same as above |
| Untyped discard: `(Shape.Circle, _)` | OK — no type to check |
| Wrong field count: `(Shape.Circle, _, _)` | **Error**: "Variant 'Circle' has 1 field(s), not 2" |
| Correct type + discard: `(Shape.Circle, double _)` | OK |

**Type matching is exact** — no implicit conversions. `double` matches `double`
only, not `float`, not `object`.

### Variant Field Access Safety

Generated fields are `public` + `[EditorBrowsable(Never)]` (hidden from
IntelliSense but accessible for property patterns).

| Context | Own variant's field | Other variant's field | No tag guard |
|---|---|---|---|
| Inside matched arm | OK | **Error** | N/A |
| After `if (tag == X)` | OK | **Error** | N/A |
| No guard at all | N/A | N/A | **Warning** |

```csharp
switch (shape) {
    case (Shape.Circle, double r):
        shape.circle_radius;  // OK — Circle's field
        shape.rect;           // Error: 'rect' belongs to 'Rectangle', not 'Circle'
        break;
}

if (shape.tag == Shape.Circle) {
    shape.circle_radius;      // OK
    shape.square_sideLength;  // Error: 'square_sideLength' belongs to 'Square', not 'Circle'
}

shape.circle_radius;          // Warning: accessing variant field without tag guard
```

### Not checked (intentionally)

- `if`/`is` patterns — not checked for exhaustiveness. Single-variant `is`
  checks are normal partial matching.
- **Unions nested in tuples** — exhaustiveness only checked when the union is the
  direct switch subject. `(shape, flag) switch { ... }` does NOT trigger
  exhaustiveness analysis for `shape`. Type safety (wrong field type) still
  applies at any nesting depth.

---

## Code Fixes

All code fixes live in `Spire.SourceGenerators`.

### 1. Add Missing Variant Arms

Trigger: exhaustiveness error (missing variants).

```csharp
// Before
shape switch {
    (Shape.Circle, double r) => ...,
};

// After fix
shape switch {
    (Shape.Circle, double r) => ...,
    (Shape.Rectangle, float w, float h) => throw new NotImplementedException(),
    (Shape.Square, int s) => throw new NotImplementedException(),
};
```

Inserts arms with correct variant constant, correct field types, variable names
from variant declaration, and `throw new NotImplementedException()` body.

### 2. Fix Wrong Field Type

Trigger: type mismatch error.

```csharp
// Before
case (Shape.Circle, string bad):

// After fix
case (Shape.Circle, double bad):
```

Replaces type with correct one, preserves variable name.

### 3. Replace Wildcard With Explicit Variants

Trigger: wildcard warning.

```csharp
// Before
shape switch {
    (Shape.Circle, double r) => ...,
    _ => defaultValue,
};

// After fix
shape switch {
    (Shape.Circle, double r) => ...,
    (Shape.Rectangle, float w, float h) => defaultValue,
    (Shape.Square, int s) => defaultValue,
};
```

Expands discard into missing variants, copies discard arm's expression body.

---

## CLR Constraints

- **Reference + value type fields cannot overlap** in `StructLayout.Explicit` —
  the CLR throws `TypeLoadException` at runtime (not a compile error). The
  poisoned type kills the entire assembly.
- The C# compiler does NOT check `FieldOffset` overlap validity — only the CLR
  does at type load time.
- **Generic types cannot have explicit layout** — the CLR throws
  `TypeLoadException` even if all type params are `unmanaged`-constrained.
  This is a blanket restriction, not an overlap issue. Compiles fine, fails
  at runtime.
- `ITypeSymbol.IsUnmanagedType` in Roslyn reliably checks whether a type is
  unmanaged (primitive, enum, pointer, or struct with all-unmanaged fields).
  The generator uses this to pick the layout strategy.

## Flow Analysis for Field Access

The analyzer determines variant context using:

1. **`switch` arms** — `ISwitchExpressionArmOperation.Pattern` / `ISwitchCaseOperation`
   tells us which variant matched. Roslyn provides this directly.
2. **`is` patterns in `if`** — `IIsPatternOperation` in the condition; the `if`
   body is the true branch.
3. **`if (shape.tag == Shape.Circle)`** — binary comparison on tag field. Analyzer
   inspects the enclosing `if` condition manually.
4. **Cross-method** — not tracked. Accessing variant fields in a method that
   receives the union as a parameter (without local guard) triggers Warning.

## Infrastructure

- **Source generator**: `IIncrementalGenerator` using
  `ForAttributeWithMetadataName("Houtamelo.Spire.DiscriminatedUnionAttribute", ...)`
- **Analyzer**: `DiagnosticAnalyzer` for exhaustiveness + type safety
- **Suppressor**: `DiagnosticSuppressor` for CS8509
- **Code fixes**: `CodeFixProvider` for all three fix types
- **Package**: `Spire.SourceGenerators` (separate from `Spire.Analyzers`)
- **Attributes**: `DiscriminatedUnionAttribute`, `VariantAttribute` — shipped in
  the generator package (or a lightweight attributes-only package)

## Benchmark Results

Benchmarks in `benchmarks/Spire.Benchmarks/`. Run via:
```
dotnet run --project benchmarks/Spire.Benchmarks -c Release -- --filter '*Unmanaged*' --join
dotnet run --project benchmarks/Spire.Benchmarks -c Release -- --filter '*UnionBenchmarks*' --join
```

### Unmanaged-only union (8 variants, 0-5 fields, all unmanaged, N=1000, shuffled)

| | Construct | Match | Copy |
|---|---|---|---|
| **explicit (property)** | 3.1μs / 0B | **611ns / 0B** | 479ns / 32KB |
| abstract class | 3.2μs / 36KB | 1,228ns / 0B | **179ns / 8KB** |

### Mixed union (8 variants, 0-5 fields, managed + unmanaged, N=1000, shuffled)

| | Construct | Match | Copy | Size |
|---|---|---|---|---|
| **hybrid explicit (property)** | 4.0μs / 40KB | **714ns / 0B** | 811ns / 40KB | 40 bytes |
| multiobj struct | 8.7μs / 84KB | 869ns / 0B | 979ns / 48KB | 48 bytes |
| abstract record | 4.6μs / 37KB | 1,218ns / 0B | **178ns / 8KB** | 8 bytes (ref) |
| abstract class | 4.6μs / 37KB | 1,273ns / 0B | 176ns / 8KB | 8 bytes (ref) |
| tupleobj struct | 3.6μs / 36KB | 5,333ns / 31KB | 304ns / 16KB | 16 bytes |

Hybrid explicit struct with three-region layout wins matching by 41% over abstract
record (714ns vs 1,218ns), both zero alloc. Trade-off is larger struct (40 bytes
vs 8-byte reference) affecting copy and storage density.

### Design decision

User chooses declaration form (`struct`, `record`, `class`) and layout strategy:
- **struct + Overlap** — best matching (714ns, 0 alloc). Three-region explicit
  layout. Non-generic only.
- **struct + BoxedFields** — decent matching (869ns, 0 alloc). Fallback for
  generic structs. Worst construction/copy.
- **struct + BoxedTuple** — smallest struct. Worst matching (5.3μs, allocates).
- **record** — abstract sealed records. Good matching (1.2μs, 0 alloc). Best
  copy (178ns). Free Equals/GetHashCode/ToString. Works with generics.
- **class** — abstract sealed classes. Same perf as record, manual equality.

### Notes

- **Overlap (explicit)** — best matching. Default for non-generic structs via Auto.
- **BoxedFields (multiobj)** — worst construction/copy but decent matching.
  Available as user choice. Default fallback for generic structs via Auto.
- **BoxedTuple** — worst matching. Available as user choice for smallest struct
  size when matching is rare.
- **Explicit + Deconstruct** — generated for all layouts including Overlap.
  Boxes on every call (~12μs/1000 items), but user may prefer ergonomics over
  speed at any given call site. Both Deconstruct and property patterns are
  available — user picks per usage.

---

## MVP Scope

- Source generator: `IIncrementalGenerator` — struct (Overlap, BoxedFields,
  BoxedTuple), record, and class paths
- Analyzer: exhaustiveness, type safety, field access safety
- Suppressor: CS8509
- Code fixes: add missing arms, fix wrong types, expand wildcards
- Attributes: `DiscriminatedUnionAttribute` (with `Layout` enum),
  `VariantAttribute`

## Post-MVP

1. **Serialization** — opt-in `[JsonDiscriminatedUnion]` attribute, generates
   `JsonConverter<T>`. Configurable tag name. Covers all layout strategies.
2. **ToString** (struct path) — generate readable `ToString()`, e.g.
   `"Circle(5.0)"`. Record/class paths get it free.
3. **Equality** (struct path) — generate `IEquatable<T>`, `Equals`,
   `GetHashCode`. Record path gets it free.
4. **Nested unions** — union variant containing another union type.
5. **Newtonsoft.Json** support alongside System.Text.Json.
