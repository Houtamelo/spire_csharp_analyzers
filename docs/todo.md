# Approved changes: implemented

The two tasks below are implemented in the `unity-6000` worktree. The full .NET
suite passes, including real generator/analyzer/code-action compilation and
execution tests. Native Unity validation is recorded in [the Unity guide](unity.md#validate).

## Struct-union code-fix arms

**Add missing variant arms** generates an `IsVariant: true` check and deconstructs
the selected variant payload. **Expand wildcard** retains its existing `kind`
patterns. Both pattern forms are supported by analysis. Captures follow factory
parameter order and accept both null and non-null field values:

```cs
switch (command)
{
    case { IsIdle: true }:
        throw new System.NotImplementedException();
    case { IsMove: true, move: var (destination, speed) }:
        throw new System.NotImplementedException();
    case { IsSpeak: true, speak: var (speech) }:
        throw new System.NotImplementedException();
    case { IsApplyEffect: true, applyEffect: var (effect) }:
        throw new System.NotImplementedException();
}
```

Capture names avoid enclosing locals and parameters; accessor names retain the
`_value` suffix when needed to avoid a factory-name conflict. Existing user arms
and record/class union arms retain their current form. See
[the deconstruction coverage](issues/2026-10-26-pending-deconstruction-coverage.md).
Predicate guards, exhaustiveness, and the shorter codefix output are covered in
[the predicate coverage](issues/2026-10-26-is-variant-analysis-and-codefix-coverage.md).

## Implementation-member visibility

Generated public/internal serialization plumbing has
`[EditorBrowsable(EditorBrowsableState.Never)]`, including the callbacks and
required `__Spire...` helpers. Unnecessary setters and serialization write helpers
were removed rather than hidden. Tests verify generated attributes and emitted
metadata; user-facing factories and accessors remain visible.

This attribute is an IDE hint. The [Rider completion probe](issues/rider-editorbrowsable-generated-members.md)
was inconclusive, so practical hiding of same-assembly generated members remains
unverified. See [the serialization footprint design](designs/2026-10-10-unity-serialization-footprint.md).
