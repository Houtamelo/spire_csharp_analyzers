# [EnforceExhaustiveness]

`[EnforceExhaustiveness]` (namespace `Houtamelo.Spire`) requires every switch expression or statement on the annotated type to explicitly handle all known cases. SPIRE015 fires as an error when any case is unhandled.

## Quick Start

```csharp
using Houtamelo.Spire;

[EnforceExhaustiveness]
public enum Color { Red, Green, Blue }

// SPIRE015: switch does not handle member(s): Blue
string Name(Color c) => c switch
{
    Color.Red   => "red",
    Color.Green => "green",
};

// Fix: add the missing arm
string Name(Color c) => c switch
{
    Color.Red   => "red",
    Color.Green => "green",
    Color.Blue  => "blue",
};
```

## On Enums

Every named member must appear in a switch arm. A `default` or discard (`_`) arm does not count as coverage — it is still allowed as a catch-all, but it does not satisfy SPIRE015.

```csharp
[EnforceExhaustiveness]
public enum Direction { North, South, East, West }

// SPIRE015: does not handle member(s): West
string Label(Direction d) => d switch
{
    Direction.North => "N",
    Direction.South => "S",
    Direction.East  => "E",
    _               => "?",   // does not cover West
};
```

Alias members (two members with the same underlying value) share coverage — handling one handles both.

`[Flags]` enums are not supported. The bitwise combination space makes exhaustive checking impractical, so SPIRE015 does not fire on `[Flags]` enum types even when `[EnforceExhaustiveness]` is present.

## On Class/Interface Hierarchies

When applied to an abstract class or interface, SPIRE015 requires every sealed direct subtype discovered in the compilation to be handled.

```csharp
using Houtamelo.Spire;

[EnforceExhaustiveness]
public abstract class Animal { }
public sealed class Dog  : Animal { }
public sealed class Cat  : Animal { }
public sealed class Bird : Animal { }

// SPIRE015: switch does not handle type(s): Bird
string Describe(Animal a) => a switch
{
    Dog => "dog",
    Cat => "cat",
};

// Fix
string Describe(Animal a) => a switch
{
    Dog  => "dog",
    Cat  => "cat",
    Bird => "bird",
};
```

Works the same on interfaces:

```csharp
[EnforceExhaustiveness]
public interface IShape { }
public sealed class Circle : IShape { }
public sealed class Square : IShape { }

string Area(IShape s) => s switch
{
    Circle c => $"pi*{c.Radius}^2",
    Square q => $"{q.Side}^2",
};
```

Only `sealed` subtypes are considered. Non-sealed subtypes (abstract classes, open classes) are ignored because their set of further subtypes is unbounded.

Subtypes discovered across the entire compilation are included, not just those in the same file or assembly. Adding a new sealed subtype in any project that references the annotated type will cause SPIRE015 to fire on all switches that don't handle it.

## Interaction with [EnforceInitialization]

`EnforceExhaustivenessAttribute` inherits from `EnforceInitializationAttribute`. All SPIRE001-008 initialization rules also apply to types annotated with `[EnforceExhaustiveness]`.

For enums, this means `default(Color)` is flagged by SPIRE003 when the enum has no zero-valued named member. If you want a safe default, add a zero member:

```csharp
[EnforceExhaustiveness]
public enum Status
{
    Unknown = 0,  // zero member — default(Status) is now valid
    Active,
    Inactive,
}
```

For class hierarchies, the initialization rules apply to the annotated base type itself (arrays of it, `default` expressions, etc.).

## Comparison with [DiscriminatedUnion]

| | `[EnforceExhaustiveness]` | `[DiscriminatedUnion]` |
|---|---|---|
| Target | Existing enums, class/interface hierarchies | New struct or record types |
| Variant data | No per-variant fields | Each variant carries its own fields |
| Rule | SPIRE015 | SPIRE009 |
| Subtypes | Discovered at compile time | Declared via `[Variant]` factory methods |

Use `[EnforceExhaustiveness]` when you already have an enum or class hierarchy and want exhaustive switch checking without changing the type structure.

Use `[DiscriminatedUnion]` when you are designing a new closed type and need per-variant data fields — the generator produces a single struct (or record) with typed accessors and SPIRE009 enforces exhaustiveness on it.

See [discriminated-unions.md](discriminated-unions.md) for the discriminated union guide.
