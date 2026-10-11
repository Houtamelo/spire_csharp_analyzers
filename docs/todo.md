# Approved changes: implemented

The two tasks below are implemented in the `unity-6000` worktree. The full .NET
suite passes, including real generator/analyzer/code-action compilation and
execution tests. Native Unity validation is recorded in [the Unity guide](unity.md#validate).

## Struct-union code-fix arms

Both **Add missing variant arms** and **Expand wildcard** generate property
patterns for struct unions. Field captures always use `property_name: var name`,
accepting null and non-null values without shared positional-output boxing:

```cs
switch (command)
{
    case { kind: GameplayCommand.Kind.Idle }:
        throw new System.NotImplementedException();
    case { kind: GameplayCommand.Kind.Move, destination: var destination, speed: var speed }:
        throw new System.NotImplementedException();
    case { kind: GameplayCommand.Kind.Speak, speech: var speech }:
        throw new System.NotImplementedException();
    case { kind: GameplayCommand.Kind.ApplyEffect, effect: var effect }:
        throw new System.NotImplementedException();
}
```

Incompatible same-name fields use variant-qualified properties, such as
`count_value: var value`; compatible reference-nullability differences share one
conservatively nullable property. Record/class union APIs and generated arms
retain their existing behavior. See [the approved collision and pattern rules](designs/2026-10-10-struct-union-field-collisions.md).

## Implementation-member visibility

Generated public/internal serialization plumbing has
`[EditorBrowsable(EditorBrowsableState.Never)]`, including the callbacks and
required `__Spire...` helpers. Unnecessary setters and serialization write helpers
were removed rather than hidden. Tests verify generated attributes and emitted
metadata; user-facing factories and accessors remain visible.

This attribute is an IDE hint. The [Rider completion probe](issues/rider-editorbrowsable-generated-members.md)
was inconclusive, so practical hiding of same-assembly generated members remains
unverified. See [the serialization footprint design](designs/2026-10-10-unity-serialization-footprint.md).
