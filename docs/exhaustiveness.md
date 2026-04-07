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

Every named member must appear in a switch arm — unless an unguarded catch-all is present. The catch-alls that opt out of the check are:

- `default:` in switch statements
- `_ => ...` in switch expressions
- `var x => ...` / `case var x:` (var declaration patterns)

Guarded catch-alls (`_ when cond => ...`) do **not** opt out — the guard may fail, leaving members uncovered.

```csharp
[EnforceExhaustiveness]
public enum Direction { North, South, East, West }

// SPIRE015: does not handle member(s): West
string Label(Direction d) => d switch
{
    Direction.North => "N",
    Direction.South => "S",
    Direction.East  => "E",
};

// OK — catch-all opts out of the check
string LabelLoose(Direction d) => d switch
{
    Direction.North => "N",
    Direction.South => "S",
    Direction.East  => "E",
    _               => "?",
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

## Global Enforcement on All Enums

By default, SPIRE015 only fires on types annotated with `[EnforceExhaustiveness]`. To enforce exhaustive switches on **all** enum types project-wide (without annotating each one), set:

```xml
<PropertyGroup>
  <Spire_EnforceExhaustivenessOnAllEnumTypes>true</Spire_EnforceExhaustivenessOnAllEnumTypes>
</PropertyGroup>
```

When enabled, every switch on any enum type in the project is checked for exhaustiveness — the same as if `[EnforceExhaustiveness]` were applied to every enum. This also activates SPIRE001-008 (initialization enforcement) and SPIRE016 (invalid enum cast) for all enums.

`[Flags]` enums are still excluded.

## CS8509 / CS8524 Suppression

The C# compiler emits two warnings for switch expressions it cannot prove exhaustive:

- **CS8509** — "The switch expression does not handle all possible values of its input type."
- **CS8524** — "The switch expression does not handle some values of its input type involving an unnamed enum value." (e.g. `(Color)3` for `Color { Red, Green, Blue }`.)

The compiler is conservative: it never excludes unnamed enum values, treats tuple value spaces as cross-products of unnamed components, and gives up on most non-trivial multi-column patterns. As a result, it warns on many switches that are exhaustive in practice.

Spire ships a `DiagnosticSuppressor` that runs the same Maranget exhaustiveness checker used by SPIRE015 / SPIRE009 against every reported CS8509 / CS8524 site. If the checker proves the switch covers every reachable case, the warning is suppressed automatically. No attribute, no opt-in, no configuration — it works on the entire pattern surface the checker supports:

- Plain enums (named members only — unnamed cast values are not part of the value space).
- Tuples of enums, including nested tuples and tuples mixed with `bool`.
- Nullable enums when both `null` and every named member are handled.
- `[EnforceExhaustiveness]` class/interface hierarchies.
- `[DiscriminatedUnion]` types.
- Boolean and numeric range patterns covered by relational / and / or / not patterns.

```csharp
public enum Stone { Red, White }

// Without Spire: CS8524 fires here ("(Stone)2 is not covered").
// With Spire: the checker proves the four named combinations exhaust the
// reachable value space, so CS8524 is suppressed.
string Outcome((Stone, Stone) result) => result switch
{
    (Stone.White, Stone.White) => "GREAT SUCCESS",
    (Stone.White, Stone.Red)   => "FLAWED SUCCESS",
    (Stone.Red,   Stone.White) => "BLESSED SETBACK",
    (Stone.Red,   Stone.Red)   => "FAILURE",
};
```

The suppressor never silences a warning when the checker reports missing cases — incomplete switches still surface CS8509 / CS8524 normally. The two suppression IDs are `SPIRE_SUP001` (CS8509) and `SPIRE_SUP002` (CS8524) if you need to reference them in tooling.

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
