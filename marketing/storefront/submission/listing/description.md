**Save time by catching errors at compile time.**

Spire brings exhaustive matching and discriminated unions to Unity.
Catch forgotten cases as your game evolves, generate missing branches in your
IDE, and give each variant the data it needs.

### Catch missing cases before you press Play

Use exhaustive matching with enums, classes, interfaces, structs, and
discriminated unions. Spire checks switch statements/expressions,
reports a compile-time error when cases are missing, and lists exactly which
cases you forgot. No `default:` or catch-all case is required.

Added a new class implementing an interface? `Spire` immediately reports the
switches that need updating. The same applies when you add an enum
member or a union variant.

Follow the diagnostics straight to the affected code. Use the IDE quick fix to generate the unhandled cases, then fill in their behavior.

### Give each variant its own data

Have you ever looked at an enum and wished you could attach different data to each variant?

*C# Utopia*:
```cs
enum Command {
    Move(Vector2 destination),
    Patrol(List<Vector2> waypoints, int current),
    Attack(Actor target),
}

Command command = Command.Move(new Vector2(6.9f, 4.20f));

switch (command) {
    case Command.Move(Vector2 destination): {
        actor.position = destination;
        break;
    }
    case Command.Patrol(List<Vector2> waypoints, int current):
    ...
}
```

With Spire:
```cs
[DiscriminatedUnion] // Spire's source generator takes care of all the plumbing
public partial struct Command {
    [Variant] public static partial Command Move(Vector2 destination);
    [Variant] public static partial Command Attack(Actor target);
    [Variant] public static partial Command Patrol(List<Vector2> waypoints, int current);
}

Command command = Command.Move(new Vector2(6.9f, 4.20f));

switch (command) {
    case { IsMove: true, move: var (destination) }: {
        actor.position = destination;
        break;
    }
    case { IsPatrol: true, patrol: var (waypoints, current) }:
    ...
}
```

Spire generates the factories and typed payloads at compile time. Match the variant and
deconstruct its data where you consume it. Different systems can react to the
same command, state, or result in their own way, without putting every operation
on the data type itself.

Spire's struct-based unions are lightweight, with compact, allocation-free type-safe
storage (No unsafe code!).

### Author unions in the Unity Inspector

Add `[Serializable]` to your union and use it in serialized fields. Spire
generates the variant picker and editable fields for the selected variant,
including inside lists.

Compatible with Odin Inspector. Odin is optional.

### Generate JSON serialization and deserialization

Generate converters for System.Text.Json, Newtonsoft.Json, or both. Declare
your variants and their data once; Spire generates the JSON serialization and
deserialization code for you.

### Use typed polymorphic data in Burst and DOTS

Bring struct-based discriminated unions into Burst jobs and DOTS. Store your polymorphic data in `NativeArray<T>`, switch on their variants, and deconstruct their payloads
inside your job.

### Included

- Runtime library, analyzers, source generator, and IDE quick fixes.
- Generated Unity serialization and Inspector integration.
- Offline documentation and runtime API tooltips.
- Optional Quick Start sample without additional package dependencies.
- Optional Unity Showcase sample with a runnable scene, Inspector-editable
  presets, and example tests.

### Installation

Standard installation via Unity's Package Manager.

For Rider support, make sure that Roslyn Analyzers are enabled: **Editor > Inspection
Settings > Roslyn Analyzers**.
