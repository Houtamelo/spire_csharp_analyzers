# Houtamelo.Spire

Roslyn analyzers and source generators for C# type safety — struct initialization enforcement, discriminated unions, and exhaustive switch checking.

## Installation

This `unity-6000` branch targets Unity 6000.5, with Mono and Linux IL2CPP
validation. Build its local package:

```sh
dotnet run -c Release --project tools/Spire.Tool -- package
```

Add `tmp/unity-package/com.houtamelo.spire/package.json` through Unity's Package
Manager. Apply `[Serializable]` to a mutable struct union to enable generated
serialization and Inspector drawers. See [the Unity guide](docs/unity.md) for
field support, asmdef references, and validation.

Annotate a variant parameter with `[UnitySerializeReference]` when its value needs
to preserve null, derived types, or shared references through Unity serialization.

Open the tracked [Unity showcase project](examples/UnityShowcase/README.md) for
Inspector-editable presets, a runnable scene, and real Unity tests of the examples.
The exported package includes the same showcase as an optional Package Manager
sample, offline guides, runtime API tooltips, and third-party notices.

The normal NuGet build is maintained on `main`.

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
- Overlapping unmanaged payloads, configurable reference storage, and record unions.

Define a struct union with `[DiscriminatedUnion]`:

```csharp
using Houtamelo.Spire;

[DiscriminatedUnion]
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
    { kind: Shape.Kind.Circle, circle: { radius: var r } } => Math.PI * r * r,
    { kind: Shape.Kind.Square, square: { side: var s } } => s * s,
    { kind: Shape.Kind.Point }                 => 0,
};

```

Each accessor returns a sibling payload struct (`ShapeCircle`, `ShapeSquare`,
`ShapePoint`) with getter-only properties and a typed `Deconstruct`. Payloads with two or
more fields support positional patterns inside the accessor pattern. Accessors
replace flat field properties and union-level deconstruction.

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

## Global Configuration

Set project-wide defaults via MSBuild properties in your `.csproj` (or `Directory.Build.props`):

```xml
<PropertyGroup>
  <!-- Discriminated union defaults -->
  <Spire_DU_DefaultReferenceStorage>Auto</Spire_DU_DefaultReferenceStorage>
  <Spire_DU_DefaultJson>SystemTextJson</Spire_DU_DefaultJson>
  <Spire_DU_DefaultJsonDiscriminator>kind</Spire_DU_DefaultJsonDiscriminator>

  <!-- Treat all enums as [EnforceExhaustiveness] -->
  <Spire_EnforceExhaustivenessOnAllEnumTypes>false</Spire_EnforceExhaustivenessOnAllEnumTypes>
</PropertyGroup>
```

| Property | Values | Default |
|----------|--------|---------|
| `Spire_DU_DefaultReferenceStorage` | `Auto`, `Typed`, `Shared` | `Auto` |
| `Spire_DU_DefaultJson` | `None`, `SystemTextJson`, `NewtonsoftJson`, `Both` | `None` |
| `Spire_DU_DefaultJsonDiscriminator` | any string | `kind` |
| `Spire_EnforceExhaustivenessOnAllEnumTypes` | `true`, `false` | `false` |

Per-attribute values override global defaults. Omitting an attribute parameter (or using `ReadGlobalCfg`) falls through to the global config.

These MSBuild settings apply to SDK projects. Unity regenerates its `.csproj`
files and does not import the NuGet `.props` file from this DLL package; use
explicit attribute arguments in Unity.

## EnforceExhaustiveness

The analyzer provides:

- Exhaustive switch checking over enum members or sealed subtypes (SPIRE015).
- Automatic suppression of `CS8509` / `CS8524` for switches Spire's checker proves exhaustive — including tuples of enums, nullable enums, and unmarked enums covering all named members. No opt-in required.
- Invalid enum cast detection (SPIRE016).
- Inherits all `[EnforceInitialization]` rules (SPIRE001-008) — prevents `default(T)`, uninitialized arrays, etc.
- Works on enums, abstract classes, and interfaces.

Apply `[EnforceExhaustiveness]` to a type to require exhaustive switches:

```csharp
using Houtamelo.Spire;

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

## Reference storage

Concrete unmanaged payloads overlap without unsafe code or generator-calculated
sizes. Generic-dependent fields stay typed. `ReferenceStorage.Typed` reuses
compatible typed slots without boxing; `Shared` reuses object slots and may box
managed structs. `Auto` selects Shared when it needs at most half as many managed
slots as Typed, after same-type reuse. It is the project default.

Set a union override with
`[DiscriminatedUnion(ReferenceStorage = ReferenceStorage.Typed)]`.
Generic unions are supported; nesting a union inside a generic type reports
SPIRE_DU018. See the [union guide](docs/discriminated-unions.md) for details.

The [existing benchmark reports](docs/benchmark-results/) measure the removed
Layout strategies. They do not describe the new payload representation.

## License

[MIT](LICENSE)
