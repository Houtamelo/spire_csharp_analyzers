# 021 — Research: Rust-Style Enums (Discriminated Unions) in C#

**Goal**: Evaluate approaches for bringing Rust-style enums to C# using structs,
leveraging Roslyn source generation and analysis.

---

## 1. Existing C# Libraries

### 1.1 OneOf

**NuGet**: `OneOf` (~97M downloads)
**Approach**: Hand-written generic structs (`OneOf<T0, T1>`, up to `OneOf<T0..T31>`).

```csharp
OneOf<string, int, bool> value = "hello";
value.Switch(
    s => Console.WriteLine(s),
    i => Console.WriteLine(i),
    b => Console.WriteLine(b));
```

Storage: single `object _value` field + `int _index`. All value types boxed.
Struct wrapper avoids heap allocation for the union itself, but boxing negates
this for value type payloads.

- No source generation — hand-written up to 32 type params
- No named cases — positional only (`T0`, `T1`, ...)
- No serialization support
- No exhaustive `switch` — only `Match()`/`Switch()` methods

### 1.2 Dunet

**NuGet**: `Dunet`
**Approach**: Source generator. Partial records with `[Union]` attribute.

```csharp
[Union]
partial record Option<T> {
    partial record Some(T Value);
    partial record None;
}

var result = option.Match(
    some => some.Value.ToString(),
    none => "nothing");
```

- **Class-only** — generates record types (reference types)
- Supports generics (`Option<T>`, etc.)
- Implicit conversions when variant properties are unique
- `MatchAsync` for `Task<T>` / `ValueTask<T>`
- No struct support

### 1.3 Funcky.DiscriminatedUnion

**NuGet**: `Funcky.DiscriminatedUnion`
**Approach**: Source generator. Abstract partial records + `[DiscriminatedUnion]`.

```csharp
[DiscriminatedUnion]
public abstract partial record Shape {
    public partial record Circle(double Radius);
    public partial record Rectangle(double W, double H);
}
```

- **Class-only** — abstract record hierarchies
- Auto-generates `[JsonDerivedType]` when `[JsonPolymorphic]` is present
- Generates `Match()` and flattened switch-style helpers
- No struct support

### 1.4 Dusharp

**NuGet**: `Dusharp`
**Approach**: Source generator. Static partial methods as case constructors.

```csharp
[Union]
public partial struct Shape<T> where T : struct, INumber<T> {
    [UnionCase]
    public static partial Shape<T> Circle(T radius);
    [UnionCase]
    public static partial Shape<T> Rectangle(T width, T height);
}

string result = shape.Match(
    radius => $"Circle r={radius}",
    (w, h) => $"Rect {w}x{h}");
```

**Supports struct unions**. Memory layout:
- Blittable value types overlap via `[StructLayout(LayoutKind.Explicit)]` + `[FieldOffset(0)]`
- Reference types share `object` fields accessed via `Unsafe.As<object, T>`
- `byte Index` discriminant

Example struct size (mixed value + ref types): ~56 bytes for a 3-case union.

Built-in System.Text.Json + Newtonsoft.Json serialization via `[GenerateJsonConverter]`.

### 1.5 SumSharp

**NuGet**: `SumSharp`
**Approach**: Source generator. Attribute-driven with **configurable storage strategies**.

```csharp
[UnionCase("Some", "T")]
[UnionCase("None")]
partial class Optional<T> { }
```

Storage strategies:

| Strategy | Behavior |
|---|---|
| `InlineValueTypes` (default) | Value types stored directly; ref types share `object` field |
| `OneObject` | Single `object` field (always boxes value types) |
| Per-case `Inline` / `AsObject` | Override per case |
| Unmanaged shared storage | Unmanaged cases share memory region sized to largest |

For generic unmanaged types, allows pre-allocated fixed-size buffer:
```csharp
[UnionStorage(UnmanagedStorageSize: 32)]
partial struct Box<T> where T : unmanaged { }
```

### 1.6 Unio

**NuGet**: `Unio`
**Approach**: `readonly struct` core + incremental source generator for named types.

```csharp
// Anonymous
Unio<int, string> result = 42;
string msg = result.Match(
    value => $"Success: {value}",
    err   => $"Error: {err}");

// Named
[GenerateUnio]
public partial class GetOrderResult : UnioBase<Order, NotFound, Unauthorized>;
```

Storage: typed generic fields (`T0? _value0`, `T1? _value1`, ...) + `byte _index`.
**No boxing** — each variant has its own field. Struct size = sum of all variant
sizes + 1 byte index.

Zero-allocation matching via TState overloads:
```csharp
string fast = union.Match(prefix,
    static (p, i) => $"{p}: {i}",
    static (p, s) => $"{p}: {s}");
```

Benchmarks (vs OneOf, .NET 10):
- `Switch<TState>`: Unio 0.69ns vs OneOf 1.95ns (2.8x faster, zero alloc)
- `TryGetT0`: Unio 0.00ns vs OneOf 2.65ns
- `ToString`: Unio 0.44ns vs OneOf 5.11ns

### 1.7 Summary Table

| Library | Mechanism | Struct? | Boxing | Named Cases | Exhaustive | Serialization |
|---|---|---|---|---|---|---|
| OneOf | Hand-written generics | Wrapper only | Yes | No | Match() | No |
| Dunet | Source gen (records) | No | N/A | Yes | Match() + switch | Manual |
| Funcky.DU | Source gen (records) | No | N/A | Yes | Match() | JsonDerivedType |
| Dusharp | Source gen (attrs) | **Yes** | Mixed | Yes | Match() | STJ + Newtonsoft |
| SumSharp | Source gen (attrs) | **Yes** | Configurable | Yes | Match() | STJ + Newtonsoft |
| Unio | readonly struct + gen | **Yes** | **No** | Via generator | Match() | No |

---

## 2. Official C# Language Proposal

### 2.1 Status

- **Champions**: Fred Silberberg, Matt Warren, Mads Torgersen
- **Milestone**: "Working Set" — active design, not approved for specific release
- **Target**: Potentially C# 15 (November 2026), **not confirmed**
- **Focus**: Class unions first, struct unions later
- **LDM meetings**: Active through August 2025

### 2.2 Proposed Syntax

```csharp
// Short form
public union Pet(Cat, Dog, Bird);

// Long form with inline case definitions
public union Pet {
    case Cat(string Name, string Personality);
    case Dog(string Name, string Breed);
    case Bird(string Name, string Species);
}
```

### 2.3 Lowering

Unions lower to a struct with `[Union]` attribute:

```csharp
[Union]
public struct Pet {
    public Pet(Dog value) { Value = value; }
    public Pet(Cat value) { Value = value; }
    public object? Value { get; }
}
```

Key design: the compiler recognizes **any type** with `[Union]` + the required
public members (constructors, `Value` property). Hand-optimized implementations
are possible.

### 2.4 Pattern Matching

```csharp
var desc = pet switch {
    Dog(var name, _) => $"Dog: {name}",
    Cat(var name, _) => $"Cat: {name}",
    Bird b           => $"Bird: {b.Species}",
};
// Exhaustive — no discard arm needed
```

Optional non-boxing access via `TryGetValue` overloads:
```csharp
public bool TryGetValue(out Dog value) { ... }
```
Compiler prefers `TryGetValue` over `Value` when available.

### 2.5 Type Unions vs Tagged Unions

C# proposal is for **type unions** (unions of existing types), not **tagged
unions** (unions of named cases carrying anonymous data).

- Rust/F#: variant name = tag, payload = anonymous data
- C#: case types are independently defined, carry own members

---

## 3. Struct-Based Approaches — Challenges

### 3.1 The CLR Restriction

**The CLR does not allow reference types and value types to overlap in memory.**

`[StructLayout(LayoutKind.Explicit)]` can overlap two `int` fields at offset 0,
but **cannot** overlap a `string` and an `int`. The GC cannot determine whether
a bit pattern is a managed reference or raw data.

Rust enum layout: `[discriminant][payload: max(sizeof(variants))]` — all variants
share payload bytes. **Impossible in C# when any variant contains managed refs.**

### 3.2 Storage Approaches

**A. Typed fields side-by-side (Unio)**
```
[index: byte][_value0: T0][_value1: T1][_value2: T2]
```
- Simple, no unsafe, works with any type
- Size = sum of all variant sizes (wasteful)

**B. Object field + boxing (OneOf)**
```
[index: int][value: object]
```
- Constant struct size
- Value types boxed → GC pressure

**C. Hybrid: explicit layout for unmanaged, object for refs (Dusharp, SumSharp)**
```
[index: int]
[FieldOffset(4)] int _case0_data;    // overlaps with...
[FieldOffset(4)] double _case1_data; // ...this
[FieldOffset(X)] object _refStorage; // for reference types
```
- Minimal size for unmanaged-only unions
- Cannot overlap unmanaged and reference storage
- Complex generated code

**D. Fixed-size buffer for unmanaged generics (SumSharp)**
```csharp
[UnionStorage(UnmanagedStorageSize: 32)]
partial struct Box<T> where T : unmanaged { }
```
- Rust-like layout for unmanaged types
- Must know max size at declaration time

### 3.3 Size Comparison: union of (int, long, string)

| Approach | Size | Notes |
|---|---|---|
| Rust enum | ~16 bytes | max(variant) + tag (niche-optimized) |
| Unio (side-by-side) | ~25 bytes | 4 + 8 + 8 + 1 + padding |
| OneOf (boxed) | ~16 bytes | 4 (index) + 8 (object ref) + padding |
| Dusharp (hybrid) | ~17 bytes | 4 (index) + 8 (long overlay) + 8 (object for string) |

### 3.4 The Default Problem

All C# structs have `default(T)` = all-zero bits. A struct union with `default`:
- Discriminant = 0 (first case or invalid state)
- All payload fields zeroed (may not be valid for first case)

Library strategies:
- Tag 0 = explicitly invalid/none state
- Tag 0 = first case's default value (accept it)
- Official proposal: `HasValue` property returns `false` for default-initialized

### 3.5 Copying Semantics

Struct unions are value types — copied on assignment, parameter passing, return.
Large unions (many variants, large payloads) make copying expensive. Side-by-side
layout (Approach A) is worst because inactive variant slots are also copied.

### 3.6 Recursive Types

A struct cannot contain a field of its own type (infinite size). Recursive DUs
**require** either:
1. Class-based union (natural indirection via reference)
2. Explicit boxing of recursive field (`Box<Self>`)

Struct DU libraries fundamentally cannot support recursive types without boxing.

---

## 4. Source Generator Infrastructure

### 4.1 IIncrementalGenerator (Required)

`ISourceGenerator` (V1) is deprecated as of Roslyn 4.10.0. All new generators
must use `IIncrementalGenerator`.

```csharp
[Generator]
public class UnionGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var unions = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                "Houtamelo.Spire.UnionAttribute",
                predicate: (node, _) => node is TypeDeclarationSyntax,
                transform: (ctx, ct) => ExtractUnionModel(ctx, ct))
            .Where(static m => m is not null);

        context.RegisterSourceOutput(unions, (spc, model) =>
            spc.AddSource($"{model.TypeName}.g.cs", GenerateCode(model)));
    }
}
```

### 4.2 ForAttributeWithMetadataName

~99x more efficient than `CreateSyntaxProvider` for attribute discovery. Roslyn
pre-filters using internal metadata tables.

### 4.3 Pipeline Best Practices

- **Never pass `SyntaxNode` or `ISymbol` through pipeline** — they change
  identity every compilation, breaking caching
- **Use `EquatableArray<T>`** — `ImmutableArray<T>` lacks structural equality
- **Never combine raw `CompilationProvider`** — changes every keystroke
- **Mark lambdas `static`** — prevents closure captures that break caching
- Extract data into record structs for pipeline stages

### 4.4 Coexistence with Analyzers

Source generators and analyzers can coexist in the same NuGet package. Both are
loaded via the `<Analyzer>` item group in .csproj. The generator emits source;
the analyzer validates usage patterns.

---

## 5. Key Design Tensions

### 5.1 Exhaustive Pattern Matching

C# compiler only knows exhaustiveness for built-in types (enums, booleans). For
library-generated DUs:

- **Match() methods**: Enforced at compile time by method signature. Adding a
  variant changes the signature → all call sites fail. Used by all libraries.
- **ExhaustiveMatching.Analyzer**: Third-party Roslyn analyzer for `[Closed]`
  abstract classes. Reports missing cases in switch expressions.
- **Official proposal**: `[Union]` attribute tells compiler the set is closed.
  Switch expressions become exhaustive without discard arm.

**Gap**: Library `Match()` enforces exhaustiveness, but standard `switch`
expressions still require discard arm. Only the official language feature or a
custom analyzer can close this gap.

### 5.2 Generics

Rust monomorphizes at compile time — knows exact size of `Option<i32>` vs
`Option<i64>`. C# source generators run before monomorphization — cannot emit
`[FieldOffset]` that depends on `sizeof(T)`.

Library approaches:
- Unio: separate nullable fields, JIT handles sizing (no overlap)
- SumSharp: explicit `UnmanagedStorageSize` hints for generic unmanaged types
- OneOf: boxes everything

### 5.3 Serialization

- `[JsonDerivedType]` + `[JsonPolymorphic]` (since .NET 7): class hierarchies only
- Struct unions need custom `JsonConverter` implementations
- Open API proposal for STJ union support: dotnet/runtime#125449
- Libraries (Dusharp, SumSharp) generate custom converters via attributes

### 5.4 Performance vs Ergonomics Spectrum

| Priority | Approach | Trade-off |
|---|---|---|
| Max performance | Explicit layout + unmanaged only | No ref types, complex codegen |
| Good performance | Side-by-side fields, no boxing | Larger struct size |
| Ergonomic | Object boxing | GC pressure, simple code |
| Language-native | Wait for C# 15 | Timeline uncertain |

---

## 6. Sources

### Libraries
- [OneOf](https://github.com/mcintyre321/OneOf)
- [Dunet](https://github.com/domn1995/dunet)
- [Funcky.DiscriminatedUnion](https://github.com/polyadic/funcky-discriminated-union)
- [Dusharp](https://github.com/kolebynov/Dusharp)
- [SumSharp](https://github.com/christiandaley/SumSharp)
- [Unio](https://benjamin-abt.com/blog/2026/03/02/unio-high-performance-discriminated-unions-csharp/)

### C# Language Proposals
- [unions.md](https://github.com/dotnet/csharplang/blob/main/proposals/unions.md)
- [TypeUnions.md](https://github.com/dotnet/csharplang/blob/main/meetings/working-groups/discriminated-unions/TypeUnions.md)
- [Union proposals overview](https://github.com/dotnet/csharplang/blob/main/meetings/working-groups/discriminated-unions/union-proposals-overview.md)
- [NDepend — C# 15 Unions](https://blog.ndepend.com/csharp-unions/)

### Source Generator Patterns
- [IIncrementalGenerator — Roslyn docs](https://github.com/dotnet/roslyn/blob/main/docs/features/incremental-generators.md)
- [Incremental generator cookbook](https://github.com/dotnet/roslyn/blob/main/docs/features/incremental-generators.cookbook.md)
- [Andrew Lock — avoiding performance pitfalls](https://andrewlock.net/creating-a-source-generator-part-9-avoiding-performance-pitfalls-in-incremental-generators/)
- [ForAttributeWithMetadataName — Thinktecture](https://www.thinktecture.com/en/net-core/roslyn-source-generators-high-level-api-forattributewithmetadataname/)

### Struct Layout / CLR
- [Padding for overlaid structs — Stephen Cleary](https://blog.stephencleary.com/2023/10/padding-for-overlaid-structs.html)
- [ExhaustiveMatching.Analyzer](https://github.com/WalkerCodeRanger/ExhaustiveMatching)
- [FieldOffset + reference types — dotnet/runtime#96321](https://github.com/dotnet/runtime/discussions/96321)
