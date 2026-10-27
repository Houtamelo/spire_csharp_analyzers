# Discriminated unions

`[DiscriminatedUnion]` generates a union from a `partial struct` or an
`abstract partial record`. Attributes belong to `Houtamelo.Spire`; the generator
and analyzers are supplied by `Houtamelo.Spire.Analyzers`.

## Struct unions

```csharp
using Houtamelo.Spire;

[DiscriminatedUnion]
public partial struct Shape
{
    [Variant] public static partial Shape Circle(double radius);
    [Variant] public static partial Shape Rectangle(double width, double height);
    [Variant] public static partial Shape Point();
}

Shape shape = Shape.Circle(5.0);
double area = shape switch
{
    { kind: Shape.Kind.Circle, circle: { radius: var r } } => Math.PI * r * r,
    { kind: Shape.Kind.Rectangle, rectangle: (var w, var h) } => w * h,
    { kind: Shape.Kind.Point } => 0,
};
```

Each variant has a payload struct under the sibling `ShapeTypes.Variants`
container: `Circle`, `Rectangle`, and `Point`. Payloads are non-partial structs with private `_`-prefixed fields,
public getter-only properties, a constructor, and a typed `Deconstruct`.
Payload getters and `Deconstruct`, along with the union's read-only accessors,
are readonly struct members, so reading through a readonly reference avoids
defensive copies of the receiver.
The outer types container matches the union's accessibility; `Variants` and its
payload structs are public within it. Payloads copy the union's generic parameters
and constraints, while their containers remain nongeneric. Generic unions use an
arity suffix (`ShapeTypes1` for `Shape<T>`). For a `Command` union nested in
`Combat`, the container is also nested there: `Combat.CommandTypes.Variants.Run`.

The union exposes `kind`, `IsCircle`/`IsRectangle`/`IsPoint`, factories, and payload
accessors `circle`/`rectangle`/`point`. Ordinary struct unions implement
`IDiscriminatedUnion<Shape.Kind>`; ref struct unions expose these members without
the interface, so their generated declarations also work before C# 13.
Accessors return payload copies. Use the union
factory to construct a replacement with changed values. There are no flat
field properties or union-level `Deconstruct` overloads.

Accessors use camelCase, including leading acronyms (`HTTPRequest` becomes
`httpRequest`). If that name matches any variant factory, the accessor receives
one `_value` suffix. Other name collisions use ordinary C# compiler diagnostics.

Guard an accessor with its corresponding kind or generated `IsVariant` predicate.
SPIRE013/014 diagnose incorrect or unguarded access. SPIRE009 checks exhaustiveness,
and the missing-arm code fix generates `IsVariant: true` with payload
deconstruction. A captured payload's getters need no further kind guard.

Variant accessors also assert the expected kind before reading their storage.
Compilations with a usable Unity assertion API use `UnityEngine.Debug.Assert`
(`UNITY_ASSERTIONS`); other hosts use `System.Diagnostics.Debug.Assert` (`DEBUG`).
Nonserialized Unity unions use constant messages compatible with Burst. When the
corresponding symbol is absent, the check and diagnostic message are omitted.

The generated `Kind` enum carries `[EnforceExhaustiveness]`. Standalone switches
on `Shape.Kind` are checked by SPIRE015, including when the enum is passed
separately from its union. Its integer conversions follow SPIRE016 and the
`SpireEnum<T>.From` code fix.

Use property patterns for a one-field payload: C# treats `(var radius)` as a
parenthesized pattern rather than positional deconstruction. With two or more
fields, positional patterns use the generated typed `Deconstruct` directly.
`var` captures preserve nulls; a declaration pattern such as `string text`
excludes null.

## Storage

Concrete unmanaged fields are grouped by variant in helpers with the C# compiler's
default sequential layout. A nongeneric explicit-layout helper overlays those
groups at offset zero. These helpers live under `ShapeTypes.Fields.Unmanaged`,
with managed slots in `ShapeTypes.Fields.Managed`. The generator calculates no sizes, offsets within a
payload, or alignment tables. It requires neither unsafe code nor an Unsafe DLL.
Fields depending on generic parameters remain in ordinary typed storage outside
the overlay, including parameters constrained to `unmanaged`.

Reference types and structs containing managed references use ordinary slots:

| `ReferenceStorage` | Behavior |
|---|---|
| `ReadGlobalCfg` | Inherit the project default; this is the attribute default. |
| `Auto` | Choose Shared when `SharedSlots * 2 <= TypedSlots`; otherwise Typed. |
| `Typed` | Reuse compatible same-type slots across variants; direct reads and no boxing. |
| `Shared` | Reuse `object?` slots across variants; reads cast or unbox, and managed structs box when stored. |

Names do not affect slot reuse. Fields present together require separate slots.
Reference nullability does not distinguish slot types. Auto counts each managed
candidate as one slot after reuse, without inspecting its contents; discriminator,
unmanaged helpers, generic-dependent fields, and serialization caches are excluded.
With no managed candidates it selects Typed. Managed references are never overlaid
using field offsets.

```csharp
[DiscriminatedUnion(ReferenceStorage = ReferenceStorage.Typed)]
public partial struct Command { /* variant factories */ }
```

Generic unions are supported. A union inside a generic containing type receives
SPIRE_DU018 because its explicit helper would inherit the enclosing generic
context. Move that union outside the generic ancestor. Code generation continues
alongside the diagnostic so references to generated members remain visible.

## Record unions

Record variants retain their ordinary reference semantics and nested type API:

```csharp
[DiscriminatedUnion]
public abstract partial record Result
{
    public sealed partial record Success(int value) : Result;
    public sealed partial record Failure(string message) : Result;
}

string Describe(Result result) => result switch
{
    Result.Success { value: var value } => value.ToString(),
    Result.Failure { message: var message } => message,
};
```

ReferenceStorage affects struct storage; record variants allocate record objects.

## JSON

```csharp
[DiscriminatedUnion(json: JsonLibrary.SystemTextJson, jsonDiscriminator: "type")]
public partial struct Shape { /* variant factories */ }
```

`Shape.Circle(5.0)` serializes as `{"type":"Circle","radius":5.0}`. The default
discriminator is `kind`. `[JsonName]` on a factory or parameter customizes its
logical name. Payload accessors and internal storage do not change the wire format.
Both libraries can be selected with
`JsonLibrary.SystemTextJson | JsonLibrary.NewtonsoftJson`.

Converters preserve the caller's serializer options. Nested user types still
follow the selected library's normal serialization rules. In particular,
System.Text.Json public fields require the caller's `IncludeFields` option or
appropriate attributes. Missing library references report SPIRE_DU006/007 while
core union generation continues.

## Project defaults

```xml
<PropertyGroup>
  <Spire_DU_DefaultReferenceStorage>Auto</Spire_DU_DefaultReferenceStorage>
  <Spire_DU_DefaultJson>SystemTextJson</Spire_DU_DefaultJson>
  <Spire_DU_DefaultJsonDiscriminator>kind</Spire_DU_DefaultJsonDiscriminator>
</PropertyGroup>
```

| Property | Values | Built-in default |
|---|---|---|
| `Spire_DU_DefaultReferenceStorage` | `Auto`, `Typed`, `Shared` | `Auto` |
| `Spire_DU_DefaultJson` | `None`, `SystemTextJson`, `NewtonsoftJson`, `Both` | `None` |
| `Spire_DU_DefaultJsonDiscriminator` | Any string | `kind` |

Omission or explicit `ReadGlobalCfg` inherits the project setting. Explicit
`ReferenceStorage.Auto` runs automatic selection even when the project default
differs. Explicit `JsonLibrary.None` disables converters. A null discriminator
inherits its project default. Unity does not import these NuGet MSBuild defaults
from its regenerated IDE projects; configure its unions on the attribute.

## Unity and diagnostics

Mutable `[Serializable]` struct unions receive generated Unity callbacks and
drawers when Unity references are present. Public payloads are reused through
one shared generic managed-reference wrapper. See [Unity](unity.md) for field
eligibility, serialization, and Editor behavior.

Unity serialization is incompatible with an unmanaged Burst/ECS representation:
the generated serialization cache is a managed reference even when every variant
field is unmanaged. If both representations are needed, manually declare a
serializable authoring union and a separate runtime union without `[Serializable]`,
then convert or bake between them. Runtime variants must contain only types
supported by Burst; unmanaged alone does not establish Burst compatibility.

| Rule | Purpose |
|---|---|
| [SPIRE009](rules/SPIRE009.md) | Exhaustive variant matching |
| [SPIRE010](rules/SPIRE010.md) | Expand wildcard arms |
| [SPIRE011](rules/SPIRE011.md) | Pattern type mismatch |
| [SPIRE012](rules/SPIRE012.md) | Payload positional field-count mismatch |
| [SPIRE013](rules/SPIRE013.md) | Wrong-variant accessor |
| [SPIRE014](rules/SPIRE014.md) | Unguarded variant accessor |

Non-record classes and ref structs are unsupported union declarations. Errors
are reported independently of source emission; unavailable optional components
do not suppress an otherwise constructible core API.

Historical layout benchmarks in [benchmark-results](benchmark-results/) describe
the removed Layout strategies and are not measurements of this representation.
