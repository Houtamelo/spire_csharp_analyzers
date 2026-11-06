//@ should_pass
// Ensure that SPIRE009 is NOT triggered when explicit null and non-null arms cover the nullable variant.
#nullable enable
using Houtamelo.Spire;

public abstract class Effect
{
    public abstract int Apply(int value);
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
    static int Read(GameplayCommand command) => command switch
    {
        { kind: GameplayCommand.Kind.ApplyEffect, applyEffect: { effect: null } } => 0,
        { kind: GameplayCommand.Kind.ApplyEffect, applyEffect: { effect: not null } } => 1,
        { kind: GameplayCommand.Kind.Idle } => 2,
    };
}
