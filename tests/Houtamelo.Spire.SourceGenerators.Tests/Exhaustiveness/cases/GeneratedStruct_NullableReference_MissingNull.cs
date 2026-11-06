//@ should_fail
// Ensure that SPIRE009 IS triggered when a nullable managed-reference payload is covered only by not-null.
#nullable enable
using Houtamelo.Spire;

public abstract class Effect
{
    public abstract int Apply(int value);
}

public sealed class DamageEffect : Effect
{
    public override int Apply(int value) => value - 1;
}

public sealed class HealingEffect : Effect
{
    public override int Apply(int value) => value + 1;
}

[DiscriminatedUnion]
partial struct GameplayCommand
{
    [Variant] public static partial GameplayCommand ApplyEffect(
        [UnitySerializeReference] Effect? effect);
    [Variant] public static partial GameplayCommand Idle();
}

static class Consumer
{
    static int Read(GameplayCommand command) => command switch //~ ERROR
    {
        { kind: GameplayCommand.Kind.ApplyEffect, applyEffect: { effect: not null } } => 0,
        { kind: GameplayCommand.Kind.Idle } => 1,
    };
}
